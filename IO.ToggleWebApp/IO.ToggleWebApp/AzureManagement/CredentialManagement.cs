using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.WindowsAzure;

namespace IO.ToggleWebApp.AzureManagement
{
    public class CredentialManagement
    {
        private static string AuthToken { get; set; }
        private static TokenCredentials TokenCredentials { get; set; }
        private static string ResourceGroupName { get; set; }

        private static readonly string ClientId = AppConfigurationSettings.ClientId;
        private static readonly string ServicePrincipalPassword = AppConfigurationSettings.ServicePrincipalPassword;
        private static readonly string AzureTenantId = AppConfigurationSettings.AzureTenantId;
        private static readonly string AzureSubscriptionId = AppConfigurationSettings.SubscriptionId;

        /// <summary>
        /// Get the credentials necessary for authentication
        /// </summary>
        /// <returns></returns>
        public static SubscriptionCloudCredentials GetCertificateCredentials()
        {
            var cert = GetStoreCertificate();

            return new CertificateCloudCredentials(AzureSubscriptionId, cert);
        }

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

        private static X509Certificate2 GetStoreCertificate()
        {
            Trace.TraceInformation("Start Get certificate from store");
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                X509Certificate2Collection certificates = store.Certificates.Find(
                    X509FindType.FindByThumbprint, AppConfigurationSettings.ThumbPrint, false);
                if (certificates.Count == 1)
                {
                    return certificates[0];
                }
            }
            finally
            {
                store.Close();
            }

            Trace.TraceError("A Certificate could not be located.");

            return null;
        }
    }
}
