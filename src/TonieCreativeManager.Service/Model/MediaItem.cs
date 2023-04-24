
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TonieCreativeManager.Service.Model
{
    public class MediaItem
    {
        private string? _Path;
        private string? _ParentId;
        public MediaItem(string? id = null)
        {
            Id = id ?? Guid.NewGuid().ToString().Replace("-", "");
        }
        public string Id { get; set; }
        public string? ParentId { get => Parent?.Id ?? _ParentId; set => _ParentId = value; }
        [JsonIgnore] public MediaItem Parent { get; set; }
        [JsonIgnore] public IEnumerable<MediaItem>? Childs { get; set; }
        public bool IsFile { get; set; }
        [JsonIgnore] public bool IsDirectory { get => !IsFile; }
        [JsonIgnore] public bool IsUploadable { get => TotalSize > 0 && (Childs?.Any(_ => _.IsFile) ?? false); }
        public string? Name { get; set; }
        [JsonIgnore] public string Path { get => (_Path ??= Parent == null ? null : System.IO.Path.Combine(Parent.Path, Name)) ?? ""; set => _Path = value; }
        public long TotalSize { get; set; }
        public DateTime LastWriteTime { get; set; }
        public double? Duration { get; set; }
        public double? ApproximateDuration { get; set; }
        public bool UpdateSums()
        {
            var changed = false;
            if (IsDirectory && Childs != null)
            {
                var totalSize = Childs.Sum(_ => _.TotalSize);
                var duration = Childs.Sum(_ => _.Duration ?? _.ApproximateDuration ?? 0);
                var approximateDuration = Childs.Sum(_ => _.ApproximateDuration);
                changed = TotalSize != totalSize || Duration != duration || ApproximateDuration != approximateDuration;
                TotalSize = totalSize;
                Duration = duration;
                ApproximateDuration = approximateDuration;
            }
            if (changed)
                Parent?.UpdateSums();
            return changed;
        }

        public bool IsAllowedForUserToBuy(PersistentData.User u)
        {
            return TotalSize > 0
                && u.AllowedMedia.Contains(Id);
        }
        public bool IsBoughtByUser(PersistentData.User u)
        {
            return IsAllowedForUserToBuy(u)
                && (!IsUploadable
                || u.BoughtMedia.Contains(Id));
        }
        public void SetNotAllowed(PersistentData.User u)
        {
            if (u.BoughtMedia.Contains(Id)) u.BoughtMedia.Remove(Id);
            if (u.AllowedMedia.Contains(Id)) u.AllowedMedia.Remove(Id);
        }
        public void SetBougntByUser(PersistentData.User u)
        {
            if (!u.BoughtMedia.Contains(Id)) u.BoughtMedia.Add(Id);
        }
        public void SetAllowed(PersistentData.User u)
        {
            if (u.BoughtMedia.Contains(Id)) u.BoughtMedia.Remove(Id);
            if (!u.AllowedMedia.Contains(Id)) u.AllowedMedia.Add(Id);
        }
        public int ToniesNeeded => (int)Math.Max(1, Math.Ceiling((Duration ?? ApproximateDuration ?? 0) / 5400.0)); //90mins per Tonie
    }
}
