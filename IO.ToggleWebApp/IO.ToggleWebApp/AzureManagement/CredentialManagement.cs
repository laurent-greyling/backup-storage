using System;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace IO.ToggleWebApp.AzureManagement
{
    public class CredentialManagement
    {
        private static readonly string ClientId = AppConfigurationSettings.ClientId;
        private static readonly string ServicePrincipalPassword = AppConfigurationSettings.ServicePrincipalPassword;
        private static readonly string AzureTenantId = AppConfigurationSettings.AzureTenantId;

        public static string GetAuthorizationToken()
        {
            var cc = new ClientCredential(ClientId, ServicePrincipalPassword);
            var context = new AuthenticationContext("https://login.windows.net/" + AzureTenantId);
            var result = context.AcquireTokenAsync("https://management.azure.com/", cc);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to obtain the JWT token");
            }

            return result.Result.AccessToken;
        }
    }
}
