using System;
using System.Web.Configuration;
using System.Web.Mvc;
using Final.BackupTool.Common.Helpers;
using Final.BackupTool.Common.Models;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Mvc.Controllers
{
    public class OperationalLogsController : Controller
    {
        // GET: Logs
        public ActionResult Index(OperationalLogModel operationalLog)
        {
            CookiesReadWrite.Delete("production");
            CookiesReadWrite.Delete("backup");
            var cookieValue = CookiesReadWrite.Read("operational", "operationalKey");
            var connectionString = operationalLog.OperationalStorageConnectionString;
            var webConfig = WebConfigurationManager.AppSettings["OperationalStorageConnectionString"];

            if (string.IsNullOrEmpty(cookieValue) &&
                string.IsNullOrEmpty(connectionString) &&
                string.IsNullOrEmpty(webConfig)) return View();

            if (string.IsNullOrEmpty(cookieValue) &&
                !string.IsNullOrEmpty(connectionString))
            {
                CookiesReadWrite.Write("operational", "operationalKey", connectionString);
            }

            var downloadLog = new AzureOperations();

            if (!string.IsNullOrEmpty(webConfig) 
                || !string.IsNullOrEmpty(cookieValue))
            {
                var latestLog = downloadLog.ReadLatestBlob(OperationalDictionary.Logs);
                ViewData["LogDetails"] = latestLog;
                if (!string.IsNullOrEmpty(latestLog))
                {
                    ViewData["ViewLog"] = "true";
                }
            }

            if (operationalLog.DownloadLog == "download log")
            {
                downloadLog.DownloadBlob(OperationalDictionary.Logs, operationalLog.LastModified);
            }

            if (operationalLog.ViewLog != "view log") return View();

            ViewData["LogDetails"] = downloadLog.ReadBlob(OperationalDictionary.Logs, operationalLog.LastModified);
            ViewData["ViewLog"] = "true";

            return View();
        }
    }
}
