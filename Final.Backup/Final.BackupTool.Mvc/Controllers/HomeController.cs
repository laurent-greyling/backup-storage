using System.Web.Mvc;
using Final.BackupTool.Common.Helpers;

namespace Final.BackupTool.Mvc.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            CookiesReadWrite.Delete("production");
            CookiesReadWrite.Delete("backup");
            return View();
        }
    }
}