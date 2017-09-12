using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Configuration;
using Final.BackupTool.Mvc.Models;
using System.Web.Mvc;
using Final.BackupTool.Common.ConsoleCommand;
using Final.BackupTool.Common.Initialization;
using Final.BackupTool.Common.Strategy;
using NLog;

namespace Final.BackupTool.Mvc.Controllers
{
    public class OperationalController : Controller
    {
        // GET: Operational
        public ActionResult Execute(OperationalModel operationalParams)
        {
            SetConnectionStrings(operationalParams);

            if (operationalParams.Start == "backup")
            {
                Task.Run(async () => await BackUpAsync(operationalParams));
            }
            
            return View();
        }

        private static void SetConnectionStrings(OperationalModel operationalParams)
        {
            if (string.IsNullOrEmpty(WebConfigurationManager.AppSettings["ProductionStorageConnectionString"]))
            {
                WebConfigurationManager.AppSettings["ProductionStorageConnectionString"] =
                operationalParams.ProductionStorageConnectionString;
            }
            if (string.IsNullOrEmpty(WebConfigurationManager.AppSettings["BackupStorageConnectionString"]))
            {
                WebConfigurationManager.AppSettings["BackupStorageConnectionString"] =
                operationalParams.BackupStorageConnectionString;
            }
            if (string.IsNullOrEmpty(WebConfigurationManager.AppSettings["OperationalStorageConnectionString"]))
            {
                WebConfigurationManager.AppSettings["OperationalStorageConnectionString"] =
                operationalParams.OperationalStorageConnectionString;
            }
        }

        private async Task BackUpAsync(OperationalModel operationalParams)
        {
            Bootstrap.Start();
            var logger = Bootstrap.Container.GetInstance<ILogger>();
            var operation = Bootstrap.Container.GetInstance<IOperationContext>();
            var command = new BackupCommand();

            if (!operationalParams.BackupTables)
            {
                command.Skip = "tables";
            }

            if (!operationalParams.BackupBlobs)
            {
                command.Skip = "blobs";
            }

            var sw = new Stopwatch();

            try
            {
                sw.Start();
                await operation.BackupAsync(command);
                logger.Info($"Total: {sw.Elapsed}");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            finally
            {
                logger.Info("*******************************************");
                operation.StoreLogInStorage().Wait();
            }
        }
    }
}
