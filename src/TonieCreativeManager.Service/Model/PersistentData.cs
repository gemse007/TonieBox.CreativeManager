using System;
using System.Collections.Generic;
using System.Linq;
using static TonieCreativeManager.Service.Model.PersistentData;

namespace TonieCreativeManager.Service.Model
{
    public class PersistentData
    {
        public class GeneralSetting
        {
            public string? TonieUserID { get; set; }
            public string? ToniePassword { get; set; }
        }
        public class TonieMapping
        {
            public string? TonieId { get; set; }
            public string? Path { get; set; }
        }
        public class History
        {
            public History(string entry)
            {
                Timestamp = DateTime.Now;
                Entry = entry;
            }

            public DateTime Timestamp { get; set; }
            public string Entry { get; set; }
        }
        public class User
        {
            private bool _DefaultBought = true;
            private bool _DefaultAllowed = false;
            public void Clone(User cloneTo)
            {
                cloneTo.Id = Id;
                cloneTo.Name = Name;
                cloneTo.Credits = Credits;
                cloneTo.MediaCost = MediaCost;
                cloneTo.UploadCost = UploadCost;
                cloneTo.ProfileImageUrl = ProfileImageUrl;
                cloneTo.ShowText = ShowText;
                cloneTo.ShowHidden = ShowHidden;
                cloneTo.Tonies = Tonies.ToList();
                cloneTo._DefaultBought = _DefaultBought;
                cloneTo._DefaultAllowed = _DefaultAllowed;
                cloneTo.AllowedMedia = AllowedMedia.ToList();
                cloneTo.BoughtMedia = BoughtMedia.ToList();
                cloneTo.HiddenMedia = HiddenMedia.ToList();
                cloneTo.History = History.ToList();
            }
            public int Id { get; set; } = 0;
            public string? Name { get; set; }
            public int Credits { get; set; }
            public int MediaCost { get; set; }
            public int UploadCost { get; set; }
            public string? ProfileImageUrl { get; set; }
            public bool ShowText { get; set; }
            public bool ShowHidden { get; set; }
            public bool DefaultBought { get => _DefaultBought; set { _DefaultBought = value; if (_DefaultBought) DefaultAllowed = false; } }
            public bool DefaultAllowed { get => _DefaultAllowed; set { _DefaultAllowed = value; if (_DefaultAllowed) DefaultBought = false; } }
            public IList<string> Tonies { get; set; } = new List<string>();
            public IList<string> AllowedMedia { get; set; } = new List<string>();
            public IList<string> BoughtMedia { get; set; } = new List<string>();
            public IList<string> HiddenMedia { get; set; } = new List<string>();
            public IList<History> History { get; set; } = new List<History>();
        }

        public class Voucher
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public string? Code { get; set; }
            public int Value { get; set; }
            public DateTime? Used { get; set; }
        }

        public GeneralSetting GeneralSettings { get; set; } = new GeneralSetting();
        public IList<TonieMapping> TonieMappings { get; set; } = new List<TonieMapping>();
        public IList<User> Users { get; set; } = new List<User>();
        public IList<Voucher> Vouchers { get; set; } = new List<Voucher>();
        public IList<MediaItem> MediaItems { get; set; } = new List<MediaItem>();
    }
}
