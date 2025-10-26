using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using TonieCloud;
using TonieCreativeManager.Service.Model;
using static TonieCloud.UploadFilesToCreativeTonieRequest;
using static TonieCreativeManager.Service.Model.PersistentData;

namespace TonieCreativeManager.Service
{
    public class UploadService
    {
        private class UploadJobProgress
        {
            public UploadJobProgress(UploadJob job)
            {
                Job = job;
            }
            public UploadJob Job { get; set; }
            public long TotalBytes { get; set; }
            public long ByesDone { get; set; }
            public CreativeTonie[]? TonieInformation { get; set; }
        }
        private class TonieChapterDetails
        {
            public MediaItem? MediaItem { get; set; }
            public Task<AmazonToken?>? UploadTask { get; set; }
            public Chapter? Chapter { get; set; }
        }
        private readonly RepositoryService _RepositoryService;
        private readonly TonieCloudClient _TonieCloudClient;
        private readonly TonieCloudService _TonieCloudService;
        private readonly MediaService _MediaService;
        private readonly Settings _Settings;
        private List<UploadJobProgress> _Jobs = new List<UploadJobProgress>();
        private Task? _UploadTask;
        private List<string> _Errors = new List<string>();

        public UploadService(TonieCloudClient tonieCloudClient, MediaService mediaService, Settings settings, TonieCloudService tonieCloudService, RepositoryService repositoryService)
        {
            _TonieCloudClient = tonieCloudClient;
            _MediaService = mediaService;
            _Settings = settings;
            _TonieCloudService = tonieCloudService;
            _RepositoryService = repositoryService;
        }

        public double? GetProgress()
        {
            if (_Jobs.Count == 0) return null;
            return 1.0 * _Jobs.Sum(_ => _.ByesDone) / _Jobs.Sum(_ => _.TotalBytes);
        }
        public string[] GetErrors()
        {
            lock (_Errors)
            {
                var ret = _Errors.ToArray();
                _Errors.Clear();
                return ret;
            }
        }
        public void AddJob(UploadJob job)
        {
            lock (_Jobs)
            {
                _Jobs.Add(new UploadJobProgress(job)
                {
                    TotalBytes = job.Files.Sum(_ => _.TotalSize)
                });
                _UploadTask ??= UploadJobsAsync();
            }
        }

        private async Task UploadJobsAsync()
        {
            UploadJobProgress job;
            while (true)
            {
                lock (_Jobs)
                {
                    job = _Jobs.FirstOrDefault();
                    if (job == null)
                    {
                        _UploadTask = null;
                        break;
                    }
                }
                try
                {
                    job.TonieInformation = await GetTonieInformation(job.Job.TonieIds);
                    if (job.TonieInformation == null) throw new Exception("Error -> UploadFileAsync invalid Tonie!");
                    await UploadFilesAsync(job);
                }
                catch
                {
                    job.ByesDone = job.TotalBytes;
                }
                finally
                {
                    if (job.Job.Errors.Count > 0)
                        lock (_Errors)
                            _Errors.AddRange(job.Job.Errors);
                    lock (_Jobs)
                        _Jobs.Remove(job);
                }
            }
        }
        private async Task<CreativeTonie[]> GetTonieInformation(string[] tonieIds)
        {
            var alltonies = await _TonieCloudService.GetTonieHouseholds();

            var list = new List<CreativeTonie>();
            foreach (var t in tonieIds)
                list.Add(await _TonieCloudClient.GetCreativeTonie(alltonies[t], t));
            return list.ToArray();
        }
        private async Task PatchTonies(UploadJobProgress job, Dictionary<string,string> hh, TonieChapterDetails[] files)
        {
            int ix = 0;
            foreach (var t in job.TonieInformation)
            {
                double sr = t.SecondsRemaining + (job.Job.Append ? 0 : t.SecondsPresent);
                int chr = t.ChaptersRemaining + (job.Job.Append ? 0 : t.ChaptersPresent);
                var chap = job.Job.Append ? t.Chapters.ToList() : new List<Chapter>();
                while (ix < files.Length && chr > 0)
                {
                    var file = files[ix];
                    var duration = file.Chapter?.Seconds > 0 ? file.Chapter.Seconds.Value : (file.MediaItem.Duration ?? file.MediaItem.ApproximateDuration ?? 0);
                    if (sr < duration) break;
                    if (file.Chapter?.File != null)
                    {
                        chap.Add(file.Chapter);
                    }
                    sr -= duration;
                    chr--;
                    ix++;
                }
                await _TonieCloudClient.PatchCreativeTonie(hh[t.Id!], t.Id!, job.Job.Name, chap);
            }
            if (ix < files.Length)
                throw new Exception("Zuwenige Tonies spezifiziert!");
        }
        private async Task UploadFilesAsync(UploadJobProgress job)
        {
            var hh = await _TonieCloudService.GetTonieHouseholds();
            try
            {
                var files = job.Job.Files.Select(_ =>
                {
                    long lastprogress = 0;
                    var progress = new Progress<long>(progress =>
                    {
                        if (progress > lastprogress)
                        {
                            job.ByesDone += progress - lastprogress;
                            lastprogress = progress;
                        }
                    });
                    var fullpath = Path.Combine(_Settings.LibraryRoot, _.Path);

                    return new TonieChapterDetails
                    {
                        MediaItem = _,
                        UploadTask = Task.Run<AmazonToken?>(() =>
                        {
                            int retry = 0;
                            var stream = File.OpenRead(fullpath);
                            while (retry < 3)
                            {
                                try
                                {
                                    var r = _TonieCloudClient.UploadFile(stream, progress, fullpath);
                                    r.Wait();
                                    if (r.IsCompletedSuccessfully)
                                        return r.Result;
                                }
                                catch
                                {
                                    job.Job.Errors.Add($"Error uploading {fullpath} retry {retry}");
                                }
                                stream.Position = 0;
                                retry++;
                            }
                            return null;
                        })

                    };
                }).ToArray();
                CreativeTonie[]? tonieinfo = null;
                //wait till everything is over
                while (files.Any(_ => _.Chapter == null || (_.Chapter.Transcoding ?? false)))
                {
                    await Task.Delay(5000);
                    //is tony empty and upload complete or anything transcoding
                    if (files.Any(_ => (_.Chapter == null && (_.UploadTask?.IsCompletedSuccessfully ?? false)) || (_.Chapter != null && (_.Chapter.Transcoding ?? false))))
                    {
                        tonieinfo = await GetTonieInformation(job.Job.TonieIds);
                        if (!(tonieinfo?.Any(_ => _.Transcoding) ?? true))
                        {
                            tonieinfo?.SelectMany(t => t.Chapters)
                            .ToList()
                            .ForEach(ch =>
                            {
                                var entry = files.FirstOrDefault(_ => _.MediaItem.Id == ch.Title);
                                if (entry != null)
                                {
                                    if (ch.Seconds != entry.MediaItem.Duration && (ch.Seconds ?? 0) != 0)
                                        _MediaService.UpdateDuration(entry.MediaItem.Path, ch.Seconds.Value).Wait();
                                    entry.Chapter = new Chapter { File = ch.File, Id = ch.Id, Seconds = ch.Seconds, Title = entry.MediaItem.Name };
                                }
                            });
                            tonieinfo?.SelectMany(t => t.TranscodingErrors?.Where(_ => _.Reason == "tooLong").SelectMany(_ => _.DeletedChapters))
                                .ToList()
                                .ForEach(ch =>
                                {
                                    var entry = files.FirstOrDefault(_ => _.MediaItem.Id == ch.Title);
                                    if (entry != null)
                                    {
                                        if (ch.Seconds != entry.MediaItem.Duration && (ch.Seconds ?? 0) != 0)
                                            _MediaService.UpdateDuration(entry.MediaItem.Path, ch.Seconds.Value).Wait();
                                        entry.Chapter = new Chapter { Seconds = ch.Seconds };
                                    }
                                });
                            tonieinfo?.SelectMany(t => t.TranscodingErrors?.Where(_ => _.Reason != "tooLong").SelectMany(_ => _.DeletedChapters))
                                .ToList()
                                .ForEach(ch =>
                                {
                                    var entry = files.FirstOrDefault(_ => _.MediaItem.Id == ch.Title);
                                    if (entry != null)
                                    {
                                        job.Job.Errors.Add($"{entry.MediaItem.Name} failed");
                                        entry.Chapter = new Chapter { Title = ch.Title };
                                    }
                                });
                            files.Where(entry => entry.Chapter == null
                                    && (entry.UploadTask?.IsCompletedSuccessfully ?? false)
                                    && entry.UploadTask.Result != null)
                                .ToList()
                                .ForEach(entry =>
                                    entry.Chapter = new Chapter()
                                    {
                                        Id = entry.UploadTask.Result.FileId,
                                        File = entry.UploadTask.Result.FileId,
                                        Title = entry.MediaItem.Id,
                                        Seconds = (float)(entry.MediaItem.Duration ?? entry.MediaItem.ApproximateDuration ?? 0),
                                        Transcoding = true
                                    });
                            if (tonieinfo.Any(_ => _.TranscodingErrors?.Length > 0))
                                System.Diagnostics.Debug.WriteLine("TranscodingError");
                            await PatchTonies(job, hh, files);
                            tonieinfo = await GetTonieInformation(job.Job.TonieIds);
                        }
                    }
                }
                await PatchTonies(job, hh, files);
                tonieinfo = await GetTonieInformation(job.Job.TonieIds);
                while (tonieinfo?.Any(_ => _.Transcoding) ?? false)
                {
                    await Task.Delay(5000);
                    tonieinfo = await GetTonieInformation(job.Job.TonieIds);
                }

                var errors = tonieinfo?.SelectMany(_ => _.TranscodingErrors).ToArray() ?? new CreativeTonie.Transcodingerror[] { };
                if (errors.Length > 0)
                    throw new Exception($"Transcoding errors: {string.Join("\n", errors.Select(_ => $"Reason: {_.Reason}, Message: {_.Message}"))}");

                job.Job.TonieIds.ToList().ForEach(_ => _RepositoryService.SetMapping(new PersistentData.TonieMapping { TonieId = _, Path = job.Job.Path }).Wait());
                var user = (await _RepositoryService.GetUsers()).FirstOrDefault(_ => _.Id == job.Job.UserId);
                user.History.Add(new PersistentData.History($"Tonies {string.Join(",", job.Job.TonieIds)} uploaded with {job.Job.Path}"));
                await _RepositoryService.SetUser(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                foreach (var t in job.TonieInformation)
                    await _TonieCloudClient.PatchCreativeTonie(hh[t.Id], t.Id, t.Name, t.Chapters);
                var user = (await _RepositoryService.GetUsers()).FirstOrDefault(_ => _.Id == job.Job.UserId);
                user.Credits += user.UploadCost * job.TonieInformation.Length;
                await _RepositoryService.SetUser(user);
            }
        }
    }
}
