using System.Linq;
using System.Web.Mvc;
using Final.BackupTool.Common.Helpers;
using Final.BackupTool.Common.Models;
using Final.BackupTool.Common.Operational;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Mvc.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            CookiesReadWrite.Delete(OperationalDictionary.GroupsTable);
            if (string.IsNullOrEmpty(CookiesReadWrite.Read(OperationalDictionary.OperationalCookie, OperationalDictionary.OperationalCookieKey)))
            {
                return RedirectToAction("Index","GetOperationalConnection");
            }
            var azureOperations = new AzureOperations();
            azureOperations.CreateOperationsTable(OperationalDictionary.GroupsTable);
            var table = azureOperations.OperationsTableReference(OperationalDictionary.GroupsTable);
            var query = new TableQuery();
            var result = table.ExecuteQuery(query).Select(x=>x.PartitionKey).ToList();
            if (!result.Any())
            {
                return RedirectToAction("Create", "ConnectionString");
            }
            var groups = new SelectList(result);
            
            return View(new OperationalModel{Groups = groups});
        }
    }
}