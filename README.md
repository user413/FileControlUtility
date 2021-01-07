## FileControlUtility
### Usage
- #### Transfering files:
Create the object(s) containing the desired transfer settings:
```csharp
TransferSettings ts = new TransferSettings
{
    // Source directory:
    SourcePath = "C:\\source-directory-example",
    
    // Destiny directory:
    DestinyPath = "C:\\destiny-directory-example",
    
    // Whether to delete all files from destiny directory (DeleteUncommonFiles and FileNameConflictMethod will be ignored if true):
    CleanDestinyDirectory = false,
    
    // Whether to delete files present in the destiny directory that weren't present in source directory:
    DeleteUncommonFiles = true,
    
    // What to do with filename conflicts:
    FileNameConflictMethod = FileNameConflictMethod.REPLACE_DIFFERENT,
    
    // Whether to leave origin files untouched or delete them:
    KeepOriginFiles = false,
    
    // Whether to transfer subfolders and their content:
    MoveSubFolders = true,

    // What to do with the specified files or extensions:
    //  ALLOW_ONLY: only specified items will be transfered
    //  IGNORE: all specified items will be ignored
    SpecifiedFileNamesOrExtensionsMode = SpecifiedFileNamesAndExtensionsMode.ALLOW_ONLY
	
    // List of specified files or extensions:
    SpecifiedFileNamesAndExtensions = new System.Collections.Generic.List<string>() { ".extension1", "filename1.txt" }
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
    //-- Return an action when the particular file transfer can be repeated...
    public override FileTransferErrorActionRepeatable HandleErrorDialogRepeatable(string errorMessage, Exception e, string originFile, string destinyFile)
    {
        Console.WriteLine($"File jumped due to an error: \"{originFile}\"");
        return FileTransferErrorActionRepeatable.JUMP;
    }

    //-- and when the particular file transfer can't be repeated
    public override FileTransferErrorActionNonRepeatable HandleErrorDialogNonRepeatable(string errorMessage, Exception e, string originFile, string destinyFile)
    {
        Console.WriteLine($"File jumped due to an error: \"{originFile}\"");
        return FileTransferErrorActionNonRepeatable.JUMP;
    }

    //-- Choose what to do with log messages (what happens during the execution)
    public override void HandleLogMessage(string logMessage)
    {
        Console.WriteLine($"[{DateTime.Now}]: {logMessage}");
    }

    //-- Choose what to do with the names of files being executed
    public override void HandleCurrentFileExecution(string trimmedPathWithFileName)
    {
        Console.WriteLine($"Transfering: \"{trimmedPathWithFileName}\"");
    }
}
```
The default return value for errors is JUMP (jump execution to next file).

------------
#### FilenameConflictMethod (ENUM):
Describes what to do when there are repeated filenames in the destiny directory (conflicts):

|Type|Description|
|:------------ |:------------|
|DO_NOT_MOVE|Files will be ignored|
|REPLACE_ALL|Replace all files|
|REPLACE_DIFFERENT|Replace only different files (binary comparison)|
|RENAME_DIFFERENT|Rename different files (binary comparison)|
