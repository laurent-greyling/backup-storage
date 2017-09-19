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
            ViewData["TableCount"] = $"{statusModel.TableCount}";
            ViewData["ContainerCount"] = $"{statusModel.ContainerCount}";

            var tableStatus = GetTableOperationStatus(statusModel);
            var tableFinishedTime = tableStatus.Count > 0 ? tableStatus[0].EndTime - tableStatus[0].StartTime : null;
            ViewData["TableOperationType"] = !statusModel.BackupTable && !statusModel.RestoreTable
                ? ""
                : tableStatus.Count > 0
                    ? $"{tableStatus[0].OperationType}"
                    : "Waiting to Execute";
            ViewData["TablesCopied"] = tableStatus.Count > 0 ? $"{tableStatus[0].Copied}" : "0";
            ViewData["TablesSkipped"] = tableStatus.Count > 0 ? $"{tableStatus[0].Skipped}" : "0";
            ViewData["TablesFaulted"] = tableStatus.Count > 0 ? $"{tableStatus[0].Faulted}" : "0";
            ViewData["TablesFinishedIn"] = tableStatus.Count > 0 ? tableFinishedTime != null
                ? $"{tableFinishedTime.Value.Days}:{tableFinishedTime.Value.Hours}:{tableFinishedTime.Value.Minutes}:{tableFinishedTime.Value.Seconds}"
                : "" : "";
            ViewData["TableStatus"] = !statusModel.BackupTable && !statusModel.RestoreTable
                ? "Skipped"
                : tableFinishedTime == null
                    ? "Executing..."
                    : "Finished";

            var blobStatus = GetBlobOperationStatus(statusModel);
            var blobFinishedTime = blobStatus.Count > 0 ? blobStatus[0].EndTime - blobStatus[0].StartTime : null;
            ViewData["BlobsOperationType"] = !statusModel.BackupBlobs && !statusModel.RestoreBlobs
                ? ""
                : blobStatus.Count > 0
                    ? $"{blobStatus[0].OperationType}"
                    : "Waiting to Execute";
            ViewData["BlobsCopied"] = blobStatus.Count > 0 ? $"{blobStatus[0].Copied}" : "0";
            ViewData["BlobsSkipped"] = blobStatus.Count > 0 ? $"{blobStatus[0].Skipped}" : "0";
            ViewData["BlobsFaulted"] = blobStatus.Count > 0 ? $"{blobStatus[0].Faulted}" : "0";
            ViewData["BlobsFinishedIn"] = blobStatus.Count > 0 ? blobFinishedTime != null
                ? $"{blobFinishedTime.Value.Days}:{blobFinishedTime.Value.Hours}:{blobFinishedTime.Value.Minutes}:{blobFinishedTime.Value.Seconds}"
                : "" : "";
            ViewData["BlobStatus"] = !statusModel.BackupBlobs && !statusModel.RestoreBlobs
                ? "Skipped"
                : blobFinishedTime == null
                    ? "Executing..."
                    : "Finished";
            
            ModelState.Clear();
            return View();
        }

        private List<StatusModel> GetTableOperationStatus(StatusModel statusModel)
        {
            var operationTableReference = _azureOperation.OperationsTableReference(OperationalDictionary.OperationTableName);

            var query = new TableQuery<StorageOperationEntity>();
            var results = new List<StorageOperationEntity>();

            if (statusModel.BackupTable && statusModel.Operation == "Backup Status")
            {
                var backupTableOperationStore = new StartBackupTableOperationStore();
                results = operationTableReference.ExecuteQuery(query)
                .Where(t => t.PartitionKey == backupTableOperationStore.GetOperationPartitionKey()).ToList();
            }

            if (statusModel.RestoreTable && statusModel.Operation == "Restore Status")
            {
                var restoreTableOperationStore = new StartRestoreTableOperationStore();
                results = operationTableReference.ExecuteQuery(query)
                .Where(t => t.PartitionKey == restoreTableOperationStore.GetOperationPartitionKey()).ToList();
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
            
            if (statusModel.BackupBlobs && statusModel.Operation == "Backup Status")
            {
                var backupBlobOperationStore = new StartBackUpBlobOperationStore();
                results = operationTableReference.ExecuteQuery(query)
                .Where(t => t.PartitionKey == backupBlobOperationStore.GetOperationPartitionKey()).ToList();
            }
            
            if (statusModel.RestoreBlobs && statusModel.Operation == "Restore Status")
            {
                var restoreBlobOperationStore = new StartRestoreBlobOperationStore();
                results = operationTableReference.ExecuteQuery(query)
                .Where(t => t.PartitionKey == restoreBlobOperationStore.GetOperationPartitionKey()).ToList();
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
