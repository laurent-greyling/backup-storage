using Final.BackupTool.Mvc.Models;
using System.Web.Mvc;

namespace Final.BackupTool.Mvc.Controllers
{
    public class OperationalController : Controller
    {
        // GET: Operational
        public ActionResult Execute(OperationalModel operationalParams)
        {
            var t = operationalParams;
            return View();
        }
    }
}
