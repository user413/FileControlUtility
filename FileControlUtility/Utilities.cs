using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileControlUtility
{
    internal struct EnumeratedFile
    {
        public int Number { get; set; }
        public FileInfo File { get; set; }
    }

    internal static class Utility
    {
        internal static string AdjustPath(string path)
        {
            //return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            //    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            return string.Join(Path.DirectorySeparatorChar.ToString(), path.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, 
                StringSplitOptions.RemoveEmptyEntries));
        }

        internal static bool CharIsPathSeparator(char v) => v == Path.DirectorySeparatorChar || v == Path.AltDirectorySeparatorChar;

        internal static bool PathContainsDirectory(string path, string directory)
        {
            int index = path.IndexOf(directory, StringComparison.OrdinalIgnoreCase);
            return index >= 0 &&
                (
                    (path.Length == index + directory.Length) || //-- directory is in the end of the path
                    (path[index + directory.Length] == Path.DirectorySeparatorChar) //-- next character is a separator
                );
        }

        internal static bool PathIsSubdirectory(string path, string directory)
        {
            return path.Length > directory.Length && path.Substring(0, directory.Length).Equals(directory, StringComparison.OrdinalIgnoreCase)
                && (path[directory.Length] == Path.DirectorySeparatorChar/* || path[directory.Length] == Path.AltDirectorySeparatorChar*/);
        }
    }

    public partial class FileControl
    {
        private bool FileEquals(FileInfo file1, FileInfo file2, TransferSettings settings)
        {
            try
            {
                using (FileStream s1 = new FileStream(file1.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream s2 = new FileStream(file2.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                ManageErrorActions(HandleTransferErrorRepeatable($"An error has occurred when comparing files: \"{file1}\" and \"{file2}\". {e.Message}", e,
                    file1, file2.DirectoryName, settings));

                if (RepeatFileExecution) throw;

                NotTransferedFilesReports.Add(new NotTransferedFilesReport
                {
                    File = file1.FullName,
                    //Destiny = file2.Substring(0, file2.LastIndexOf("\\")),
                    Destiny = file2.DirectoryName,
                    Reason = "Error: " + e.Message
                });

                throw;// new Exception($"An error occurred when comparing files: \"{file1}\" and \"{file2}\". {e.Message}");
                //HandleErrorDialogRepeatable(e.Message);
            }
        }

        /// <summary>
        /// Update the numbers from all filenames enumerated with the pattern &lt;name&gt; (&lt;number&gt;)&lt;extension&gt;,
        /// originated from the given file, in it's own directory.<para>The maximum quantity of enumerated files (including the original) can be informed in <c>maxKeptFileCount</c>, 
        /// meaning the enumerated files, selected from highest number in descending way, will be kept and re-enumerated and the excess will be deleted. A value of 0 for 
        /// <c>maxKeptFileCount</c> means all files will be kept.</para>
        /// </summary>
        public void OrganizeEnumeratedFiles(string file, int maxKeptFileCount = 0)
        {
            ClearReportLists();
            OrganizeEnumeratedFiles(new FileInfo(file), maxKeptFileCount, null);
        }

        /// <summary>
        /// Update the numbers from all filenames enumerated with the pattern &lt;name&gt; (&lt;number&gt;)&lt;extension&gt;,
        /// originated from the given file, in it's own directory.<para>The maximum quantity of enumerated files (including the original) can be informed in <c>maxKeptFileCount</c>, 
        /// meaning the enumerated files, selected from highest number in descending way, will be kept and re-enumerated and the excess will be deleted. A value of 0 for 
        /// <c>maxKeptFileCount</c> means all files will be kept.</para>
        /// </summary>
        public void OrganizeEnumeratedFiles(FileInfo file, int maxKeptFileCount = 0)
        {
            ClearReportLists();
            OrganizeEnumeratedFiles(file, maxKeptFileCount, null);
        }

        ///// <summary>
        ///// Update the numbers from all filenames enumerated with the pattern &lt;name&gt; (&lt;number&gt;)&lt;extension&gt;,
        ///// originated from each file from the specified directory.<para>The maximum quantity of enumerated files (including the original) can be informed in <c>maxKeptFileCount</c>, 
        ///// meaning the enumerated files, selected from highest number in descending way, will be kept and re-enumerated and the excess will be deleted. A value of 0 for 
        ///// <c>maxKeptFileCount</c> means all files will be kept.</para>
        ///// </summary>
        //public void OrganizeEnumeratedFiles(string directory, SearchOption mode, int maxKeptFileCount = 0)
        //{
        //    ClearReportLists();
        //    foreach (var file in new DirectoryInfo(directory).GetFiles("*", mode))
        //        OrganizeEnumeratedFiles(file, maxKeptFileCount, null);
        //}

        ///// <summary>
        ///// Update the numbers from all filenames enumerated with the pattern &lt;name&gt; (&lt;number&gt;)&lt;extension&gt;,
        ///// originated from each file from the specified directory.<para>The maximum quantity of enumerated files (including the original) can be informed in <c>maxKeptFileCount</c>, 
        ///// meaning the enumerated files, selected from highest number in descending way, will be kept and re-enumerated and the excess will be deleted. A value of 0 for 
        ///// <c>maxKeptFileCount</c> means all files will be kept.</para>
        ///// </summary>
        //public void OrganizeEnumeratedFiles(DirectoryInfo directory, SearchOption mode, int maxKeptFileCount = 0)
        //{
        //    ClearReportLists();
        //    foreach (var file in directory.GetFiles("*", mode))
        //        OrganizeEnumeratedFiles(file, maxKeptFileCount, null);
        //}

        private void OrganizeEnumeratedFiles(FileInfo file, int maxKeptFileCount, List<EnumeratedFile> enumeratedFiles)
        {
            if (enumeratedFiles == null)
                enumeratedFiles = GetAllEnumeratedFiles(file); 

            bool needsReenumeration = false;

            for (int i = 0; i < enumeratedFiles.Count; i++)
                if (enumeratedFiles[i].Number != i)
                {
                    needsReenumeration = true;
                    break;
                }

            List<EnumeratedFile> filesToKeep;

            if (maxKeptFileCount == 0 || enumeratedFiles.Count <= maxKeptFileCount)
            {
                //-- Return if the files don't need to be re-enumerated or deleted
                if (!needsReenumeration) return;
                filesToKeep = enumeratedFiles.ToList();
                enumeratedFiles.Clear();
            }
            else
            {
                filesToKeep = enumeratedFiles.GetRange(enumeratedFiles.Count - maxKeptFileCount, maxKeptFileCount);
                enumeratedFiles.RemoveRange(enumeratedFiles.Count - maxKeptFileCount, maxKeptFileCount);
            }

            foreach (EnumeratedFile f in enumeratedFiles)
            {
                f.File.Delete();

                RemovedFilesAndDirReports.Add(new RemovedFilesAndDirectoriesReport
                {
                    Entry = f.File.FullName,
                    Description = "Deleted enumerated file"
                });

                HandleLogMessage($"File deleted: \"{f.File.FullName}\"");
            }

            for (int i = 0; i < filesToKeep.Count; i++)
            {
                string newName;

                if (i == 0)
                    newName = file.FullName;
                else
                    newName = $"{file.DirectoryName}\\{Path.GetFileNameWithoutExtension(file.FullName)} ({i}){file.Extension}";

                if (filesToKeep[i].File.FullName == newName) continue;

                File.Move(filesToKeep[i].File.FullName, newName);
                //filesToKeep[i].Info.MoveTo(newName);

                HandleLogMessage($"File renamed: \"{filesToKeep[i].File.FullName}\" to \"{newName}\"");

                RenamedFilesReports.Add(new RenamedFilesReport
                {
                    File = filesToKeep[i].File.FullName,
                    Destiny = newName
                });
            }
        }

        private static List<EnumeratedFile> GetAllEnumeratedFiles(FileInfo file)
        {
            List<EnumeratedFile> enumeratedFiles = new List<EnumeratedFile>();
            string nameWOExt = Path.GetFileNameWithoutExtension(file.FullName);

            if (file.Exists)
                enumeratedFiles.Add(new EnumeratedFile
                {
                    File = file,
                    Number = 0
                });

            foreach (FileInfo f in Directory.GetFiles(file.DirectoryName, "*", SearchOption.TopDirectoryOnly).Select(n => new FileInfo(n)))
            {
                if (!Regex.IsMatch(f.Name, $"^{nameWOExt} \\([0-9]+\\){file.Extension}$", RegexOptions.IgnoreCase)) continue;

                string restOfTheName = f.Name.Replace(nameWOExt, "");
                enumeratedFiles.Add(new EnumeratedFile
                {
                    File = f,
                    Number = int.Parse(restOfTheName.Remove(restOfTheName.IndexOf(")")).Substring(restOfTheName.IndexOf("(") + 1))
                });
            }

            enumeratedFiles = enumeratedFiles.OrderBy(f => f.Number).ToList();
            return enumeratedFiles;
        }
    }
}
