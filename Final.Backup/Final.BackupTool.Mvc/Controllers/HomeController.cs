using System.Web.Mvc;
using Final.BackupTool.Common.Helpers;

namespace Final.BackupTool.Mvc.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewData["ProductionStorageConnectionString"] = CookiesReadWrite.Read("production", "productionKey") ?? string.Empty;
            ViewData["BackupStorageConnectionString"] = CookiesReadWrite.Read("backup", "backupKey") ?? string.Empty;
            ViewData["OperationalStorageConnectionString"] = CookiesReadWrite.Read("operational", "operationalKey") ?? string.Empty;
            return View();
        }
    }
}