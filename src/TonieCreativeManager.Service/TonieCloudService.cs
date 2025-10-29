using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using TonieCloud;
using System;
using System.Dynamic;

namespace TonieCreativeManager.Service
{
    public class TonieCloudService
    {
        private readonly TonieCloudClient _Client;

        public TonieCloudService(TonieCloudClient client)
        {
            _Client = client;
        }

        public void ClearCache()
        {
            _CreativeTonies = new Dictionary<string, IEnumerable<CreativeTonie>?>();
            _Households = null;
        }

        private Dictionary<string, IEnumerable<CreativeTonie>?> _CreativeTonies = new Dictionary<string, IEnumerable<CreativeTonie>?>();
        private DateTime Refresh { get; set; }
        private IEnumerable<Household>? _Households;

        public async Task<IEnumerable<Household>?> GetHouseholds() => _Households ??= await _Client.GetHouseholds();

        public async Task<IEnumerable<CreativeTonie>?> GetCreativeTonies(string householdId)
        {
            if (DateTime.Now >= Refresh || !_CreativeTonies.TryGetValue(householdId, out var creativeTonies))
            {
                Refresh = DateTime.Now.AddMinutes(1);
                _CreativeTonies[householdId] = creativeTonies = await _Client.GetCreativeTonies(householdId);
            }
            return creativeTonies;
        }

        public async Task<Dictionary<string, string>> GetTonieHouseholds()
        {
            var result = new Dictionary<string, string>();
            foreach(var hh in await GetHouseholds() ?? new Household[] { })
            {
                foreach(var t in await GetCreativeTonies(hh.Id) ?? new CreativeTonie[] { })
                {
                    result[t.Id] = hh.Id;
                }
            }
            return result;
        }
    }
}
