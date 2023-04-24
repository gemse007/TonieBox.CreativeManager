using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TonieCreativeManager.Service.Model;

namespace TonieCreativeManager.Service
{
    public class MediaService
    {
        private readonly Settings _Settings;
        private readonly RepositoryService _RepositoryService;
        private Dictionary<string, MediaItem>? _Cache;
        private Task? _ReadItems;
        private Task? _ReadSubItems;

        public MediaService(Settings settings, RepositoryService repositoryService)
        {
            Console.WriteLine($"Using '{settings.LibraryRoot}' for library root");

            _Settings = settings;
            _RepositoryService = repositoryService;
        }

        /// <summary>
        /// Loads and caches the Mediafiles in the whole tree
        /// When Loading a Path Directories and Files are compared with cache (by Name, Length (Files only) and DateTime)
        ///     when matched -> no subcheck
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<MediaItem> GetMediaItemAsync(string path)
        {
            var fullpath = Path.Combine(_Settings.LibraryRoot, path);
            if (_Cache == null)
            {
                lock (_RepositoryService)
                {
                    if (_ReadItems == null)
                    {
                        _ReadSubItems = new Task(async () =>
                        {
                            MediaItem dir;
                            Console.WriteLine("MediaService.ReadSubItems starting");
                            while ((dir = _Cache.Values.FirstOrDefault(_ => (_.IsDirectory && _.Childs == null) || _.ApproximateDuration == null)) != null)
                            {
                                await GetMediaItemAsync(dir.Path);
                            }
                            Console.WriteLine("MediaService.ReadSubItems finished");
                        });
                        _ReadItems = new Task(async () =>
                        {
                            Console.WriteLine("MediaService.ReadItems starting");
                            var data = await _RepositoryService.GetMediaItems() ?? new MediaItem[] { };
                            var dirs = data.Where(_ => _.IsDirectory).ToDictionary(_ => _.Id);
                            data.Where(_ => _.ParentId != null && dirs.ContainsKey(_.ParentId))
                                .GroupBy(_ => _.ParentId)
                                .ToList()
                                .ForEach(_ =>
                                {
                                    var parent = dirs[_.Key];
                                    parent.Childs = _.ToArray();
                                    parent.Childs.ToList().ForEach(__ => __.Parent = parent);
                                });
                            data = data.Where(_ => _.ParentId == null && _.Name == "Library" || _.Parent != null).ToArray();
                            _Cache = data?.ToDictionary(_ => _.Path ?? "") ?? new Dictionary<string, MediaItem>();
                            if (!_Cache.ContainsKey(""))
                            {
                                _Cache[""] = new MediaItem { Path = "", Name = "Library" };
                                await GetItemsAsync(_Cache[""]);
                            }
                            Console.WriteLine("MediaService.ReadItems finished");
                            _ReadSubItems.Start();
                        });
                        _ReadItems.Start();
                    }
                }
                await _ReadItems;
            }
            if (_Cache[path].IsDirectory && await GetItemsAsync(_Cache[path]))
            {
                await _RepositoryService.SetMediaItems(_Cache.Values.ToList());
            }
            if (await CalculateApproximateDurationAsync(_Cache[path]))
            {
                await _RepositoryService.SetMediaItems(_Cache.Values.ToList());
            }

            return _Cache[path];
        }
        //If MediaItem is Mp3File -> Calculate Length, If Is Directory Create Sum
        private async Task<bool> CalculateApproximateDurationAsync(MediaItem mi)
        {
            System.Diagnostics.Debug.WriteLine(mi.Path);
            bool changed = false;
            if (mi.ApproximateDuration == null && mi.IsFile)
            {
                mi.ApproximateDuration = Path.GetExtension(mi.Path) == ".mp3" ? await Mp3Helper.Mp3FileLengthAsync(Path.Combine(_Settings.LibraryRoot, mi.Path)) : 0;
                changed = true;
                mi.Parent?.UpdateSums();
            }
            else if (mi.IsDirectory)
                changed = mi.UpdateSums();
            return changed;
        }
        private async Task<bool> GetItemsAsync(MediaItem parent)
        {
            bool haschanged = false;
            await Task.Delay(0);
            var fullpath = Path.Combine(_Settings.LibraryRoot, parent.Path);
            var files = Directory.GetFiles(fullpath)
                .Where(_ => _Settings.MediaFileExtensions.Contains(Path.GetExtension(_)))
                .Select(filepath =>
                {
                    var fileinfo = new FileInfo(filepath);
                    var name = fileinfo.Name;
                    var subpath = Path.Combine(parent.Path, name);

                    MediaItem? cached = null;
                    if (_Cache.ContainsKey(subpath))
                    {
                        cached = _Cache[subpath];
                        if (cached.LastWriteTime == fileinfo.LastWriteTime && cached.TotalSize == fileinfo.Length)
                            return cached;
                    }
                    haschanged = true;
                    var mi = new MediaItem(cached?.Id)
                    {
                        Parent = parent,
                        IsFile = true,
                        Path = subpath,
                        Name = name,
                        TotalSize = fileinfo.Length,
                        LastWriteTime = fileinfo.LastWriteTime,
                    };
                    _Cache[subpath] = mi;
                    return mi;
                })
                .OrderBy(p => p.Name)
                .ToArray();

            var directory = Directory.GetDirectories(fullpath)
                .Select(subfullpath =>
                {
                    var dirinfo = new DirectoryInfo(subfullpath);
                    var name = dirinfo.Name;
                    var subpath = Path.Combine(parent.Path, name);
                    MediaItem? cached = null;
                    if (_Cache.ContainsKey(subpath))
                    {
                        cached = _Cache[subpath];
                        if (cached.LastWriteTime == dirinfo.LastWriteTime)
                            return cached;
                    }
                    haschanged = true;
                    var mi = new MediaItem(cached?.Id)
                    {
                        Parent = parent,
                        Path = subpath,
                        Name = name,
                        LastWriteTime = dirinfo.LastWriteTime,
                    };
                    _Cache[subpath] = mi;
                    return mi;
                })
                .OrderBy(p => p.Name)
                .ToArray();

            if (haschanged || parent.Childs == null)
            {
                parent.Childs = directory.Concat(files).ToArray();
                var c = parent.UpdateSums();
                haschanged = haschanged || c;
            }
            return haschanged;
        }
        public async Task<Cover> GetCoverAsync(string path)
        {
            if (path != "folder")
            {
                while (true)
                {
                    var coverPath = await TryGetCoverPathAsync(path);

                    if (coverPath != null)
                    {
                        return new Cover
                        {
                            Data = File.OpenRead(coverPath),
                            MimeType = "image/octet-stream"
                        };
                    }

                    // switch to parent
                    path = path.GetParentPath();

                    if (string.IsNullOrEmpty(path))
                    {
                        break;
                    }
                }
            }

            // default folder image
            return new Cover
            {
                Data = typeof(MediaService).GetTypeInfo().Assembly.GetManifestResourceStream("TonieCreativeManager.Service.folder.png"),
                MimeType = "image/png"
            };
        }
        private Task<string?> TryGetCoverPathAsync(string path)
        {
            var fullPath = _Settings.LibraryRoot + path;
            var files = Directory.GetFiles(fullPath);

            // specific cover files
            var coverFile = files.FirstOrDefault(p => _Settings.FolderCoverFiles.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase));

            if (coverFile != null)
            {
                return Task.FromResult((string?)coverFile);
            }

            // any image files
            var imageFile = files.FirstOrDefault(p => _Settings.ImageExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));

            if (imageFile != null)
            {
                return Task.FromResult((string?)imageFile);
            }

            return Task.FromResult((string?)null);
        }

        public async Task UpdateDuration(string path, double duration)
        {
            if (_Cache.ContainsKey(path))
            {
                var item = _Cache[path];
                item.Duration = duration;
                item.Parent?.UpdateSums();
                await _RepositoryService.SetMediaItems(_Cache.Values.ToList());
            }
        }
        public async Task<IEnumerable<MediaItem>> GetAllowableMediaItemAsync(string path)
        {
            var mi = await GetMediaItemAsync(path);
            return await GetAllowableMediaItemAsync(mi);
        }
        private async Task<IEnumerable<MediaItem>> GetAllowableMediaItemAsync(MediaItem mi)
        {
            if (mi.IsUploadable) return new[] { mi };
            if (mi.TotalSize == 0) return new MediaItem[] { };
            await Task.Delay(0);
            var data = mi.Childs.Where(_ => _.IsDirectory).SelectMany(_ => GetAllowableMediaItemAsync(_).Result).ToArray();
            return data.Append(mi).ToArray();
        }
        public async Task<IEnumerable<MediaItem>> GetParentItemsAsync(string path)
        {
            if (path == "") return new MediaItem[] { };
            var ret = new List<MediaItem>();
            var mi = await GetMediaItemAsync(Path.GetDirectoryName(path));
            while (mi != null && mi.Path != "")
            {
                ret.Add(mi);
                mi = await GetMediaItemAsync(Path.GetDirectoryName(mi.Path));
            }
            return ret;
        }
    }
}
