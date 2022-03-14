namespace FileControlUtility
{
    public struct TransferedFilesReport
    {
        public string File { get; set; }
        public string Destiny { get; set; }
    }
    public struct NotTransferedFilesReport
    {
        public string File { get; set; }
        public string Destiny { get; set; }
        public string Reason { get; set; }
    }
    public struct RenamedFilesReport
    {
        public string File { get; set; }
        public string Destiny { get; set; }
    }
    public struct CreatedDirectoriesReport
    {
        public string Directory { get; set; }
        public string Origin { get; set; }
    }

    public struct ReplacedFilesReport
    {
        public string File { get; set; }
        public string Destiny { get; set; }
    }

    public struct RemovedFilesAndDirectoriesReport
    {
        public string Entry { get; set; }
        public string Description { get; set; }
    }
}
