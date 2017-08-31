using System;
using System.Diagnostics;
using Final.BackupTool.Common.Initialization;
using Final.BackupTool.Common.Strategy;
using NLog;

namespace Final.BackupTool.Common.ConsoleCommand
{
    public class BackupCommand : ManyConsole.ConsoleCommand
    {
        public string Skip { get; set; }

        public BackupCommand()
        {
            IsCommand("backup", "Perform full or incremental backup operation");
            HasOption("s|skip", "Skip backup of tables or blobs", s => { Skip = s; });
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
                operation.BackupAsync(this).Wait();
                logger.Info($"Total: {sw.Elapsed}");
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
