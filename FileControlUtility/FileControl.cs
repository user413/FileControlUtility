using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileControlUtility
{
    //public class LogSettings
    //{
    //    public bool LogSuccessfulTransfers { get; set; } = false;
    //    public bool LogDeleteFiles { get; set; } = false;
    //    public bool LogDeleteDirectories { get; set; } = false;
    //}

    //public enum FileTransferErrorActionRepeatable
    //{
    //    /// <summary>
    //    /// Ignore current file and jump to next.
    //    /// </summary>
    //    SKIP,
    //    /// <summary>
    //    /// Cancel execution for the instance.
    //    /// </summary>
    //    CANCEL,
    //    /// <summary>
    //    /// Try to transfer current file again.
    //    /// </summary>
    //    RETRY
    //}
    //public enum FileTransferErrorActionNonRepeatable
    //{
    //    /// <summary>
    //    /// Ignore current file and jump to next.
    //    /// </summary>
    //    SKIP,
    //    /// <summary>
    //    /// Cancel execution for the instance.
    //    /// </summary>
    //    CANCEL
    //}
    public enum TransferErrorAction
    {
        /// <summary>
        /// Ignore the action in which the error occured and proceed to the next action.
        /// </summary>
        SKIP,
        /// <summary>
        /// Cancel execution for the whole transfer for all settings.
        /// </summary>
        CANCEL,
        /// <summary>
        /// Retry the action in which the error occured.
        /// </summary>
        RETRY
    }
    public enum TransferErrorOrigin { TransferingFile, DeletingDestinyFileOrDirectory, DeletingSourceFileOrDirectory }

    public class TransferErrorArgs : EventArgs
    {
        public TransferErrorArgs(string errorMessage, Exception exception, TransferErrorOrigin transferErrorOrigin, FileInfo? originFile, string? destinyDirectory,
            TransferSettings transferSettings, TransferErrorAction transferErrorAction = TransferErrorAction.SKIP)
        {
            ErrorMessage = errorMessage;
            Exception = exception;
            TransferErrorOrigin = transferErrorOrigin;
            OriginFile = originFile;
            DestinyDirectory = destinyDirectory;
            TransferSettings = transferSettings;
            TransferErrorAction = transferErrorAction;
        }

        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }
        /// <summary>
        /// The type of action which caused the error.
        /// </summary>
        public TransferErrorOrigin TransferErrorOrigin { get; set; }
        /// <summary>
        /// Available only when TransferErrorOrigin is TransferingFile. Null for the other types.
        /// </summary>
        public FileInfo? OriginFile { get; private set; }
        /// <summary>
        /// Available only when TransferErrorOrigin is TransferingFile. Null for the other types.
        /// </summary>
        public string? DestinyDirectory { get; private set; }
        /// <summary>
        /// The TransferSettings object for the current transfer.
        /// </summary>
        public TransferSettings TransferSettings { get; private set; }
        /// <summary>
        /// Describes what to do with the action which caused the error.<para>Default: <c>TransferErrorAction.SKIP</c></para>
        /// </summary>
        public TransferErrorAction TransferErrorAction { get; set; } = TransferErrorAction.SKIP;
    }
    public class FileExecutingArgs
    {
        public FileExecutingArgs(string trimmedPathWithFileName, FileInfo originFile, string destinyDir, TransferSettings settings)
        {
            TrimmedPathWithFileName = trimmedPathWithFileName;
            OriginFile = originFile;
            DestinyDir = destinyDir;
            Settings = settings;
        }

        /// <summary>
        /// File name containing part of the path that is common between source and destiny files paths informed 
        /// in the <c>TransferSettings</c> object of the transfer.
        /// </summary>
        public string TrimmedPathWithFileName { get; private set; }
        public FileInfo OriginFile { get; private set; }
        public string DestinyDir { get; private set; }
        public TransferSettings Settings { get; private set; }
    }

    public delegate void ErrorOccuredHandler(object sender, TransferErrorArgs args);
    public delegate void LogMessageGeneratedHandler(object sender, string logMessage);
    public delegate void FileExecutingHandler(object sender, FileExecutingArgs args);

    /// <summary>
    /// Main class. To run transfers <c>ManageFiles</c> should be invoked.<para>Most exceptions/errors (non fatal) are managed by the ErrorOccured event, which can 
    /// manage the actions to be taken after an error occurs (retry, skip and cancel), and contains valuable information. Exceptions that the program 
    /// can't handle are thrown.</para><para>If no handler for the <c>ErrorOccured</c> event is set by the client, the default behavior for each action 
    /// which causes an error is to skip.</para>
    /// </summary>
    public partial class FileControl
    {
        private enum RepeatableTaskResultedAction { Continue, Retry, Skip }
        private delegate void RepeatableTask(ref RepeatableTaskResultedAction resultedAction);

        /// <summary>
        /// Handles the loop for a repeatable task. Task will repeat while <c>retryTask</c> parameter from the <c>RepeatableTask</c> delegate is true.
        /// </summary>
        private void HandleRepeatableTask(RepeatableTask task)
        {
            RepeatableTaskResultedAction resultedAction;

            do
            {
                resultedAction = RepeatableTaskResultedAction.Continue;
                task.Invoke(ref resultedAction);
            }
            while (resultedAction == RepeatableTaskResultedAction.Retry);
        }

        //-- Event related properties

        /// <summary>
        /// Happens when an non fatal error occurs during the transfer. The <c>TransferErrorAction</c> property of <c>TransferErrorArgs</c> 
        /// can be set, indicating whether the task in which the error occured must be retried (RETRY), skipped (SKIP) or the 
        /// whole transfer must be canceled (CANCEL).
        /// </summary>
        /// <remarks>Default error action: <c>TransferErrorAction.SKIP</c></remarks>
        public event ErrorOccuredHandler? ErrorOccured;
        /// <summary>
        /// Happens when a log message is generated.
        /// </summary>
        public event LogMessageGeneratedHandler? LogMessageGenerated;
        /// <summary>
        /// Happens before the execution of each file.
        /// </summary>
        public event FileExecutingHandler? FileExecuting;

        public TransferErrorAction OnErrorOccured(TransferErrorArgs args)
        {
            if (ErrorOccured != null)
            {
                ErrorOccured.Invoke(this, args);
                return args.TransferErrorAction;
            }

            return TransferErrorAction.SKIP;
        }
        public void OnLogMessageGenerated(string logMessage) => LogMessageGenerated?.Invoke(this, logMessage);
        public void OnFileExecuting(FileExecutingArgs args) => FileExecuting?.Invoke(this, args);

        public static class DefaultHandlers
        {
            /// <summary>
            /// Built in handler for the LogMessageCreated event. Outputs the log message to the console with a timestamp.
            /// </summary>
            public static void LogMessageGenerated(string logMessage) =>
                Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}]: {logMessage}");

            /// <summary>
            /// Built in handler for the FileExecuting event. Outputs the current file being executed to the console with a timestamp.
            /// </summary>
            public static void FileExecuting(object sender, FileExecutingArgs args) =>
                Console.WriteLine($"Executing: {args.TrimmedPathWithFileName.Substring(args.TrimmedPathWithFileName.LastIndexOf("\\") + 1)}");
        }

        //-- Main code

        public List<TransferedFilesReport> TransferedFilesReports { get; }
        public List<NotTransferedFilesReport> NotTransferedFilesReports { get; }
        public List<RenamedFilesReport> RenamedFilesReports { get; }
        public List<CreatedDirectoriesReport> CreatedDirReports { get; }
        public List<ReplacedFilesReport> ReplacedFilesReports { get; }
        public List<RemovedFilesAndDirectoriesReport> RemovedFilesAndDirReports { get; }

        //public LogSettings LogSettings { get; } = new LogSettings();
        public bool CancelExecution { get; set; } = false;
        //private bool SkipActionExecution;
        //private bool RetryActionExecution;

        public FileControl()
        {
            TransferedFilesReports = new List<TransferedFilesReport>();
            NotTransferedFilesReports = new List<NotTransferedFilesReport>();
            RenamedFilesReports = new List<RenamedFilesReport>();
            CreatedDirReports = new List<CreatedDirectoriesReport>();
            ReplacedFilesReports = new List<ReplacedFilesReport>();
            RemovedFilesAndDirReports = new List<RemovedFilesAndDirectoriesReport>();
        }

        private void ClearReportLists()
        {
            TransferedFilesReports.Clear();
            NotTransferedFilesReports.Clear();
            RenamedFilesReports.Clear();
            CreatedDirReports.Clear();
            ReplacedFilesReports.Clear();
            RemovedFilesAndDirReports.Clear();
        }

        /// <summary>
        /// Main method. Runs the transfer for the specified settings.<para>Information about the last transfer from the instance will be available in
        /// the following properties:</para>
        /// <code>TransferedFilesReports
        /// NotTransferedFilesReports
        /// RenamedFilesReports
        /// CreatedDirReports
        /// ReplacedFilesReports
        /// RemovedFilesAndDirReports
        /// </code>
        /// </summary>
        public void Transfer(List<TransferSettings> settingsList)
        {
            ClearReportLists();

            CancelExecution = false;

            Exception? fatalException = null;

            OnLogMessageGenerated($"##### New transfer ({DateTime.Now.ToLongDateString()})");

            foreach (TransferSettings settings in settingsList)
            {
                try
                {
                    ManageTransferSettings(settings);
                }
                catch (Exception e)
                {
                    if (fatalException == null)
                    {
                        fatalException = e;
                        CancelExecution = true;
                    }
                }
            }

            //OnLogMessageGenerated("DONE");

            if (fatalException != null) throw fatalException;
        }

        private void ManageTransferSettings(TransferSettings settings)
        {
            //bool AllowOnlyExt = false;
            //bool IgnoreExt = false;

            bool deleteUncommonFiles = settings.DeleteUncommonFiles && settings.FileNameConflictMethod != FileNameConflictMethod.RENAME_DIFFERENT;

            if (!CancelExecution)
            {
                OnLogMessageGenerated("## From: " + settings.SourcePath);
                OnLogMessageGenerated("## To: " + settings.DestinyPath);

                switch (settings.FileNameConflictMethod)
                {
                    case FileNameConflictMethod.SKIP:
                        OnLogMessageGenerated("## For repeated filenames: skip");
                        break;
                    case FileNameConflictMethod.REPLACE_ALL:
                        OnLogMessageGenerated("## For repeated filenames: replace all");
                        break;
                    case FileNameConflictMethod.REPLACE_DIFFERENT:
                        OnLogMessageGenerated("## For repeated filenames: replace different files (binary comparison)");
                        break;
                    case FileNameConflictMethod.RENAME_DIFFERENT:
                        OnLogMessageGenerated("## For repeated filenames: rename different files (binary comparison)");
                        break;
                }

                OnLogMessageGenerated("## Include subfolders: " + settings.IncludeSubFolders);
                OnLogMessageGenerated("## Keep files: " + settings.KeepOriginFiles);
                OnLogMessageGenerated("## Clean destiny: " + settings.CleanDestinyDirectory);
                OnLogMessageGenerated("## Delete uncommon: " + settings.DeleteUncommonFiles);
                OnLogMessageGenerated("## Reorganize renamed files: " + settings.ReenumerateRenamedFiles);
                OnLogMessageGenerated("## Max. renamed files kept: " + settings.MaxKeptRenamedFileCount);
                OnLogMessageGenerated("## Specified filenames/extensions mode: " + settings.FilteredFileNamesOrExtensionsMode);
                OnLogMessageGenerated($"## Specified filenames/extensions:" +
                    (settings.FilteredFileNamesAndExtensions == null ? "" : $" \"{string.Join("\",\"", settings.FilteredFileNamesAndExtensions)}\""));
                OnLogMessageGenerated("## Specified directories mode: " + settings.FilteredDirectoriesMode);
                OnLogMessageGenerated($"## Specified directories:" +
                    (settings.FilteredDirectories == null ? "" : $" \"{string.Join("\",\"", settings.FilteredDirectories)}\""));
            }

            string[]? originDirectories = null; //-- Used for IncludeSubFolders, !KeepOriginFiles and deleteUncommonFiles
            List<string> trimmedFiles = new List<string>();
            List<string> trimmedDirectories = new List<string>(); //-- Used for IncludeSubFolders and deleteUncommonFiles
            string[] destinyFiles = { }; //-- Used for deleteUncommonFiles
            string[] destinyDirectories = { }; //-- Used for deleteUncommonFiles
            //List<string>? adjustedSpecDirs = null; //-- adjusted specified directories

            //GenerateFilesAndDirectoriesLists();

            //try
            //{
            //    if (settings.SpecifiedDirectories != null)
            //    {
            //        adjustedSpecDirs = settings.SpecifiedDirectories.Select(d =>
            //            Utility.CharIsPathSeparator(d[0]) && !Utility.CharIsPathSeparator(d[1]) ? //-- if spec dir is relative otherwise full
            //                $"{Path.DirectorySeparatorChar}{Utility.AdjustPath(d)}" :
            //                $"{Utility.AdjustPath(d)}"
            //        ).ToList();
            //    }
            //}
            //catch (Exception e)
            //{
            //    OnLogMessageGenerated($"An error has occurred while formating specified directories list. {e.Message} Aborting.");
            //    throw;
            //}

            try
            {
                OnLogMessageGenerated("Generating files and directories lists...");

                string[] originFiles;

                if (settings.IncludeSubFolders || !settings.KeepOriginFiles)
                    originDirectories = Directory.GetDirectories(settings.SourcePath, "*", SearchOption.AllDirectories);

                if (settings.IncludeSubFolders)
                {
                    originFiles = Directory.GetFiles(settings.SourcePath, "*", SearchOption.AllDirectories);

                    if (deleteUncommonFiles)
                    {
                        if (Directory.Exists(settings.DestinyPath))
                        {
                            destinyDirectories = Directory.GetDirectories(settings.DestinyPath, "*", SearchOption.AllDirectories);
                            destinyFiles = Directory.GetFiles(settings.DestinyPath, "*", SearchOption.AllDirectories);
                        }
                    }
                }
                else
                {
                    originFiles = Directory.GetFiles(settings.SourcePath, "*", SearchOption.TopDirectoryOnly);

                    if (deleteUncommonFiles && Directory.Exists(settings.DestinyPath))
                        destinyFiles = Directory.GetFiles(settings.DestinyPath, "*", SearchOption.TopDirectoryOnly);
                }

                //-- GENERATING COMMON TRIMMED FILE PATHS
                foreach (string file in originFiles)
                    //trimmedFiles.Add(file.Replace(settings.SourcePath, ""));
                    trimmedFiles.Add(file.Substring(settings.SourcePath.Length)); //, file.Length - settings.SourcePath.Length));

                if (settings.IncludeSubFolders)
                    foreach (string originDirectory in originDirectories)
                        //trimmedDirectories.Add(originDirectory.Replace(settings.SourcePath, ""));
                        trimmedDirectories.Add(originDirectory.Substring(settings.SourcePath.Length)); //, originDirectory.Length - settings.SourcePath.Length));
            }
            catch (Exception e)
            {
                OnLogMessageGenerated($"An error has occurred while retrieving files/directories lists. {e.Message} Aborting.");
                throw;
            }

            if (!CancelExecution)
            {
                if (settings.CleanDestinyDirectory)
                {
                    OnLogMessageGenerated("Cleaning destiny directory...");

                    HandleRepeatableTask((ref RepeatableTaskResultedAction fileTransAction) =>
                    {
                        try
                        {
                            if (Directory.Exists(settings.DestinyPath))
                            {
                                DirectoryInfo dir = new DirectoryInfo(settings.DestinyPath);

                                foreach (FileInfo file in dir.GetFiles())
                                {
                                    file.IsReadOnly = false;
                                    file.Delete();
                                    OnLogMessageGenerated($"File deleted: \"{file.FullName}\"");

                                    RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                                    {
                                        Entry = file.FullName,
                                        Description = "Removed file"
                                    });
                                }

                                foreach (DirectoryInfo subdir in dir.GetDirectories())
                                {
                                    subdir.Delete(true);
                                    OnLogMessageGenerated($"Directory deleted: {subdir.FullName}");

                                    RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                                    {
                                        Entry = subdir.FullName,
                                        Description = "Removed origin directory"
                                    });
                                }
                                //for each (string dire in Directory.GetDirectories(settings.DestinyPath, "*", SearchOption.AllDirectories)) Directory.Delete(dire);
                            }
                        }
                        catch (Exception e)
                        {
                            OnLogMessageGenerated($"An error has occurred while cleaning destiny directory. {e.Message}.");

                            fileTransAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs($"An error has occurred while cleaning destiny directory.",
                                e, TransferErrorOrigin.DeletingSourceFileOrDirectory, null, null, settings)));

                            if (CancelExecution)
                            {
                                foreach (string trimmedFile in trimmedFiles)
                                {
                                    NotTransferedFilesReports.Add(new NotTransferedFilesReport
                                    {
                                        File = settings.SourcePath + trimmedFile,
                                        Destiny = Path.GetDirectoryName(settings.DestinyPath + trimmedFile),
                                        Reason = "Canceled"
                                    });
                                }

                                return;
                            }
                        }
                    });
                }

                try
                {
                    OnLogMessageGenerated("Creating directories...");

                    if (!Directory.Exists(settings.DestinyPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(settings.DestinyPath);
                            CreatedDirReports.Add(new CreatedDirectoriesReport
                            {
                                Directory = settings.DestinyPath,
                                Origin = "None (created)"
                            });

                            OnLogMessageGenerated($"Directory created: " + settings.DestinyPath);
                        }
                        catch (Exception e)
                        {
                            OnLogMessageGenerated($"Error: {e} when creating the directory {settings.DestinyPath} Aborting.");
                            //throw new Exception($"An error has occurred when creating the directory {settings.DestinyPath}. {e.Message} Aborting.");
                            throw;
                        }
                    }

                    if (settings.IncludeSubFolders)
                    {
                        foreach (string trimmedDirectory in trimmedDirectories)
                        {
                            //-- MANAGING SPECIFIED DIRECTORIES
                            if (settings.FilteredDirectories != null && settings.FilteredDirectories.Count > 0)
                            {
                                string originDirectory = settings.SourcePath + trimmedDirectory;
                                bool dirIsSpecified = settings.FilteredDirectories.Exists(d =>
                                    Utility.PathIsRelative(d) ?
                                        Utility.PathContainsDirectory(trimmedDirectory, d) :
                                        (
                                            Utility.PathIsSubdirectory(originDirectory, d) ||
                                            d.Equals(originDirectory, StringComparison.OrdinalIgnoreCase)
                                        )
                                );

                                if (
                                    !dirIsSpecified && settings.FilteredDirectoriesMode == FilterMode.TRANSFER_ONLY ||
                                    dirIsSpecified && settings.FilteredDirectoriesMode == FilterMode.IGNORE
                                )
                                    continue;
                            }

                            string destinyDir = settings.DestinyPath + trimmedDirectory;

                            if (!Directory.Exists(destinyDir))
                            {
                                try
                                {
                                    Directory.CreateDirectory(destinyDir);
                                    CreatedDirReports.Add(new CreatedDirectoriesReport
                                    {
                                        Directory = destinyDir,
                                        Origin = settings.SourcePath + trimmedDirectory
                                    });

                                    OnLogMessageGenerated($"Directory created: " + destinyDir);
                                }
                                catch (Exception e)
                                {
                                    OnLogMessageGenerated($"Error: {e} when creating the directory {destinyDir}. Aborting.");
                                    //throw new Exception($"An error has occurred while creating the directory {settings.DestinyPath + trimmedDirectory}. {e.Message} Aborting");
                                    throw;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    foreach (string trimmedFile in trimmedFiles)
                    {
                        NotTransferedFilesReports.Add(new NotTransferedFilesReport
                        {
                            File = settings.SourcePath + trimmedFile,
                            //Destiny = settings.DestinyPath + trimmedFile.Substring(0, trimmedFile.LastIndexOf("\\")),
                            Destiny = Path.GetDirectoryName(settings.DestinyPath + trimmedFile),
                            Reason = "Canceled"
                        });
                    }

                    throw;
                }

                OnLogMessageGenerated("Transfering files...");
            }

            foreach (string trimmedPathWithFileName in trimmedFiles)
            {
                //string originPathWithFileName = settings.SourcePath + trimmedPathWithFileName;
                //string destinyPathWithFileName = settings.DestinyPath + trimmedPathWithFileName;
                //string destinyPathWOFileName = settings.DestinyPath + trimmedPathWithFileName.Substring(0, trimmedPathWithFileName.LastIndexOf("\\"));
                //string currentFileName = trimmedPathWithFileName.Substring(trimmedPathWithFileName.LastIndexOf("\\") + 1);

                FileInfo originFile = new FileInfo(settings.SourcePath + trimmedPathWithFileName);
                FileInfo destinyFile = new FileInfo(settings.DestinyPath + trimmedPathWithFileName);
                string currentFileName = originFile.Name;

                if (CancelExecution)
                {
                    NotTransferedFilesReports.Add(new NotTransferedFilesReport
                    {
                        File = originFile.FullName,
                        Destiny = destinyFile.DirectoryName,
                        Reason = "Canceled"
                    });

                    continue;
                }

                OnFileExecuting(new FileExecutingArgs(trimmedPathWithFileName, originFile, destinyFile.DirectoryName, settings));
                //System.Threading.Thread.Sleep(5000);

                //-- MANAGING SPECIFIED DIRECTORIES
                if (settings.FilteredDirectories != null && settings.FilteredDirectories.Count > 0)
                {
                    string trimmedDir = originFile.DirectoryName.Substring(settings.SourcePath.Length); //, originFile.DirectoryName.Length - settings.SourcePath.Length);

                    bool dirIsSpecified = settings.FilteredDirectories.Exists(d =>
                        Utility.PathIsRelative(d) ?
                            Utility.PathContainsDirectory(trimmedDir, d) :
                            (
                                Utility.PathIsSubdirectory(originFile.DirectoryName, d) ||
                                d.Equals(originFile.DirectoryName, StringComparison.OrdinalIgnoreCase)
                            )
                    );

                    if (
                        !dirIsSpecified && settings.FilteredDirectoriesMode == FilterMode.TRANSFER_ONLY ||
                        dirIsSpecified && settings.FilteredDirectoriesMode == FilterMode.IGNORE
                    )
                    {
                        NotTransferedFilesReports.Add(new NotTransferedFilesReport
                        {
                            File = originFile.FullName,
                            Destiny = destinyFile.DirectoryName,
                            Reason = $"Directory ignored"
                        });

                        continue;
                    }
                }

                //-- MANAGING SPECIFIED FILENAMES AND EXTENSIONS
                if (settings.FilteredFileNamesAndExtensions != null && settings.FilteredFileNamesAndExtensions.Count > 0)
                {
                    string extension = Path.GetExtension(currentFileName);
                    bool fileIsSpecified = settings.FilteredFileNamesAndExtensions.Exists(
                        f => f.Equals(currentFileName, StringComparison.OrdinalIgnoreCase) || f.Equals(originFile.FullName, StringComparison.OrdinalIgnoreCase));

                    bool extensionIsSpecified = settings.FilteredFileNamesAndExtensions.Exists(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));

                    if (
                        (
                            settings.FilteredFileNamesOrExtensionsMode == FilterMode.TRANSFER_ONLY &&
                            !fileIsSpecified && !extensionIsSpecified
                        ) ||
                        (
                            settings.FilteredFileNamesOrExtensionsMode == FilterMode.IGNORE &&
                            (fileIsSpecified || extensionIsSpecified)
                        )
                    )
                    {
                        string ignoredReasonText;
                        //string ignoredLogText;

                        if (settings.FilteredFileNamesOrExtensionsMode == FilterMode.TRANSFER_ONLY)
                        {
                            ignoredReasonText = "Filename or extension ignored";
                            //ignoredLogText = $"Filename or extension ignored. File not transfered:\"'{originFile.FullName}\" as \"{destinyFile.FullName}\"";
                        }
                        else if (settings.FilteredFileNamesOrExtensionsMode == FilterMode.IGNORE && fileIsSpecified)
                        {
                            ignoredReasonText = $"Filename ignored ({currentFileName})";
                            //ignoredLogText = $"Filename ignored ({currentFileName}). File not transfered:\"'{originFile.FullName}\" as \"{destinyFile.FullName}\"";
                        }
                        else
                        {
                            ignoredReasonText = $"File extension ignored ({extension})";
                            //ignoredLogText = $"File extension ignored ({extension}). File not transfered:\"'{originFile.FullName}\" as \"{destinyFile.FullName}\"";
                        }

                        NotTransferedFilesReports.Add(new NotTransferedFilesReport
                        {
                            File = originFile.FullName,
                            Destiny = destinyFile.DirectoryName,
                            Reason = ignoredReasonText
                        });

                        //OnLogMessageGenerated(ignoredLogText);
                        continue;
                    }
                }

                //do
                //    TransferFile(settings, originFile, destinyFile);
                //while (RetryActionExecution);

                HandleRepeatableTask((ref RepeatableTaskResultedAction fileTransAction) =>
                    TransferFile(settings, originFile, destinyFile, ref fileTransAction));
            }

            //-- DELETING ORIGIN DIRECTORIES
            if (!settings.KeepOriginFiles && !CancelExecution)
            {
                OnLogMessageGenerated("Cleaning origin directories...");

                //try
                //{
                foreach (string entry in originDirectories.Reverse())
                {
                    HandleRepeatableTask((ref RepeatableTaskResultedAction fileTransAction) =>
                    {
                        if (Directory.Exists(entry))
                        {
                            try
                            {
                                Directory.Delete(entry, true);

                                //if (LogSettings.LogDeleteDirectories)
                                OnLogMessageGenerated($"Directory/files deleted: \"{entry}\"");

                                RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                                {
                                    Entry = entry,
                                    Description = "Removed origin directory"
                                });
                            }
                            catch (Exception e)
                            {
                                OnLogMessageGenerated($"Error: {e} when deleting origin directory \"{entry}\".");

                                fileTransAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs($"An error has occurred while deleting origin directory " +
                                    $"\"{entry}\".", e, TransferErrorOrigin.DeletingSourceFileOrDirectory, null, null, settings)));
                            }
                        }
                    });

                    if (CancelExecution) return;

                    //}
                    //catch (Exception e)
                    //{
                    //    //OnLogMessageGenerated($"Error: {e.ToString()} when deleting directory \"{entry}\"");
                    //    OnLogMessageGenerated($"Error: {e} when deleting origin directories.");
                    //    //throw;
                    //} 
                }
            }

            //-- DELETING UNCOMMON DESTINY FILES AND DIRECTORIES
            if (deleteUncommonFiles && !settings.CleanDestinyDirectory && !CancelExecution)
            {
                //try
                //{
                foreach (string destinyFile in destinyFiles)
                {
                    //string trimmedDestinyFile = destinyFile.Replace(settings.DestinyPath, "");
                    string trimmedDestinyFile = destinyFile.Substring(settings.DestinyPath.Length);

                    if (!trimmedFiles.Exists(x => x.Equals(trimmedDestinyFile, StringComparison.OrdinalIgnoreCase)))
                    {
                        FileInfo dFile = new FileInfo(destinyFile);
                        dFile.IsReadOnly = false;

                        if (dFile.Exists)
                        {
                            HandleRepeatableTask((ref RepeatableTaskResultedAction fileTransAction) =>
                            {
                                try
                                {
                                    dFile.Delete();

                                    //if (LogSettings.LogDeleteFiles)
                                    OnLogMessageGenerated($"File deleted: \"{destinyFile}\"");

                                    RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                                    {
                                        Entry = destinyFile,
                                        Description = "Removed uncommon destiny file"
                                    });
                                }
                                catch (Exception e)
                                {
                                    OnLogMessageGenerated($"Error: {e} while deleting uncommon destiny file \"{destinyFile}\".");

                                    fileTransAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs($"An error has occurred while deleting uncommon " +
                                        $"destiny file \"{destinyFile}\".", e, TransferErrorOrigin.DeletingDestinyFileOrDirectory, null, null, settings)));
                                }
                            });
                        }

                        if (CancelExecution) return;
                    }
                }

                if (settings.IncludeSubFolders)
                {
                    foreach (string destinyDirectory in destinyDirectories.Reverse())
                    {
                        HandleRepeatableTask((ref RepeatableTaskResultedAction fileTransAction) =>
                        {
                            try
                            {
                                //string trimmedDestinyDirectory = destinyDirectory.Replace(settings.DestinyPath, "");
                                string trimmedDestinyDirectory = destinyDirectory.Substring(settings.DestinyPath.Length);

                                if (!trimmedDirectories.Exists(d => d.Equals(trimmedDestinyDirectory, StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (Directory.Exists(destinyDirectory))
                                    {
                                        Directory.Delete(destinyDirectory);

                                        OnLogMessageGenerated($"Directory deleted: \"{destinyDirectory}\"");

                                        RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                                        {
                                            Entry = destinyDirectory,
                                            Description = "Removed uncommon destiny directory"
                                        });
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                OnLogMessageGenerated($"Error: {e} while deleting uncommon destiny directory \"{destinyDirectory}\".");

                                fileTransAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs($"An error has occurred while deleting uncommon " +
                                    $"destiny directory \"{destinyDirectory}\".", e, TransferErrorOrigin.DeletingDestinyFileOrDirectory, null, null, settings)));
                            }
                        });

                        if (CancelExecution) return;
                    }
                }
                //}
                //catch (Exception e)
                //{
                //    OnLogMessageGenerated($"Error: {e} when deleting uncommon destiny files and directories.");
                //    throw;
                //}
            }
        }

        //private void TransferFile(TransferSettings settings, string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        private void TransferFile(TransferSettings settings, FileInfo originFile, FileInfo destinyFile, ref RepeatableTaskResultedAction fileTransAction)
        {
            //if (SkipActionExecution) SkipActionExecution = false;
            //if (RetryActionExecution) RetryActionExecution = false;

            bool fileExists;
            List<EnumeratedFile>? enumeratedFiles = null;

            if (settings.FileNameConflictMethod == FileNameConflictMethod.RENAME_DIFFERENT)
            {
                enumeratedFiles = GetAllEnumeratedFiles(destinyFile);
                fileExists = enumeratedFiles.Count > 0;
            }
            else
                fileExists = destinyFile.Exists;

            if (!fileExists)
            {
                originFile.IsReadOnly = false;

                try
                {
                    if (settings.KeepOriginFiles)
                        originFile.CopyTo(destinyFile.FullName);
                    else
                    {
                        File.Move(originFile.FullName, destinyFile.FullName);
                        //originFile.MoveTo(destinyFile.FullName);

                        RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                        {
                            Entry = originFile.FullName,
                            Description = "Removed origin file"
                        });
                    }

                    TransferedFilesReports.Add(new TransferedFilesReport
                    {
                        File = originFile.FullName,
                        Destiny = destinyFile.DirectoryName
                    });

                    OnLogMessageGenerated($"Transfered file: \"{originFile.FullName}\" to \"{destinyFile.DirectoryName}\"");
                }
                catch (Exception e)
                {
                    OnLogMessageGenerated($"Error: {e} when transfering \"{originFile.FullName}\" to \"{destinyFile.DirectoryName}\"");

                    if (File.Exists(destinyFile.FullName))
                    {
                        try
                        {
                            File.Delete(destinyFile.FullName);
                            OnLogMessageGenerated($"File deleted for safety: \"{destinyFile.FullName}\"");
                        }
                        catch (Exception e1)
                        {
                            OnLogMessageGenerated($"Error: {e1} when deleting \"{destinyFile.FullName}\"");
                        }
                    }

                    //throw new Exception($"An error has occurred when transfering the file: '{settings.DestinyPath + destiny}'. {e.Message}");
                    //ManageErrorActions(HandleTransferErrorRepeatable($"An error has occurred when transfering the file \"{originFile.FullName}\" to " +
                    //    $"\"{destinyFile.DirectoryName}\".", e, originFile, destinyFile.DirectoryName, settings));

                    fileTransAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs($"An error has occurred when transfering the file \"{originFile.FullName}\" to " +
                        $"\"{destinyFile.DirectoryName}\".", e, TransferErrorOrigin.TransferingFile, originFile, destinyFile.DirectoryName, settings)));

                    //if (RetryActionExecution) return;
                    if (fileTransAction == RepeatableTaskResultedAction.Retry) return;

                    NotTransferedFilesReports.Add(new NotTransferedFilesReport
                    {
                        File = originFile.FullName,
                        Destiny = destinyFile.DirectoryName,
                        Reason = "Error: " + e.Message
                    });
                }
            }
            else
            {
                switch (settings.FileNameConflictMethod)
                {
                    case FileNameConflictMethod.SKIP:
                        HandleFilenameConflictSkip(originFile, destinyFile); //originPathWithFileName, destinyPathWithFileName, destinyPathWOFileName
                        break;
                    case FileNameConflictMethod.REPLACE_ALL:
                        HandleFilenameConflictReplaceAll(originFile, destinyFile, settings, ref fileTransAction);
                        break;
                    case FileNameConflictMethod.REPLACE_DIFFERENT:
                        HandleFilenameConflictReplaceDifferent(originFile, destinyFile, settings, ref fileTransAction);
                        break;
                    case FileNameConflictMethod.RENAME_DIFFERENT:
                        HandleFilenameConflictRenameDifferent(originFile, destinyFile, enumeratedFiles, settings, ref fileTransAction);
                        break;
                }

                //-- DELETING ORIGIN FILE
                //if (!settings.KeepOriginFiles && !CancelExecution && !SkipActionExecution && !RetryActionExecution)
                if (!settings.KeepOriginFiles && fileTransAction != RepeatableTaskResultedAction.Continue)
                {
                    HandleRepeatableTask((ref RepeatableTaskResultedAction resultAction) =>
                    {
                        try
                        {
                            if (originFile.Exists)
                            {
                                originFile.Delete();

                                RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                                {
                                    Entry = originFile.FullName,
                                    Description = "Deleted origin file"
                                });
                            }
                        }
                        catch (Exception e)
                        {
                            OnLogMessageGenerated($"Error: {e} when deleting the source file \"{originFile.FullName}\"");
                            //throw new Exception($"An error has occurred when deleting the source file '{entry}'. {e.Message}");
                            //-- Repeating would lead to repeated reports
                            //ManageErrorActions(HandleTransferErrorNonRepeatable($"An error has occurred while deleting the source file \"{originFile.FullName}\".",
                            //    e, originFile, destinyFile.DirectoryName, settings));

                            resultAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs($"An error has occurred while deleting the source file " +
                                $"\"{originFile.FullName}\".", e, TransferErrorOrigin.DeletingSourceFileOrDirectory, originFile, destinyFile.DirectoryName, settings)));
                        }
                    });
                }

                //if (this.CancelExecution) OnLogMessageGenerated("Canceling operation...");
            }
        }

        //private void HandleFilenameConflictDoNotReplaceFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        private void HandleFilenameConflictSkip(FileInfo originFile, FileInfo destinyFile)
        {
            NotTransferedFilesReports.Add(new NotTransferedFilesReport
            {
                File = originFile.FullName,
                Destiny = destinyFile.DirectoryName,
                Reason = "Repeated name"
            });

            OnLogMessageGenerated($"Conflicted filename. File not moved: \"{originFile.FullName}\" as \"{destinyFile.FullName}\"");
        }

        //private void HandleFilenameConflictReplaceAllFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        private void HandleFilenameConflictReplaceAll(FileInfo originFile, FileInfo destinyFile, TransferSettings settings, ref RepeatableTaskResultedAction fileTransAction)
        {
            //try
            //{
            //    FileInfo bkp = new FileInfo(destinyPath + destiny);
            //    bkp.CopyTo(destinyPath + destiny + ".bac", true);
            //}
            //catch (Exception e)
            //{
            //    NotTransFilesReports.Add(new NotTransferedFilesReport
            //    {
            //        File = entry,
            //        Destiny = destinyPath + destiny.Substring(0, destiny.LastIndexOf("\\")),
            //        Reason = "Error: " + e.Message
            //    });
            //    OnLogMessageGenerated($"Error: {e.ToString()} when creating a backup file (.bac) for '{destinyPath + destiny}'");
            //    try
            //    {
            //        //if(File.Exists(destinyPath + destiny + ".bac"))
            //        File.Delete(destinyPath + destiny + ".bac");
            //    }
            //    catch (Exception) { }
            //    throw new Exception($"An error has occurred when creating a backup file (.bac) for '{destinyPath + destiny}'. {e.Message} Aborting.");
            //}

            //FileInfo sourceFile = new FileInfo(originPathWithFileName);

            try
            {
                originFile.CopyTo(destinyFile.FullName, true);
                //File.Copy(originFile.FullName, destinyFile.FullName, true);

                ReplacedFilesReports.Add(new ReplacedFilesReport
                {
                    File = originFile.FullName,
                    Destiny = destinyFile.DirectoryName
                });

                OnLogMessageGenerated($"File replaced: \"{destinyFile.FullName}\"");
            }
            catch (Exception e)
            {
                OnLogMessageGenerated($"Error: {e} when replacing \"{destinyFile.FullName}\"");
                //if (File.Exists(destinyPath + destiny + ".bac"))
                //{
                if (destinyFile.Exists)
                {
                    try
                    {
                        destinyFile.Delete();
                        OnLogMessageGenerated($"File deleted as safety procedure: \"{destinyFile.FullName}\"");
                    }
                    catch (Exception e1)
                    {
                        OnLogMessageGenerated($"Error: {e1} when deleting \"{destinyFile.FullName}\" as safety procedure");
                    }
                }

                //try
                //{
                //    File.Move(destinyPath + destiny + ".bac", destinyPath + destiny);
                //    OnLogMessageGenerated($"Original file restored: '{destinyPath + destiny}' from backup file");
                //}
                //catch (Exception e1)
                //{
                //    OnLogMessageGenerated($"Error: {e1.ToString()} when restoring file '{destinyPath + destiny}' from backup");
                //}
                //}
                //else
                //{
                //    OnLogMessageGenerated($"Backup file could not be found '{destinyPath + destiny}.bac'. Couldn't restore original file");
                //}
                //throw new Exception($"An error has occurred when when replacing \"{destinyPath + destiny}\": {e.Message}");
                //ManageErrorActions(HandleTransferErrorRepeatable($"An error has occurred while replacing \"{destinyFile.FullName}\".", e,
                //    originFile, destinyFile.DirectoryName, settings));

                fileTransAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs($"An error has occurred while replacing \"{destinyFile.FullName}\".", e,
                    TransferErrorOrigin.TransferingFile, originFile, destinyFile.DirectoryName, settings)));

                //if (RetryActionExecution) return;
                if (fileTransAction == RepeatableTaskResultedAction.Retry) return;

                NotTransferedFilesReports.Add(new NotTransferedFilesReport
                {
                    File = originFile.FullName,
                    Destiny = destinyFile.DirectoryName,
                    Reason = "Error: " + e.Message
                });
            }
            //try
            //{
            //    File.Delete(destinyPath + destiny + ".bac");
            //}
            //catch (Exception e)
            //{
            //    OnLogMessageGenerated($"Error: {e.ToString()} when deleting the backup file '{destinyPath + destiny}.bac'");
            //    throw new Exception($"An error has occurred when deleting the backup file (.bac) for '{destinyPath + destiny}'. {e.Message} Aborting.");
            //}
        }

        //private void HandleFilenameConflictReplaceUniqueFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        private void HandleFilenameConflictReplaceDifferent(FileInfo originFile, FileInfo destinyFile, TransferSettings settings,
            ref RepeatableTaskResultedAction fileTransAction)
        {
            bool filesAreTheSame;

            try
            {
                filesAreTheSame = FileEquals(originFile, destinyFile, settings, ref fileTransAction);
            }
            catch (Exception)
            {
                //HandleErrorDialogRepeatable($"An error occurred when comparing files: \"{originPathWithFileName}\" and \"{destinyPathWithFileName}\". {e.Message}",true);
                return;
            }

            if (!filesAreTheSame)
            {
                //try
                //{
                //    FileInfo bkp = new FileInfo(destinyPath + destiny);
                //    bkp.CopyTo(destinyPath + destiny + ".bac", true);
                //}
                //catch (Exception e)
                //{
                //    NotTransFilesReports.Add(new NotTransferedFilesReport
                //    {
                //        File = entry,
                //        Destiny = destinyPath + destiny.Substring(0, destiny.LastIndexOf("\\")),
                //        Reason = "Error: " + e.Message
                //    });
                //    OnLogMessageGenerated($"Error: {e.ToString()} when creating backup file (.bac) for '{destinyPath + destiny}'");
                //    try
                //    {
                //        //if(File.Exists(destinyPath + destiny + ".bac"))
                //        File.Delete(destinyPath + destiny + ".bac");
                //    }
                //    catch (Exception) { }
                //    throw new Exception($"An error has occurred when creating a backup file (.bac) for '{destinyPath + destiny}'. {e.Message} Aborting.");
                //}

                //FileInfo sourceFile = new FileInfo(originPathWithFileName);

                try
                {
                    originFile.CopyTo(destinyFile.FullName, true);
                    //File.Copy(originFile.FullName, destinyFile.FullName, true);

                    ReplacedFilesReports.Add(new ReplacedFilesReport
                    {
                        File = originFile.FullName,
                        Destiny = destinyFile.DirectoryName
                    });

                    OnLogMessageGenerated($"Replaced file: \"{destinyFile.FullName}\"");
                }
                catch (Exception e)
                {
                    OnLogMessageGenerated($"Error: {e} when replacing \"{destinyFile.FullName}\"");
                    //if (File.Exists(destinyPath + destiny + ".bac"))
                    //{
                    if (File.Exists(destinyFile.FullName))
                    {
                        try
                        {
                            File.Delete(destinyFile.FullName);
                            OnLogMessageGenerated($"File deleted for safety: \"{destinyFile.FullName}\"");
                        }
                        catch (Exception e1)
                        {
                            OnLogMessageGenerated($"Error: {e1} when deleting \"{destinyFile.FullName}\" as safety procedure");
                        }
                    }
                    //try
                    //{
                    //    File.Move(destinyPath + destiny + ".bac", destinyPath + destiny);
                    //    OnLogMessageGenerated($"Original file restored: '{destinyPath + destiny}' from backup file");
                    //}
                    //catch (Exception e1)
                    //{
                    //    OnLogMessageGenerated($"Error: {e1.ToString()} when restoring file '{destinyPath + destiny}' from backup");
                    //}
                    //}
                    //else
                    //{
                    //    OnLogMessageGenerated($"Backup file could not be found '{destinyPath + destiny}.bac'. Couldn't restore original file");
                    //}

                    //throw new Exception($"An error has occurred when replacing the file: \"{destinyPath + destiny}\": {e.Message}");
                    //ManageErrorActions(HandleTransferErrorRepeatable($"An error has occurred when replacing the file: \"{destinyFile.FullName}\".", e,
                    //    originFile, destinyFile.DirectoryName, settings));

                    fileTransAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs($"An error has occurred when replacing the file: " +
                        $"\"{destinyFile.FullName}\".", e, TransferErrorOrigin.TransferingFile, originFile, destinyFile.DirectoryName, settings)));

                    //if (RetryActionExecution) return;
                    if (fileTransAction == RepeatableTaskResultedAction.Retry) return;

                    NotTransferedFilesReports.Add(new NotTransferedFilesReport
                    {
                        File = originFile.FullName,
                        Destiny = destinyFile.DirectoryName,
                        Reason = "Error: " + e.Message
                    });
                }
                //try
                //{
                //    File.Delete(destinyPath + destiny + ".bac");
                //}
                //catch (Exception e)
                //{
                //    OnLogMessageGenerated($"Error: {e.ToString()} when deleting the backup file '{destinyPath + destiny}.bac'");
                //    throw new Exception($"An error has occurred when deleting the backup file (.bac) for '{destinyPath + destiny}'. {e.Message} Aborting.");
                //}
            }
            else
            {
                NotTransferedFilesReports.Add(new NotTransferedFilesReport
                {
                    File = originFile.FullName,
                    Destiny = destinyFile.DirectoryName,
                    Reason = "File already exists"
                });

                OnLogMessageGenerated($"File not replaced: \"{originFile.FullName}\" and \"{destinyFile.FullName}\" are the same");
            }
            //}
            //catch (Exception e)
            //{
            //    HandleErrorDialogRepeatable(e.Message);
            //}
        }

        private void HandleFilenameConflictRenameDifferent(FileInfo originFile, FileInfo destinyFile, List<EnumeratedFile> enumeratedFileNames,
            TransferSettings settings, ref RepeatableTaskResultedAction fileTransAction)
        {
            bool filesAreTheSame;

            //try
            //{
            //    filesAreTheSame = FileEquals(originPathWithFileName, destinyPathWithFileName);
            //}
            //catch (Exception)
            //{
            //    //HandleErrorDialogRepeatable($"An error occurred when comparing files: \"{originPathWithFileName}\" and \"{destinyPathWithFileName}\". {e.Message}",true);
            //    return;
            //}

            //if (!filesAreTheSame)
            //{

            string destinyPathWithNewFileName;
            //List<EnumeratedFile> enumeratedFileNames;
            EnumeratedFile lastEnumeratedFile = enumeratedFileNames.Last();
            string errorLogMessage = "";
            string errorDialogMessage = "";

            //enumeratedFileNames = GetAllEnumeratedFiles(new FileInfo(destinyPathWithFileName));
            //lastEnumeratedFile = enumeratedFileNames.Last();

            try
            {
                filesAreTheSame = FileEquals(originFile, lastEnumeratedFile.File, settings, ref fileTransAction);
            }
            catch (Exception)
            {
                return;
            }

            if (filesAreTheSame)
            {
                NotTransferedFilesReports.Add(new NotTransferedFilesReport
                {
                    File = originFile.FullName,
                    Destiny = destinyFile.DirectoryName,
                    Reason = $"File already exists as \"{lastEnumeratedFile.File.Name}\""
                });

                OnLogMessageGenerated($"File not moved: \"{originFile.FullName}\" and \"{lastEnumeratedFile.File.FullName}\" are the same");
                //return;
            }

            try
            {
                if (!filesAreTheSame)
                {
                    try
                    {
                        destinyPathWithNewFileName = $"{destinyFile.DirectoryName}\\{Path.GetFileNameWithoutExtension(destinyFile.FullName)} " +
                            $"({lastEnumeratedFile.Number + 1}){destinyFile.Extension}";

                        if (settings.ReenumerateRenamedFiles)
                            enumeratedFileNames.Add(new EnumeratedFile { File = new FileInfo(destinyPathWithNewFileName), Number = lastEnumeratedFile.Number + 1 });

                    }
                    catch (Exception e)
                    {
                        errorLogMessage = $"Error: {e} while generating a new valid name for \"{originFile.FullName}\" in the destiny directory.";
                        errorDialogMessage = $"An error has occurred while generating a new valid name for \"{originFile.FullName}\" in the destiny directory.";
                        throw;
                    }

                    try
                    {
                        //throw new Exception();
                        originFile.CopyTo(destinyPathWithNewFileName, true);

                        RenamedFilesReports.Add(new RenamedFilesReport
                        {
                            File = originFile.FullName,
                            Destiny = destinyPathWithNewFileName
                        });

                        OnLogMessageGenerated($"File moved and renamed: \"{originFile.FullName}\" to \"{destinyPathWithNewFileName}\"");
                    }
                    catch (Exception e)
                    {
                        errorLogMessage = $"Error: \"{e}\" while copying/renaming \"{originFile.FullName}\" to \"{destinyPathWithNewFileName}\"";
                        errorDialogMessage = $"An error has occurred while copying/renaming the file: \"{originFile.FullName}\" " +
                            $"to \"{destinyPathWithNewFileName}\".";

                        try
                        {
                            if (File.Exists(destinyPathWithNewFileName))
                            {
                                File.Delete(destinyPathWithNewFileName);
                                OnLogMessageGenerated($"File deleted for safety: \"{destinyPathWithNewFileName}\"");
                            }
                        }
                        catch (Exception e1)
                        {
                            OnLogMessageGenerated($"Error: {e1} when deleting \"{destinyPathWithNewFileName}\" as safety procedure");
                        }

                        throw;
                    }
                }

                try
                {
                    if (settings.ReenumerateRenamedFiles)
                        OrganizeEnumeratedFiles(destinyFile, settings.MaxKeptRenamedFileCount, enumeratedFileNames);
                }
                catch (Exception e)
                {
                    errorLogMessage = $"Error: \"{e}\" while reorganizing enumerated files originated from \"{originFile.FullName}\"";
                    errorDialogMessage = $"An error has occurred while reorganizing enumerated files originated from \"{originFile.FullName}\".";
                    throw;
                }
            }
            catch (Exception e)
            {
                OnLogMessageGenerated(errorLogMessage);

                //throw new Exception($"An error has occurred when moving and renaming the file: '{entry}' to '{newFileName}'. {e.Message}");
                //ManageErrorActions(HandleTransferErrorRepeatable(errorDialogMessage, e, originFile, destinyFile.DirectoryName, settings));

                fileTransAction = ManageErrorActions(OnErrorOccured(new TransferErrorArgs(errorDialogMessage, e, TransferErrorOrigin.TransferingFile, originFile,
                    destinyFile.DirectoryName, settings)));

                //if (RetryActionExecution) return;
                if (fileTransAction == RepeatableTaskResultedAction.Retry) return;

                NotTransferedFilesReports.Add(new NotTransferedFilesReport
                {
                    File = originFile.FullName,
                    Destiny = destinyFile.DirectoryName,
                    Reason = "Error: " + e.Message
                });

                return;
            }
            //}
            //else
            //{
            //    NotTransferedFilesReports.Add(new NotTransferedFilesReport
            //    {
            //        File = originPathWithFileName,
            //        Destiny = destinyPathWOFileName,
            //        Reason = "File already exists as \"" + destinyPathWithFileName.Substring(destinyPathWithFileName.LastIndexOf("\\") + 1) + "\""
            //    });

            //    OnLogMessageGenerated($"File not moved: \"{originPathWithFileName}\" and \"{destinyPathWithFileName}\" are the same");
            //}
        }

        /// <summary>
        /// Retrieves the number of files that would be subject to a transfer given a list of settings.<para>Can be used as data for a progress bar, for example.</para>
        /// </summary>
        public int FilesTotal(List<TransferSettings> settings)
        {
            int fileCount = 1;
            string[] files;

            foreach (TransferSettings s in settings)
            {
                if (s.IncludeSubFolders == true)
                    files = Directory.GetFiles(s.SourcePath, "*", SearchOption.AllDirectories);
                else
                    files = Directory.GetFiles(s.SourcePath, "*", SearchOption.TopDirectoryOnly);

                fileCount += files.Length;
            }

            return fileCount;
        }

        //-- #### OVERRIDE METHODS ####
        //-- Implement what to do with the error message, exception, and the related filenames when a non fatal error ocurred.

        //-- ...and return an action when the particular file transfer can be repeated...
        ///// <summary>
        ///// Invoked when an error ocurrs while transfering a file. Returns a FileTransferErrorActionRepeatable indicating whether the transfer of the file must 
        ///// be retried (RETRY), skipped (SKIP) or the whole process must be canceled (CANCEL).
        ///// </summary>
        ///// <remarks>Default: <c>FileTransferErrorActionRepeatable.SKIP</c></remarks>
        //protected virtual FileTransferErrorActionRepeatable HandleTransferErrorRepeatable(string errorMessage, Exception e, FileInfo originFile, string destinyDir,
        //    TransferSettings settings)
        //{
        //    return FileTransferErrorActionRepeatable.SKIP;
        //}

        //-- ...and when the particular file transfer can't be repeated...
        ///// <summary>
        ///// Invoked when an error occurs while transfering a file. Returns a FileTransferErrorActionNonRepeatable indicating whether the transfer of the file must 
        ///// be skipped (SKIP) or the whole process must be canceled (CANCEL).
        ///// </summary>
        ///// <remarks>Default: <c>FileTransferErrorActionNonRepeatable.SKIP</c></remarks>
        //protected virtual FileTransferErrorActionNonRepeatable HandleTransferErrorNonRepeatable(string errorMessage, Exception e, FileInfo originFile,
        //    string destinyDir, TransferSettings settings)
        //{
        //    return FileTransferErrorActionNonRepeatable.SKIP;
        //}

        //-- Choose what to do with log messages (what happens during the execution)
        ///// <summary>
        ///// Invoked when log messages occur.
        ///// </summary>
        //protected virtual void OnLogMessageGenerated(string logMessage)
        //{
        //    Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}]: {logMessage}");
        //}

        //-- Choose what to do the names of files being executed
        ///// <summary>
        ///// Invoked for each file being executed. <list type="table"><term><c>trimmedPathWithFileName</c></term>
        ///// <description>file name containing part of the path that is common between source and destiny files paths.</description></list>
        ///// </summary>
        //protected virtual void HandleCurrentFileExecution(string trimmedPathWithFileName, FileInfo originFile, string destinyDir, TransferSettings settings)
        //{
        //    Console.WriteLine($"Executing: {trimmedPathWithFileName.Substring(trimmedPathWithFileName.LastIndexOf("\\") + 1)}");
        //}

        //private void ManageErrorActions(Enum fileTransferErrorAction)
        private RepeatableTaskResultedAction ManageErrorActions(TransferErrorAction fileTransferErrorAction)
        {
            switch (fileTransferErrorAction)
            {
                case TransferErrorAction.CANCEL:
                    CancelExecution = true;
                    //RetryActionExecution = false;
                    OnLogMessageGenerated("Canceling transfer");
                    return RepeatableTaskResultedAction.Skip;
                //break;
                case TransferErrorAction.SKIP:
                    //SkipActionExecution = true;
                    //RetryActionExecution = false;
                    OnLogMessageGenerated("Skipping task");
                    return RepeatableTaskResultedAction.Skip;
                //break;
                case TransferErrorAction.RETRY:
                    //RetryActionExecution = true;
                    OnLogMessageGenerated("Repeating task");
                    return RepeatableTaskResultedAction.Retry;
                    //break;
            }

            return RepeatableTaskResultedAction.Continue;

            //if (fileTransferErrorAction.GetType().Equals(typeof(FileTransferErrorActionRepeatable)))
            //{
            //    switch ((FileTransferErrorActionRepeatable)fileTransferErrorAction)
            //    {
            //        case FileTransferErrorActionRepeatable.CANCEL:
            //            CancelExecution = true;
            //            RepeatFileExecution = false;
            //            OnLogMessageGenerated("Canceling transfer");
            //            break;
            //        case FileTransferErrorActionRepeatable.SKIP:
            //            JumpFileExecution = true;
            //            RepeatFileExecution = false;
            //            OnLogMessageGenerated("Jumping to the next file");
            //            break;
            //        case FileTransferErrorActionRepeatable.RETRY:
            //            RepeatFileExecution = true;
            //            OnLogMessageGenerated("Repeating file execution");
            //            break;
            //    }
            //}
            //else if (fileTransferErrorAction.GetType().Equals(typeof(FileTransferErrorActionNonRepeatable)))
            //{
            //    switch ((FileTransferErrorActionRepeatable)fileTransferErrorAction)
            //    {
            //        case FileTransferErrorActionRepeatable.CANCEL:
            //            CancelExecution = true;
            //            RepeatFileExecution = false;
            //            OnLogMessageGenerated("Canceling transfer");
            //            break;
            //        case FileTransferErrorActionRepeatable.SKIP:
            //            JumpFileExecution = true;
            //            RepeatFileExecution = false;
            //            OnLogMessageGenerated("Jumping to the next file");
            //            break;
            //    }
            //}
        }

        /// <summary>
        /// Returns an adjusted full or relative path string. Relative paths start with a directory separator.
        /// </summary>
        static public string AdjustPath(string path) => Utility.AdjustPath(path);
    }
}
