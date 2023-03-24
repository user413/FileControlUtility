## FileControlUtility
### Usage
- #### Transfering files:
Create the object(s) containing the desired transfer settings:
```csharp
//-- Specify a group of settings for a transfer
TransferSettings ts = new()
{
    SourcePath = "C:\\source-directory",
    DestinyPath = "C:\\destiny-directory",

    // Whether to delete all files from destiny directory before transfering
    CleanDestinyDirectory = false,
    // Whether to delete files present in the destiny directory that weren't present in source directory
    DeleteUncommonFiles = true,
    // What to do with filename conflicts
    FileNameConflictMethod = FileNameConflictMethod.REPLACE_DIFFERENT,
    // Whether to copy origin files (keep), otherwise move/delete
    KeepOriginFiles = false,
    // Whether to transfer subfolders and their content, otherwise only top directory
    IncludeSubFolders = true,

    // What to do with the specified files or extensions
    //  TRANSFER_ONLY: only specified items will be transfered
    //  IGNORE: all specified items will be ignored
    FilteredFileNamesOrExtensionsMode = FilterMode.TRANSFER_ONLY,

    // List of specified files and extensions
    FilteredFileNamesAndExtensions = new List<string>()
    {
        ".extension1",
        "filename1.txt",
        @"C:\full-path-to\file.exe"
    },

    // What to do with the specified directories
    FilteredDirectoriesMode = FilterMode.IGNORE,

    // List of specified directories - full and relative paths can be used
    FilteredDirectories = new List<string>()
    {
        @"\git\Example", //relative path
        @"C:\Users\Username\Example", //full path
        @"\\server\Example" //server path
    },

    // Update the numbers from filenames in the destiny directory enumerated with the pattern <name> (<number>)<extension>, 
    // for each file from which they have originated. Works only with FileNameConflictMethod.RENAME_DIFFERENT.
    ReenumerateRenamedFiles = false,

    // The maximum quantity of enumerated files (including the original) selected from highest number in descending way to be kept and re-enumerated,
    // and the excess to be deleted.
    MaxKeptRenamedFileCount = 0
};
```
Begin transfer for the specified settings:
```csharp
FileControl fc = new();
fc.Transfer(new List<TransferSettings>() { ts });
```
Information about a transfer will be present in a FileControl instance:
```csharp
//-- Displaying all successfully transfered files...
foreach (TransferedFilesReport report in fc.TransFilesReports)
    Console.WriteLine($"Successfully transfered file: \"{report.File}\" to \"{report.Destiny}\"");
  
//-- Displaying all files that weren't transfered...
foreach (NotTransferedFilesReport report in fc.NotTransFilesReports)
    Console.WriteLine($"File not transfered: \"{report.File}\" Reason: {report.Reason}");
	
//-- Displaying all files that were replaced...
foreach (ReplacedFilesReport report in fc.ReplacedFilesReports)
    Console.WriteLine($"Successfully replaced file: \"{report.Destiny}\"");

//-- Displaying all renamed files...
foreach (RenamedFilesReport report in fc.RenamedFilesReports)
    Console.WriteLine($"Successfully moved and renamed file: \"{report.File}\" to \"{report.Destiny}\"");

//-- Displaying created directories...
foreach (CreatedDirectoriesReport report in fc.CreatedDirReports)
    Console.WriteLine($"Successfully created directory: \"{report.Directory}\"");

//-- Displaying all removed files and directories...
foreach (RemovedFilesAndDirectoriesReport report in fc.RemovedFilesAndDirReports)
    Console.WriteLine($"Removed file/directory: \"{report.Entry}\" Description: {report.Description}");
```
- #### Events
The ErrorOccured event allows the client to choose a TransferErrorAction which allows the action which caused the error to be retried, skipped or 
canceled. Canceling will shutdown the whole transfer.
```csharp
//-- Examples:

//-- Handle an error and set the next step
fc.ErrorOccured += new ErrorOccuredHandler((sender, args) =>
{
    //-- Skipping all transfer of files by default when errors occur
    if (args.TransferErrorOrigin == TransferErrorOrigin.TransferingFile)
    {
        Console.WriteLine($"Skipping file due to an error: \"{args.OriginFile}\"");
        args.TransferErrorAction = TransferErrorAction.SKIP;
    }
});

//-- Choose what to do with the names of files being executed
fc.FileExecuting += new FileExecutingHandler((sender, args) =>
{
    Console.WriteLine($"Transfering: \"{args.TrimmedPathWithFileName}\"");
});

//-- Choose what to do with log messages
fc.LogMessageGenerated += new LogMessageGeneratedHandler((sender, logMessage) =>
{
    Console.WriteLine($"[{DateTime.Now}]: {logMessage}");
});
```
The default return value for errors is SKIP when no handler is set.

------------
#### Public methods:
```csharp
public void Transfer(List<TransferSettings> settings);
public int FilesTotal(List<TransferSettings> settings);
public void OrganizeEnumeratedFiles(string file, int maxKeptFileCount = 0);
public static string AdjustPath(string path);
```
#### FilenameConflictMethod (ENUM):
Describes what to do when there are repeated filenames in the destiny directory (conflicts):

|Type|Description|
|:------------ |:------------|
|SKIP|Files will be ignored|
|REPLACE_ALL|Replace all files|
|REPLACE_DIFFERENT|Replace only different files (binary comparison)|
|RENAME_DIFFERENT|Rename different files giving it a number (binary comparison)|
