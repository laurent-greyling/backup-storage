using System.Web.Configuration;
using System.Web.Mvc;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Mvc.Controllers
{
    public class OperationalLogsController : Controller
    {
        // GET: Logs
        public ActionResult Index()
        {
            if (!string.IsNullOrEmpty(WebConfigurationManager.AppSettings["OperationalStorageConnectionString"]))
            {
                var downloadLog = new AzureOperations();
                ViewData["LogDetails"] = downloadLog.ReadLatestBlob(OperationalDictionary.Logs);
            }
            
            return View();
        }
    }
}
