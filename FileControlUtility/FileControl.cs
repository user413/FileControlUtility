using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileControlUtility
{
    public enum FileTransferErrorActionRepeatable
    {
        /// <summary>
        /// Ignore current file and jump to next.
        /// </summary>
        JUMP,
        /// <summary>
        /// Cancel execution for the instance.
        /// </summary>
        CANCEL,
        /// <summary>
        /// Try to transfer current file again.
        /// </summary>
        REPEAT
    }

    public enum FileTransferErrorActionNonRepeatable
    {
        /// <summary>
        /// Ignore current file and jump to next.
        /// </summary>
        JUMP,
        /// <summary>
        /// Cancel execution for the instance.
        /// </summary>
        CANCEL
    }

    public partial class FileControl
    {
        public List<TransferedFilesReport> TransferedFilesReports { get; }
        public List<NotTransferedFilesReport> NotTransferedFilesReports { get; }
        public List<RenamedFilesReport> RenamedFilesReports { get; }
        public List<CreatedDirectoriesReport> CreatedDirReports { get; }
        public List<ReplacedFilesReport> ReplacedFilesReports { get; }
        public List<RemovedFilesAndDirectoriesReport> RemovedFilesAndDirReports { get; }

        public bool CancelExecution { get; set; } = false;
        private bool JumpFileExecution;
        private bool RepeatFileExecution;

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
        public void ManageFiles(List<TransferSettings> settingsList)
        {        
            ClearReportLists();

            CancelExecution = false;

            Exception fatalException = null;

            HandleLogMessage($"##### New transfer ({DateTime.Now.ToLongDateString()})");

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

            //HandleLogMessage("DONE");

            if (fatalException != null) throw fatalException;
        }

        private void ManageTransferSettings(TransferSettings settings)
        {
            //bool AllowOnlyExt = false;
            //bool IgnoreExt = false;

            bool deleteUncommonFiles = settings.DeleteUncommonFiles && settings.FileNameConflictMethod != FileNameConflictMethod.RENAME_DIFFERENT;

            if (!CancelExecution)
            {
                HandleLogMessage("## From: " + settings.SourcePath);
                HandleLogMessage("## To: " + settings.DestinyPath);

                switch (settings.FileNameConflictMethod)
                {
                    case FileNameConflictMethod.SKIP:
                        HandleLogMessage("## For repeated filenames: skip");
                        break;
                    case FileNameConflictMethod.REPLACE_ALL:
                        HandleLogMessage("## For repeated filenames: replace all");
                        break;
                    case FileNameConflictMethod.REPLACE_DIFFERENT:
                        HandleLogMessage("## For repeated filenames: replace different files (binary comparison)");
                        break;
                    case FileNameConflictMethod.RENAME_DIFFERENT:
                        HandleLogMessage("## For repeated filenames: rename different files (binary comparison)");
                        break;
                }

                HandleLogMessage("## Include subfolders: " + settings.IncludeSubFolders);
                HandleLogMessage("## Keep Files: " + settings.KeepOriginFiles);
                HandleLogMessage("## Clean destiny: " + settings.CleanDestinyDirectory);
                HandleLogMessage("## Delete uncommon: " + settings.DeleteUncommonFiles);
                HandleLogMessage("## Reorganize renamed files: " + settings.ReenumerateRenamedFiles);
                HandleLogMessage("## Max. renamed files kept: " + settings.MaxKeptRenamedFileCount);
                HandleLogMessage("## Specified filenames/extensions mode: " + settings.SpecifiedFileNamesOrExtensionsMode);
                HandleLogMessage($"## Specified filenames/extensions:" +
                    (settings.SpecifiedFileNamesAndExtensions == null ? "" : $" \"{string.Join("\",\"", settings.SpecifiedFileNamesAndExtensions)}\""));
            }

            string[] originDirectories = null; //-- Used for IncludeSubFolders, !KeepOriginFiles and deleteUncommonFiles
            List<string> trimmedFiles = new List<string>();
            List<string> trimmedDirectories = new List<string>(); //-- Used for IncludeSubFolders and deleteUncommonFiles
            string[] destinyFiles = null; //-- Used for deleteUncommonFiles
            string[] destinyDirectories = null; //-- Used for deleteUncommonFiles

            //GenerateFilesAndDirectoriesLists();

            try
            {
                HandleLogMessage("Generating files and directories lists...");

                string[] originFiles;

                if (settings.IncludeSubFolders || !settings.KeepOriginFiles)
                    originDirectories = Directory.GetDirectories(settings.SourcePath, "*", SearchOption.AllDirectories);

                if (settings.IncludeSubFolders)
                {
                    originFiles = Directory.GetFiles(settings.SourcePath, "*", SearchOption.AllDirectories);

                    if (deleteUncommonFiles)
                    {
                        destinyDirectories = Directory.GetDirectories(settings.DestinyPath, "*", SearchOption.AllDirectories);
                        destinyFiles = Directory.GetFiles(settings.DestinyPath, "*", SearchOption.AllDirectories);
                    }
                }
                else
                {
                    originFiles = Directory.GetFiles(settings.SourcePath, "*", SearchOption.TopDirectoryOnly);

                    if (deleteUncommonFiles)
                        destinyFiles = Directory.GetFiles(settings.DestinyPath, "*", SearchOption.TopDirectoryOnly);
                }

                //-- GENERATING COMMON TRIMMED FILE PATHS
                foreach (string file in originFiles)
                    trimmedFiles.Add(file.Replace(settings.SourcePath, ""));

                if (settings.IncludeSubFolders) 
                    foreach (string originDirectory in originDirectories)
                        trimmedDirectories.Add(originDirectory.Replace(settings.SourcePath, ""));
            }
            catch (Exception e)
            {
                HandleLogMessage($"An error has occurred while retrieving files/directories lists. {e.Message} Aborting.");
                throw;
            }

            if (!CancelExecution)
            {
                try
                {
                    if (settings.CleanDestinyDirectory)
                    {
                        HandleLogMessage("Cleaning destiny directory...");
                        try
                        {
                            if (Directory.Exists(settings.DestinyPath))
                            {
                                DirectoryInfo dir = new DirectoryInfo(settings.DestinyPath);

                                foreach (FileInfo file in dir.GetFiles())
                                {
                                    file.IsReadOnly = false;
                                    file.Delete();
                                    HandleLogMessage($"File deleted: {file.Name}");
                                }

                                foreach (DirectoryInfo subdir in dir.GetDirectories())
                                {
                                    subdir.Delete(true);
                                    HandleLogMessage($"Folder deleted: {subdir.Name}");
                                }
                                //for each (string dire in Directory.GetDirectories(settings.DestinyPath, "*", SearchOption.AllDirectories)) Directory.Delete(dire);
                            }
                        }
                        catch (Exception e)
                        {
                            HandleLogMessage($"An error has occurred while cleaning destiny directory. {e.Message} Aborting.");
                            throw;
                        }
                    }

                    HandleLogMessage("Creating directories...");

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

                            HandleLogMessage($"Directory created: " + settings.DestinyPath);
                        }
                        catch (Exception e)
                        {
                            HandleLogMessage($"Error: {e} when creating the directory {settings.DestinyPath} Aborting.");
                            //throw new Exception($"An error has occurred when creating the directory {settings.DestinyPath}. {e.Message} Aborting.");
                            throw;
                        }
                    }

                    if (settings.IncludeSubFolders)
                    {
                        foreach (string trimmedDirectory in trimmedDirectories)
                        {
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

                                    HandleLogMessage($"Directory created: " + destinyDir);
                                }
                                catch (Exception e)
                                {
                                    HandleLogMessage($"Error: {e} when creating the directory {destinyDir}. Aborting.");
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

                HandleLogMessage("Transfering files..."); 
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

                HandleCurrentFileExecution(trimmedPathWithFileName, originFile, destinyFile.DirectoryName, settings);
                //System.Threading.Thread.Sleep(5000);

                //-- MANAGING SPECIFIED FILENAMES AND EXTENSIONS
                if (settings.SpecifiedFileNamesAndExtensions != null && settings.SpecifiedFileNamesAndExtensions.Count > 0)
                {
                    string extension = Path.GetExtension(currentFileName);
                    bool fileIsSpecified = settings.SpecifiedFileNamesAndExtensions.Exists(x => x.Equals(currentFileName, StringComparison.OrdinalIgnoreCase));
                    bool extensionIsSpecified = settings.SpecifiedFileNamesAndExtensions.Exists(x => x.Equals(extension, StringComparison.OrdinalIgnoreCase));

                    if (
                        (
                            settings.SpecifiedFileNamesOrExtensionsMode == SpecifiedFileNamesAndExtensionsMode.ALLOW_ONLY &&
                            !fileIsSpecified && !extensionIsSpecified
                        ) ||
                        (
                            settings.SpecifiedFileNamesOrExtensionsMode == SpecifiedFileNamesAndExtensionsMode.IGNORE &&
                            (fileIsSpecified || extensionIsSpecified)
                        )
                    )
                    {
                        string ignoredReasonText;
                        string ignoredLogText;

                        if (settings.SpecifiedFileNamesOrExtensionsMode == SpecifiedFileNamesAndExtensionsMode.ALLOW_ONLY)
                        {
                            ignoredReasonText = $"Filename or extension ignored";
                            ignoredLogText = $"Filename or extension ignored. File not transfered:\"'{originFile.FullName}\" as \"{destinyFile.FullName}\"";
                        }

                        else if (settings.SpecifiedFileNamesOrExtensionsMode == SpecifiedFileNamesAndExtensionsMode.IGNORE && fileIsSpecified)
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

                        //HandleLogMessage(ignoredLogText);
                        continue;
                    }
                }

                do
                    //TransferFile(settings, originPathWithFileName, destinyPathWithFileName, destinyFile.Directory);
                    TransferFile(settings, originFile, destinyFile);
                while (RepeatFileExecution);
            }

            //-- DELETING ORIGIN DIRECTORIES
            if (!settings.KeepOriginFiles && !CancelExecution)
            {
                HandleLogMessage("Cleaning origin directories...");
                try
                {
                    foreach (string entry in originDirectories.Reverse())
                    {
                        if (Directory.Exists(entry))
                        {
                            Directory.Delete(entry, true);
                            HandleLogMessage($"File deleted: \"{entry}\"");

                            RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                            {
                                Entry = entry,
                                Description = "Removed origin directory"
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    HandleLogMessage($"Error: {e} when deleting origin directories.");
                    //HandleLogMessage($"Error: {e.ToString()} when deleting directory \"{entry}\"");
                    throw;
                }
            }

            //-- DELETING UNCOMMON DESTINY FILES AND DIRECTORIES
            if (deleteUncommonFiles && !settings.CleanDestinyDirectory && !CancelExecution)
            {
                try
                {
                    foreach (string destinyFile in destinyFiles)
                    {
                        string trimmedDestinyFile = destinyFile.Replace(settings.DestinyPath, "");

                        if (!trimmedFiles.Exists(x => x.Equals(trimmedDestinyFile, StringComparison.OrdinalIgnoreCase)))
                        {
                            FileInfo dFile = new FileInfo(destinyFile);
                            dFile.IsReadOnly = false;

                            if (dFile.Exists)
                            {
                                dFile.Delete();
                                RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                                {
                                    Entry = destinyFile,
                                    Description = "Removed uncommon destiny file"
                                });
                            }
                        }
                    }

                    if (settings.IncludeSubFolders)
                    {
                        foreach (string destinyDirectory in destinyDirectories.Reverse())
                        {
                            string trimmedDestinyDirectory = destinyDirectory.Replace(settings.DestinyPath, "");

                            if (!trimmedDirectories.Exists(d => d.Equals(trimmedDestinyDirectory, StringComparison.OrdinalIgnoreCase)))
                            {
                                if (Directory.Exists(destinyDirectory))
                                {
                                    Directory.Delete(destinyDirectory);
                                    RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                                    {
                                        Entry = destinyDirectory,
                                        Description = "Removed uncommon destiny directory"
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    HandleLogMessage($"Error: {e} when deleting uncommon destiny files and directories.");
                    throw;
                }
            }
        }

        //private void TransferFile(TransferSettings settings, string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        private void TransferFile(TransferSettings settings, FileInfo originFile, FileInfo destinyFile)
        {
            if (JumpFileExecution) JumpFileExecution = false;
            if (RepeatFileExecution) RepeatFileExecution = false;

            bool fileExists;
            List<EnumeratedFile> enumeratedFiles = null;

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

                    HandleLogMessage($"Transfered file: \"{originFile.FullName}\" to \"{destinyFile.DirectoryName}\"");
                }
                catch (Exception e)
                {
                    HandleLogMessage($"Error: {e} when transfering \"{originFile.FullName}\" to \"{destinyFile.DirectoryName}\"");
                    
                    if (File.Exists(destinyFile.FullName))
                    {
                        try
                        {
                            File.Delete(destinyFile.FullName);
                            HandleLogMessage($"File deleted for safety: \"{destinyFile.FullName}\"");
                        }
                        catch (Exception e1)
                        {
                            HandleLogMessage($"Error: {e1} when deleting \"{destinyFile.FullName}\"");
                        }
                    }

                    //throw new Exception($"An error has occurred when transfering the file: '{settings.DestinyPath + destiny}'. {e.Message}");
                    ManageErrorActions(HandleTransferErrorRepeatable($"An error has occurred when transfering the file \"{originFile.FullName}\" to " +
                        $"\"{destinyFile.DirectoryName}\": {e.Message}", e, originFile, destinyFile.DirectoryName, settings));
                    
                    if (RepeatFileExecution) return;

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
                        HandleFilenameConflictReplaceAll(originFile, destinyFile, settings);
                        break;
                    case FileNameConflictMethod.REPLACE_DIFFERENT:
                        HandleFilenameConflictReplaceDifferent(originFile, destinyFile, settings);
                        break;
                    case FileNameConflictMethod.RENAME_DIFFERENT:
                        HandleFilenameConflictRenameDifferent(originFile, destinyFile, enumeratedFiles, settings);
                        break;
                }

                //-- DELETING ORIGIN FILE
                if (!settings.KeepOriginFiles && !CancelExecution && !JumpFileExecution && !RepeatFileExecution)
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
                        HandleLogMessage($"Error: {e} when deleting the source file \"{originFile.FullName}\"");
                        //throw new Exception($"An error has occurred when deleting the source file '{entry}'. {e.Message}");
                        ManageErrorActions(HandleTransferErrorNonRepeatable($"An error has occurred when deleting the source file \"{originFile.FullName}\". " +
                            $"{e.Message}", e, originFile, destinyFile.DirectoryName, settings));
                    }
                }

                //if (this.CancelExecution) HandleLogMessage("Canceling operation...");
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

            HandleLogMessage($"Conflicted filename. File not moved: \"{originFile.FullName}\" as \"{destinyFile.FullName}\"");
        }

        //private void HandleFilenameConflictReplaceAllFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        private void HandleFilenameConflictReplaceAll(FileInfo originFile, FileInfo destinyFile, TransferSettings settings)
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
            //    HandleLogMessage($"Error: {e.ToString()} when creating a backup file (.bac) for '{destinyPath + destiny}'");
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

                HandleLogMessage($"File replaced: \"{destinyFile.FullName}\"");
            }
            catch (Exception e)
            {
                HandleLogMessage($"Error: {e} when replacing \"{destinyFile.FullName}\"");
                //if (File.Exists(destinyPath + destiny + ".bac"))
                //{
                    if (destinyFile.Exists)
                    {
                        try
                        {
                            destinyFile.Delete();
                            HandleLogMessage($"File deleted as safety procedure: \"{destinyFile.FullName}\"");
                        }
                        catch (Exception e1)
                        {
                            HandleLogMessage($"Error: {e1} when deleting \"{destinyFile.FullName}\" as safety procedure");
                        }
                    }

                //try
                //{
                //    File.Move(destinyPath + destiny + ".bac", destinyPath + destiny);
                //    HandleLogMessage($"Original file restored: '{destinyPath + destiny}' from backup file");
                //}
                //catch (Exception e1)
                //{
                //    HandleLogMessage($"Error: {e1.ToString()} when restoring file '{destinyPath + destiny}' from backup");
                //}
                //}
                //else
                //{
                //    HandleLogMessage($"Backup file could not be found '{destinyPath + destiny}.bac'. Couldn't restore original file");
                //}
                //throw new Exception($"An error has occurred when when replacing \"{destinyPath + destiny}\": {e.Message}");

                ManageErrorActions(HandleTransferErrorRepeatable($"An error has occurred while replacing \"{destinyFile.FullName}\": {e.Message}", e,
                    originFile, destinyFile.DirectoryName, settings));
                
                if (RepeatFileExecution) return;
                
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
            //    HandleLogMessage($"Error: {e.ToString()} when deleting the backup file '{destinyPath + destiny}.bac'");
            //    throw new Exception($"An error has occurred when deleting the backup file (.bac) for '{destinyPath + destiny}'. {e.Message} Aborting.");
            //}
        }

        //private void HandleFilenameConflictReplaceUniqueFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        private void HandleFilenameConflictReplaceDifferent(FileInfo originFile, FileInfo destinyFile, TransferSettings settings)
        {
            bool filesAreTheSame;

            try
            {
                filesAreTheSame = FileEquals(originFile, destinyFile, settings);
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
                //    HandleLogMessage($"Error: {e.ToString()} when creating backup file (.bac) for '{destinyPath + destiny}'");
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

                    HandleLogMessage($"Replaced file: \"{destinyFile.FullName}\"");
                }
                catch (Exception e)
                {
                    HandleLogMessage($"Error: {e} when replacing \"{destinyFile.FullName}\"");
                    //if (File.Exists(destinyPath + destiny + ".bac"))
                    //{
                    if (File.Exists(destinyFile.FullName))
                    {
                        try
                        {
                            File.Delete(destinyFile.FullName);
                            HandleLogMessage($"File deleted for safety: \"{destinyFile.FullName}\"");
                        }
                        catch (Exception e1)
                        {
                            HandleLogMessage($"Error: {e1} when deleting \"{destinyFile.FullName}\" as safety procedure");
                        }
                    }
                    //try
                    //{
                    //    File.Move(destinyPath + destiny + ".bac", destinyPath + destiny);
                    //    HandleLogMessage($"Original file restored: '{destinyPath + destiny}' from backup file");
                    //}
                    //catch (Exception e1)
                    //{
                    //    HandleLogMessage($"Error: {e1.ToString()} when restoring file '{destinyPath + destiny}' from backup");
                    //}
                    //}
                    //else
                    //{
                    //    HandleLogMessage($"Backup file could not be found '{destinyPath + destiny}.bac'. Couldn't restore original file");
                    //}

                    //throw new Exception($"An error has occurred when replacing the file: \"{destinyPath + destiny}\": {e.Message}");
                    ManageErrorActions(HandleTransferErrorRepeatable($"An error has occurred when replacing the file: \"{destinyFile.FullName}\": {e.Message}", e,
                        originFile, destinyFile.DirectoryName, settings));

                    if (RepeatFileExecution) return;

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
                //    HandleLogMessage($"Error: {e.ToString()} when deleting the backup file '{destinyPath + destiny}.bac'");
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

                HandleLogMessage($"File not replaced: \"{originFile.FullName}\" and \"{destinyFile.FullName}\" are the same");
            }
            //}
            //catch (Exception e)
            //{
            //    HandleErrorDialogRepeatable(e.Message);
            //}
        }

        private void HandleFilenameConflictRenameDifferent(FileInfo originFile, FileInfo destinyFile, List<EnumeratedFile> enumeratedFileNames, 
            TransferSettings settings)
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
                filesAreTheSame = FileEquals(originFile, lastEnumeratedFile.File, settings);
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

                HandleLogMessage($"File not moved: \"{originFile.FullName}\" and \"{lastEnumeratedFile.File.FullName}\" are the same");
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
                        errorDialogMessage = $"An error has occurred while generating a new valid name for \"{originFile.FullName}\" in the destiny directory. " +
                            $"{e.Message}";
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

                        HandleLogMessage($"File moved and renamed: \"{originFile.FullName}\" to \"{destinyPathWithNewFileName}\"");
                    }
                    catch (Exception e)
                    {
                        errorLogMessage = $"Error: \"{e}\" while copying/renaming \"{originFile.FullName}\" to \"{destinyPathWithNewFileName}\"";
                        errorDialogMessage = $"An error has occurred while copying/renaming the file: \"{originFile.FullName}\" " +
                            $"to \"{destinyPathWithNewFileName}\". {e.Message}";

                        try
                        {
                            if (File.Exists(destinyPathWithNewFileName))
                            {
                                File.Delete(destinyPathWithNewFileName);
                                HandleLogMessage($"File deleted for safety: \"{destinyPathWithNewFileName}\"");
                            }
                        }
                        catch (Exception e1)
                        {
                            HandleLogMessage($"Error: {e1} when deleting \"{destinyPathWithNewFileName}\" as safety procedure");
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
                    errorDialogMessage = $"An error has occurred while reorganizing enumerated files originated from \"{originFile.FullName}\". {e.Message}";
                    throw;
                }
            }
            catch (Exception e)
            {
                HandleLogMessage(errorLogMessage);

                //throw new Exception($"An error has occurred when moving and renaming the file: '{entry}' to '{newFileName}'. {e.Message}");
                ManageErrorActions(HandleTransferErrorRepeatable(errorDialogMessage, e, originFile, destinyFile.DirectoryName, settings));

                if (RepeatFileExecution) return;

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

            //    HandleLogMessage($"File not moved: \"{originPathWithFileName}\" and \"{destinyPathWithFileName}\" are the same");
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
        /// <summary>
        /// Handles transfer errors in which the transfer of the file can be repeated, other than jumped or skipped.
        /// </summary>
        protected virtual FileTransferErrorActionRepeatable HandleTransferErrorRepeatable(string errorMessage, Exception e, FileInfo originFile, string destinyDir, 
            TransferSettings settings)
        {
            return FileTransferErrorActionRepeatable.JUMP;
        }

        //-- ...and when the particular file transfer can't be repeated...
        /// <summary>
        /// Handles transfer errors in which the transfer of the file cannot be repeated, only jumped or skipped.
        /// </summary>
        protected virtual FileTransferErrorActionNonRepeatable HandleTransferErrorNonRepeatable(string errorMessage, Exception e, FileInfo originFile, 
            string destinyDir, TransferSettings settings)
        {
            return FileTransferErrorActionNonRepeatable.JUMP;
        }

        //-- Choose what to do with log messages (what happens during the execution)
        /// <summary>
        /// Handles log messages containing technical details.
        /// </summary>
        protected virtual void HandleLogMessage(string logMessage)
        {
            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}]: {logMessage}");
        }

        //-- Choose what to do the names of files being executed
        /// <summary>
        /// Handles names of the files currently being transfered. <list type="table"><term><c>trimmedPathWithFileName</c></term>
        /// <description>file name including part of the path that is common between source and destiny files paths.</description></list>
        /// </summary>
        protected virtual void HandleCurrentFileExecution(string trimmedPathWithFileName, FileInfo originFile, string destinyDir, TransferSettings settings)
        {
            Console.WriteLine($"Executing: {trimmedPathWithFileName.Substring(trimmedPathWithFileName.LastIndexOf("\\") + 1)}");
        }

        private void ManageErrorActions(Enum fileTransferErrorAction)
        {
            if (fileTransferErrorAction.GetType().Equals(typeof(FileTransferErrorActionRepeatable)))
            {
                switch ((FileTransferErrorActionRepeatable)fileTransferErrorAction)
                {
                    case FileTransferErrorActionRepeatable.CANCEL:
                        CancelExecution = true;
                        RepeatFileExecution = false;
                        HandleLogMessage("Canceling transfer");
                        break;
                    case FileTransferErrorActionRepeatable.JUMP:
                        JumpFileExecution = true;
                        RepeatFileExecution = false;
                        HandleLogMessage("Jumping to the next file");
                        break;
                    case FileTransferErrorActionRepeatable.REPEAT:
                        RepeatFileExecution = true;
                        HandleLogMessage("Repeating file execution");
                        break;
                }
            }
            else if (fileTransferErrorAction.GetType().Equals(typeof(FileTransferErrorActionNonRepeatable)))
            {
                switch ((FileTransferErrorActionRepeatable)fileTransferErrorAction)
                {
                    case FileTransferErrorActionRepeatable.CANCEL:
                        CancelExecution = true;
                        RepeatFileExecution = false;
                        HandleLogMessage("Canceling transfer");
                        break;
                    case FileTransferErrorActionRepeatable.JUMP:
                        JumpFileExecution = true;
                        RepeatFileExecution = false;
                        HandleLogMessage("Jumping to the next file");
                        break;
                }
            }
        }
    }
}
