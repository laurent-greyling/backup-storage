using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Final.BackupTool.Common.ConsoleCommand;
using Final.BackupTool.Common.Helpers;
using Final.BackupTool.Common.Models;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Mvc.Controllers
{
    public class OperationalController : Controller
    {
        // GET: Operational
        public ActionResult Execute(OperationalModel operationalParams)
        {
            var operation = string.Empty;
            var operationDate = DateTimeOffset.UtcNow;
            
            CookiesReadWrite.Write("production", "productionKey", operationalParams.ProductionStorageConnectionString);
            CookiesReadWrite.Write("backup", "backupKey", operationalParams.BackupStorageConnectionString);
            CookiesReadWrite.Write("operational", "operationalKey", operationalParams.OperationalStorageConnectionString);
            var setOperations = new AzureOperations();

            if (operationalParams.Start == "backup")
            {
                Task.Run(async () => await BackUpAsync(operationalParams));
                operation = "Backup Status";
            }

            if (operationalParams.Start == "restore")
            {
                operationalParams.FromDate = DateTimeOffset.ParseExact(operationalParams.FromDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
               .ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);
                operationalParams.ToDate = DateTimeOffset.ParseExact(operationalParams.ToDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
                    .ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);

                if (operationalParams.RestoreBlobs && operationalParams.RestoreTables)
                {
                    operationalParams.ContainerName = "*";
                    operationalParams.BlobName = "*";
                    operationalParams.TableName = "*";
                }

                Task.Run(() => Restore(operationalParams));

                operation = "Restore Status";
            }
            
            var statusModel = new StatusModel
            {
                Operation = operation,
                OperationDate = operationDate,
                BackupTable = operationalParams.BackupTables,
                BackupBlobs = operationalParams.BackupBlobs,
                RestoreTable = operationalParams.RestoreTables,
                RestoreBlobs = operationalParams.RestoreBlobs
            };

            return RedirectToAction("Index","Status", statusModel);
        }

        private async Task<HttpStatusCodeResult> BackUpAsync(OperationalModel operationalParams)
        {
            try
            {
                var command = new BackupCommand();

                if (!operationalParams.BackupTables)
                {
                    command.Skip = "tables";
                }

                if (!operationalParams.BackupBlobs)
                {
                    command.Skip = "blobs";
                }

                command.Run(null);
            }
            catch
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        private static HttpStatusCodeResult Restore(OperationalModel operationalParams)
        {
            var fromDate = operationalParams.FromDate;
            var toDate = operationalParams.ToDate;
            try
            {
                if (operationalParams.RestoreTables && operationalParams.RestoreBlobs)
                {
                    var restoreCommand = new RestoreCommand
                    {
                        FromDate = fromDate,
                        ToDate = toDate
                    };
                    restoreCommand.Run(null);
                }

                if (operationalParams.RestoreTables && !operationalParams.RestoreBlobs)
                {
                    var restoreTablesCommand = new RestoreTableCommand
                    {
                        TableName = operationalParams.TableName,
                        FromDate = fromDate,
                        ToDate = toDate
                    };

                    restoreTablesCommand.Run(null);
                }

                if (operationalParams.RestoreBlobs && !operationalParams.RestoreTables)
                {
                    var restoreBlobsCommand = new RestoreBlobCommand
                    {
                        ContainerName = operationalParams.ContainerName,
                        BlobPath = operationalParams.BlobName,
                        FromDate = fromDate,
                        ToDate = toDate,
                        Force = operationalParams.Force
                    };

                    restoreBlobsCommand.Run(null);
                }
            }
            catch
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }
    }
}
