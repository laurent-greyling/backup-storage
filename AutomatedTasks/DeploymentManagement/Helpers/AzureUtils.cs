using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Helpers
{
    public class AzureUtils
    {
        /// <summary>
        /// Get the service names which we need for backing up and deleting
        /// </summary>
        /// <param name="environmentName"></param>
        /// <param name="computeManegManagementClient"></param>
        /// <returns></returns>
        public static List<string> GetServiceNames(string environmentName, ComputeManagementClient computeManegManagementClient)
        {
            var listOfSerives = computeManegManagementClient.HostedServices.ListAsync();
            var getTeamServices = listOfSerives.Result.Where(r => r.ServiceName.Contains(environmentName)).Select(e => e.ServiceName);

            return getTeamServices.ToList();
        }

        /// <summary>
        /// Checks if deployment is active on azure productionslot
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="computeManegManagementClient"></param>
        /// <returns></returns>
        public static async Task<bool> IsProductionSlotDeploymentAvailable(string serviceName, ComputeManagementClient computeManegManagementClient)
        {
            var getServiceDetails = await computeManegManagementClient.HostedServices.GetDetailedAsync(serviceName);

            return getServiceDetails.Deployments.Any(d => d.DeploymentSlot == DeploymentSlot.Production);

        }

        /// <summary>
        /// Get blob container where packages will be read from and saved to
        /// </summary>
        /// <param name="environmentName"></param>
        /// <param name="storageConnectionString"></param>
        /// <returns></returns>
        public static CloudBlobContainer GetBlobContainerSetup(string environmentName, string storageConnectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            
            return blobClient.GetContainerReference($"{environmentName}service");
        }

        /// <summary>
        /// Check if it is a weekend so that code does not run on a weekend
        /// Only done because azure scheduler at this point didn't allow us to schedule week days only
        /// </summary>
        /// <returns></returns>
        public static bool IsWeekend()
        {
            var dayOfWeek = DateTime.Now;

            return dayOfWeek.DayOfWeek==DayOfWeek.Saturday || dayOfWeek.DayOfWeek == DayOfWeek.Sunday;
        }

        /// <summary>
        /// Checks if the process was completed successfully
        /// Used for retries 
        /// </summary>
        /// <param name="operationStatus"></param>
        /// <param name="computeManagementClient"></param>
        /// <param name="serviceName"></param>
        /// <param name="operationType"></param>
        /// <returns></returns>
        public static async Task<bool> IsOperationSucceeded(
           OperationStatusResponse operationStatus,
           ComputeManagementClient computeManagementClient,
           string serviceName,
           string operationType
           )
        {
            Trace.TraceInformation($"{operationType} operation status: {operationStatus.Status} for service {serviceName}");

            if (operationStatus.Status == OperationStatus.Succeeded)
            {
                return true;
            }

            while (operationStatus.Status == OperationStatus.InProgress)
            {
                Trace.TraceInformation($"Getting operation status {serviceName}");

                operationStatus =
                    await computeManagementClient.GetOperationStatusAsync(operationStatus.RequestId);

                Trace.TraceInformation($"{operationType} operation updated status: {operationStatus.Status} for service {serviceName}");
                
                if (operationStatus.Status == OperationStatus.Succeeded)
                {
                    return true;
                }

                if (operationStatus.Status == OperationStatus.Failed)
                    break;

                await Task.Delay(1000);
            }

            SlackNotificationService.Notify($"Operation Status Error:: {operationStatus.Error}");

            return false;
        }

    }
}
