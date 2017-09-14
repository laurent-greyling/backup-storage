using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Operational;
using Final.BackupTool.Mvc.Models;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Mvc.Controllers
{
    public class StatusController : Controller
    {
        // GET: Status
        public ActionResult Index(StatusModel statusModel)
        {
            ViewData["operation"] = statusModel.Operation;
            ViewData["TableCount"] = $"{statusModel.TableCount}";
            ViewData["ContainerCount"] = $"{statusModel.ContainerCount}";

            var status = GetOperationStatus(statusModel);
            var tableStatus = status.Where(x => x.PartitionKey.StartsWith("tables_")).ToList();

            var tableFinishedTime = tableStatus.Count > 0 ? tableStatus[0].EndTime - tableStatus[0].StartTime : null;
            ViewData["TableOperationType"] = tableStatus.Count > 0 ? $"{tableStatus[0].OperationType}" : "Waiting to Execute";
            ViewData["TablesCopied"] = tableStatus.Count > 0 ? $"{tableStatus[0].Copied}" : "0";
            ViewData["TablesSkipped"] = tableStatus.Count > 0 ? $"{tableStatus[0].Skipped}" : "0";
            ViewData["TablesFaulted"] = tableStatus.Count > 0 ? $"{tableStatus[0].Faulted}" : "0";
            ViewData["TablesFinishedIn"] = tableStatus.Count > 0 ? tableFinishedTime != null
                ? $"{tableFinishedTime.Value.Days}:{tableFinishedTime.Value.Hours}:{tableFinishedTime.Value.Minutes}:{tableFinishedTime.Value.Seconds}"
                : "" : "";
            ViewData["TableStatus"] = tableFinishedTime == null ? "Executing..." : "Finished";

            var blobStatus = status.Where(x => x.PartitionKey.StartsWith("blobs_")).ToList();
            var blobFinishedTime = blobStatus.Count > 0 ? blobStatus[0].EndTime - blobStatus[0].StartTime : null;
            ViewData["BlobsOperationType"] = blobStatus.Count > 0 ? $"{blobStatus[0].OperationType}" : "Waiting to Execute";
            ViewData["BlobsCopied"] = blobStatus.Count > 0 ? $"{blobStatus[0].Copied}" : "0";
            ViewData["BlobsSkipped"] = blobStatus.Count > 0 ? $"{blobStatus[0].Skipped}" : "0";
            ViewData["BlobsFaulted"] = blobStatus.Count > 0 ? $"{blobStatus[0].Faulted}" : "0";
            ViewData["BlobsFinishedIn"] = blobStatus.Count > 0 ? blobFinishedTime != null
                ? $"{blobFinishedTime.Value.Days}:{blobFinishedTime.Value.Hours}:{blobFinishedTime.Value.Minutes}:{blobFinishedTime.Value.Seconds}"
                : "" : "";
            ViewData["BlobStatus"] = blobFinishedTime == null ? "Executing..." : "Finished";

            //TODO: figure out how to give a nice output, faster than this
            var operationDetails = GetOperationDetails(statusModel);
            ViewData["Source"] = "";
            ViewData["Status"] = "";
            ViewData["ExtraInfo"] = "";

            return View();
        }

        private List<StatusModel> GetOperationStatus(StatusModel statusModel)
        {
            var azureOperation = new AzureOperations();
            var operationTableReference = azureOperation.OperationsTableReference(OperationalDictionary.OperationTableName);
            //var operationDetailsTableReference = azureOperation.OperationsTableReference(OperationalDictionary.OperationDetailsTableName);

            var query = new TableQuery<StorageOperationEntity>();
            var results = operationTableReference.ExecuteQuery(query)
                .Where(t => t.StartTime >= statusModel.OperationDate).ToList();

            return results.Select(result => new StatusModel
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

        private List<CopyStorageOperationEntity> GetOperationDetails(StatusModel statusModel)
        {
            var azureOperation = new AzureOperations();
            var operationDetailsTableReference = azureOperation.OperationsTableReference(OperationalDictionary.OperationDetailsTableName);

            var query = new TableQuery<CopyStorageOperationEntity>();
            //var xxx = operationDetailsTableReference.ExecuteQuery(query)
            //    .Where(t => t.Timestamp >= statusModel.OperationDate).ToList();

            return null;
        }
    }
}
