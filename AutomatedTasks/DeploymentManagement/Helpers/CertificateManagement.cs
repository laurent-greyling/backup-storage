using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.WindowsAzure;

namespace Helpers
{
    public class CertificateManagement
    {
        /// <summary>
        /// Get the credentials necessary for authentication
        /// </summary>
        /// <returns></returns>
        public static SubscriptionCloudCredentials GetCredentials()
        {
            var cert = GetStoreCertificate();

            return new CertificateCloudCredentials(AppConfigurationSettings.SubscriptionId, cert);
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
