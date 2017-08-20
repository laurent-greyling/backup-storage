using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Helpers;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace BackupAndRestoreServices
{
    public class RestoreDeploymentCommand
    {
        private readonly ComputeManagementClient _computeManagementClient;
        private readonly CloudBlobContainer _container;

        public RestoreDeploymentCommand()
        {
            var credentials = CertificateManagement.GetCredentials();
            _computeManagementClient = CloudContext.Clients.CreateComputeManagementClient(credentials);
            _container = AzureUtils.GetBlobContainerSetup(AppConfigurationSettings.EnvironmentName, AppConfigurationSettings.StorageConnectionString);
        }

        public void Execute()
        {
            try
            {
                if (AzureUtils.IsWeekend()) return;

                SlackNotificationService.Notify(
                    $"{AppConfigurationSettings.EnvironmentName.ToUpper()}: Start restoring Deployments");

                var allServices = AzureUtils.GetServiceNames(AppConfigurationSettings.EnvironmentName, _computeManagementClient);

                Trace.TraceInformation("Busy Restoring Deployments");

                Task.WaitAll(allServices.Select(RestoreDeployment).ToArray());

                SlackNotificationService.Notify(
                   $"{AppConfigurationSettings.EnvironmentName.ToUpper()}: Finished restoring deployments");
            }
            catch (Exception ex)
            {
                SlackNotificationService.Notify(
                    $"{AppConfigurationSettings.EnvironmentName.ToUpper()}: - Exception: {ex.Message} ---" +
                    $" For more information see {AppConfigurationSettings.Storage}deploymentlog RestoreDeploymentPackages");
            }
        }

        /// <summary>
        /// Finds deployment package from storage and deploys the packages available from there, only if deployment is not available on azure
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        private async Task RestoreDeployment(string serviceName)
        {
            try
            {
                if (!await AzureUtils.IsProductionSlotDeploymentAvailable(serviceName, _computeManagementClient))
                {
                    //Here we add the extention making it available for uploading. Just adding the extention will not actually enable it on azure.
                    #region addextension

                    //We get the extensions to see if they exist for adding or not, if this id exist we delete it, mostly because we don't want to create alot 
                    //of available extensions. Once the added extension is removed we can upload the new extension
                    var getExtension = await _computeManagementClient.HostedServices.ListExtensionsAsync(serviceName);
                    var doesExtensionExist =
                        getExtension.FirstOrDefault(x => x.Id == $"{serviceName}-PaaSDiagnostics-Production-Ext-0");

                    if (doesExtensionExist!=null)
                    {
                        await
                            _computeManagementClient.HostedServices.DeleteExtensionAsync(serviceName,
                                $"{serviceName}-PaaSDiagnostics-Production-Ext-0");
                    }

                    //These files should not be removed from the container [temaname]service container in blob. If this part fails, check if they exist there
                    //if they were deleted use the xml config files in BackupAndRestore Service, make sure to change the private config to your team and key for storage

                    var publicConfigPath = _container.GetBlockBlobReference("DiagnosticsPublicConfig.xml");
                    var privateConfigPath = _container.GetBlockBlobReference("DiagnosticsPrivateConfig.xml");

                    string publicConfig;
                    string privateConfig;

                    using (var reader = new StreamReader(privateConfigPath.OpenRead()))
                    {
                        privateConfig = reader.ReadToEnd();
                    }

                    using (var reader = new StreamReader(publicConfigPath.OpenRead()))
                    {
                        publicConfig = reader.ReadToEnd();
                    }

                    var extensionParam = new HostedServiceAddExtensionParameters
                    {
                        Id = $"{serviceName}-PaaSDiagnostics-Production-Ext-0",
                        PublicConfiguration = publicConfig,
                        PrivateConfiguration = privateConfig,
                        ProviderNamespace = "Microsoft.Azure.Diagnostics",
                        Type = "PaaSDiagnostics",
                        Version = "1.*"
                    };

                    await _computeManagementClient.HostedServices.AddExtensionAsync(serviceName, extensionParam);
                    #endregion

                    //Once the extension is added you can ready the paramater the restore deployment will need.
                    //The parameter needed is the extentionConfiguration in DeploymentCreateParameters
                    //ExtensionConfiguration Represents an extension that is added to the cloud service. In Azure,
                    //a process can run as an extension of a cloud service. For example, Remote Desktop
                    //Access or the Azure Diagnostics Agent can run as extensions to the cloud service.
                    //You must add an extension to the cloud service by using Add Extension before
                    //it can be added to the deployment.

                    //Represents an extension that is to be deployed to a role in a cloud service.
                    var extension = new ExtensionConfiguration.Extension(extensionParam.Id);
                    //Specifies a list of extensions that are applied to all roles in a deployment.
                    var extensionList = new List<ExtensionConfiguration.Extension> { extension };

                    var extensionConfig = new ExtensionConfiguration
                    {
                        AllRoles = extensionList
                    };

                    var blob = _container.GetBlockBlobReference($"{serviceName}.cscfg");
                    var deploymentConfiguration = blob.DownloadText();
                    
                    // create deployment from blob
                    var deploymentParameters = new DeploymentCreateParameters
                    {
                        Configuration = deploymentConfiguration,
                        ExtensionConfiguration = extensionConfig,
                        Name = $"autodeploy-{serviceName}-{DateTime.UtcNow.ToString("ddMMyyyy-HHmmss")}",
                        Label = $"autodeploy-{serviceName}-{DateTime.UtcNow.ToString("ddMMyyyy-HHmmss")}",
                        PackageUri =
                            new Uri($"{AppConfigurationSettings.Storage}{_container.Name}/{serviceName}.cspkg"),
                        StartDeployment = true
                    };

                    var isOperationSucceeded = await IsRestoreOperationSuccesful(serviceName, deploymentParameters);

                    if (!isOperationSucceeded)
                    {
                        Trace.TraceInformation($"Retry Restore Cloud Services: {serviceName}");
                        await IsRestoreOperationSuccesful(serviceName, deploymentParameters);
                    }
                }
            }
            catch (Exception ex)
            {
                var t = ex.Message;
                throw;
            }

        }

        /// <summary>
        /// Restore the deployments and check if status is success
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="deploymentParameters"></param>
        /// <param name="isRetry"></param>
        /// <returns></returns>
        private async Task<bool> IsRestoreOperationSuccesful(string serviceName,
            DeploymentCreateParameters deploymentParameters, bool isRetry = false)
        {
            var operationType = isRetry ? "Retry Restore" : "Restore";

            var operation = await
                _computeManagementClient.Deployments.CreateAsync(serviceName, DeploymentSlot.Production,
                    deploymentParameters);

            var isOperationSucceeded =
                await AzureUtils.IsOperationSucceeded(operation, _computeManagementClient, serviceName, operationType);

            return isOperationSucceeded;
        }
    }
}
