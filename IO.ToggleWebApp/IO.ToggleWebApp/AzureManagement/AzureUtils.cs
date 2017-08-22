using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Rest;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure;

namespace IO.ToggleWebApp.AzureManagement
{
    public class AzureUtils
    {
        private static readonly string AzureSubscriptionId = AppConfigurationSettings.SubscriptionId;
        /// <summary>
        /// Check if it is a weekend so that code does not run on a weekend
        /// Only done because azure scheduler at this point didn't allow us to schedule week days only
        /// </summary>
        /// <returns></returns>
        public static bool IsWeekend()
        {
            var dayOfWeek = DateTime.Now;

            return dayOfWeek.DayOfWeek == DayOfWeek.Saturday || dayOfWeek.DayOfWeek == DayOfWeek.Sunday;
        }

        public static void RetreiveResource(TokenCredentials tokenCredentials)
        {
            var resourceManagementClient = new ResourceManagementClient(tokenCredentials)
            {
                SubscriptionId = AzureSubscriptionId
            };
            
            var resource = resourceManagementClient.ResourceGroups.Get("");
        }
    }
}
