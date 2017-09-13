using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Final.BackupTool.Mvc.Models;

namespace Final.BackupTool.Mvc.Controllers
{
    public class StatusController : Controller
    {
        // GET: Status
        public ActionResult Index(StatusModel statusModel)
        {
            ViewData["op"] = statusModel.Operation;
            return View();
        }
        
    }
}
