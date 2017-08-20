using System.Configuration;
using Microsoft.Azure;

namespace Helpers
{
    public static class AppConfigurationSettings
    {
        public static string Storage => ConfigurationManager.AppSettings["storage"];
        public static string EnvironmentName => ConfigurationManager.AppSettings["environmentName"];
        public static string ThumbPrint => CloudConfigurationManager.GetSetting("WEBSITE_LOAD_CERTIFICATES");
        
        public static string SubscriptionId => ConfigurationManager.AppSettings["subscriptionId"];
        public static string StorageConnectionString => ConfigurationManager.AppSettings["storageConnectionString"];
        public static string Webhook => ConfigurationManager.AppSettings["slackhookuri"];
    }
}
