using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Configuration;
using Final.BackupTool.Mvc.Models;
using System.Web.Mvc;
using Final.BackupTool.Common.ConsoleCommand;
using Final.BackupTool.Common.Initialization;
using Final.BackupTool.Common.Operational;
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
            var operationDate = DateTimeOffset.UtcNow;
            var tableCount = 0;
            var containerCount = 0;

            if (operationalParams.Start == "backup")
            {
                Task.Run(async () => await BackUpAsync(operationalParams));
                operation = "Backup Status";
                tableCount = operationalParams.BackupTables ? GetNumberOfTablesInProduction() : 0;
                containerCount = operationalParams.BackupBlobs ? GetNumberOfContainersInProduction() : 0;
            }

            if (operationalParams.Start == "restore")
            {
                Task.Run(async () => await RestoreAsync(operationalParams));
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
