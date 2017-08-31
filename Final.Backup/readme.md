# Nfield Backup Tool v0.9.0

**NOTES:**
- **A new (full) backup to a clean storage must be performed due to backup storage reorganization**

The Nfield Backup tool can back up storage account blobs and tables on a periodic basis. It is intended to be run as a console application on a virtual machine. A first run backs up all business-critical production data (domain blobs and tables) from the configured production account to the configured backup account. Each subsequent run is incremental for new and changed blobs, and performs full backups for tables. Note that backups of tables are stored as blobs in JSON format.

An operational account stores tables that log the backup and restore operations performed. The operational account should not be configured to use a production or backup storage account. However, one operational account to list operations for all regions.   

## Version history

### Current Issues
- None

### v0.9.0
- Restore all added, can now restore all tables and blobs in one go
  - Can also restore individually as previously, but if you need to restore everything use `restore-all`
- Added info to error logging in order to understand exception messages and where they might happen
- Added optional command to skip either tables or blobs when backing up.

### v0.8.1
- Out of memory exception fixes
 - Lowered the amount of memory backup and restore of tables use.
 - Release memory when finished
 
### v0.8
- Restore specified blobs
- Added to log files the restore command lines needed to restore with actual dates
- Some minor improvements and refactoring

### v0.7
- Restore from specified container(s)
   - Can now restore one or many or all containers for backed up blobs. 
- Operation type is always full for restoring blobs and tables.
- Restore from point in time - snapshot - for blob restore
- Force restore
   - If forced a blob will be restored.
   - If no force but also last modified time is null, it will force a restore automatically.
   - Only skip restore if it is not forced and last modified has a value and this value is smaller than what 
     we recorded
- isBackUp removed. Backup and restore split now
- Have table storage have its own building blocks same as blob storage - backup/restore in own blocks
- Have Backup/Restore blob in own classes rather than shared
- Fix duplicate partition key issue in operation tables
- Other refactoring and fixes

### v0.6
- Operational storage account to keep track of backup / restore operations performed 
- Greatly improve backup and restore speed
- Add option to restore multiple tables, or all tables at once
- Set date range for point in time restore for tables
- Automated tests

### v0.5
- Log Backup tool version at log start
- Ignore transient file exceptions when cleaning journal folder rather than crash  

### v0.4
- Fix restore table
- Fix restore blob

### v0.3
- Fix logging bug that crashed the app
- Fix exceeding a local path length that could cause backup to skip `CapiInterviewIdMapping`, `InterviewerWork` and `SurveyParaData` tables
- App no longer swallows errors during copy (it always said it was 'done' even if the copy failed)
- App throws and exits when it cannot locate AzCopy rather than continue to run
- Change configuration item names to make them more clear, removed unnecessary items
- Skip copying non-vital blobs (wads, azure, cache, arm templates, deployment logs, downloads, staged files, staging artifacts)
- Skip copying non-vital tables (wad, wawapplog, activities, staged files)
- Check that before cleaning a backup storage account, it does not accidentally point to a production account due to misconfiguration
- Some code cleanup

### v0.2
- Back up table storages between configured storage accounts

### v0.1
- Back up blob storages between configured storage accounts

## How to use
The command line executable is called `Nfield.BackupTool.Console`. You must edit the configuration settings before the first run.

### Configuration
Configuration is stored in the `Nfield.BackupTool.Console.exe.config` file, using the following settings:

|Setting|Purpose
|:---:|---
|`AzcopyNumberOfConcurrent`|The number of concurrent copy operations that AzCopy can use. It is recommended to multiply the number of CPU cores by 16. Note that concurrent copy operations are throttled by the bandwidth of the connection. The tool is best run 'close' to the storage accounts, such as on a VM in the region where the backup storage accounts are.
|`DaysRetentionAfterDelete`|Sets the period of days a deleted resource is retained in backup until it is deleted. A value of `60` is the recommended default. __Currently not supported__
|`ProductionStorageConnectionString`|Connection string of the storage account that needs to be backed up.
|`BackupStorageConnectionString`|Connection string of the backup account.
|`OperationalStorageConnectionString`|Connection string for the operational storage (do not reuse production or backup accounts).


### Backup
To run a single or incremental backup:

```
Nfield.BackupTool.Console.exe backup <-s=["tables" or "blobs"]>
```

A first-time run, or switching to the next backup storage account, will do a full backup. Subsequent runs will do incremental backups. Incremental backups back up only new or changed blobs. A full backup will always occur for tables.

If the optional parameter is set to either `tables` or `blobs` it will skip backing up this specific storage, e.g. `Nfield.BackupTool.Console.exe backup -s="tables"` will skip table backup and only backup blobs. If left unspecified, all will be restored as in the past. This is mainly implemented to get to a specified section like blobs, little faster,in order to investigate a specific error occuring. 

### Restore
The backup tool can restore individual tables, entire blob containers (from here on called *resource*). A restore is done from one of the storage accounts configured as `BackupstorageConnectionString` to the storage configured as `ProductionStorageConnectionString`.

##### Table restore
To restore tables, use:

```
Nfield.BackupTool.Console.exe restore-table -t=[name] -d=[date] -e=[date]
```

Specifying dates is mandatory. `fromDate` (or -d) and `toDate` (or -e) are dates specifying a date range in which the table is expected to have been backed up. The `fromDate` time would typically match the start time of a backup in the past. These times can be found in the operational log. The `toDate` date / time would typically be a few hours after this time, up to the start time of the next backup. In the current situation of daily backups a 24 hour difference can be used.

Easiest way to get the times for restoring is to go into the container `backup-tablestorage`. Right click on first and last __blob>snapshots>view__, grab restore date you need. This path need to be followed only if you need exact time, for instance you have one of two backups in one day else a 24 hour difference will suffice.

For `tableName` (or -t), specify one or more comma-separated table names of the table(s) to restore, e.g. `-t="myTable"` or `-t="myTable1,myTable2"`. Use `*` to restore all tables (use with caution!). This is not a wildcard that can be used in combination with partial names, e.g. "*this" does not work.

The format for specifying a date is:

```
[YYYY]-[MM]-[DD]T[HH]:[MM]:[SS]
```

If the table to restore does not exist in the production storage, it is recreated. If the table exists in the production storage, records in the production table are either inserted if they do not exist, or updated with the backed up data if they do exist. __Note that in the latter case, 'newer' values may be overwritten with 'older' values stored in the backup.__

##### Blob (container) restore
To restore blobs, use:

```
Nfield.BackupTool.Console.exe restore-blob -c=[containerName]  -b=[blobName] -d=[fromDate] -e=[toDate] -f=[force]>
```

For `containerName` (or -c), specify one or more comma-separated container names to restore, e.g. `-c="myContainer"` or `-c="myContainer1,myContainer2"`. Use `*` to restore all containers (use with caution!). This is not a wildcard that can be used in combination with partial names, e.g. "*this" does not work.

`-d` specify the snapshot date you want to restore from. 
`-e` specify the snapshot date you want to restore to (same as for table storage).

The format for specifying a date is:

```
[YYYY]-[MM]-[DD]T[HH]:[MM]:[SS]
```

`-f` is a bool specifying if you want to force a restore or not. Currently this use is only on container level and serve little purpose. This will serve a greater purpose once restore a specific blob is implemented, but can still be used when a destination blob exist and you want to force the restore for it.

##### Restore All
To restore all blob and tables

```
Nfield.BackupTool.Console.exe restore-all -d=[fromDate] -e=[toDate]
```

This command will restore all tables and blobs where force is always true. This means that this command will take the same amount of time to restore as it would've taken to backup.

### Log File information
The log files contain alot of information that can be used to understand bugs, exceptions and some general informtion about the app. But the most useful information the log file holds is the 

``
===>USE FOR RESTORING<=== 
restore-table -t="*" -d="2017-08-14T10:01:07" -e="2017-08-14T10:01:08" 
restore-blob -c="*" -b="*" -d="2017-08-14T10:01:08" -e="2017-08-14T10:01:08" -f=false 
restore-all -d="2017-08-14T10:01:08" -e="2017-08-14T10:01:08" 
``

This is the command lines you will need to restore. Most of the fields are set to theur default values needed. For example `-t="*"` or `-c="*"` is to restore all tables or containers. But for the convenience of the user the dates `-d` and `-e` is already filled in with the correct dates needed to find and restore the point in time. In this manner you can find the log file of the date you want to restore, copy the command line and the dates are set, only change needed might be `-c`, `-b`, `-t` and `-f`. 