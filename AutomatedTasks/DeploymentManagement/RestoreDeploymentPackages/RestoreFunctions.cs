using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Helpers;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace RestoreDeploymentPackages
{
    internal class RestoreFunctions
    {
        /// <summary>
        /// This will restore deployments for cloud services
        /// </summary>
        public void RestoreDeploymentPackages()
        {
            Trace.TraceInformation("Start restoring Deployments");

            var environmentName = CloudConfigurationManager.GetSetting("team");

            var thumbPrint = ConfigurationManager.AppSettings["WEBSITE_LOAD_CERTIFICATES"];
            var subScriptionId = ConfigurationManager.AppSettings["subscriptionId"];
            var storageConnectionString = ConfigurationManager.AppSettings["storageConnectionString"];
            var storage = string.Format(ConfigurationManager.AppSettings["storage"], environmentName);

            Trace.TraceInformation("Start Authentication");
            var credentials = CertificateManagement.GetCredentials(thumbPrint, subScriptionId);
            
            var computeManagementClient = CloudContext.Clients.CreateComputeManagementClient(credentials);

            // read deployment configuration from blob
            var container = AzureUtils.GetBlobContainerSetup(environmentName, storageConnectionString);
            
            var serviceNames = AzureUtils.GetServiceNames(environmentName, computeManagementClient);

            List<Task> deploymentTasks = new List<Task>();

            Trace.TraceInformation("Busy Restoring Deployments");
            foreach (var serviceName in serviceNames)
            {
                var blob = container.GetBlockBlobReference($"{serviceName}.cscfg");
                var deploymentConfiguration = blob.DownloadText();
                
                // create deployment from blob
                var deploymentParameters = new DeploymentCreateParameters
                {
                    Configuration = deploymentConfiguration,
                    Name = $"autodeploy{DateTime.UtcNow.ToString("ddMMyyyy-HHmmss")}",
                    Label = $"autodeploy{DateTime.UtcNow.ToString("ddMMyyyy-HHmmss")}",
                    PackageUri =
                        new Uri(string.Concat(storage, container.Name, "/", serviceName, ".cspkg")),
                    StartDeployment = true
                };

                deploymentTasks.Add(computeManagementClient.Deployments.CreateAsync(serviceName, DeploymentSlot.Production, deploymentParameters));
            }
            
            Task.WaitAll(deploymentTasks.ToArray());
            Trace.TraceInformation("Finished restoring deployments");
        }
    }
}
