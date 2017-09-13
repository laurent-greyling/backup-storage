using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
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
            var operation = string.Empty;

            if (operationalParams.Start == "backup")
            {
                Task.Run(async () => await BackUpAsync(operationalParams));
                operation = "Backup Status";
            }

            if (operationalParams.Start == "restore")
            {
                Task.Run(async () => await RestoreAsync(operationalParams));
                operation = "Restore Status";
            }
            
            var statusModel = new StatusModel
            {
                Operation = operation
            };
            return RedirectToAction("Index","Status", statusModel);
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

        private async Task<HttpStatusCodeResult> BackUpAsync(OperationalModel operationalParams)
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
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            finally
            {
                logger.Info("*******************************************");
                operation.StoreLogInStorage().Wait();
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        private async Task<HttpStatusCodeResult> RestoreAsync(OperationalModel operationalParams)
        {
            Bootstrap.Start();
            var logger = Bootstrap.Container.GetInstance<ILogger>();
            var operation = Bootstrap.Container.GetInstance<IOperationContext>();
            var sw = new Stopwatch();
            var fromDate = operationalParams.FromDate.ToString();
            var toDate = operationalParams.ToDate.ToString();
            try
            {
                sw.Start();
                if (!operationalParams.RestoreTables && !operationalParams.RestoreBlobs)
                {
                    var restoreCommand = new RestoreCommand
                    {
                        FromDate = fromDate,
                        ToDate = toDate
                    };

                    await operation.RestoreAll(restoreCommand);
                }

                if (operationalParams.RestoreTables)
                {
                    var restoreTablesCommand = new RestoreTableCommand
                    {
                        TableName = operationalParams.TableName,
                        FromDate = fromDate,
                        ToDate = toDate
                    };

                    await operation.RestoreTableAsync(restoreTablesCommand);
                }

                if (operationalParams.RestoreBlobs)
                {
                    var restoreBlobsCommand = new RestoreBlobCommand
                    {
                        ContainerName = operationalParams.ContainerName,
                        BlobPath = operationalParams.BlobName,
                        FromDate = fromDate,
                        ToDate = toDate,
                        Force = operationalParams.Force
                    };

                    await operation.RestoreBlobAsync(restoreBlobsCommand);
                }

                logger.Info("Total:{0}", sw.Elapsed);
            }
            catch (Exception e)
            {
                logger.Error(e);
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            finally
            {
                logger.Info("*******************************************");
                operation.StoreLogInStorage().Wait();
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }
    }
}
