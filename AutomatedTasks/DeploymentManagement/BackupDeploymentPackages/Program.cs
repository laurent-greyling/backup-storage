using System;
using System.Diagnostics;
using Helpers;

namespace BackupDeploymentPackages
{
    class Program
    {
        static void Main()
        {
            try
            {
                if(!AzureUtils.IsWeekend())
                    new BackupFunctions().BackupDeleteDeploymentPackages().Wait();
            }
            catch (Exception ex)
            {
                Trace.TraceInformation($"Exception: {ex}");
            }
        }
    }
}
