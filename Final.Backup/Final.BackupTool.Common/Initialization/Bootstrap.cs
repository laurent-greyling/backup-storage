using Final.BackupTool.Common.Strategy;
using NLog;
using SimpleInjector;

namespace Final.BackupTool.Common.Initialization
{
    public static class Bootstrap
    {
        public static Container Container;

        public static void Start()
        {
            Container = new Container();

            Container.Register<ILogger>(() => LogManager.GetLogger("Final.BackupTool.Console"), Lifestyle.Singleton);
            Container.Register<IOperationContext, OperationContext>(Lifestyle.Singleton);
            Container.Register<IBlobOperation, AzureBlobOperation>(Lifestyle.Singleton);
            Container.Register<ITableOperation, AzureTableOperation>(Lifestyle.Singleton);
            
            Container.Verify();
        }
    }
}