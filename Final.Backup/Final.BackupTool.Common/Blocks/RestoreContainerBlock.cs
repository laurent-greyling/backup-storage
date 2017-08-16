using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage.Blob;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class RestoreContainerBlock
    {
        public static IPropagatorBlock<string, CopyStorageOperation> Create(BlobOperation blobOperation, StorageConnection storageConnection,
            BlobCommands commands)
        {
            var containerToBlobs = CreateContainerToCopyBlobs(storageConnection, commands);
            var copyBlob = CreateCopyBlobs(blobOperation, storageConnection, commands.Force);

            containerToBlobs.LinkTo(copyBlob, new DataflowLinkOptions { PropagateCompletion = true });

            return DataflowBlock.Encapsulate(containerToBlobs, copyBlob);
        }

        private static TransformBlock<CopyStorageOperation, CopyStorageOperation> CreateCopyBlobs(BlobOperation blobOperation,
            StorageConnection storageConnection, bool force)
        {
            return new TransformBlock<CopyStorageOperation, CopyStorageOperation>(
                async operation =>
                {
                    try
                    {
                        switch (operation.SourceBlobType)
                        {
                            case BlobType.BlockBlob:
                                var sourceBlobClient = storageConnection.BackupStorageAccount.CreateCloudBlobClient();

                                var sourceContainer =
                                    sourceBlobClient.GetContainerReference(operation.SourceContainerName);

                                var sourceBlob = sourceContainer.GetBlockBlobReference(operation.SourceBlobName, operation.Snapshot);

                                var destinationBlobClient =
                                    storageConnection.ProductionStorageAccount.CreateCloudBlobClient();

                                var destinationContainer =
                                    destinationBlobClient.GetContainerReference(operation.DestinationContainerName);

                                var destinationBlob =
                                    destinationContainer.GetBlockBlobReference(operation.SourceBlobName);

                                if (!force && destinationBlob.Exists())
                                {
                                    operation.CopyStatus = StorageCopyStatus.Skipped;
                                    return operation;
                                }

                                var success = await CopyBlockBlobAsync(sourceBlob, destinationBlob, blobOperation);

                                operation.CopyStatus = success ? StorageCopyStatus.Completed : StorageCopyStatus.Faulted;
                                return operation;
                            default:
                                operation.CopyStatus = StorageCopyStatus.Faulted;
                                operation.ExtraInformation =
                                    $"Unsupported blob type: {operation.SourceBlobType}";
                                return operation;
                        }
                    }
                    catch (Exception e)
                    {
                        operation.CopyStatus = StorageCopyStatus.Faulted;
                        operation.ExtraInformation = e;
                        return operation;
                    }
                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 16,
                    BoundedCapacity = 16,
                    EnsureOrdered = false
                }
            );
        }

        private static List<CloudBlockBlob> GetBlobItems(string blobsToRestore,
           string snapShotTime,
           string endSnapShotTime,
           CloudBlobContainer container)
        {
            var blobs = blobsToRestore.Replace(" ", "").Split(',').ToList();
            if (!string.IsNullOrEmpty(snapShotTime))
            {
                try
                {
                    var from = DateTimeOffset.ParseExact(snapShotTime, "yyyy-MM-ddTHH:mm:ss",
                        CultureInfo.InvariantCulture);
                    var to = DateTimeOffset.ParseExact(endSnapShotTime, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

                    var snapShotsItems = container.ListBlobs(useFlatBlobListing: true,
                            blobListingDetails: BlobListingDetails.All).Cast<CloudBlockBlob>()
                        .Where(s => s.IsSnapshot && s.SnapshotTime.GetValueOrDefault().DateTime >= from.DateTime)
                        .ToList();

                    if (snapShotsItems.Count > 1)
                    {
                        snapShotsItems = snapShotsItems
                            .Where(c => c.SnapshotTime.GetValueOrDefault().DateTime < to.DateTime).ToList();
                    }

                    return blobs.Contains("*")
                        ? snapShotsItems
                        : snapShotsItems.Where(c => blobs.Any(n => n == c.Name)).ToList();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLineAsync($"Could not retrieve blob; {e}");
                }
            }

            throw new Exception("Set d|date= to a time stamp of 2017-07-15T19:05:46 to restore");
        }

        private static TransformManyBlock<string, CopyStorageOperation>
            CreateContainerToCopyBlobs(StorageConnection storageConnection, BlobCommands commands)
        {
            var containerToBlobs = new TransformManyBlock<string, CopyStorageOperation>(
                async containerName =>
                {
                    Console.WriteLine($"Processing container: {containerName}");

                    // Make sure the container is created in the destination side
                    var destinationBlobClient = storageConnection.ProductionStorageAccount.CreateCloudBlobClient();
                    var destinationContainer = destinationBlobClient.GetContainerReference(containerName);
                    await destinationContainer.CreateIfNotExistsAsync();

                    var sourceBlobClient = storageConnection.BackupStorageAccount.CreateCloudBlobClient();
                    var container = sourceBlobClient.GetContainerReference(containerName);
                    
                    var blobs = GetBlobItems(commands.BlobPath, commands.FromDate,commands.ToDate, container);

                    return blobs
                        .Select(b => new CopyStorageOperation
                        {
                            SourceContainerName = containerName,
                            SourceBlobName = b.Name,
                            SourceBlobType = b.BlobType,
                            SourceSize = b.Properties.Length,
                            SourceBlobLastModified = b.Properties.LastModified,
                            DestinationContainerName = containerName,
                            Snapshot = b.SnapshotTime
                        }).ToList();
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 16,
                    BoundedCapacity = 16,
                }
            );
            return containerToBlobs;
        }

        private static async Task<bool> CopyBlockBlobAsync(CloudBlockBlob sourceBlob, 
            CloudBlockBlob destinationBlob, 
            BlobOperation blobOperation)
        {
            for (var i = 0; i < 5; ++i)
            {
                var cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;
                
                var copyBlob = CopyBlockBlobAsync(sourceBlob, destinationBlob, cancellationToken, blobOperation);
                
                var completedTask = await Task.WhenAny(copyBlob, Task.Delay(TimeSpan.FromMinutes(2 * (i + 1)), cancellationToken));
                var copyCompleted = completedTask == copyBlob;

                if (!copyCompleted)
                {
                    await Console.Error.WriteLineAsync($"Unable to copy {sourceBlob.Container.Name}/{sourceBlob.Name} (Attempt {i+1})");
                }

                // Also cancel the delay
                cancellationTokenSource.Cancel();
                await completedTask;

                if (copyCompleted)
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task CopyBlockBlobAsync(
            CloudBlockBlob sourceBlob,
            CloudBlockBlob destinationBlob,
            CancellationToken cancellationToken,
            BlobOperation blobOperation)
        {
            try
            {
                using (var stream = await sourceBlob.OpenReadAsync(cancellationToken))
                {
                    CopyMetadata(sourceBlob, destinationBlob);
                    
                    // Mark the blob with the date of the backup
                    destinationBlob.Metadata[OperationalDictionary.BackUpDate] = blobOperation.Date.ToString("u");

                    await destinationBlob.UploadFromStreamAsync(stream, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                // we do cancellations as the copy can get stuck 'indefinitely', stopping our pipeline
                await Console.Error.WriteLineAsync($"Canceled copy for {sourceBlob.Container.Name}/{sourceBlob.Name}");
            }
        }

        private static void CopyMetadata(CloudBlob sourceBlob, ICloudBlob destinationBlob)
        {
            foreach (var prop in sourceBlob.Metadata)
            {
                destinationBlob.Metadata[prop.Key] = prop.Value;
            }
        }
    }
}
