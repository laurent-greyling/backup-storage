using System;
using System.Globalization;
using System.Linq;
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
            var tableCount = 0;
            var containerCount = 0;


            CookiesReadWrite.Write("production", "productionKey", operationalParams.ProductionStorageConnectionString);
            CookiesReadWrite.Write("backup", "backupKey", operationalParams.BackupStorageConnectionString);
            CookiesReadWrite.Write("operational", "operationalKey", operationalParams.OperationalStorageConnectionString);
            
            if (operationalParams.Start == "backup")
            {
                Task.Run(async () => await BackUpAsync(operationalParams));
                operation = "Backup Status";
                tableCount = operationalParams.BackupTables ? GetNumberOfTablesInProduction() : 0;
                containerCount = operationalParams.BackupBlobs ? GetNumberOfContainersInProduction() : 0;
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
                tableCount = operationalParams.RestoreTables ? GetNumberOfTablesInBackup() : 0;
                containerCount = operationalParams.RestoreBlobs ? GetNumberOfContainersInBackup() : 0;
            }
            
            var statusModel = new StatusModel
            {
                Operation = operation,
                OperationDate = operationDate,
                TableCount = tableCount,
                ContainerCount = containerCount,
                BackupTable = operationalParams.BackupTables,
                BackupBlobs = operationalParams.BackupBlobs,
                RestoreTable = operationalParams.RestoreTables,
                RestoreBlobs = operationalParams.RestoreBlobs
            };

            return RedirectToAction("Index","Status", statusModel);
        }

        private int GetNumberOfTablesInProduction()
        {
            var azureOperation = new AzureOperations();
            var productionTableStorage = azureOperation.CreateProductionTableClient();
            return productionTableStorage.ListTables().Where(c =>
            {
                var acceptedContainer = c.Name.ToLowerInvariant();
                return !acceptedContainer.StartsWith(OperationalDictionary.Wad) && // Exclude WAD logs
                       !acceptedContainer.StartsWith(OperationalDictionary.WawsAppLogTable) && // Exclude wawsapplogtable tables
                       !acceptedContainer.StartsWith(OperationalDictionary.Activities) && // Exclude runtime data
                       !acceptedContainer.StartsWith(OperationalDictionary.StagedFiles);
            }).Count();
        }

        private int GetNumberOfTablesInBackup()
        {
            var azureOperation = new AzureOperations();
            var blobClient = azureOperation.CreateBackupBlobClient();
            var containerReference = blobClient.GetContainerReference(OperationalDictionary.TableBackupContainer);

            return containerReference.ListBlobs().Count();
        }

        private int GetNumberOfContainersInBackup()
        {
            var azureOperation = new AzureOperations();
            var productionTableStorage = azureOperation.CreateBackupBlobClient();
            return productionTableStorage.ListContainers()
                .Where(c =>
                {
                    var n = c.Name.ToLowerInvariant();
                    return !n.StartsWith(OperationalDictionary.Wad) &&
                           !n.StartsWith(OperationalDictionary.Azure) &&
                           !n.StartsWith(OperationalDictionary.CacheClusterConfigs) &&
                           !n.StartsWith(OperationalDictionary.ArmTemplates) &&
                           !n.StartsWith(OperationalDictionary.DeploymentLog) &&
                           !n.StartsWith(OperationalDictionary.DataDownloads) &&
                           !n.StartsWith(OperationalDictionary.Downloads) &&
                           !n.StartsWith(OperationalDictionary.StagedDashFiles) &&
                           !n.StartsWith(OperationalDictionary.StagedFiles) &&
                           !n.Contains(OperationalDictionary.StageArtifacts) &&
                           !n.Contains(OperationalDictionary.MyDeployments) &&
                           //on RC storage and for local testing ignore this blob
                           !n.Contains(OperationalDictionary.Temporary) &&
                           !n.Equals(OperationalDictionary.Logs) &&
                           !n.StartsWith(OperationalDictionary.TableBackUpContainerName);
                }).Count();
        }

        private int GetNumberOfContainersInProduction()
        {
            var azureOperation = new AzureOperations();
            var productionTableStorage = azureOperation.CreateProductionBlobClient();
            return productionTableStorage.ListContainers()
                .Where(c =>
                {
                    var n = c.Name.ToLowerInvariant();
                    return !n.StartsWith(OperationalDictionary.Wad) &&
                           !n.StartsWith(OperationalDictionary.Azure) &&
                           !n.StartsWith(OperationalDictionary.CacheClusterConfigs) &&
                           !n.StartsWith(OperationalDictionary.ArmTemplates) &&
                           !n.StartsWith(OperationalDictionary.DeploymentLog) &&
                           !n.StartsWith(OperationalDictionary.DataDownloads) &&
                           !n.StartsWith(OperationalDictionary.Downloads) &&
                           !n.StartsWith(OperationalDictionary.StagedDashFiles) &&
                           !n.StartsWith(OperationalDictionary.StagedFiles) &&
                           !n.Contains(OperationalDictionary.StageArtifacts) &&
                           !n.StartsWith(OperationalDictionary.TableBackUpContainerName);
                }).Count();
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
            catch (Exception ex)
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
            catch (Exception e)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }
    }
}
