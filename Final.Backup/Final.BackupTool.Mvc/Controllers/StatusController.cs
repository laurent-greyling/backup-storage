using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Models;
using Final.BackupTool.Common.Operational;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Mvc.Controllers
{
    public class StatusController : Controller
    {
        private readonly AzureOperations _azureOperation;

        public StatusController()
        {
            _azureOperation = new AzureOperations();
        }

        // GET: Status
        public ActionResult Index(StatusModel statusModel)
        {
            ViewData["operation"] = statusModel.Operation;

            TableStatusOverView(statusModel);

            BlobStatusOverView(statusModel);

            ModelState.Clear();
            return View();
        }

        private void TableStatusOverView(StatusModel statusModel)
        {
            var tableStatus = GetTableOperationStatus(statusModel);
            var tableFinishedTime = tableStatus.Count > 0 ? tableStatus[0].EndTime - tableStatus[0].StartTime : null;
            ViewData["TableOperationType"] = !statusModel.BackupTable && !statusModel.RestoreTable
                ? OperationalDictionary.Empty
                : tableStatus.Count > 0
                    ? $"{tableStatus[0].OperationType}"
                    : OperationalDictionary.WaitingToExecute;
            ViewData["TablesCopied"] = tableStatus.Count > 0 ? $"{tableStatus[0].Copied}" : OperationalDictionary.Zero;
            ViewData["TablesSkipped"] = tableStatus.Count > 0 ? $"{tableStatus[0].Skipped}" : OperationalDictionary.Zero;
            ViewData["TablesFaulted"] = tableStatus.Count > 0 ? $"{tableStatus[0].Faulted}" : OperationalDictionary.Zero;
            ViewData["TablesFinishedIn"] = tableStatus.Count > 0
                ? tableFinishedTime != null
                    ? $"{tableFinishedTime.Value.Days} days, {tableFinishedTime.Value.Hours}h:{tableFinishedTime.Value.Minutes}m:{tableFinishedTime.Value.Seconds}s"
                    : OperationalDictionary.Empty
                : OperationalDictionary.Empty;
            ViewData["TableStatus"] = !statusModel.BackupTable && !statusModel.RestoreTable
                ? OperationalDictionary.Skipped
                : tableFinishedTime == null
                    ? OperationalDictionary.Executing
                    : OperationalDictionary.Finished;
        }

        private void BlobStatusOverView(StatusModel statusModel)
        {
            var blobStatus = GetBlobOperationStatus(statusModel);
            var blobFinishedTime = blobStatus.Count > 0 ? blobStatus[0].EndTime - blobStatus[0].StartTime : null;
            ViewData["BlobsOperationType"] = !statusModel.BackupBlobs && !statusModel.RestoreBlobs
                ? OperationalDictionary.Empty
                : blobStatus.Count > 0
                    ? $"{blobStatus[0].OperationType}"
                    : OperationalDictionary.WaitingToExecute;
            ViewData["BlobsCopied"] = blobStatus.Count > 0 ? $"{blobStatus[0].Copied}" : OperationalDictionary.Zero;
            ViewData["BlobsSkipped"] = blobStatus.Count > 0 ? $"{blobStatus[0].Skipped}" : OperationalDictionary.Zero;
            ViewData["BlobsFaulted"] = blobStatus.Count > 0 ? $"{blobStatus[0].Faulted}" : OperationalDictionary.Zero;
            ViewData["BlobsFinishedIn"] = blobStatus.Count > 0
                ? blobFinishedTime != null
                    ? $"{blobFinishedTime.Value.Days} days, {blobFinishedTime.Value.Hours}h:{blobFinishedTime.Value.Minutes}m:{blobFinishedTime.Value.Seconds}s"
                    : OperationalDictionary.Empty
                : OperationalDictionary.Empty;
            ViewData["BlobStatus"] = !statusModel.BackupBlobs && !statusModel.RestoreBlobs
                ? OperationalDictionary.Skipped
                : blobFinishedTime == null
                    ? OperationalDictionary.Executing
                    : OperationalDictionary.Finished;
        }

        private List<StatusModel> GetTableOperationStatus(StatusModel statusModel)
        {
            var operationTableReference = _azureOperation.OperationsTableReference(OperationalDictionary.OperationTableName);

            var query = new TableQuery<StorageOperationEntity>();
            var results = new List<StorageOperationEntity>();

            if (operationTableReference.Exists())
            {
                if (statusModel.BackupTable && statusModel.Operation == OperationalDictionary.BackupStatus)
                {
                    var backupTableOperationStore = new StartBackupTableOperationStore();
                    results = operationTableReference.ExecuteQuery(query)
                        .Where(t => t.PartitionKey == backupTableOperationStore.GetOperationPartitionKey()).ToList();
                }

                if (statusModel.RestoreTable && statusModel.Operation == OperationalDictionary.RestoreStatus)
                {
                    var restoreTableOperationStore = new StartRestoreTableOperationStore();
                    results = operationTableReference.ExecuteQuery(query)
                        .Where(t => t.PartitionKey == restoreTableOperationStore.GetOperationPartitionKey()).ToList();
                }
            }

            return results.Where(x=>x.OperationDate > statusModel.OperationDate).Select(result => new StatusModel
                {
                    PartitionKey = result.PartitionKey,
                    Operation = statusModel.Operation,
                    OperationDate = statusModel.OperationDate,
                    TableCount = statusModel.TableCount,
                    ContainerCount = statusModel.ContainerCount,
                    OperationType = result.OperationType,
                    Copied = result.Copied,
                    Skipped = result.Skipped,
                    Faulted = result.Faulted,
                    EndTime = result.EndTime,
                    StartTime = result.StartTime
            }).ToList();
        }

        private List<StatusModel> GetBlobOperationStatus(StatusModel statusModel)
        {
            var operationTableReference = _azureOperation.OperationsTableReference(OperationalDictionary.OperationTableName);

            var query = new TableQuery<StorageOperationEntity>();
            var results = new List<StorageOperationEntity>();

            if (operationTableReference.Exists())
            {
                if (statusModel.BackupBlobs && statusModel.Operation == OperationalDictionary.BackupStatus)
                {
                    var backupBlobOperationStore = new StartBackUpBlobOperationStore();
                    results = operationTableReference.ExecuteQuery(query)
                        .Where(t => t.PartitionKey == backupBlobOperationStore.GetOperationPartitionKey()).ToList();
                }

                if (statusModel.RestoreBlobs && statusModel.Operation == OperationalDictionary.RestoreStatus)
                {
                    var restoreBlobOperationStore = new StartRestoreBlobOperationStore();
                    results = operationTableReference.ExecuteQuery(query)
                        .Where(t => t.PartitionKey == restoreBlobOperationStore.GetOperationPartitionKey()).ToList();
                }
            }

            return results.Where(x => x.OperationDate > statusModel.OperationDate).Select(result => new StatusModel
            {
                PartitionKey = result.PartitionKey,
                Operation = statusModel.Operation,
                OperationDate = statusModel.OperationDate,
                TableCount = statusModel.TableCount,
                ContainerCount = statusModel.ContainerCount,
                OperationType = result.OperationType,
                Copied = result.Copied,
                Skipped = result.Skipped,
                Faulted = result.Faulted,
                EndTime = result.EndTime,
                StartTime = result.StartTime
            }).ToList();
        }
    }
}
