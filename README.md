## FileControlUtility
### Usage
- #### Transfering files:
Create the object(s) containing the desired transfer settings:
```csharp
//-- Specify a group of settings for a transfer
TransferSettings ts = new TransferSettings
{
    SourcePath = "C:\\source-directory",
    DestinyPath = "c:\\destiny-directory",
    
    // Whether to delete all files from destiny directory (DeleteUncommonFiles and FileNameConflictMethod will have no effect)
    CleanDestinyDirectory = false,
    // Whether to delete files present in the destiny directory that weren't present in source directory
    DeleteUncommonFiles = true,
    // What to do with filename conflicts
    FileNameConflictMethod = FileNameConflictMethod.REPLACE_DIFFERENT,
    // Whether to copy origin files, otherwise move/delete
    KeepOriginFiles = false,
    // Whether to transfer subfolders and their content, otherwise only top directory
    IncludeSubFolders = true,

    // What to do with the specified files or extensions
    //  ALLOW_ONLY: only specified items will be transfered
    //  IGNORE: all specified items will be ignored
    SpecifiedFileNamesOrExtensionsMode = SpecifiedFileNamesAndExtensionsMode.ALLOW_ONLY,

    // List of specified files or extensions
    SpecifiedFileNamesAndExtensions = new System.Collections.Generic.List<string>() { ".extension1", "filename1.txt" },
	
    // What to do with the specified directories
    SpecifiedDirectoriesMode = SpecifiedEntriesMode.IGNORE,

    // List of specified directories - full and relative paths can be used
    SpecifiedDirectories = new List<string>() 
    {
        @"\git\Example", //relative path
        @"C:\Users\Username\Example" //full path
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
FileControl fc = new FileControl();
fc.ManageFiles(new System.Collections.Generic.List<TransferSettings>() { ts });
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
- #### Override methods
Implement what to do with the error message, exception, and the related filenames when a non fatal error ocurred and other information during the transfer of a particular file (ideal for user interfaces and log) by extending the class FileControl:
```csharp
//-- EXAMPLE:
public class FileControlConsoleImpl : FileControl
{
    //-- Choose what to do with the names of files being executed
    protected override void HandleCurrentFileExecution(string trimmedPathWithFileName, FileInfo originFile, string destinyDir, TransferSettings settings)
    {
        Console.WriteLine($"Transfering: \"{trimmedPathWithFileName}\"");
    }

    //-- Choose what to do with log messages (what happens during the execution)
    protected override void HandleLogMessage(string logMessage)
    {
        Console.WriteLine($"[{DateTime.Now}]: {logMessage}");
    }

    //-- Return an action when the particular file transfer can be repeated...
    protected override FileTransferErrorActionRepeatable HandleTransferErrorRepeatable(string errorMessage, Exception e, FileInfo originFile, string destinyDir, 
        TransferSettings settings)
    {
        Console.WriteLine($"File jumped due to an error: \"{originFile}\"");
        return base.HandleTransferErrorRepeatable(errorMessage, e, originFile, destinyDir, settings);
    }

    //-- and when the particular file transfer can't be repeated
    protected override FileTransferErrorActionNonRepeatable HandleTransferErrorNonRepeatable(string errorMessage, Exception e, FileInfo originFile, string destinyDir,
        TransferSettings settings)
    {
        Console.WriteLine($"File jumped due to an error: \"{originFile}\"");
        return base.HandleTransferErrorNonRepeatable(errorMessage, e, originFile, destinyDir, settings);
    }
}
```
The default return value for errors is JUMP (jump execution to next file).

------------
#### Public methods:
```csharp
public void ManageFiles(List<TransferSettings> settings);
public int FilesTotal(List<TransferSettings> settings);
public void OrganizeEnumeratedFiles(string file, int maxKeptFileCount = 0);
```
#### FilenameConflictMethod (ENUM):
Describes what to do when there are repeated filenames in the destiny directory (conflicts):

|Type|Description|
|:------------ |:------------|
|SKIP|Files will be ignored|
|REPLACE_ALL|Replace all files|
|REPLACE_DIFFERENT|Replace only different files (binary comparison)|
|RENAME_DIFFERENT|Rename different files giving it a number (binary comparison)|
