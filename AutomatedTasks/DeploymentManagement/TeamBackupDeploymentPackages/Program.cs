using BackupAndRestoreServices;

namespace BlueBackupDeploymentPackages
{
    class Program
    {
        static void Main()
        {
            new BackupDeploymentCommand().Execute();
        }
    }
}
