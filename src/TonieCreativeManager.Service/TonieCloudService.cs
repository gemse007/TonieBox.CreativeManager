using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using TonieCloud;
using System;

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
            _CreativeTonies = null;
            _Households = null;
        }

        private IEnumerable<CreativeTonie>? _CreativeTonies;
        private IEnumerable<Household>? _Households;

        public async Task<Household> GetHousehold() => (await GetHouseholds()).FirstOrDefault() ?? throw new Exception("No household found");

        public async Task<IEnumerable<Household>?> GetHouseholds() => _Households ??= await _Client.GetHouseholds();

        public async Task<IEnumerable<CreativeTonie>?> GetCreativeTonies() => _CreativeTonies ??= await _Client.GetCreativeTonies((await GetHousehold()).Id ?? "");
    }
}
