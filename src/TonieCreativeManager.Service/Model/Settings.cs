using System.Collections.Generic;

namespace TonieCreativeManager.Service.Model
{
    public class Settings
    {
        public IEnumerable<string>? MediaFileExtensions { get; set; }
        public IEnumerable<string>? FolderCoverFiles { get; set; }
        public IEnumerable<string>? ImageExtensions { get; set; }
        public string? LibraryRoot { get; set; }
        public string? RepositoryDataFile { get; set; }
        public IEnumerable<string>? KeyboardCharacters { get; set; }
        public int MaxParallelFileUploades { get; set; }
    }
}
