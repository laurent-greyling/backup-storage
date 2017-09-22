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
            var cookieValue = CookiesReadWrite.Read(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey);

            if (string.IsNullOrEmpty(cookieValue))
            {
                return RedirectToAction("Index", "GetOperationalConnection");
            }
            
            var connectionString = operationalLog.OperationalStorageConnectionString;
            var webConfig = WebConfigurationManager.AppSettings["OperationalStorageConnectionString"];
            
            if (string.IsNullOrEmpty(cookieValue) &&
                !string.IsNullOrEmpty(connectionString))
            {
                CookiesReadWrite.Write(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey, connectionString);
            }

            var azureOperations = new AzureOperations();

            if (!string.IsNullOrEmpty(webConfig) 
                || !string.IsNullOrEmpty(cookieValue))
            {
                var latestLog = azureOperations.ReadLatestBlob(OperationalDictionary.Logs);
                ViewData[OperationalDictionary.LogDetails] = latestLog;
                if (!string.IsNullOrEmpty(latestLog))
                {
                    ViewData[OperationalDictionary.ViewLog] = "true";
                }
            }

            if (operationalLog.DownloadLog == "download")
            {
                azureOperations.DownloadBlob(OperationalDictionary.Logs, operationalLog.LastModified);
            }

            if (operationalLog.ViewLog != "view") return View();

            ViewData[OperationalDictionary.LogDetails] = azureOperations.ReadBlob(OperationalDictionary.Logs, operationalLog.LastModified);
            ViewData[OperationalDictionary.ViewLog] = "true";

            return View();
        }
    }
}
