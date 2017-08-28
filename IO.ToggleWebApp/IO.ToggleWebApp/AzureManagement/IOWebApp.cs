using System;
using System.Threading.Tasks;
using Microsoft.Rest;

namespace IO.ToggleWebApp.AzureManagement
{
    public class IoWebApp
    {
        private readonly TokenCredentials _tokenCredentials;
        private readonly AzureUtils _azureUtils;
        public IoWebApp()
        {
            var credentials = CredentialManagement.GetAuthorizationToken();
            _tokenCredentials = new TokenCredentials(credentials);
            _azureUtils = new AzureUtils(_tokenCredentials);
        }

        public async Task Execute()
        {
            if (!AppConfigurationSettings.SwitchOn)
            {
                Console.WriteLine("Stopping WebApp");
                await _azureUtils.StopWebApp();
                Console.WriteLine("WebApp Stopped");
                return;
            }

            Console.WriteLine("Starting WebApp");
            await _azureUtils.StartWebApp();
            Console.WriteLine("WebApp Started");
        }

        public void CreateWebApp(string resourceGroupName, 
            string webApp, 
            string location, 
            string customerId, 
            string customerEmail)
        {
            Console.WriteLine("Starting Creating ResourceGroup");
            _azureUtils.CreateResourceGroup(resourceGroupName, location, customerId, customerEmail);

            Console.WriteLine("Starting Creating App Service Plan");
            _azureUtils.CreateAppServicePlan(resourceGroupName, webApp, location);

            Console.WriteLine("Starting Creating WebApp");
            _azureUtils.CreateWebApp(resourceGroupName, webApp, location);
            Console.WriteLine("Finished Creating WebApp");
        }
    }
}
