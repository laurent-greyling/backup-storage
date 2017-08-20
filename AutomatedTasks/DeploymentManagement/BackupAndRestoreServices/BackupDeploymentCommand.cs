using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Helpers;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Storage.Blob;

namespace BackupAndRestoreServices
{
    public class BackupDeploymentCommand
    {
        private readonly ComputeManagementClient _computeManagementClient;
        private readonly CloudBlobContainer _container;

        public BackupDeploymentCommand()
        {
            var credentials = CertificateManagement.GetCredentials();

            _computeManagementClient = CloudContext.Clients.CreateComputeManagementClient(credentials);
            _container = AzureUtils.GetBlobContainerSetup(AppConfigurationSettings.EnvironmentName,
                AppConfigurationSettings.StorageConnectionString);
        }

        /// <summary>
        /// This will first save the deployment packages deployed on Azure and then delete them if they exist
        /// </summary>
        /// <returns></returns>
        public void Execute()
        {
            try
            {
                if (AzureUtils.IsWeekend()) return;

                SlackNotificationService.Notify(
                    $"{AppConfigurationSettings.EnvironmentName.ToUpper()}: Start Process for Backup/Delete of Deployments");

                var allServices = AzureUtils.GetServiceNames(AppConfigurationSettings.EnvironmentName, _computeManagementClient);

                Task.WaitAll(allServices.Select(BackupAndDeleteDeployment).ToArray());

                SlackNotificationService.Notify(
                    $"{AppConfigurationSettings.EnvironmentName.ToUpper()}: Finished backing up all deployments");
            }
            catch (Exception ex)
            {
                Trace.TraceInformation("BackupDeploymentCommand execution failed.");
                SlackNotificationService.Notify(
                    $"{AppConfigurationSettings.EnvironmentName.ToUpper()}: - Exception: {ex.Message} ---" +
                    $" For more information see {AppConfigurationSettings.Storage}deploymentlog BackupDeploymentPackages");
            }
        }

        /// <summary>
        /// Get packages from azure cloud service deployments and saves it in Storage
        /// After saving the packages deletes the deployments
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        private async Task BackupAndDeleteDeployment(string serviceName)
        {
            if (await AzureUtils.IsProductionSlotDeploymentAvailable(serviceName, _computeManagementClient))
            {
                Trace.TraceInformation($"Save Cloud Services on Blob: {serviceName}");

                var isOperationSucceeded = await IsBackupOperationSuccesful(serviceName);

                //Retry if first operation failed
                if (!isOperationSucceeded)
                {
                    Trace.TraceInformation($"Retry Save Cloud Services on Blob: {serviceName}");
                    isOperationSucceeded = await IsBackupOperationSuccesful(serviceName, true);
                }

                if (isOperationSucceeded)
                {
                    Trace.TraceInformation($"Delete deployments: {serviceName}");
                    var isDeleteOperationSucceeded = await IsDeleteOperationSuccesful(serviceName);

                    //Retry if first operation failed
                    if (!isDeleteOperationSucceeded)
                    {
                        Trace.TraceInformation($"Retry Delete deployments: {serviceName}");
                        await IsDeleteOperationSuccesful(serviceName, true);
                    }
                }
            }
        }

        /// <summary>
        /// delete the packages and checks the operation status for success.
        /// Returns true if operation succeeded
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="isRetry"></param>
        /// <returns></returns>
        private async Task<bool> IsDeleteOperationSuccesful(string serviceName, bool isRetry = false)
        {
            var operationType = isRetry ? "Retry Delete" : "Delete";

            var deleteOperation = await _computeManagementClient.Deployments.DeleteBySlotAsync(serviceName,
                DeploymentSlot.Production);

            var isDeleteOperationSucceeded =
                await AzureUtils.IsOperationSucceeded(deleteOperation, _computeManagementClient, serviceName, operationType);

            return isDeleteOperationSucceeded;
        }

        /// <summary>
        /// get the packages and checks the operation status for success.
        /// Returns true if operation succeeded
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="isRetry"></param>
        /// <returns></returns>
        private async Task<bool> IsBackupOperationSuccesful(string serviceName, bool isRetry = false)
        {
            var operationType = isRetry ? "Retry Save" : "Save";

            var operation =
                await
                    GetPackageAsync(serviceName, AppConfigurationSettings.Storage, _container,
                        _computeManagementClient);

            var isOperationSucceeded =
                await AzureUtils.IsOperationSucceeded(operation, _computeManagementClient, serviceName, operationType);

            return isOperationSucceeded;
        }

        private static async Task<OperationStatusResponse> GetPackageAsync(string serviceName, string storage,
            CloudBlobContainer container,
            IComputeManagementClient computeManagementClient)
        {
            var deploymentGetPackageParameters =
                new DeploymentGetPackageParameters
                {
                    ContainerUri = new Uri($"{storage}{container.Name}"),
                    OverwriteExisting = true
                };

            return await computeManagementClient.Deployments.GetPackageBySlotAsync(serviceName
                    , DeploymentSlot.Production, deploymentGetPackageParameters);

        }
    }
}
