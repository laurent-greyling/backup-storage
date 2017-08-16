using System;
using System.Diagnostics;
using Final.BackupTool.Common.Initialization;
using Final.BackupTool.Common.Strategy;
using NLog;

namespace Final.BackupTool.Common.ConsoleCommand
{
    public class RestoreBlobCommand : ManyConsole.ConsoleCommand
    {
        public string ContainerName { get; set; }
        public string BlobPath { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public bool Force { get; set; }

        public RestoreBlobCommand()
        {
            IsCommand("restore-blob", "Restore blob or blob container");

            HasRequiredOption("c|containerName=", "Name of the blob container to restore", s => { ContainerName = s; });

            HasRequiredOption("b|blob=", "Name of the blob to restore", s => { BlobPath = s; });
            HasRequiredOption("e|toDate=", "Specify the date to where you need to update to, must be greater than the fromdate", s => { ToDate = s; });
            HasRequiredOption("d|date=", "Date of the snapshot to restore. Restores the blob with that snapshot date or the last one before it", s => { FromDate = s; });
            HasOption("f|force=", "Force restore. Overwrite if it exists in production", s => { Force = Convert.ToBoolean(s); });
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

                operation.RestoreBlobAsync(this).Wait();
                logger.Info("Total:{0}", sw.Elapsed);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }

            return 0;
        }
    }
}