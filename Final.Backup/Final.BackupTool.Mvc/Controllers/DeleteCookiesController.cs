using System.Web.Mvc;
using Final.BackupTool.Common.Helpers;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Mvc.Controllers
{
    public class DeleteCookiesController : Controller
    {
        public ActionResult Index()
        {
            CookiesReadWrite.Delete(OperationalDictionary.OperationalCookie);
            CookiesReadWrite.Delete(OperationalDictionary.GroupsTable);
            return RedirectToAction("Index","GetOperationalConnection");
        }
        
    }
}
