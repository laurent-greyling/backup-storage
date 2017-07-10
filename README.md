# backup-storage

## Create storage accounts
For this app to work you need storage accounts for where your storage is and where it need to be backedup. For myself I created a storage in West-Europe and in North-Europe to see that I can copy data over correctly and into different azure storage facilities.

Once you have created these, copy the storage account name and key into the App.config and run it same as web job

```
  <appSettings>
    <add key="StorageConnectionString" value="DefaultEndpointsProtocol=https;AccountName=??;AccountKey=??;EndpointSuffix=core.windows.net" />
    <add key="DestStorageConnectionString" value="DefaultEndpointsProtocol=https;AccountName=??;AccountKey=??;EndpointSuffix=core.windows.net" />
  </appSettings>
```

__OR__ run the command line options (can be debugged in project>properties>debug>command line arguments)

```
<bool> -b : "backup", "backups up table and blob storage"
<bool> -r : "restore","restore table and blob storage"
<bool> -f : "fillstorage", "indicate if storage should be filled with dummy info"
<string> -t: "tables", "list of tables to restore"
<string> -c: "containers", "list of containers to restore"
<string> -s: "storageconnectionstring","connectionstring to storage that need to be backedup or restored"
<string> -d: "deststorageconnectionstring","destination connectionstring of storage where storage need to be backedup or restored to"
```

## Create Table and Blob Storage
This app will first create and populate table and blob storage. Once you start it up it will create these. It will create new containers and tables every time it is run and will populate the existing containers and tables with data. This will allow the storage to ever grow, this is done so testing the copying as your storage grows. 

There is also commented code that will allow you to first run a large number of these instances first before copying the data to new storage. 

```
//for (var i = 0; i < 50; i++)
//{                
   Console.WriteLine("Creating and populating some more dummy tables....");
   CreateTableStorage.CreateAndPopulateTable(storageAccount);
   Console.WriteLine("Finished Creating and populating some more dummy tables....");

   Console.WriteLine("Creating and populating some more dummy blobs....");
   CreateBlobStorage.CreateAndPopulateBlob(storageAccount);
   Console.WriteLine("Finished Creating and populating some more dummy blobs....");
//}
```

The table class also have a method to delete all tables. This is only here as you cannot at time of writing this, delete tables easily from the azure portal. You can also download the [Microsoft Azure Storage Explorer](http://storageexplorer.com/) to view your data.

## Copy and BackUp
After your storage have been populated the copy and backup sections will move your data to the destination storage.

### Copy Table Storage
Currently there are two methods for copying data over to your destination storage.

- __CopyTableStorage__ : This will copy tables to the destination storage via paralellism. This is the most basic copy action to move tables to backup storage. Depending on the size of your storage this will be a bit slow, yet faster than doing copy actions without paralallism.

- __CopyAndBackUpTableStorage__: Of the two methods, this is the faster option to copy tables over. It uses dataflow to setup a pipeline for the data to be moved. Every pipeline is based on blocks linked together. For more info on DataFlow see Stephen Cleary's blog on [dataflow](https://blog.stephencleary.com/2012/09/introduction-to-dataflow-part-1.html)

First I do a ```TransformManyBlock``` whichg is a one-to-n mapping for data items. After this we do an ```ActionBlock``` which is an input buffer combined with a processing task, which executes a delegate for each input item. In this ```ActionBlock``` I nest and do a ```BatchBlock``` to bact operations together in order to enhance the amount of operations that will be done once executed. This batchoperation is then executed in another nested ```ActionBlock``` and linked to the ```BatchBlock``` and waited to complete.

The reason these blocks were nested was in order not to loose the reference to the table you are currently busy with. First I had these blocks outside and linked, but this lost my table reference and data was copied into incorrect tables.

### Copy Blob Storage
Currently there is two methods to copy blob storage over to your destination storage.

- __CopyBlobStorage__: This will copy your blob storage over to a backup destination. This is a basic copy method in a paralell form. This method as well as the dataflow method also show way to exclude containers you are not interested in copying.

- __BackupBlobToStorage__: This is a dataflow method that sets up a pipeline for the data to be moved. Every pipeline is based on blocks linked together (Same as table storage).

