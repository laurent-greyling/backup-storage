using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Helpers;
using Final.BackupTool.Common.Models;
using Final.BackupTool.Common.Operational;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Mvc.Controllers
{
    public class RecentStatusController : Controller
    {
        public ActionResult Index(StatusModel status)
        {
            CookiesReadWrite.Delete(OperationalDictionary.ProductionCookie);
            CookiesReadWrite.Delete(OperationalDictionary.BackupCookie);

            var cookieValue = CookiesReadWrite.Read(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey);
            var connectionString = status.OperationalStorageConnectionString;
            var webConfig = WebConfigurationManager.AppSettings["OperationalStorageConnectionString"];

            if (string.IsNullOrEmpty(cookieValue) &&
                string.IsNullOrEmpty(connectionString) &&
                string.IsNullOrEmpty(webConfig)) return View();

            if (string.IsNullOrEmpty(cookieValue) &&
                !string.IsNullOrEmpty(connectionString))
            {
                CookiesReadWrite.Write(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey, connectionString);
            }

            var azureOperations = new AzureOperations();
            var recentStatus = new List<StatusModel>();
            if (!string.IsNullOrEmpty(webConfig)
                || !string.IsNullOrEmpty(cookieValue))
            {
                var table = azureOperations.OperationsTableReference(OperationalDictionary.OperationTableName);
                var query = new TableQuery<StorageOperationEntity>();
                var data = table.ExecuteQuery(query)
                    .OrderByDescending(x => x.Timestamp)
                    .Take(20)
                    .ToList();

                recentStatus = data.Select(x =>
                {
                    var finished = x.EndTime - x.StartTime;
                    return new StatusModel
                    {
                        OperationType = x.OperationType,
                        Copied = x.Copied,
                        Skipped = x.Skipped,
                        Faulted = x.Faulted,
                        FinishedIn = finished != null
                            ? $"{finished.Value.Days}:{finished.Value.Hours}:{finished.Value.Minutes}:{finished.Value.Seconds}"
                            : OperationalDictionary.Empty,
                        
                        FinalStatus = finished == null
                            ? OperationalDictionary.Executing
                            : OperationalDictionary.Finished
                    };
                }).ToList();

                if (recentStatus.Count > 0) ViewData[OperationalDictionary.ViewRecentStatus] = "true";
            }

            return View(recentStatus);
        }
    }
}
