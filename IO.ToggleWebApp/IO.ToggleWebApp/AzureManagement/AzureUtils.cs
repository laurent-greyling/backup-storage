using System.Threading.Tasks;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Rest;

namespace IO.ToggleWebApp.AzureManagement
{
    public class AzureUtils
    {
        private static WebSiteManagementClient _webSiteManagementClient;
        private static readonly string ResourceGroup = AppConfigurationSettings.ResourceGroupName;
        private static readonly string AppName = AppConfigurationSettings.WebAppName;

        public AzureUtils(ServiceClientCredentials tokenCredentials)
        {
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
    }
}
