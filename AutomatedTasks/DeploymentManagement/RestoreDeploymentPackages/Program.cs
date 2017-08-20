using System;
using System.Diagnostics;
using Helpers;

namespace RestoreDeploymentPackages
{
    class Program
    {
        static void Main()
        {
            try
            {
                if (!AzureUtils.IsWeekend())
                    new RestoreFunctions().RestoreDeploymentPackages();
            }
            catch (Exception ex)
            {
                Trace.TraceInformation($"Exception: {ex}");
            }
            
        }
    }
}
