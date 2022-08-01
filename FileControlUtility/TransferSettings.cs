using System.Collections.Generic;

namespace FileControlUtility
{
    public enum FileNameConflictMethod
    {
        SKIP, REPLACE_ALL, REPLACE_DIFFERENT, RENAME_DIFFERENT
    }

    public enum SpecifiedEntriesMode
    {
        ALLOW_ONLY, IGNORE
    }

    public class TransferSettings
    {
        private string sourcePath;
        private string destinyPath;

        public TransferSettings() { }
        public TransferSettings(string sourcePath, string destinyPath, FileNameConflictMethod fileNameConflictMethod = FileNameConflictMethod.SKIP,
            bool includeSubFolders = false, bool keepOriginFiles = true, bool cleanDestinyDirectory = false, bool deleteUncommonFiles = false,
            SpecifiedEntriesMode specifiedFileNamesOrExtensionsMode = SpecifiedEntriesMode.ALLOW_ONLY, List<string> specifiedFileNamesAndExtensions = null,
            SpecifiedEntriesMode specifiedDirectoriesMode = SpecifiedEntriesMode.ALLOW_ONLY, List<string> specifiedDirectories = null,
            bool reenumerateRenamedFiles = false, int maxKeptRenamedFileCount = 0)
        {
            SourcePath = sourcePath;
            DestinyPath = destinyPath;
            IncludeSubFolders = includeSubFolders;
            KeepOriginFiles = keepOriginFiles;
            CleanDestinyDirectory = cleanDestinyDirectory;
            FileNameConflictMethod = fileNameConflictMethod;
            SpecifiedFileNamesOrExtensionsMode = specifiedFileNamesOrExtensionsMode;
            SpecifiedFileNamesAndExtensions = specifiedFileNamesAndExtensions;
            DeleteUncommonFiles = deleteUncommonFiles;
            ReenumerateRenamedFiles = reenumerateRenamedFiles;
            MaxKeptRenamedFileCount = maxKeptRenamedFileCount;
            SpecifiedDirectoriesMode = specifiedDirectoriesMode;
            SpecifiedDirectories = specifiedDirectories;
        }

        public string SourcePath { get => sourcePath; set => sourcePath = Utility.AdjustPath(value); }
        public string DestinyPath { get => destinyPath; set => destinyPath = Utility.AdjustPath(value); }
        /// <summary>
        /// Whether to include subfolders and their content, otherwise only the top directory.
        /// </summary>
        /// <remarks>Default: <c>false</c></remarks>
        public bool IncludeSubFolders { get; set; }
        /// <summary>
        /// Whether to copy origin files, otherwise move.
        /// </summary>
        /// <remarks>Default: <c>true</c></remarks>
        public bool KeepOriginFiles { get; set; } = true;
        /// <summary>
        /// Delete all files from destiny directory before transfering.
        /// </summary>
        /// <remarks>Default: <c>false</c></remarks>
        public bool CleanDestinyDirectory { get; set; }
        /// <summary>
        /// What to do with filename conflicts.
        /// <list type="table"><item><term><c>FileNameConflictMethod.DO_NOT_MOVE</c></term><description>files will be skipped.</description></item>
        /// <item><term><c>FileNameConflictMethod.REPLACE_ALL</c></term><description>replace all files.</description></item>
        /// <item><term><c>FileNameConflictMethod.REPLACE_DIFFERENT</c></term><description>perfom a binary comparison, then replace, if they're not equal.</description></item>
        /// <item><term><c>FileNameConflictMethod.RENAME_DIFFERENT</c></term><description>perfom a binary comparison, then copy the file giving it an enumerated name, 
        /// if they're not equal.</description></item>
        /// </list>
        /// </summary>
        /// <remarks>Default: <c>FileNameConflictMethod.DO_NOT_MOVE</c></remarks>
        public FileNameConflictMethod FileNameConflictMethod { get; set; }
        /// <summary>
        /// What to do with the specified files or extensions inside SpecifiedFileNamesOrExtensions list.
        /// <list type="table"><item><term><c>SpecifiedEntriesMode.ALLOW_ONLY</c></term><description>only specified items will be transfered.</description></item>
        /// <item><term><c>SpecifiedEntriesMode.IGNORE</c></term><description>all specified items will be ignored.</description></item></list>
        /// </summary>
        /// <remarks>Default: <c>SpecifiedEntriesMode.ALLOW_ONLY</c></remarks>
        public SpecifiedEntriesMode SpecifiedFileNamesOrExtensionsMode { get; set; }
        /// <summary>
        /// List of files or extensions to be selected or excluded from the transfer based on <c>SpecifiedFileNamesOrExtensionsMode</c>. The list can be empty, meaning 
        /// no file will be excluded.
        /// </summary>
        public List<string> SpecifiedFileNamesAndExtensions { get; set; }
        /// <summary>
        /// What to do with the specified directories inside SpecifiedDirectories list.
        /// <list type="table"><item><term><c>SpecifiedEntriesMode.ALLOW_ONLY</c></term><description>only specified items will be transfered.</description></item>
        /// <item><term><c>SpecifiedEntriesMode.IGNORE</c></term><description>all specified items will be ignored.</description></item></list>
        /// </summary>
        /// <remarks>Default: <c>SpecifiedEntriesMode.ALLOW_ONLY</c></remarks>
        public SpecifiedEntriesMode SpecifiedDirectoriesMode { get; set; }
        /// <summary>
        /// List of directories to be selected or excluded from the transfer based on <c>SpecifiedDirectoriesMode</c>. The list can be empty, meaning 
        /// no directory will be excluded.
        /// </summary>
        public List<string> SpecifiedDirectories { get; set; }
        /// <summary>
        /// <para>Whether to delete files present in the destiny directory that weren't present in source directory.</para>
        /// <para>Note: no effect while using <c>RENAME_DIFFERENT</c> conflict method type.</para>
        /// </summary>
        /// <remarks>Default: <c>false</c></remarks>
        public bool DeleteUncommonFiles { get; set; }
        /// <summary>
        /// Update the numbers from filenames in the destiny directory enumerated with the pattern &lt;name&gt; (&lt;number&gt;)&lt;extension&gt;, 
        /// for each file from which they have originated. The maximum quantity of enumerated files (including the original) can be informed in <c>MaxKeptRenamedFileCount</c>, 
        /// meaning the enumerated files, selected from highest number in descending way, will be kept and re-enumerated and the excess will
        /// be deleted. <para>Note: has effect only while using <c>RENAME_DIFFERENT</c> conflict 
        /// method type.</para><para>Note: applies only to files subject to the transfer, not all files in the destiny directory. To enumerate all files use 
        /// the <c>OrganizeEnumeratedFiles</c> method.</para>
        /// </summary>
        /// <remarks>Default: <c>false</c></remarks>
        public bool ReenumerateRenamedFiles { get; set; }
        /// <summary>
        /// The maximum quantity of enumerated files in the destiny directory (including the original) to be kept and re-enumerated, 
        /// for each file from which they have originated. Files with the highest enumeration will be selected   
        /// and the excess will be deleted.<para>Note: 0 means all files will be kept.</para><para>Note: has effect only while using <c>RENAME_DIFFERENT</c> conflict 
        /// method type and <c>ReenumerateRenamedFiles</c> = true.</para>
        /// </summary>
        /// <remarks>Default: <c>0</c></remarks>
        public int MaxKeptRenamedFileCount { get; set; }
    }
}
