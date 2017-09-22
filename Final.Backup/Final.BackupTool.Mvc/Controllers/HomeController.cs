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
            var azureOperations = new AzureOperations();
            var table = azureOperations.OperationsTableReference(OperationalDictionary.GroupsTable);
            var query = new TableQuery();
            var result = table.ExecuteQuery(query).Select(x=>x.PartitionKey).ToList();
            var groups = new SelectList(result);

            CookiesReadWrite.Delete(OperationalDictionary.ProductionCookie);
            CookiesReadWrite.Delete(OperationalDictionary.BackupCookie);
            return View(new OperationalModel{Groups = groups});
        }
    }
}