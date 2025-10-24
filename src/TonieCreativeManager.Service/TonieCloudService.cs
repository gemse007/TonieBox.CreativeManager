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
        private IEnumerable<Household>? _Households;

        public async Task<Household> GetHousehold() => (await GetHouseholds()).FirstOrDefault() ?? throw new Exception("No household found");

        public async Task<IEnumerable<Household>?> GetHouseholds() => _Households ??= await _Client.GetHouseholds();

        public async Task<IEnumerable<CreativeTonie>?> GetCreativeTonies(string householdId)
        {
            if (!_CreativeTonies.TryGetValue(householdId, out var creativeTonies))
                _CreativeTonies[householdId] = creativeTonies = await _Client.GetCreativeTonies(householdId);
            return creativeTonies;
        }
            
    }
}
