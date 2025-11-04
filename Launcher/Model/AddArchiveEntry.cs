namespace Launcher.Model
{
    internal sealed class AddArchiveEntry
    {
        public string? ArchiveName { get; set; }
        public string? Url { get; set; }
        public long Size { get; set; }
        public string? Sha256 { get; set; }
        public List<AddFileEntry>? Entries { get; set; }
    }
}
