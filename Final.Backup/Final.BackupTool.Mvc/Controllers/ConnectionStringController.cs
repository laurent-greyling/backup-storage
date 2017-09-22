using System;
using System.Web.Mvc;
using Final.BackupTool.Common.Entities;
using Final.BackupTool.Common.Helpers;
using Final.BackupTool.Common.Models;
using Final.BackupTool.Common.Operational;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.Mvc.Controllers
{
    public class ConnectionStringController : Controller
    {
        // GET: ConnectionString
        public ActionResult Index()
        {
            return View();
        }

        // GET: ConnectionString/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: ConnectionString/Create
        public ActionResult Create(ConnectionStringsEntity connectionEntity)
        {
            if (string.IsNullOrEmpty(connectionEntity.PartitionKey)) return View();

            CookiesReadWrite.Write(OperationalDictionary.OperationalCookie,
                OperationalDictionary.OperationalCookieKey,
                connectionEntity.OperationStorageConnectionString);
            connectionEntity.RowKey = DateTimeOffset.MaxValue.Ticks.ToString("d19");

            var azureOperations = new AzureOperations();
            var table = azureOperations.CreateOperationsTable(OperationalDictionary.ConnectionTable);
            var groupTable = azureOperations.CreateOperationsTable(OperationalDictionary.GroupsTable);

            var insertOperation = TableOperation.InsertOrMerge(connectionEntity);
            var insertGroupOperation = TableOperation.InsertOrMerge(new TableEntity
            {
                PartitionKey = connectionEntity.PartitionKey,
                RowKey = connectionEntity.RowKey
            });

            table.Execute(insertOperation);
            groupTable.Execute(insertGroupOperation);

            return RedirectToAction("Index","Home");
        }

        // POST: ConnectionString/Create
        [HttpPost]
        public ActionResult Create(FormCollection collection)
        {
            try
            {
                // TODO: Add insert logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        // GET: ConnectionString/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: ConnectionString/Edit/5
        [HttpPost]
        public ActionResult Edit(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add update logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        // GET: ConnectionString/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: ConnectionString/Delete/5
        [HttpPost]
        public ActionResult Delete(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }
    }
}
