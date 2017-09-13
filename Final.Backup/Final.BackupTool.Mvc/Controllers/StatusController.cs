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
            ViewData["TableOperationType"] = $"{tableStatus[0].OperationType}";
            ViewData["TablesCopied"] = $"{tableStatus[0].Copied}";
            ViewData["TablesSkipped"] = $"{tableStatus[0].Skipped}";
            ViewData["TablesFaulted"] = $"{tableStatus[0].Faulted}";
            ViewData["TablesFinishedIn"] = $"000";
            
            var blobStatus = status.Where(x => x.PartitionKey.StartsWith("blobs_")).ToList();
            ViewData["BlobsOperationType"] = status.Count > 1 ? $"{blobStatus[0].OperationType}" : "Waiting to Execute";
            ViewData["BlobsCopied"] = status.Count > 1 ? $"{blobStatus[0].Copied}" : "0";
            ViewData["BlobsSkipped"] = status.Count > 1 ? $"{blobStatus[0].Skipped}" : "0";
            ViewData["BlobsFaulted"] = status.Count > 1 ? $"{blobStatus[0].Faulted}" : "0";
            ViewData["BlobsFinishedIn"] = $"000";

            return View();
        }

        private List<StatusModel> GetOperationStatus(StatusModel statusModel)
        {
            var azureOperation = new AzureOperations();
            var operationTableReference = azureOperation.OperationsTableReference(OperationalDictionary.OperationTableName);
            //var operationDetailsTableReference = azureOperation.OperationsTableReference(OperationalDictionary.OperationDetailsTableName);

            var query = new TableQuery<StorageOperationEntity>();
            var results = operationTableReference.ExecuteQuery(query).Where(t => t.StartTime >= statusModel.OperationDate).ToList();

            var status = results.Select(result => new StatusModel
                {
                    PartitionKey = result.PartitionKey,
                    Operation = statusModel.Operation,
                    OperationDate = statusModel.OperationDate,
                    TableCount = statusModel.TableCount,
                    ContainerCount = statusModel.ContainerCount,
                    OperationType = result.OperationType,
                    Copied = result.Copied,
                    Skipped = result.Skipped,
                    Faulted = result.Faulted
                })
                .ToList();

            return status;

        }
        
    }
}
