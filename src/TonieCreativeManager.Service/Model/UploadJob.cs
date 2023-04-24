using System;
using System.Collections.Generic;
using System.Text;

namespace TonieCreativeManager.Service.Model
{
    public class UploadJob
    {
        public UploadJob(int userId, string[] tonieIds, string path, string name, MediaItem[] files, bool append)
        {
            UserId = userId;
            TonieIds = tonieIds;
            Path = path;
            Name = name;
            Files = files;
            Append = append;
        }
        public int UserId { get; }
        public string[] TonieIds { get; }
        public string Path { get; }
        public string Name { get; }
        public MediaItem[] Files { get; }
        public bool Append { get; }
    }
}
