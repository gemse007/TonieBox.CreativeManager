using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TonieCreativeManager.Service.Model;

namespace TonieCreativeManager.Service
{
    public class RepositoryService
    {
        private readonly Settings settings;

        private PersistentData? data;
        private DateTime dataTimestamp;
        private CancellationTokenSource? _PersistCancellation;

        public RepositoryService(Settings settings)
        {
            this.settings = settings;
        }

        public async Task<PersistentData.GeneralSetting?> GetGeneralSettings() => (await GetData())?.GeneralSettings;
        public Task<IEnumerable<PersistentData.TonieMapping>?> GetMappings() => GetData(d => d?.TonieMappings);
        public Task<IEnumerable<MediaItem>?> GetMediaItems() => GetData(d => d?.MediaItems);

        public Task<IEnumerable<PersistentData.User>?> GetUsers() => GetData(d => d?.Users);

        public Task<IEnumerable<PersistentData.Voucher>?> GetVouchers() => GetData(d => d?.Vouchers);

        public async Task<PersistentData.GeneralSetting> SetGeneralSettings(PersistentData.GeneralSetting generalSetting) =>
            (await SetValue(generalSetting)).GeneralSettings;
        public async Task SetMediaItems(IList<MediaItem> mediaItems) => await SetValue<PersistentData>(null, null, v => v.MediaItems = mediaItems);
        public Task<PersistentData.TonieMapping> SetMapping(PersistentData.TonieMapping tonieMapping) =>
            SetValue(
                data => data.TonieMappings,
                mapping => mapping.TonieId == tonieMapping.TonieId,
                mapping =>
                {
                    mapping.TonieId = tonieMapping.TonieId;
                    mapping.Path = tonieMapping.Path;
                }
            );

        public Task<PersistentData.Voucher> SetVoucher(PersistentData.Voucher voucher) =>
            SetValue(
                data => data.Vouchers,
                v => v.Id == voucher.Id,
                v =>
                {
                    v.Id = voucher.Id;
                    v.Code = voucher.Code;
                    v.Used = voucher.Used;
                    v.Value = voucher.Value;
                }
            );
        public Task DeleteVoucher(PersistentData.Voucher voucher) => RemoveValue(data => data.Vouchers, v => v.Id == voucher.Id);
        public Task DeleteUser(int userId) => RemoveValue(data => data.Users, v => v.Id == userId);
        public Task<PersistentData.User> SetUser(PersistentData.User user) =>
            SetValue(
                data => data.Users,
                v => v.Id == user.Id,
                v => { user.Clone(v); }
            );

        private async Task<PersistentData> GetData()
        {
            var dataFileInfo = new FileInfo(settings.RepositoryDataFile);
            bool invalid = true;
            if (data == null || dataFileInfo.LastWriteTime != dataTimestamp)
            {
                if (dataFileInfo.Exists)
                {
                    try
                    {
                        await Task.Delay(0);
                        var json = File.ReadAllText(settings.RepositoryDataFile);
                        data = JsonConvert.DeserializeObject<PersistentData>(json);
                        dataTimestamp = dataFileInfo.LastWriteTime;
                        invalid = false;
                    }
                    catch { }
                }
                if (invalid && data == null)
                {
                    data = new PersistentData();
                    dataTimestamp = DateTime.Now;
                }
                OnRepositoryChanged(null);
            }

            return data;
        }

        private async Task<IEnumerable<T>?> GetData<T>(Func<PersistentData, IEnumerable<T>?> select) => select.Invoke(await GetData());

        private async Task<PersistentData> SetValue(PersistentData.GeneralSetting data) => await SetValue<PersistentData>(null, null, v => v.GeneralSettings = data);
        private async Task<T> SetValue<T>(Func<PersistentData, IList<T>>? set, Func<T, bool>? select, Action<T> update) where T : class
        {
            var data = await GetData();
            T? value = null;
            if (set != null && data != null)
            {
                var list = set.Invoke(data);
                value = list.FirstOrDefault(select);

                if (value == null)
                {
                    value = Activator.CreateInstance<T>();

                    list.Add(value);
                }

                update.Invoke(value);
            }
            else
            {
                value = data as T;
                if (value == null) value = Activator.CreateInstance<T>();
                update.Invoke(value);
            }

            _PersistCancellation?.Cancel();
            _PersistCancellation = new CancellationTokenSource();
            _ = Task.Factory.StartNew(() => PersistData(_PersistCancellation.Token));
            OnRepositoryChanged(typeof(T).Name);

            return value;
        }
        private async Task RemoveValue<T>(Func<PersistentData, IList<T>>? set, Func<T, bool>? select) where T : class
        {
            var data = await GetData();
            T? value = null;
            if (set != null && data != null)
            {
                var list = set.Invoke(data);
                value = list.FirstOrDefault(select);

                if (value != null)
                {
                    list.Remove(value);

                    _PersistCancellation?.Cancel();
                    _PersistCancellation = new CancellationTokenSource();
                    _ = Task.Factory.StartNew(() => PersistData(_PersistCancellation.Token));
                    OnRepositoryChanged(typeof(T).Name);
                }
            }
        }

        private async Task PersistData(CancellationToken token)
        {
            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(100);
                if (token.IsCancellationRequested) return;
            }
            var json = JsonConvert.SerializeObject(await GetData(), Formatting.Indented);

            await File.WriteAllTextAsync(settings.RepositoryDataFile, json);
            dataTimestamp = File.GetLastWriteTime(settings.RepositoryDataFile);
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm:ss} Persisted");
        }

        public EventHandler<string?> RepositoryChanged;

        protected void OnRepositoryChanged(string? name)
        {
            var handler = RepositoryChanged;
            if (handler != null)
                RepositoryChanged(this, name);
        }
    }
}
