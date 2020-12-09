namespace FileControlUtility
{
    public class TransferedFilesReport
    {
        public string File { get; set; }
        public string Destiny { get; set; }
    }
    public class NotTransferedFilesReport
    {
        public string File { get; set; }
        public string Destiny { get; set; }
        public string Reason { get; set; }
    }
    public class RenamedFilesReport
    {
        public string File { get; set; }
        public string Destiny { get; set; }
    }
    public class CreatedDirectoriesReport
    {
        public string Directory { get; set; }
        public string Origin { get; set; }
    }
    public class ReplacedFilesReport
    {
        public string File { get; set; }
        public string Destiny { get; set; }
    }

    public class RemovedFilesAndDirectoriesReport
    {
        public string Entry { get; set; }
        public string Description { get; set; }
    }
}
