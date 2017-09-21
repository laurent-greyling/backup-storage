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
            CookiesReadWrite.Delete(OperationalDictionary.ProductionCookie);
            CookiesReadWrite.Delete(OperationalDictionary.BackupCookie);
            var cookieValue = CookiesReadWrite.Read(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey);
            var connectionString = operationalLog.OperationalStorageConnectionString;
            var webConfig = WebConfigurationManager.AppSettings["OperationalStorageConnectionString"];

            if (string.IsNullOrEmpty(cookieValue) &&
                string.IsNullOrEmpty(connectionString) &&
                string.IsNullOrEmpty(webConfig)) return View();

            if (string.IsNullOrEmpty(cookieValue) &&
                !string.IsNullOrEmpty(connectionString))
            {
                CookiesReadWrite.Write(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey, connectionString);
            }

            var downloadLog = new AzureOperations();

            if (!string.IsNullOrEmpty(webConfig) 
                || !string.IsNullOrEmpty(cookieValue))
            {
                var latestLog = downloadLog.ReadLatestBlob(OperationalDictionary.Logs);
                ViewData[OperationalDictionary.LogDetails] = latestLog;
                if (!string.IsNullOrEmpty(latestLog))
                {
                    ViewData[OperationalDictionary.ViewLog] = "true";
                }
            }

            if (operationalLog.DownloadLog == "download")
            {
                downloadLog.DownloadBlob(OperationalDictionary.Logs, operationalLog.LastModified);
            }

            if (operationalLog.ViewLog != "view") return View();

            ViewData[OperationalDictionary.LogDetails] = downloadLog.ReadBlob(OperationalDictionary.Logs, operationalLog.LastModified);
            ViewData[OperationalDictionary.ViewLog] = "true";

            return View();
        }
    }
}
