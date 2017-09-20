using System.Web.Mvc;
using Final.BackupTool.Common.Helpers;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Mvc.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            CookiesReadWrite.Delete(OperationalDictionary.ProductionCookie);
            CookiesReadWrite.Delete(OperationalDictionary.BackupCookie);
            return View();
        }
    }
}