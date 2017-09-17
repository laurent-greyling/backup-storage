using System;
using System.Web.Configuration;
using System.Web.Mvc;
using Final.BackupTool.Common.Operational;
using Final.BackupTool.Mvc.Models;

namespace Final.BackupTool.Mvc.Controllers
{
    public class OperationalLogsController : Controller
    {
        // GET: Logs
        public ActionResult Index(OperationalLogModel operationalLog)
        {
            var downloadLog = new AzureOperations();

            if (!string.IsNullOrEmpty(WebConfigurationManager.AppSettings["OperationalStorageConnectionString"]))
            {
                ViewData["LogDetails"] = downloadLog.ReadLatestBlob(OperationalDictionary.Logs);
                ViewData["ViewLog"] = "true";
            }
            else
            {
                WebConfigurationManager.AppSettings["OperationalStorageConnectionString"] =
                    operationalLog.OperationalStorageConnectionString;
            }

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
