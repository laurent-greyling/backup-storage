using System;
using System.Diagnostics;
using Final.BackupTool.Common.Initialization;
using Final.BackupTool.Common.Strategy;
using NLog;

namespace Final.BackupTool.Common.ConsoleCommand
{
    public class RestoreCommand : ManyConsole.ConsoleCommand
    {
        public string ContainerName => "*";
        public string BlobPath => "*";
        public string TableName => "*";
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public bool Force => true;

        public RestoreCommand()
        {
            IsCommand("restore-all", "Restore all tables and blobs");
            HasRequiredOption("e|toDate=", "Specify the date to where you need to update to, must be greater than the fromdate", s => { ToDate = s; });
            HasRequiredOption("d|fromDate=", "Date of the snapshot to restore. Restores the blob with that snapshot date or the last one before it", s => { FromDate = s; });
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

                operation.RestoreAll(this).Wait();
                logger.Info("Total:{0}", sw.Elapsed);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            finally
            {
                logger.Info("*******************************************");
                operation.StoreLogInStorage().Wait();
            }

            return 0;
        }
    }
}

