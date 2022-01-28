using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileControlUtility
{
    public class FileControl
    {
        public List<TransferedFilesReport> TransFilesReports { get; }
        public List<NotTransferedFilesReport> NotTransFilesReports { get; }
        public List<RenamedFilesReport> RenamedFilesReports { get; }
        public List<CreatedDirectoriesReport> CreatedDirReports { get; }
        public List<ReplacedFilesReport> ReplacedFilesReports { get; }
        public List<RemovedFilesAndDirectoriesReport> RemovedFilesAndDirReports { get; }

        public bool CancelExecution { get; set; } = false;
        private bool JumpFileExecution;
        private bool RepeatFileExecution;

        public FileControl()
        {
            TransFilesReports = new List<TransferedFilesReport>();
            NotTransFilesReports = new List<NotTransferedFilesReport>();
            RenamedFilesReports = new List<RenamedFilesReport>();
            CreatedDirReports = new List<CreatedDirectoriesReport>();
            ReplacedFilesReports = new List<ReplacedFilesReport>();
            RemovedFilesAndDirReports = new List<RemovedFilesAndDirectoriesReport>();
        }

        private void ClearReportLists()
        {
            TransFilesReports.Clear();
            NotTransFilesReports.Clear();
            RenamedFilesReports.Clear();
            CreatedDirReports.Clear();
            ReplacedFilesReports.Clear();
            RemovedFilesAndDirReports.Clear();
        }

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

            if (!CancelExecution)
            {
                HandleLogMessage("## Transfering from: " + settings.SourcePath);
                HandleLogMessage("## To: " + settings.DestinyPath);

                switch (settings.FileNameConflictMethod)
                {
                    case FileNameConflictMethod.DO_NOT_MOVE:
                        HandleLogMessage("## For repeated filenames: do not move");
                        break;
                    case FileNameConflictMethod.REPLACE_ALL:
                        HandleLogMessage("## For repeated filenames: replace all");
                        break;
                    case FileNameConflictMethod.REPLACE_DIFFERENT:
                        HandleLogMessage("## For repeated filenames: replace unique files (binary comparison)");
                        break;
                    case FileNameConflictMethod.RENAME_DIFFERENT:
                        HandleLogMessage("## For repeated filenames: rename unique files (binary comparison)");
                        break;
                }

                HandleLogMessage("## Include subfolders: " + settings.MoveSubFolders);
                HandleLogMessage("## Keep Files: " + settings.KeepOriginFiles);
                HandleLogMessage("## Clean destiny: " + settings.CleanDestinyDirectory);
                HandleLogMessage("## Delete uncommon: " + settings.DeleteUncommonFiles);
                HandleLogMessage("## Specified filenames/extensions mode: " + settings.SpecifiedFileNamesOrExtensionsMode);
                HandleLogMessage($"## Specified filenames/extensions: \"{string.Join("\",\"", settings.SpecifiedFileNamesAndExtensions)}\"");
            }

            string[] originFiles = null;
            string[] originDirectories = null;
            List<string> trimmedFiles = new List<string>();
            List<string> trimmedDirectories = new List<string>();
            string[] destinyFiles = null;
            string[] destinyDirectories = null;

            //GenerateFilesAndDirectoriesLists();

            try
            {
                HandleLogMessage("Generating files and directories lists...");

                if (settings.MoveSubFolders || !settings.KeepOriginFiles)
                    originDirectories = Directory.GetDirectories(settings.SourcePath, "*", SearchOption.AllDirectories);

                if (settings.MoveSubFolders)
                {
                    originFiles = Directory.GetFiles(settings.SourcePath, "*", SearchOption.AllDirectories);

                    if (settings.DeleteUncommonFiles)
                    {
                        destinyDirectories = Directory.GetDirectories(settings.DestinyPath, "*", SearchOption.AllDirectories);
                        destinyFiles = Directory.GetFiles(settings.DestinyPath, "*", SearchOption.AllDirectories);
                    }
                }
                else
                {
                    originFiles = Directory.GetFiles(settings.SourcePath, "*", SearchOption.TopDirectoryOnly);

                    if (settings.DeleteUncommonFiles)
                        destinyFiles = Directory.GetFiles(settings.DestinyPath, "*", SearchOption.TopDirectoryOnly);
                }

                //-- GENERATING COMMON TRIMMED FILE PATHS
                foreach (string file in originFiles)
                    trimmedFiles.Add(file.Replace(settings.SourcePath, ""));
                if (settings.MoveSubFolders) foreach (string originDirectory in originDirectories)
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
                                foreach (DirectoryInfo subdirs in dir.GetDirectories())
                                {
                                    subdirs.Delete(true);
                                    HandleLogMessage($"Folder deleted: {subdirs.Name}");
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

                    if (settings.MoveSubFolders)
                    {
                        foreach (string trimmedDirectory in trimmedDirectories)
                        {
                            if (!Directory.Exists(settings.DestinyPath + trimmedDirectory))
                            {
                                try
                                {
                                    Directory.CreateDirectory(settings.DestinyPath + trimmedDirectory);
                                    CreatedDirReports.Add(new CreatedDirectoriesReport
                                    {
                                        Directory = settings.DestinyPath + trimmedDirectory,
                                        Origin = settings.SourcePath + trimmedDirectory
                                    });
                                    HandleLogMessage($"Directory created: " + settings.DestinyPath + trimmedDirectory);
                                }
                                catch (Exception e)
                                {
                                    HandleLogMessage($"Error: {e} when creating the directory {settings.DestinyPath + trimmedDirectory} Aborting.");
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
                        NotTransFilesReports.Add(new NotTransferedFilesReport
                        {
                            File = settings.SourcePath + trimmedFile,
                            Destiny = settings.DestinyPath + trimmedFile.Substring(0, trimmedFile.LastIndexOf("\\")),
                            Reason = "Canceled"
                        });
                    }

                    throw;
                }

                HandleLogMessage("Transfering files..."); 
            }

            foreach (string trimmedPathWithFileName in trimmedFiles)
            {
                string originPathWithFileName = settings.SourcePath + trimmedPathWithFileName;
                string destinyPathWithFileName = settings.DestinyPath + trimmedPathWithFileName;
                string destinyPathWOFileName = settings.DestinyPath + trimmedPathWithFileName.Substring(0, trimmedPathWithFileName.LastIndexOf("\\"));
                string currentFileName = trimmedPathWithFileName.Substring(trimmedPathWithFileName.LastIndexOf("\\") + 1);

                if (CancelExecution)
                {
                    NotTransFilesReports.Add(new NotTransferedFilesReport
                    {
                        File = originPathWithFileName,
                        Destiny = destinyPathWOFileName,
                        Reason = "Canceled"
                    });

                    continue;
                }

                HandleCurrentFileExecution(trimmedPathWithFileName);
                //System.Threading.Thread.Sleep(5000);

                //-- MANAGING SPECIFIED FILENAMES AND EXTENSIONS
                if (settings.SpecifiedFileNamesAndExtensions != null && settings.SpecifiedFileNamesAndExtensions.Count > 0)
                {
                    string extension = Path.GetExtension(currentFileName);

                    if (settings.SpecifiedFileNamesOrExtensionsMode == SpecifiedFileNamesAndExtensionsMode.ALLOW_ONLY)
                    {
                        if (settings.SpecifiedFileNamesAndExtensions.Find(x => 
                                x.Equals(currentFileName, StringComparison.OrdinalIgnoreCase) ||
                                x.Equals(extension, StringComparison.OrdinalIgnoreCase)
                            ) == null)
                        {
                            NotTransFilesReports.Add(new NotTransferedFilesReport
                            {
                                File = originPathWithFileName,
                                Destiny = destinyPathWOFileName,
                                Reason = $"File extension ignored ({extension})"
                            });

                            HandleLogMessage($"Filename extension ignored. File not transfered:\"'{originPathWithFileName}\" as \"{destinyPathWithFileName}\"");
                            continue;
                        }
                    }
                    else if (settings.SpecifiedFileNamesOrExtensionsMode == SpecifiedFileNamesAndExtensionsMode.IGNORE)
                    {
                        if (settings.SpecifiedFileNamesAndExtensions.Find(x => 
                                x.Equals(currentFileName, StringComparison.OrdinalIgnoreCase) ||
                                x.Equals(extension, StringComparison.OrdinalIgnoreCase)
                            ) != null)
                        {
                            NotTransFilesReports.Add(new NotTransferedFilesReport
                            {
                                File = originPathWithFileName,
                                Destiny = destinyPathWOFileName,
                                Reason = $"File extension ignored ({extension})"
                            });

                            HandleLogMessage($"Filename extension ignored. File not ransfered: \"{originPathWithFileName}\" as \"{destinyPathWithFileName}\"");
                            continue;
                        }
                    }
                }

                do
                    TransferFile(settings, originPathWithFileName, destinyPathWithFileName, destinyPathWOFileName);
                while (RepeatFileExecution);
            }

            //-- DELETING ORIGIN DIRECTORIES
            if (!settings.KeepOriginFiles && !CancelExecution)
            {
                HandleLogMessage("Cleaning origin directories...");
                try
                {
                    foreach (string entry in originDirectories.Reverse().ToList())
                    {
                        if (Directory.Exists(entry))
                        {
                            Directory.Delete(entry, true);

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
            if (settings.DeleteUncommonFiles && !settings.CleanDestinyDirectory && !CancelExecution)
            {
                try
                {
                    foreach (string destinyFile in destinyFiles)
                    {
                        string trimmedDestinyFile = destinyFile.Replace(settings.DestinyPath, "");
                        if (trimmedFiles.Find(x => x.Equals(trimmedDestinyFile, StringComparison.OrdinalIgnoreCase)) == null)
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
                    if (settings.MoveSubFolders)
                    {
                        foreach (string destinyDirectory in destinyDirectories.Reverse().ToList())
                        {
                            string trimmedDestinyDirectory = destinyDirectory.Replace(settings.DestinyPath, "");
                            if (trimmedDirectories.Find(x => x.Equals(trimmedDestinyDirectory,StringComparison.OrdinalIgnoreCase)) == null)
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

        private void TransferFile(TransferSettings settings, string originPathWithFileName,string destinyPathWithFileName,
            string destinyPathWOFileName)
        {
            if (JumpFileExecution) JumpFileExecution = false;
            if (RepeatFileExecution) RepeatFileExecution = false;

            if (!File.Exists(destinyPathWithFileName))
            {
                FileInfo oFile = new FileInfo(originPathWithFileName);
                oFile.IsReadOnly = false;

                try
                {
                    if (settings.KeepOriginFiles)
                    {
                        oFile.CopyTo(destinyPathWithFileName);
                    }
                    else
                    {
                        oFile.MoveTo(destinyPathWithFileName);

                        RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                        {
                            Entry = originPathWithFileName,
                            Description = "Removed origin file"
                        });
                    }

                    TransFilesReports.Add(new TransferedFilesReport
                    {
                        File = originPathWithFileName,
                        Destiny = destinyPathWOFileName
                    });

                    HandleLogMessage($"Transfered file: \"{originPathWithFileName}\" to \"{destinyPathWOFileName}\"");
                }
                catch (Exception e)
                {
                    HandleLogMessage($"Error: {e} when transfering \"{originPathWithFileName}\" to \"{destinyPathWOFileName}\"");
                    if (File.Exists(destinyPathWithFileName))
                    {
                        try
                        {
                            File.Delete(destinyPathWithFileName);
                            HandleLogMessage($"File deleted for safety: \"{destinyPathWithFileName}\"");
                        }
                        catch (Exception e1)
                        {
                            HandleLogMessage($"Error: {e1} when deleting \"{destinyPathWithFileName}\"");
                        }
                    }

                    //throw new Exception($"An error has occurred when transfering the file: '{settings.DestinyPath + destiny}'. {e.Message}");
                    ManageErrorActions(HandleErrorDialogRepeatable($"An error has occurred when transfering the file \"{originPathWithFileName}\" to " +
                        $"\"{destinyPathWOFileName}\": {e.Message}", e,originPathWithFileName,destinyPathWithFileName));
                    
                    if (RepeatFileExecution) return;

                    NotTransFilesReports.Add(new NotTransferedFilesReport
                    {
                        File = originPathWithFileName,
                        Destiny = destinyPathWOFileName,
                        Reason = "Error: " + e.Message
                    });
                }
            }
            else
            {
                switch (settings.FileNameConflictMethod)
                {
                    case FileNameConflictMethod.DO_NOT_MOVE:
                        HandleFilenameConflictDoNotReplaceFiles(originPathWithFileName, destinyPathWithFileName, destinyPathWOFileName);
                        break;
                    case FileNameConflictMethod.REPLACE_ALL:
                        HandleFilenameConflictReplaceAllFiles(originPathWithFileName, destinyPathWithFileName, destinyPathWOFileName);
                        break;
                    case FileNameConflictMethod.REPLACE_DIFFERENT:
                        HandleFilenameConflictReplaceUniqueFiles(originPathWithFileName, destinyPathWithFileName, destinyPathWOFileName);
                        break;
                    case FileNameConflictMethod.RENAME_DIFFERENT:
                        HandleFilenameConflictRenameUniqueFiles(originPathWithFileName, destinyPathWithFileName, destinyPathWOFileName);
                        break;
                }

                //-- DELETING ORIGIN FILE
                if (!settings.KeepOriginFiles && !CancelExecution && !JumpFileExecution && !RepeatFileExecution)
                {
                    try
                    {
                        if (File.Exists(originPathWithFileName))
                        {
                            File.Delete(originPathWithFileName);

                            RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                            {
                                Entry = originPathWithFileName,
                                Description = "Removed origin file"
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        HandleLogMessage($"Error: {e} when deleting the source file \"{originPathWithFileName}\"");
                        //throw new Exception($"An error has occurred when deleting the source file '{entry}'. {e.Message}");
                        ManageErrorActions(HandleErrorDialogNonRepeatable($"An error has occurred when deleting the source file \"{originPathWithFileName}\". " +
                            $"{e.Message}", e, originPathWithFileName,destinyPathWithFileName));
                    }
                }

                //if (this.CancelExecution) HandleLogMessage("Canceling operation...");
            }
        }

        private void HandleFilenameConflictDoNotReplaceFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        {
            NotTransFilesReports.Add(new NotTransferedFilesReport
            {
                File = originPathWithFileName,
                Destiny = destinyPathWOFileName,
                Reason = "Repeated name"
            });

            HandleLogMessage($"Conflicted filename. File not moved: \"{originPathWithFileName}\" as \"{destinyPathWithFileName}\"");
        }

        private void HandleFilenameConflictReplaceAllFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
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
            FileInfo sourceFile = new FileInfo(originPathWithFileName);

            try
            {
                sourceFile.CopyTo(destinyPathWithFileName, true);
                //File.Replace(entry, destinyPath + destiny, destinyPath + destiny + ".bac", false);
                //File.Delete(destinyPath + destiny);
                //File.Copy(entry, destinyPath + destiny);
                ReplacedFilesReports.Add(new ReplacedFilesReport
                {
                    File = originPathWithFileName,
                    Destiny = destinyPathWOFileName
                });

                HandleLogMessage($"File replaced: \"{destinyPathWithFileName}\"");
            }
            catch (Exception e)
            {
                HandleLogMessage($"Error: {e} when replacing \"{destinyPathWithFileName}\"");
                //if (File.Exists(destinyPath + destiny + ".bac"))
                //{
                    if (File.Exists(destinyPathWithFileName))
                    {
                        try
                        {
                            File.Delete(destinyPathWithFileName);
                            HandleLogMessage($"File deleted for safety: \"{destinyPathWithFileName}\"");
                        }
                        catch (Exception e1)
                        {
                            HandleLogMessage($"Error: {e1} when deleting \"{destinyPathWithFileName}\" as safety procedure");
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

                ManageErrorActions(HandleErrorDialogRepeatable($"An error has occurred while replacing \"{destinyPathWithFileName}\": {e.Message}", e,
                    originPathWithFileName, destinyPathWithFileName));
                
                if (RepeatFileExecution) return;
                
                NotTransFilesReports.Add(new NotTransferedFilesReport
                {
                    File = originPathWithFileName,
                    Destiny = destinyPathWOFileName,
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

        private void HandleFilenameConflictReplaceUniqueFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        {
            bool filesAreTheSame;

            try
            {
                filesAreTheSame = FileEquals(originPathWithFileName, destinyPathWithFileName);
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
                FileInfo sourceFile = new FileInfo(originPathWithFileName);

                try
                {
                    sourceFile.CopyTo(destinyPathWithFileName, true);
                    //File.Delete(destinyPath + destiny);
                    //File.Copy(entry, destinyPath + destiny);
                    //File.Replace(entry, destinyPath + destiny, destinyPath + destiny + ".bac", false);
                    ReplacedFilesReports.Add(new ReplacedFilesReport
                    {
                        File = originPathWithFileName,
                        Destiny = destinyPathWOFileName
                    });

                    HandleLogMessage($"Replaced file: \"{destinyPathWithFileName}\"");
                }
                catch (Exception e)
                {
                    HandleLogMessage($"Error: {e} when replacing \"{destinyPathWithFileName}\"");
                    //if (File.Exists(destinyPath + destiny + ".bac"))
                    //{
                    if (File.Exists(destinyPathWithFileName))
                    {
                        try
                        {
                            File.Delete(destinyPathWithFileName);
                            HandleLogMessage($"File deleted for safety: \"{destinyPathWithFileName}\"");
                        }
                        catch (Exception e1)
                        {
                            HandleLogMessage($"Error: {e1} when deleting \"{destinyPathWithFileName}\" as safety procedure");
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
                    ManageErrorActions(HandleErrorDialogRepeatable($"An error has occurred when replacing the file: \"{destinyPathWithFileName}\": {e.Message}",e,
                        originPathWithFileName,destinyPathWithFileName));
                    if (RepeatFileExecution) return;

                    NotTransFilesReports.Add(new NotTransferedFilesReport
                    {
                        File = originPathWithFileName,
                        Destiny = destinyPathWOFileName,
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
                NotTransFilesReports.Add(new NotTransferedFilesReport
                {
                    File = originPathWithFileName,
                    Destiny = destinyPathWOFileName,
                    Reason = "File already exists"
                });

                HandleLogMessage($"File not replaced: \"{originPathWithFileName}\" and \"{destinyPathWithFileName}\" are the same");
            }
            //}
            //catch (Exception e)
            //{
            //    HandleErrorDialogRepeatable(e.Message);
            //}
        }

        private void HandleFilenameConflictRenameUniqueFiles(string originPathWithFileName, string destinyPathWithFileName, string destinyPathWOFileName)
        {
            bool filesAreTheSame;

            try
            {
                filesAreTheSame = FileEquals(originPathWithFileName, destinyPathWithFileName);
            }
            catch (Exception)
            {
                //HandleErrorDialogRepeatable($"An error occurred when comparing files: \"{originPathWithFileName}\" and \"{destinyPathWithFileName}\". {e.Message}",true);
                return;
            }

            if (!filesAreTheSame)
            {
                string filenameExtension = Path.GetExtension(destinyPathWithFileName);

                bool i = true;
                int repetitions = 1;
                while (i)
                {
                    //try
                    string destinyPathWithNewFileName = $"{destinyPathWOFileName}\\{Path.GetFileNameWithoutExtension(destinyPathWithFileName)} ({repetitions}){filenameExtension}";

                    if (!File.Exists(destinyPathWithNewFileName))
                    {
                        try
                        {
                            //throw new Exception();
                            File.Copy(originPathWithFileName, destinyPathWithNewFileName);
                            RenamedFilesReports.Add(new RenamedFilesReport
                            {
                                File = originPathWithFileName,
                                Destiny = destinyPathWithNewFileName
                            });

                            HandleLogMessage($"File moved and renamed: \"{originPathWithFileName}\" to \"{destinyPathWithNewFileName}\"");
                            i = false;
                        }
                        catch (Exception e)
                        {
                            HandleLogMessage($"Error: {e} when moving and renaming \"{originPathWithFileName}\" to \"{destinyPathWithNewFileName}\"");
                            if (File.Exists(destinyPathWithNewFileName))
                            {
                                try
                                {
                                    File.Delete(destinyPathWithNewFileName);
                                    HandleLogMessage($"File deleted for safety: \"{destinyPathWithNewFileName}\"");
                                }
                                catch (Exception e1)
                                {
                                    HandleLogMessage($"Error: {e1} when deleting \"{destinyPathWithNewFileName}\"as safety procedure");
                                }
                            }

                            //throw new Exception($"An error has occurred when moving and renaming the file: '{entry}' to '{newFileName}'. {e.Message}");
                            ManageErrorActions(HandleErrorDialogRepeatable($"An error has occurred when moving and renaming the file: \"{originPathWithFileName}\" " +
                                $"to \"{destinyPathWithNewFileName}\". {e.Message}",e,originPathWithFileName,destinyPathWithFileName));
                            if (RepeatFileExecution) return;

                            NotTransFilesReports.Add(new NotTransferedFilesReport
                            {
                                File = originPathWithFileName,
                                Destiny = destinyPathWOFileName,
                                Reason = "Error: " + e.Message
                            });

                            break;
                        }
                    }
                    //catch (IOException)
                    else
                    {
                        try
                        {
                            filesAreTheSame = FileEquals(originPathWithFileName, destinyPathWithNewFileName);
                        }
                        catch (Exception)
                        {
                            //HandleErrorDialogRepeatable($"An error occurred when comparing files: \"{originPathWithFileName}\" and \"{destinyPathWithNewFileName}\". {e.Message}",true);
                            return;
                        }
                        if (!filesAreTheSame)
                        {
                            repetitions++;
                        }
                        else
                        {
                            NotTransFilesReports.Add(new NotTransferedFilesReport
                            {
                                File = originPathWithFileName,
                                Destiny = destinyPathWOFileName,
                                Reason = "File already exists as \"" + destinyPathWithNewFileName.Substring(destinyPathWithNewFileName.LastIndexOf("\\") + 1) + "\""
                            });

                            HandleLogMessage($"File not moved: {originPathWithFileName} and \"{destinyPathWithNewFileName}\" are the same");
                            i = false;
                        }
                    }
                }
            }
            else
            {
                NotTransFilesReports.Add(new NotTransferedFilesReport
                {
                    File = originPathWithFileName,
                    Destiny = destinyPathWOFileName,
                    Reason = "File already exists as \"" + destinyPathWithFileName.Substring(destinyPathWithFileName.LastIndexOf("\\") + 1) + "\""
                });
                HandleLogMessage($"File not moved: \"{originPathWithFileName}\" and \"{destinyPathWithFileName}\" are the same");
            }
        }

        private bool FileEquals(string file1, string file2)
        {
            try
            {
                using (FileStream s1 = new FileStream(file1, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream s2 = new FileStream(file2, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader b1 = new BinaryReader(s1))
                using (BinaryReader b2 = new BinaryReader(s2))
                {
                    while (true)
                    {
                        byte[] data1 = b1.ReadBytes(65536); //64 * 1024
                        byte[] data2 = b2.ReadBytes(65536);
                        if (data1.Length != data2.Length)
                            return false;
                        if (data1.Length == 0)
                            return true;
                        if (!data1.SequenceEqual(data2))
                            return false;
                    }
                }
            }
            catch (Exception e)
            {
                HandleLogMessage($"Error: {e} when comparing files: \"{file1}\" and \"{file2}\"");
                ManageErrorActions(HandleErrorDialogRepeatable($"An error has occurred when comparing files: \"{file1}\" and \"{file2}\". {e.Message}", e, 
                    file1, file2));
                if (RepeatFileExecution) throw;

                NotTransFilesReports.Add(new NotTransferedFilesReport
                {
                    File = file1,
                    Destiny = file2.Substring(0, file2.LastIndexOf("\\")),
                    Reason = "Error: "+ e.Message
                });

                throw;// new Exception($"An error occurred when comparing files: \"{file1}\" and \"{file2}\". {e.Message}");
                //HandleErrorDialogRepeatable(e.Message);
            }
        }

        public int FilesTotal(List<TransferSettings> settings)
        {
            int fileCount = 1;
            string[] files;

            foreach (TransferSettings s in settings)
            {
                if (s.MoveSubFolders == true)
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
        public virtual FileTransferErrorActionRepeatable HandleErrorDialogRepeatable(string errorMessage, Exception e, string originFile, string destinyFile)
        {
            return FileTransferErrorActionRepeatable.JUMP;
        }

        //-- ...and when the particular file transfer can't be repeated...
        public virtual FileTransferErrorActionNonRepeatable HandleErrorDialogNonRepeatable(string errorMessage, Exception e, string originFile, string destinyFile)
        {
            return FileTransferErrorActionNonRepeatable.JUMP;
        }

        //-- Choose what to do with log messages (what happens during the execution)
        public virtual void HandleLogMessage(string logMessage)
        {
            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}]: {logMessage}");
        }

        //-- Choose what to do the names of files being executed
        public virtual void HandleCurrentFileExecution(string trimmedPathWithFileName)
        {
            Console.WriteLine($"Executing: {trimmedPathWithFileName.Substring(trimmedPathWithFileName.LastIndexOf("\\") + 1)}");
        }

        private void ManageErrorActions(object fileTransferErrorAction)
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

    public enum FileTransferErrorActionRepeatable
    {
        JUMP, //-- Ignore current file and jump to next
        CANCEL, //-- Cancel execution for the instance
        REPEAT //-- Try to transfer current file again
    }

    public enum FileTransferErrorActionNonRepeatable
    {
        JUMP, CANCEL
    }
}
