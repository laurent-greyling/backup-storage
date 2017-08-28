using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Microsoft.Rest;

namespace IO.ToggleWebApp.AzureManagement
{
    public class AzureUtils
    {
        private static ResourceManagementClient _resourceManagementClient;
        private static WebSiteManagementClient _webSiteManagementClient;
        
        private static readonly string ResourceGroup = AppConfigurationSettings.ResourceGroupName;
        private static readonly string AppName = AppConfigurationSettings.WebAppName;

        public AzureUtils(ServiceClientCredentials tokenCredentials)
        {
            _resourceManagementClient = new ResourceManagementClient(tokenCredentials)
            {
                SubscriptionId = AppConfigurationSettings.SubscriptionId
            };
            _webSiteManagementClient = new WebSiteManagementClient(tokenCredentials)
            {
                SubscriptionId = AppConfigurationSettings.SubscriptionId
            };
        }

        public async Task StopWebApp()
        {
            await _webSiteManagementClient.WebApps.StopAsync(ResourceGroup, AppName);
        }

        public async Task StartWebApp()
        {
            await _webSiteManagementClient.WebApps.StartAsync(ResourceGroup, AppName);
        }

        public void CreateWebApp(string resourceGroup, string webApp, string location)
        {
            _webSiteManagementClient.WebApps.CreateOrUpdate(resourceGroup, webApp, new SiteInner
            {
                Location = location.Replace(" ", "").ToLowerInvariant()
            });
        }

        public void CreateResourceGroup(string resourceGroupName, string location, string customerId, string customerEmail)
        {
            var resourceGroup = new ResourceGroupInner
            {
                Location = location,
                Tags = new Dictionary<string, string>
                {
                    {"CustomerId", customerId},
                    {"CustomerEmail", customerEmail}
                }
            };

            _resourceManagementClient.ResourceGroups.CreateOrUpdate(resourceGroupName, resourceGroup);
        }

        public void CreateAppServicePlan(string resourceGroup, string webApp, string location)
        {
            _webSiteManagementClient.AppServicePlans.CreateOrUpdate(resourceGroup, webApp, new AppServicePlanInner
            {
                Location = location.Replace(" ", "").ToLowerInvariant()
            });
        }
    }
}
