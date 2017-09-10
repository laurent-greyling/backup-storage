using Final.BackupTool.WebSite.Model;
using Microsoft.AspNetCore.Mvc;

namespace Final.BackupTool.WebSite.Controllers
{
    public class BackupController : Controller
    {
        // POST: BackupTable
        public ActionResult BackupTable(ConnectionStringModel connectionStrings
        )
        {
            var t = connectionStrings.ProductionStorageConnectionString;
            return View();
        }

        // POST: BackupBlob
        public ActionResult BackupBlob(
            string productionStorage,
            string backupStorage,
            string operationalStorage
        )
        {
            return View();
        }

        // POST: BackupAll
        public ActionResult BackupAll(
            string productionStorage,
            string backupStorage,
            string operationalStorage
        )
        {
            return View();
        }
    }
}