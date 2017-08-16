using System;
using System.Diagnostics;
using Final.BackupTool.Common.Initialization;
using Final.BackupTool.Common.Strategy;
using NLog;

namespace Final.BackupTool.Common.ConsoleCommand
{
    public class BackupCommand : ManyConsole.ConsoleCommand
    {
        public BackupCommand()
        {
            IsCommand("backup", "Perform full or incremental backup operation");
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
                operation.BackupAsync().Wait();
                logger.Info($"Total: {sw.Elapsed}");
                logger.Info("*******************************************");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }

            return 0;
        }
    }
}
