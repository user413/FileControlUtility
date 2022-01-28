using System.Collections.Generic;

namespace FileControlUtility
{
    public enum FileNameConflictMethod
    {
        DO_NOT_MOVE, REPLACE_ALL, REPLACE_DIFFERENT, RENAME_DIFFERENT
    }

    public enum SpecifiedFileNamesAndExtensionsMode
    {
        ALLOW_ONLY,
        IGNORE
    }

    public class TransferSettings
    {
        public string SourcePath { get; set; }
        public string DestinyPath { get; set; }
        public bool MoveSubFolders { get; set; }
        public bool KeepOriginFiles { get; set; }
        public bool CleanDestinyDirectory { get; set; }
        public bool DeleteUncommonFiles { get; set; }
        //public bool AllowIgnoreFileExt { get; set; }
        public FileNameConflictMethod FileNameConflictMethod { get; set; }
        public SpecifiedFileNamesAndExtensionsMode SpecifiedFileNamesOrExtensionsMode { get; set; }
        public List<string> SpecifiedFileNamesAndExtensions { get; set; }
    }
}
