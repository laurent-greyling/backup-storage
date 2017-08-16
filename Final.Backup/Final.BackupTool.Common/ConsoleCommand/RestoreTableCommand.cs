using System;
using System.Diagnostics;
using Final.BackupTool.Common.Initialization;
using Final.BackupTool.Common.Strategy;
using NLog;

namespace Final.BackupTool.Common.ConsoleCommand
{
    public class RestoreTableCommand : ManyConsole.ConsoleCommand
    {
        public string TableName { get; set; }
        public string FromDate { get; set; }

        public string ToDate { get; set; }

        public RestoreTableCommand()
        {
            IsCommand("restore-table", "Restore table");

            HasRequiredOption("t|tableName=", "Name of the table to restore", s => { TableName = s; });

            HasRequiredOption("e|toDate=", "Specify the date to where you need to update to, must be greater than the fromdate", s => { ToDate = s; });

            HasRequiredOption("d|fromDate=", "Date from when you want to restore from.", s => { FromDate = s; });
        }

        public override int Run(string[] remainingArguments)
        {
            Bootstrap.Start();

            var logger = Bootstrap.Container.GetInstance<ILogger>();
            var operation = Bootstrap.Container.GetInstance<IOperationContext>();

            var sw = new Stopwatch();

            try
            {
                sw.Start();

                operation.RestoreTableAsync(this).Wait();
                
                logger.Info($"Total: {sw.Elapsed}");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }

            return 0;
        }
    }
}