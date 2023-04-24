using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TonieCloud;
using TonieCreativeManager.Service.Model;

namespace TonieCreativeManager.Service
{
    public class UserService
    {
        private readonly TonieCloudClient _TonieCloudClient;
        private readonly RepositoryService _RepositoryService;
        private readonly VoucherService _VoucherService;
        private readonly Settings _Settings;
        private readonly MediaService _MediaService;
        private readonly UploadService _UploadService;
        private Tonie[]? _Tonies;
        private DateTime _ToniesCacheUntil;
        public UserService(TonieCloudClient tonieCloudClient, RepositoryService repositoryService, VoucherService voucherService, Settings settings, MediaService mediaService, UploadService uploadService)
        {
            _TonieCloudClient = tonieCloudClient;
            _RepositoryService = repositoryService;
            _VoucherService = voucherService;
            _Settings = settings;
            _MediaService = mediaService;
            _UploadService = uploadService;
            _RepositoryService.RepositoryChanged += (s, e) => { if (e == null || e == "User") _ToniesCacheUntil = DateTime.MinValue; };
        }

        public async Task<IEnumerable<PersistentData.User>?> GetUsers() => await _RepositoryService.GetUsers();

        public async Task<PersistentData.User> GetUser(int id) => (await GetUsers()).FirstOrDefault(u => u.Id == id);

        public async Task<bool> CanBuyItem(int userId)
        {
            var user = await GetUser(userId);

            return user.Credits >= user.MediaCost;
        }

        public async Task<bool> CanUploadItem(int userId, int toniesNeeded)
        {
            var user = await GetUser(userId);

            return user.Credits >= user.UploadCost * toniesNeeded;
        }

        public async Task<PersistentData.Voucher> RedeemVoucher(Guid code, int userId)
        {
            var user = await GetUser(userId);

            // redeem voucher
            var voucher = await _VoucherService.Redeem(code);

            // credit user
            user.Credits += voucher.Value;

            // save user
            await _RepositoryService.SetUser(user);

            return voucher;
        }

        public async Task BuyItem(int userId, string path)
        {
            var user = await GetUser(userId);
            var mediaItem = await _MediaService.GetMediaItemAsync(path);

            // check credit
            if (user.Credits < user.MediaCost)
                throw new Exception("Insufficient credits");

            // subtract credit
            user.Credits -= user.MediaCost;

            // save user
            user.BoughtMedia.Add(mediaItem.Id);
            user.History.Add(new PersistentData.History($"MediaItem {mediaItem.Id} {mediaItem.Path} bought"));
            await _RepositoryService.SetUser(user);
        }

        public async Task UploadItem(int userId, string path, string[] creativeTonieId, bool append)
        {
            var user = await GetUser(userId);
            var mediaItem = await _MediaService.GetMediaItemAsync(path);

            // check credit
            if (user.Credits < user.UploadCost * mediaItem.ToniesNeeded)
                throw new Exception("Insufficient credits");
            // check TonieCounts
            if (creativeTonieId?.Length < mediaItem.ToniesNeeded)
                throw new Exception("Insufficient Tonies");

            // upload
            _UploadService.AddJob(new UploadJob(userId, creativeTonieId, mediaItem.Path, mediaItem.Name, mediaItem.Childs.ToArray(), append));
            //_UploadService.AddJob(new UploadJob("", creativeTonieId, "", (path, creativeTonieId).Start();

            // subtract credit
            user.Credits -= user.UploadCost * mediaItem.ToniesNeeded;

            // save user
            await _RepositoryService.SetUser(user);
        }
        private async Task<IEnumerable<Tonie>?> GetTonies()
        {
            if (_ToniesCacheUntil > DateTime.UtcNow) return _Tonies;
            _ToniesCacheUntil = DateTime.UtcNow.AddHours(1);
            var hh = (await _TonieCloudClient.GetHouseholds())?.FirstOrDefault();
            if (hh != null)
            {
                var tonies = await _TonieCloudClient.GetCreativeTonies(hh.Id);
                var map = await _RepositoryService.GetMappings();
                _Tonies = tonies?.Select(ct => new Tonie(ct.Id)
                {
                    ImageUrl = ct.ImageUrl,
                    Name = ct.Name,
                    CurrentMediaPath = map?.FirstOrDefault(m => m.TonieId == ct.Id)?.Path
                }).ToArray();
            }
            else
                _Tonies = null;
            return _Tonies;
        }

        public async Task<IEnumerable<Tonie>> GetCreativeTonies(int? userId = null)
        {
            var user = userId == null ? null : await GetUser(userId.Value);
            return (await GetTonies())?.Where(t => user == null || user.Tonies.Contains(t.Id)).ToArray() ?? new Tonie[] { };
        }

        public async Task<IEnumerable<MediaItem>> GetUploadableItems(string path, int userId)
        {
            var user = await GetUser(userId);
            var mediaItem = await _MediaService.GetMediaItemAsync(path);
            return mediaItem
                .Childs
                .Where(item =>
                    item.TotalSize != 0
                    && user.AllowedMedia.Any(p => item.Path.IndexOf(p) == 0)
                    && user.BoughtMedia.Any(p => item.Path.IndexOf(p) == 0))
                .ToArray();
        }

        public async Task SetCredits(int userId, int credits)
        {
            var user = await GetUser(userId);

            user.Credits = credits;

            await _RepositoryService.SetUser(user);
        }
    }
}
