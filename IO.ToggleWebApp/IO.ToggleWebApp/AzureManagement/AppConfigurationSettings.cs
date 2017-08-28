using System.Configuration;

namespace IO.ToggleWebApp.AzureManagement
{
    public static class AppConfigurationSettings
    {
        public static string WebAppName => ConfigurationManager.AppSettings["webappName"];
        public static string ResourceGroupName => ConfigurationManager.AppSettings["resourceGroupName"];
        public static string WebAppUri => ConfigurationManager.AppSettings["webAppUri"];

        public static string ClientId => ConfigurationManager.AppSettings["clientId"];
        public static string ServicePrincipalPassword => ConfigurationManager.AppSettings["servicePrincipalPassword"];
        public static string AzureTenantId => ConfigurationManager.AppSettings["azureTenantId"];
        
        public static string SubscriptionId => ConfigurationManager.AppSettings["subscriptionId"];

        public static bool SwitchOn
            => ConfigurationManager.AppSettings["IO"].ToLowerInvariant() == "on";
    }
}
