using System;
using System.Threading.Tasks;
using Microsoft.Rest;

namespace IO.ToggleWebApp.AzureManagement
{
    public class IoWebApp
    {
        private readonly TokenCredentials _tokenCredentials;
        public IoWebApp()
        {
            var credentials = CredentialManagement.GetAuthorizationToken();
            _tokenCredentials = new TokenCredentials(credentials);
        }

        public async Task Execute()
        {
            var azureUtils = new AzureUtils(_tokenCredentials);

            if (!AppConfigurationSettings.SwitchOn)
            {
                Console.WriteLine("Stopping WebApp");
                await azureUtils.StopWebApp();
                Console.WriteLine("WebApp Stopped");
                return;
            }

            Console.WriteLine("Starting WebApp");
            await azureUtils.StartWebApp();
            Console.WriteLine("WebApp Started");
        }
    }
}
