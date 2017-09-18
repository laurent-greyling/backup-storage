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
            var downloadLog = new AzureOperations();

            if (!string.IsNullOrEmpty(WebConfigurationManager.AppSettings["OperationalStorageConnectionString"]) 
                || !string.IsNullOrEmpty(CookiesReadWrite.Read("operational", "operationalKey")))
            {
                ViewData["LogDetails"] = downloadLog.ReadLatestBlob(OperationalDictionary.Logs);
                ViewData["ViewLog"] = "true";
            }
            else
            {
                CookiesReadWrite.Write("operational", "operationalKey", operationalLog.OperationalStorageConnectionString);
            }

            ViewData["OperationalStorageConnectionString"] =
                WebConfigurationManager.AppSettings["OperationalStorageConnectionString"] ??
                CookiesReadWrite.Read("operational", "operationalKey");

            if (operationalLog.DownloadLog == "download log")
            {
                downloadLog.DownloadBlob(OperationalDictionary.Logs, operationalLog.LastModified);
            }

            if (operationalLog.ViewLog == "view log")
            {
                ViewData["LogDetails"] = downloadLog.ReadBlob(OperationalDictionary.Logs, operationalLog.LastModified);
                ViewData["ViewLog"] = "true";
            }

            return View();
        }
    }
}
