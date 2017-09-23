using System;
using System.Collections.Generic;
using System.Globalization;
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
        public ActionResult Index(LatestStatusModel status)
        {
            var cookieValue = CookiesReadWrite.Read(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey);
            if (string.IsNullOrEmpty(cookieValue))
            {
                return RedirectToAction("Index", "GetOperationalConnection");
            }
            
            var connectionString = status.ConnectionString;
            var webConfig = WebConfigurationManager.AppSettings["OperationalStorageConnectionString"];
            
            if (string.IsNullOrEmpty(cookieValue) &&
                !string.IsNullOrEmpty(connectionString))
            {
                CookiesReadWrite.Write(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey, connectionString);
                cookieValue = CookiesReadWrite.Read(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey);
            }

            var azureOperations = new AzureOperations();
            var recentStatus = new List<StatusModel>();
            if (!string.IsNullOrEmpty(webConfig)
                || !string.IsNullOrEmpty(cookieValue))
            {
                azureOperations.CreateOperationsTable(OperationalDictionary.OperationTableName);
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
                        PartitionKey = x.PartitionKey,
                        OperationType = x.OperationType,
                        Copied = x.Copied,
                        Skipped = x.Skipped,
                        Faulted = x.Faulted,
                        Stime = x.StartTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentUICulture) ?? OperationalDictionary.Empty,
                        Etime = x.EndTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentUICulture) ?? OperationalDictionary.Empty,
                        FinishedIn = finished != null
                            ? $"{finished.Value.Days} days, {finished.Value.Hours}h:{finished.Value.Minutes}m:{finished.Value.Seconds}s"
                            : OperationalDictionary.Empty,
                        
                        FinalStatus = finished == null
                            ? OperationalDictionary.Executing
                            : OperationalDictionary.Finished
                    };
                }).ToList();

                if (recentStatus.Count > 0) ViewData[OperationalDictionary.ViewRecentStatus] = "true";
            }
            return View(new LatestStatusModel
            {
                ConnectionString = connectionString,
                Status = recentStatus
            });
        }
    }
}
