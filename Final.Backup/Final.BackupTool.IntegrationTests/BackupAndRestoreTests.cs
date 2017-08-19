using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Azure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Final.BackupTool.IntegrationTests
{
    /*
     *  WARNING: TEST CURRENTLY CANNOT BE RUN BY MORE THAN A SINGLE WORKSTATION
     */


    [TestClass]
    public class BackupAndRestoreTests
    {
        public static CloudBlobClient ProductionBlobClient { get; private set; }
        public static CloudBlobClient BackupBlobClient { get; private set; }
        public static CloudTableClient ProductionTableClient { get; private set; }

        public static CloudTableClient OperationalTableClient { get; private set; }

        private static List<string> _history;

        private const string TableContainer = "backup-tablestorage";

        // Construct guid but remove characters that pose problems in storage
        public string GetGuid => Guid.NewGuid().ToString().Replace("-", "");


        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            // Get connections
            var productionStorageConnectionString = CloudConfigurationManager.GetSetting("ProductionStorageConnectionString");
            var productionStorageAccount = CloudStorageAccount.Parse(productionStorageConnectionString);
            ProductionBlobClient = productionStorageAccount.CreateCloudBlobClient();
            ProductionTableClient = productionStorageAccount.CreateCloudTableClient();

            var backupStorageConnectionString = CloudConfigurationManager.GetSetting("BackupStorageConnectionString");
            var backupStorageAccount = CloudStorageAccount.Parse(backupStorageConnectionString);
            BackupBlobClient = backupStorageAccount.CreateCloudBlobClient();

            var operationalConnectionString = CloudConfigurationManager.GetSetting("OperationalStorageConnectionString");
            var operationalStorageAccount = CloudStorageAccount.Parse(operationalConnectionString);
            OperationalTableClient = operationalStorageAccount.CreateCloudTableClient();

            // History starts here
            _history = new List<string>();

        }

        [TestInitialize]
        public void TestInitialize()
        {
            _history.Clear();
            DeleteAllBlobContainers(ProductionBlobClient);
            DeleteAllTables(ProductionTableClient);
            DeleteAllBlobContainers(BackupBlobClient);
            ClearAllTables(OperationalTableClient);
        }

        #region Blob tests

        [TestMethod]
        public void Test_RunBackup_IgnoresSystemBlobs()
        {
            // System blob containers
            var wadContainer = $"wad{GetGuid}";
            var azureContainer = $"azure{GetGuid}";
            var cacheClusterConfigsContainer = $"cacheclusterconfigs{GetGuid}";
            var armTemplatesContainer = $"arm-templates{GetGuid}";
            var deploymentLogContainer = $"deploymentlog{GetGuid}";
            var dataDownloadsContainer = $"data-downloads{GetGuid}";
            var downloadsContainer = $"downloads{GetGuid}";
            var stagedFilesContainer = $"staged-files{GetGuid}";
            var stageArtifactsContainer = $"stageartifacts{GetGuid}";
            var anyOtherContainer = $"anyothercontainer{GetGuid}";
            var anyOtherBlob = $"anyotherblob{GetGuid}";

            // Set up blob containers that (may) exist in production but that should not be backed up
            UpsertBlobInProductionContainer(wadContainer, "wadblob", "some wad blob");
            UpsertBlobInProductionContainer(azureContainer, "azureblob", "some azure blob");
            UpsertBlobInProductionContainer(cacheClusterConfigsContainer, "cacheclusterblob", "some cacheclusterblob");
            UpsertBlobInProductionContainer(armTemplatesContainer, "armtemplatesblob", "some armtemplatesblob");
            UpsertBlobInProductionContainer(deploymentLogContainer, "deploymentlogblob", "some deploymentlogblob");
            UpsertBlobInProductionContainer(dataDownloadsContainer, "datadownloadblob", "some datadownloadblob");
            UpsertBlobInProductionContainer(downloadsContainer, "downloadsblob", "some downloadsblob");
            UpsertBlobInProductionContainer(stagedFilesContainer, "stagedfilesblob", "some stagedfilesblob");
            UpsertBlobInProductionContainer(stageArtifactsContainer, "stageartifactsblob", "some stageartifactsblob");
            UpsertBlobInProductionContainer(anyOtherContainer, anyOtherBlob, "anyotherblobvalue");

            BackupToolBackup();

            // Get list
            var backupContainers = BackupBlobClient.ListContainers().Select(n => n.Name);
            var containers = backupContainers as string[] ?? backupContainers.ToArray();

            // Verify ignored containers do not exist in backup
            Assert.IsFalse(containers.Contains(wadContainer));
            Assert.IsFalse(containers.Contains(azureContainer));
            Assert.IsFalse(containers.Contains(cacheClusterConfigsContainer));
            Assert.IsFalse(containers.Contains(armTemplatesContainer));
            Assert.IsFalse(containers.Contains(deploymentLogContainer));
            Assert.IsFalse(containers.Contains(dataDownloadsContainer));
            Assert.IsFalse(containers.Contains(stagedFilesContainer));
            Assert.IsFalse(containers.Contains(stageArtifactsContainer));

            // Verify the one that is not ignored exists
            Assert.IsTrue(ContainerOrBlobExists(BackupBlobClient, anyOtherContainer, anyOtherBlob));
        }

        [TestMethod]
        public void Test_RestoreBlobAfterDelete_MatchesOriginal()
        {
            var blobContainerName = $"container{GetGuid}";
            var blobName = $"blob{GetGuid}";
            const string blobContents = "blob contents";
            
            UpsertBlobInProductionContainer(blobContainerName, blobName, blobContents);
            BackupToolBackup();
            DeleteProductionBlob(blobContainerName, blobName);
            
            // Make sure they're goners 
            Assert.IsFalse(ContainerOrBlobExists(ProductionBlobClient, blobContainerName, blobName));

            var restoreDate = GetRestoreFromDate(blobContainerName, blobName);
            var startDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var endDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .AddHours(2)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            BackupToolRestoreBlob(blobContainerName, blobName, startDate, endDate);

            Assert.AreEqual(blobContents, GetProductionBlob(blobContainerName, blobName));
        }
        
        [TestMethod]
        public void Test_RestoreBlobAfterChangeNoForce_OriginalBlobIsNotRestored()
        {
            var blobContainerName = $"container{GetGuid}";
            var blobName = $"blob{GetGuid}";
            const string blobContentsFirst = "blob contents first";
            const string blobContentsSecond = "blob contents second";

            UpsertBlobInProductionContainer(blobContainerName, blobName, blobContentsFirst);
            BackupToolBackup();
            UpsertBlobInProductionContainer(blobContainerName, blobName, blobContentsSecond);

            // Assert blob contents have changed
            Assert.AreEqual(blobContentsSecond, GetProductionBlob(blobContainerName, blobName));

            var restoreDate = GetRestoreFromDate(blobContainerName, blobName);
            var startDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var endDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .AddHours(2)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            // Do not force restore
            BackupToolRestoreBlob(blobContainerName, blobName, startDate, endDate);

            // Assert previous version is NOT put back
            Assert.AreEqual(blobContentsSecond, GetProductionBlob(blobContainerName, blobName));
        }

        [TestMethod]
        public void Test_RestoreBlobAfterChangeForce_OriginalBlobIsRestored()
        {
            var blobContainerName = $"container{GetGuid}";
            var blobName = $"blob{GetGuid}";
            const string blobContentsFirst = "blob contents first";
            const string blobContentsSecond = "blob contents second";

            UpsertBlobInProductionContainer(blobContainerName, blobName, blobContentsFirst);
            BackupToolBackup();
            UpsertBlobInProductionContainer(blobContainerName, blobName, blobContentsSecond);

            // Assert blob contents have changed
            Assert.AreEqual(blobContentsSecond, GetProductionBlob(blobContainerName, blobName));

            var restoreDate = GetRestoreFromDate(blobContainerName, blobName);
            var startDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var endDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .AddHours(2)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            // Force restore
            BackupToolRestoreBlob(blobContainerName, blobName, startDate, endDate, true);

            // Assert previous version is put back
            Assert.AreEqual(blobContentsFirst, GetProductionBlob(blobContainerName, blobName));
        }

        [TestMethod]
        // Restoring earlier backups is broken at the moment, ignore test for now 
        public void Test_RestoreEarlierBlobAfterDelete_CorrectBlobIsRestored()
        {
            var blobContainerName = $"container{GetGuid}";
            var blobName = $"blob{GetGuid}";
            const string blobContentsFirst = "blob contents first";
            const string blobContentsSecond = "blob contents second";
            
            UpsertBlobInProductionContainer(blobContainerName, blobName, blobContentsFirst);
            var startTime = DateTimeOffset.UtcNow;
            BackupToolBackup();
            var endTime = DateTimeOffset.UtcNow;

            UpsertBlobInProductionContainer(blobContainerName, blobName, blobContentsSecond);

            // Verify the blob has changed
            Assert.AreEqual(blobContentsSecond, GetProductionBlob(blobContainerName, blobName));
            BackupToolBackup();
            DeleteProductionBlob(blobContainerName, blobName);

            var restoreDate = GetSnapShotDate(blobContainerName,startTime, endTime);
            var startDate = DateTimeOffset.ParseExact(restoreDate, "d-M-yyyy H:mm:ss +00:00", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var endDate = DateTimeOffset.ParseExact(restoreDate, "d-M-yyyy H:mm:ss +00:00", CultureInfo.InvariantCulture)
                .AddSeconds(3)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            // Restore the blob from the first backup
            BackupToolRestoreBlob(blobContainerName, blobName, startDate, endDate);
            Assert.AreEqual(blobContentsFirst, GetProductionBlob(blobContainerName, blobName));
        }

       #endregion

        #region Blob Container tests

        [TestMethod]
        public void Test_RestoreContainerAfterDelete_MatchesOriginal()
        {
            var blobContainerName = $"container{GetGuid}";
            var blob1Name = $"blob-1-{GetGuid}";
            var blob2Name = $"blob-2-{GetGuid}";
            var blob3Name = $"blob-3-{GetGuid}";
            const string blob1Contents = "blob contents 1";
            const string blob2Contents = "blob contents 2";
            const string blob3Contents = "blob contents 3";

            // Populate the container
            UpsertBlobInProductionContainer(blobContainerName, blob1Name, blob1Contents);
            UpsertBlobInProductionContainer(blobContainerName, blob2Name, blob2Contents);
            UpsertBlobInProductionContainer(blobContainerName, blob3Name, blob3Contents);

            var restoreDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            // Back it up
            BackupToolBackup();
            var endDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            // Oh noes! Disaster strikes!
            DeleteProductionContainer(blobContainerName);

            // Make sure they're goners 
            Assert.IsFalse(ContainerOrBlobExists(ProductionBlobClient, blobContainerName));
            
            // Put the latest back
            BackupToolRestoreBlob(blobContainerName, "*", restoreDate, endDate);

            // Assert they're back and in mint condition
            Assert.AreEqual(blob1Contents, GetProductionBlob(blobContainerName, blob1Name));
            Assert.AreEqual(blob2Contents, GetProductionBlob(blobContainerName, blob2Name));
            Assert.AreEqual(blob3Contents, GetProductionBlob(blobContainerName, blob3Name));
        }

        [TestMethod]
        public void Test_RestoreContainerAfterChanges_MatchesOriginal()
        {
            var blobContainerName = $"container{GetGuid}";
            var blob1Name = $"blob-1-{GetGuid}";  // will stay the same
            var blob2Name = $"blob-2-{GetGuid}";  // will be changed
            var blob3Name = $"blob-3-{GetGuid}";  // will be deleted
            const string blob1Contents = "blob contents 1";
            const string blob2Contents = "blob contents 2";
            const string blob2ContentsAltered = "blob contents new";
            const string blob3Contents = "blob contents 3";

            // Populate the container
            UpsertBlobInProductionContainer(blobContainerName, blob1Name, blob1Contents);
            UpsertBlobInProductionContainer(blobContainerName, blob2Name, blob2Contents);
            UpsertBlobInProductionContainer(blobContainerName, blob3Name, blob3Contents);

            var restoreDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            // Back it up
            BackupToolBackup();
            var endDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            // Apply some changes
            UpsertBlobInProductionContainer(blobContainerName, blob2Name, blob2ContentsAltered);
            DeleteProductionBlob(blobContainerName, blob3Name);

            // Verify the changes arrived
            Assert.AreEqual(blob2ContentsAltered, GetProductionBlob(blobContainerName, blob2Name));
            Assert.IsFalse(ContainerOrBlobExists(ProductionBlobClient, blobContainerName, blob3Name));

            // Put the latest back
            BackupToolRestoreBlob(blobContainerName, "*", restoreDate, endDate, true);

            // Assert they're back and in mint condition
            Assert.AreEqual(blob1Contents, GetProductionBlob(blobContainerName, blob1Name));
            Assert.AreEqual(blob2Contents, GetProductionBlob(blobContainerName, blob2Name));
            Assert.AreEqual(blob3Contents, GetProductionBlob(blobContainerName, blob3Name));
        }

        [TestMethod]
        public void Test_IncrementalBackup_NewContainersAndNewOrChangedBlobsAreBackedUp()
        {
            var container1Name = $"container1-{GetGuid}";
            var container2Name = $"container2-{GetGuid}";
            var container1Blob1Name = $"container1blob1-{GetGuid}";
            var container1Blob2Name = $"container1blob2-{GetGuid}";
            var container2Blob1Name = $"container2blob1-{GetGuid}";

            const string container1Blob1Value = "container 1 blob 1";
            const string container1Blob2Value = "container 1 blob 2";
            const string container2Blob1Value = "container 2 blob 1";

            // First backup ("full")
            UpsertBlobInProductionContainer(container1Name, container1Blob1Name, container1Blob1Value);
            BackupToolBackup();

            // Create more data
            UpsertBlobInProductionContainer(container1Name, container1Blob2Name, container1Blob2Value);
            UpsertBlobInProductionContainer(container2Name, container2Blob1Name, container2Blob1Value);

            // Second backup ("incremental")
            BackupToolBackup();

            // Verify all containers ands blobs are there
            Assert.IsTrue(ContainerOrBlobExists(BackupBlobClient, container1Name, container1Blob1Name));
            Assert.IsTrue(ContainerOrBlobExists(BackupBlobClient, container1Name, container1Blob2Name));
            Assert.IsTrue(ContainerOrBlobExists(BackupBlobClient, container2Name, container2Blob1Name));

            // Verify second backup did not do a second back up of blobs that were not changed (only 1 snapshot)
            Assert.AreEqual(1, BackupBlobSnapshotCount(container1Name, container1Blob1Name));

            UpsertBlobInProductionContainer(container1Name, container1Blob1Name, "new value");

            // Third backup ("incremental")
            BackupToolBackup();

            // Verify third backup took a snapshot of the changed blob
            Assert.AreEqual(2, BackupBlobSnapshotCount(container1Name, container1Blob1Name));
        }

        #endregion

        #region Table tests
        
        [TestMethod]
        public void Test_RunBackup_IgnoresSystemTables()
        {
            // System tables
            var wadLogTable = $"WadLogTable{GetGuid}";
            var wawsAppLogTable = $"wawsApplogtable{GetGuid}";
            var activitiesTable = $"Activities{GetGuid}";
            var stagedFilesTable = $"StagedFiles{GetGuid}";
            var anyOtherTable = $"AnyOtherTabe{GetGuid}";

            // Set up tables that (may) exist in production but that should not be backed up
            UpsertEntityInProductionTable(wadLogTable, "1234", "456", "wadlog table");
            UpsertEntityInProductionTable(wawsAppLogTable, "123", "456", "wawsapplog table");
            UpsertEntityInProductionTable(activitiesTable, "123", "456", "One of many activities tables");
            UpsertEntityInProductionTable(stagedFilesTable, "123", "456", "One of many staged files tables");

            // Set up a table that should be backed up
            UpsertEntityInProductionTable(anyOtherTable, "123", "456", "Table that should be backed up");

            BackupToolBackup();

            // Verify ignored tables do not exist in backup
            Assert.IsFalse(TableExistsInBackup(wadLogTable));
            Assert.IsFalse(TableExistsInBackup(wawsAppLogTable));
            Assert.IsFalse(TableExistsInBackup(activitiesTable));
            Assert.IsFalse(TableExistsInBackup(stagedFilesTable));

            // Verify that the one table to back up really is there
            Assert.IsTrue(TableExistsInBackup(anyOtherTable));
        }

        [TestMethod]
        public void Test_RestoreTableAfterDelete_MatchesOriginal()
        {
            var tableName = $"table{GetGuid}";
            var par1 = GetGuid;
            var row1 = GetGuid;
            var val1 = $"value{GetGuid}";
            var par2 = GetGuid;
            var row2 = GetGuid;
            var val2 = $"value{GetGuid}";

            UpsertEntityInProductionTable(tableName, par1, row1, val1);
            UpsertEntityInProductionTable(tableName, par2, row2, val2);

            BackupToolBackup();

            DeleteProductionTable(tableName);

            Assert.IsFalse(TableExistsInProduction(tableName));

            var restoreDate = GetRestoreFromDate("backup-tablestorage", tableName);
            var startDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var endDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .AddHours(2)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            BackupToolRestoreTable(tableName, startDate, endDate);

            Assert.AreEqual(val1, GetProductionTableEntity(tableName, par1, row1).Value);
            Assert.AreEqual(val2, GetProductionTableEntity(tableName, par2, row2).Value);
        }

        [TestMethod]
        public void Test_RestoreTableAfterChanges_OriginalTableIsRestored()
        {
            var tableName = $"table{GetGuid}";

            // This entity does not change
            var par1 = GetGuid;
            var row1 = GetGuid;
            var val1 = $"value{GetGuid}";

            // This entity is deleted
            var par2 = GetGuid;
            var row2 = GetGuid;
            var val2 = $"value{GetGuid}";

            // This entity is changed
            var par3 = GetGuid;
            var row3 = GetGuid;
            var val3 = $"value{GetGuid}";
            var val3NewValue = $"value{GetGuid}";

            UpsertEntityInProductionTable(tableName, par1, row1, val1);
            UpsertEntityInProductionTable(tableName, par2, row2, val2);
            UpsertEntityInProductionTable(tableName, par3, row3, val3);

            BackupToolBackup();
            
            DeleteEntityInProductionTable(tableName, par2, row2);
            UpsertEntityInProductionTable(tableName, par3, row3, val3NewValue);

            // Ensure the changes have arrived
            Assert.IsNull(GetProductionTableEntity(tableName, par2, row2));
            Assert.AreEqual(val3NewValue, GetProductionTableEntity(tableName, par3, row3).Value);

            var restoreDate = GetRestoreFromDate("backup-tablestorage", tableName);
            var startDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var endDate = DateTimeOffset.ParseExact(restoreDate, "yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture)
                .AddHours(2)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            BackupToolRestoreTable(tableName, startDate, endDate);

            // Ensure good old values are still there or back
            Assert.AreEqual(val1, GetProductionTableEntity(tableName, par1, row1).Value);
            Assert.AreEqual(val2, GetProductionTableEntity(tableName, par2, row2).Value);
            Assert.AreEqual(val3, GetProductionTableEntity(tableName, par3, row3).Value);
        }

        [TestMethod]
        // Restoring earlier backups is broken at the moment, ignore test for now 
        public void Test_RestoreEarlierTableBackupAfterDelete_CorrectTableIsRestored()
        {
            var tableName = $"table{GetGuid}";

            var par = GetGuid;
            var row = GetGuid;

            // Two versions of value
            var value1 = $"value{GetGuid}";
            var value2 = $"value{GetGuid}";

            UpsertEntityInProductionTable(tableName, par, row, value1);
            Assert.AreEqual(value1, GetProductionTableEntity(tableName, par, row).Value);

            var startTime = DateTimeOffset.UtcNow;
            BackupToolBackup();
            var endTime = DateTimeOffset.UtcNow;

            UpsertEntityInProductionTable(tableName, par, row, value2);
            Assert.AreEqual(value2, GetProductionTableEntity(tableName, par, row).Value);

            BackupToolBackup();

            var restoreDate = GetSnapShotDate("backup-tablestorage",startTime, endTime);
            var startDate = DateTimeOffset.ParseExact(restoreDate, "d-M-yyyy H:mm:ss +00:00", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var endDate = DateTimeOffset.ParseExact(restoreDate, "d-M-yyyy H:mm:ss +00:00", CultureInfo.InvariantCulture)
                .AddSeconds(3)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            BackupToolRestoreTable(tableName, startDate, endDate);

            // Ensure original value is in there
            Assert.AreEqual(value1, GetProductionTableEntity(tableName, par, row).Value);
        }

        #endregion

        #region Test Helpers

        private static bool ContainerOrBlobExists(CloudBlobClient blobClient, string containerName, string blobName = null)
        {
            var container = blobClient.GetContainerReference(containerName);
            return string.IsNullOrEmpty(blobName) ? container.Exists() : container.GetBlobReference(blobName).Exists();
        }

        private static bool TableExistsInProduction(string tableName)
        {
            return ProductionTableClient.GetTableReference(tableName).Exists();
        }

        private static bool TableExistsInBackup(string tableName)
        {
            var backupContainer = BackupBlobClient.GetContainerReference(TableContainer);
            var backupTables = backupContainer.ListBlobs(null, true, BlobListingDetails.Metadata)
                .Where(blob => blob.Uri.ToString().Contains(tableName));
            return backupTables.Any();
        }

        private static int BackupBlobSnapshotCount(string containerName, string blobName)
        {
            // If more than one blob exists with the given name, the others are snapshots
            var blobs = BackupBlobClient.GetContainerReference(containerName).ListBlobs(blobName, true, BlobListingDetails.Snapshots);
            return blobs.Count() - 1; // -1 to compensate for the current blob entry
        }

        private static string GetProductionBlob(string containerName, string blobName)
        {
            var container = ProductionBlobClient.GetContainerReference(containerName);
            var blob = container.GetBlockBlobReference(blobName);
            return blob.DownloadText();
        }

        private static SimpleEntity GetProductionTableEntity(string tableName, string partitionKey, string rowKey)
        {
            var table = ProductionTableClient.GetTableReference(tableName);
            var retrieveOperation = TableOperation.Retrieve<SimpleEntity>(partitionKey, rowKey);
            var query = table.Execute(retrieveOperation);
            return (SimpleEntity) query.Result;
        }

        private static void DeleteAllBlobContainers(CloudBlobClient blobClient)
        {
           foreach(var container in blobClient.ListContainers())
           {
               // Delete every container except the table backup container because recreating it may cause 409 errors
               // This only happens for the backup blob client
               if (container.Name != TableContainer)
               {
                   PerformDeleteOperationWithRetries(() => container.Delete());
                }
           }
        }

        private static void DeleteProductionBlob(string containerName, string blobName)
        {
            var container = ProductionBlobClient.GetContainerReference(containerName);
            var blob = container.GetBlockBlobReference(blobName);
            blob.Delete();
            // Give it some time too
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        private static void DeleteAllTables(CloudTableClient tableClient)
        {
            foreach (var table in tableClient.ListTables())
            {
                PerformDeleteOperationWithRetries(() => table.Delete());
            }
        }

        private static void ClearAllTables(CloudTableClient tableClient)
        {
            foreach (var table in tableClient.ListTables())
            {
                // This is rather inefficient, but given that the tables in the tests should be small, quick enough
                var entities = table.ExecuteQuery(new TableQuery<DynamicTableEntity>()).ToList();
                foreach (var entity in entities)
                {
                    table.Execute(TableOperation.Delete(entity));
                }
                
            }
        }
        private static string GetSnapShotDate(string containerName, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            var container = BackupBlobClient.GetContainerReference(containerName);
            var restoreDate = container.ListBlobs(blobListingDetails: BlobListingDetails.Snapshots, useFlatBlobListing: true)
                .Cast<CloudBlockBlob>().OrderByDescending(s=>s.SnapshotTime).Where(s=>s.SnapshotTime > startTime && s.SnapshotTime < endTime)
                .Select(c => c.SnapshotTime).ToList().First();
            return restoreDate.ToString();
        }

        private static string GetRestoreFromDate(string containerName, string blobName)
        {
            var container = BackupBlobClient.GetContainerReference(containerName);
            var restoreDate =
                container.ListBlobs(blobListingDetails: BlobListingDetails.Metadata, useFlatBlobListing: true)
                    .Cast<CloudBlockBlob>()
                    .Where(c => c.Metadata.ContainsKey("BackupDate") && c.Name == blobName)
                    .Select(c => c.Metadata.Values)
                    .ToList()[0]
                    .First();
            return restoreDate;
        }

        private static void UpsertEntityInProductionTable(string tableName, string partitionKey, string rowKey, string value)
        {
            // Construct table reference, remove dashes from the name as they're not allowed in there
            var table = ProductionTableClient.GetTableReference(tableName.Replace("-",""));
            table.CreateIfNotExists();
            var entity = new SimpleEntity(partitionKey, rowKey, value);
            table.Execute(TableOperation.InsertOrReplace(entity));            
        }

        private static void DeleteEntityInProductionTable(string tableName, string partitionKey, string rowKey)
        {
            var table = ProductionTableClient.GetTableReference(tableName);
            var retrieveOperation = TableOperation.Retrieve<SimpleEntity>(partitionKey, rowKey);
            var retrieveQuery = table.Execute(retrieveOperation);
            var deleteOperation = TableOperation.Delete((SimpleEntity) retrieveQuery.Result);
            table.Execute(deleteOperation);
        }

        private static void UpsertBlobInProductionContainer(string containerName, string blobName, string value)
        {
            var container = ProductionBlobClient.GetContainerReference(containerName);
            container.CreateIfNotExists();
            var blob = container.GetBlockBlobReference(blobName);
            blob.UploadText(value);
        }

        private static void BackupToolBackup()
        {
            // For restoring reference, add to history
            _history.Add($"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}");
            CallApp(new List<string> {"backup"});
            // Give backup a wee bit of time to complete (will be fixed later)
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        private static void BackupToolRestoreTable(string tableName, string restoreDate,string endDate, int back = 0)
        {
            CallApp(new List<string> { "restore-table", $"--tableName={tableName}", $"-d={restoreDate}", $"-e={endDate}" }, back: back);
            // Give AzCopy some time to realize it's not done yet
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        private static void BackupToolRestoreContainer(string containerName, bool force = false, int back = 0)
        {
            CallApp(new List<string> { "restore-blob", $"--containerName={containerName}" }, force, back);
        }

        private static void BackupToolRestoreBlob(string containerName, string blobName,string date, string toDate, bool force = false, int back = 0)
        {
            CallApp(new List<string>{ "restore-blob", $"-c={containerName}", $"-b={blobName}", $"-d={date}", $"-e={toDate}" }, force, back);
        }

        private static void CallApp(List<string> args, bool force = false, int back = 0)
        {
            if (force)
            {
                args.Add("--force=True");
            }
            if (back > 0)
            {
                args.Add($"--date={_history[_history.Count - back - 1]}");
            }
            Console.Program.Main(args.ToArray());
        }
       
        public static void PerformDeleteOperationWithRetries(Action deleteOperation, int maxRetryCount = 60, int delayAfterRetryMs = 1000)
        {
            var retryCount = 0;
            while (retryCount < maxRetryCount)
            {
                retryCount++;
                try
                {
                    deleteOperation();
                    return;
                }
                catch (StorageException ex)
                {
                    switch (ex.RequestInformation.HttpStatusCode)
                    {
                        case 404:
                            return; // It's gone already, we're good
                        case 409:
                            Thread.Sleep(delayAfterRetryMs); // Conflict on the operation. Try again until it works.
                            break;
                        default:
                            throw; // Any other issue needs investigating
                    }
                }
                
            }
            throw new TimeoutException(
                $"Could not succesfully execute supplied action in {maxRetryCount} retries. Delay between attempts: {delayAfterRetryMs} ms");
        }

        private static void DeleteProductionContainer(string containerName)
        {
            var container = ProductionBlobClient.GetContainerReference(containerName);
            PerformDeleteOperationWithRetries(() => container.Delete());
            // A necessary wait because you can't restore a blob straight after you've deleted it
            Thread.Sleep(TimeSpan.FromSeconds(60));
        }

        private static void DeleteProductionTable(string tableName)
        {
            var table = ProductionTableClient.GetTableReference(tableName);
            PerformDeleteOperationWithRetries(() => table.Delete());
            // A necessary wait because you can't restore a table straight after you've deleted it
            Thread.Sleep(TimeSpan.FromSeconds(60));
        }
        #endregion
    }
}
