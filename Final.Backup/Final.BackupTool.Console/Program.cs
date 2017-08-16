using ManyConsole;
using Final.BackupTool.Common.ConsoleCommand;

namespace Final.BackupTool.Console
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // locate any commands in the assembly (or use an IoC container, or whatever source)
            var commands = ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(typeof(BackupCommand));

            // then run them.
            return ConsoleCommandDispatcher.DispatchCommand(commands, args, System.Console.Out);
        }
    }
}