using BackupAndRestoreServices;

namespace BlueRestoreDeploymentPackages
{
    class Program
    {
        static void Main()
        {
            new RestoreDeploymentCommand().Execute();
        }
    }
}
