using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.WindowsAzure.Storage.Blob;
using Final.BackupTool.Common.Operational;

namespace Final.BackupTool.Common.Blocks
{
    public class BackupContainerBlock
    {
        private static readonly StorageConnection StorageConnection = new StorageConnection();

        public static IPropagatorBlock<string, CopyStorageOperation> Create(BlobOperation blobOperation)
        {
            if (blobOperation.OperationType == BlobOperationType.Incremental && !blobOperation.LastOperationDate.HasValue)
            {
                throw new ArgumentException($"{nameof(blobOperation.LastOperationDate)} should be set for incremental backups");
            }

            var containerToBlobs = CreateContainerToCopyBlobs();
            var copyBlob = CreateCopyBlobs(blobOperation);

            containerToBlobs.LinkTo(copyBlob, new DataflowLinkOptions { PropagateCompletion = true });

            return DataflowBlock.Encapsulate(containerToBlobs, copyBlob);
        }

        private static TransformBlock<CopyStorageOperation, CopyStorageOperation> CreateCopyBlobs(BlobOperation blobOperation)
        {
            return new TransformBlock<CopyStorageOperation, CopyStorageOperation>(
                async operation =>
                {
                    if (blobOperation.OperationType == BlobOperationType.Incremental &&
                        operation.SourceBlobLastModified.HasValue &&
                        operation.SourceBlobLastModified.Value <= blobOperation.LastOperationDate.Value)
                    {
                        operation.CopyStatus = StorageCopyStatus.Skipped;
                        return operation;
                    }
                    try
                    {
                        switch (operation.SourceBlobType)
                        {
                            case BlobType.BlockBlob:
                                var sourceBlobClient = StorageConnection.ProductionStorageAccount.CreateCloudBlobClient();
                                var sourceContainer = sourceBlobClient.GetContainerReference(operation.SourceContainerName);
                                var sourceBlob = sourceContainer.GetBlockBlobReference(operation.SourceBlobName);

                                var destinationBlobClient = StorageConnection.BackupStorageAccount.CreateCloudBlobClient();
                                var destinationContainer =
                                    destinationBlobClient.GetContainerReference(operation.DestinationContainerName);
                                var destinationBlob =
                                    destinationContainer.GetBlockBlobReference(operation.SourceBlobName);

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

        private static TransformManyBlock<string, CopyStorageOperation>
            CreateContainerToCopyBlobs()
        {
            var containerToBlobs = new TransformManyBlock<string, CopyStorageOperation>(
                async containerName =>
                {
                    Console.WriteLine($"Processing container: {containerName}");

                    // Make sure the container is created in the destination side
                    var destinationBlobClient = StorageConnection.BackupStorageAccount.CreateCloudBlobClient();
                    var destinationContainer = destinationBlobClient.GetContainerReference(containerName);
                    await destinationContainer.CreateIfNotExistsAsync();

                    var sourceBlobClient = StorageConnection.ProductionStorageAccount.CreateCloudBlobClient();
                    var container = sourceBlobClient.GetContainerReference(containerName);

                    return container.ListBlobs(useFlatBlobListing: true).Cast<CloudBlob>()
                        .Select(b => new CopyStorageOperation
                        {
                            SourceContainerName = containerName,
                            SourceBlobName = b.Name,
                            SourceBlobType = b.BlobType,
                            SourceSize = b.Properties.Length,
                            SourceBlobLastModified = b.Properties.LastModified,
                            DestinationContainerName = containerName
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

                    await destinationBlob.CreateSnapshotAsync(cancellationToken);
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
