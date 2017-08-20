using System;
using System.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Helpers;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Storage.Blob;

namespace BackupDeploymentPackages
{
    internal class BackupFunctions
    {
        /// <summary>
        /// This will first save the deployment packages deployed on Azure and then delete them if they exist
        /// </summary>
        /// <returns></returns>
        public async Task BackupDeleteDeploymentPackages()
        {
            Trace.TraceInformation("Start Process for Backup/Delete of Deployments");

            var environmentName = CloudConfigurationManager.GetSetting("team");
            var thumbPrint = CloudConfigurationManager.GetSetting("WEBSITE_LOAD_CERTIFICATES");
            var subScriptionId = ConfigurationManager.AppSettings["subscriptionId"];
            var storageConnectionString = ConfigurationManager.AppSettings["storageConnectionString"];
            var storage = string.Format(ConfigurationManager.AppSettings["storage"], environmentName);

            Trace.TraceInformation("Start Authentication");
            var credentials = CertificateManagement.GetCredentials(thumbPrint, subScriptionId);
            
            var computeManagementClient = CloudContext.Clients.CreateComputeManagementClient(credentials);
            var container = AzureUtils.GetBlobContainerSetup(environmentName, storageConnectionString);
            var serviceNames = AzureUtils.GetServiceNames(environmentName, computeManagementClient);

            foreach (var serviceName in serviceNames)
            {
                if (!AzureUtils.IsDeploymentAvailable(serviceName, computeManagementClient)) continue;

                Trace.TraceInformation($"Save Cloud Services on Blob: {serviceName}");
                var cloudServicesPackages =
                    await GetCloudServicePackagesAsync(serviceName, storage, container, computeManagementClient);

                if (await IsLatestPackages(cloudServicesPackages, computeManagementClient))
                {
                    Trace.TraceInformation($"Delete deployments: {serviceName}");
                    await computeManagementClient.Deployments.DeleteBySlotAsync(serviceName,
                        DeploymentSlot.Production);
                }
            }

            Trace.TraceInformation("Finished backing up all deployments");
        }
        
        private async Task<OperationStatusResponse> GetCloudServicePackagesAsync(string serviceName, string storage,
            CloudBlobContainer container,
            ComputeManagementClient computeManagementClient)
        {
            var deploymentGetPackageParameters =
                new DeploymentGetPackageParameters
                {
                    ContainerUri = new Uri(storage + container.Name),
                    OverwriteExisting = true
                };

            return await computeManagementClient.Deployments.GetPackageBySlotAsync(serviceName
                    , DeploymentSlot.Production, deploymentGetPackageParameters);
            
        }

        private async Task<bool> IsLatestPackages(
            OperationStatusResponse cloudServicesPackages,
            ComputeManagementClient computeManagementClient
            )
        {

            if (cloudServicesPackages.Status == OperationStatus.Succeeded)
            {
                Trace.TraceInformation("Operation Status Succeeded");
                return true;
            }

            while (cloudServicesPackages.Status == OperationStatus.InProgress)
            {
                cloudServicesPackages =
                    await computeManagementClient.GetOperationStatusAsync(cloudServicesPackages.RequestId);

                if (cloudServicesPackages.Status == OperationStatus.Succeeded)
                {
                    Trace.TraceInformation("Operation Status Succeeded");
                    return true;
                }

                if (cloudServicesPackages.Status != OperationStatus.Failed) continue;

                Trace.TraceInformation($"Operation Status Failed: {cloudServicesPackages.Error}");
                break;
            }

            return false;
        }
    }
}
