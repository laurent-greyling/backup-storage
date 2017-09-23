using System.Web.Mvc;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Helpers;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Mvc.Controllers
{
    public class GetOperationalConnectionController : Controller
    {
        // GET: GetOperationalConnection
        public ActionResult Index(ConnectionStringsEntity operationalConnection)
        {
            if (string.IsNullOrEmpty(operationalConnection.OperationStorageConnectionString)) return View();
            CookiesReadWrite.Write(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey,
                operationalConnection.OperationStorageConnectionString);

            return RedirectToAction("Index", "Home");
        }
    }
}
