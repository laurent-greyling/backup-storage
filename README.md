# Backup and Restore Azure Table and Blob Storage

## Create storage accounts
For this app to work you need storage accounts for where your storage is and where it need to be backedup. For myself I created a storage in West-Europe and in North-Europe to see that I can copy data over correctly and into different azure storage facilities.

Once you have created these, run the command line options (can be debugged in project>properties>debug>command line arguments)

```
<bool> -b : "backup", "backups up table and blob storage"
<bool> -r : "restore","restore table and blob storage"
<bool> -f : "fillstorage", "indicate if storage should be filled with dummy info"
<string> -t: "tables", "comma seperated string of tables to restore"
<string> -c: "containers", "comma seperated string of containers to restore"
<string> -s: "storageconnectionstring","connectionstring to storage that need to be backedup or restored"
<string> -d: "deststorageconnectionstring","destination connectionstring of storage where storage need to be backedup or restored to"
```

## Create Table and Blob Storage
If you set -f to true this app will first create and populate table and blob storage with dummy info. It will create new containers and tables every time it is run and will populate the existing containers and tables with data. This will allow the storage to ever grow, this is done so testing the copying as your storage grows. 

The code will allow you to first run a large number of these instances first before copying the data to new storage. 

```
for (var i = 0; i < 200; i++)
{                
   Console.WriteLine("Creating and populating some more dummy tables....");
   CreateTableStorage.CreateAndPopulateTable(storageAccount);
   Console.WriteLine("Finished Creating and populating some more dummy tables....");

   Console.WriteLine("Creating and populating some more dummy blobs....");
   CreateBlobStorage.CreateAndPopulateBlob(storageAccount);
   Console.WriteLine("Finished Creating and populating some more dummy blobs....");
}
```

The table class also have a method to delete all tables. This is only here as you cannot at time of writing this, delete tables easily from the azure portal. You can also download the [Microsoft Azure Storage Explorer](http://storageexplorer.com/) to view your data.

__NOTE__ this app when creating table storage will create the same partition key for all entities. In a real world scenario this might not be the case. This will make things either very fast or very slow depending on scenario.

## Copy/BackUp and Restore
After your storage have been populated the copy and backup sections will move your data to the destination storage.

### Copy Table Storage
Currently there are three methods for copying data over to your destination storage.

- __CopyTableStorage__ : This will copy tables to the destination storage via paralellism. This is the most basic copy action to move tables to backup storage. Depending on the size of your storage this will be a bit slow, yet faster than doing copy actions without paralallism.

- __CopyAndBackUpTableStorageAsync__: Of the two methods, this is the faster option to copy tables over. It uses dataflow to setup a pipeline for the data to be moved. Every pipeline is based on blocks linked together. For more info on DataFlow see Stephen Cleary's blog on [dataflow](https://blog.stephencleary.com/2012/09/introduction-to-dataflow-part-1.html)

First I do a ```TransformManyBlock``` which is a one-to-n mapping for data items. After this we do an ```ActionBlock``` which is an input buffer combined with a processing task, which executes a delegate for each input item. In this ```ActionBlock``` I nest and do a ```BatchBlock``` to batch operations together in order to enhance the amount of operations that will be done once executed. This batchoperation is then executed in another nested ```ActionBlock``` and linked to the ```BatchBlock``` and waited to complete.

The reason these blocks were nested was in order not to loose the reference to the table you are currently busy with. First I had these blocks outside and linked, but this lost my table reference and data was copied into incorrect tables.

- __CopyTableStorageIntoBlobAsync__: This method will serialise your table entity into Json and copy the Json structure into blob as backup for your entire table entity. 

Here we could not just directly serialise the the table entity into a json structure as your table from table storage could be a dynamic table entity which then cannot be serialised. For this we create dictionairies that is then serialised as the entire table and sent to your batchblock - see method __SerialiseAndAddEntityToBatchAsync__.

### Restore Table Storage
To restore table storage we only use the dataflow method as this seems to be the faster and better option for now.

- __CopyAndRestoreTableStorageAsync__ : This will copy the specified tables from your backup account and restore them into the specified storage account, be it your original source or a new storage account.

- __RestoreTableStorageFromBlobAsync__ : This method will restore your tables from the backup blob created by __CopyTableStorageIntoBlobAsync__. The Json will be deserialised back into a dynamic table entity with the correct data types in your properties and then batched into groups where the partition keys are the same. This need to be noted, if you have a large group of entities and all the partition keys are different, the restoration will be slower than when you have all the same partition keys.
From batching them all together data will be restored into your destination.

### Copy Blob Storage
Currently there is two methods to copy blob storage over to your destination storage.

- __CopyBlobStorage__: This will copy your blob storage over to a backup destination. This is a basic copy method in a paralell form. This method as well as the dataflow method also show way to exclude containers you are not interested in copying.

- __BackupBlobToStorageAsync__: This is a dataflow method that sets up a pipeline for the data to be moved. Every pipeline is based on blocks linked together (Same as table storage).

