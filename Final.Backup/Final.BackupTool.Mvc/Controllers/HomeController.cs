using System.Web.Mvc;
using Final.BackupTool.Common.Helpers;

namespace Final.BackupTool.Mvc.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}