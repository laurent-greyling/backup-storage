using CommandLine;
using CommandLine.Text;

namespace backup_storage
{
    internal class Options
    {
        [Option('b', "backup", HelpText = "backup table and blob storage", Required = false, DefaultValue = false)]
        public bool BackUp { get; set; }

        [Option('r', "restore", HelpText = "restore table and blob storage", Required = false, DefaultValue = false)]
        public bool Restore { get; set; }

        [Option('f', "fillstorage", HelpText = "indicate if storage should be filled with dummy info", Required = false, DefaultValue = false)]
        public bool FillStorage { get; set; }

        [Option('t', "tables", HelpText = "comma seperated string of tables to restore", Required = false, DefaultValue = "*")]
        public string Tables { get; set; }

        [Option('c', "containers", HelpText = "comma seperated string of containers to restore", Required = false, DefaultValue = "all")]
        public string Containers { get; set; }

        [Option('s', "storageconnectionstring", HelpText = "connectionstring to storage that need to be backedup or restored", Required = true)]
        public string StorageConnectionString { get; set; }

        [Option('d', "deststorageconnectionstring", HelpText = "destination connectionstring of storage where storage need to be backedup or restored to", Required = true)]
        public string DestStorageConnectionString { get; set; }

        [Option('m', "snapshot", HelpText = "snapshot time and date you want to restore from point in time - e.g. 07/15/2017 19:05:46", Required = false)]
        public string Momentopname { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = new HelpText
            {
                Heading = new HeadingInfo("backup-storage.exe", "1.00.000"),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true,
            };
            help.AddPreOptionsLine("Usage: [options]");
            help.AddOptions(this);
            return help;
        }
    }
}
