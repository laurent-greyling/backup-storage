using System.Threading.Tasks;
using System.Web.Mvc;

namespace Final.BackupTool.Mvc.Models
{
    public class StatusModel
    {
        public Task<HttpStatusCodeResult> Result { get; set; }
    }
}