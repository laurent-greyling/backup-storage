using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;

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
            if (AzureUtils.IsWeekend()) return;
           
            AzureUtils.RetreiveResource(_tokenCredentials);
        }
    }
}
