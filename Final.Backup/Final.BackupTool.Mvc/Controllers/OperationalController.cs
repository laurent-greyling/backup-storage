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
            
            SetCookies(operationalParams);

            var setOperations = new AzureOperations();

            if (operationalParams.Start == "backup")
            {
                Task.Run(() => BackUp(operationalParams));
                operation = OperationalDictionary.BackupStatus;
            }

            if (operationalParams.Start == "restore")
            {
                Task.Run(() => Restore(operationalParams));
                operation = OperationalDictionary.RestoreStatus;
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

        private static void SetCookies(OperationalModel operationalParams)
        {
            CookiesReadWrite.Write(OperationalDictionary.GroupsTable, OperationalDictionary.GroupsTable, operationalParams.SelectedConnectionGroup);
        }

        private HttpStatusCodeResult BackUp(OperationalModel operationalParams)
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
            try
            {
                var fromDate = DateTimeOffset.ParseExact(operationalParams.FromDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
                    .ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);
                var toDate = DateTimeOffset.ParseExact(operationalParams.ToDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
                    .ToString(OperationalDictionary.DateFormat, CultureInfo.InvariantCulture);

                if (operationalParams.RestoreTables && operationalParams.RestoreBlobs)
                {
                    operationalParams.ContainerName = "*";
                    operationalParams.BlobName = "*";
                    operationalParams.TableName = "*";

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
                        TableName = string.IsNullOrEmpty(operationalParams.TableName) ? "*" : operationalParams.TableName,
                        FromDate = fromDate,
                        ToDate = toDate
                    };

                    restoreTablesCommand.Run(null);
                }

                if (operationalParams.RestoreBlobs && !operationalParams.RestoreTables)
                {
                    var restoreBlobsCommand = new RestoreBlobCommand
                    {
                        ContainerName = string.IsNullOrEmpty(operationalParams.ContainerName)
                            ? "*"
                            : operationalParams.ContainerName,
                        BlobPath = string.IsNullOrEmpty(operationalParams.BlobName) ? "*" : operationalParams.BlobName,
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
