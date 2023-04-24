using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TonieCreativeManager.Service.Model;

namespace TonieCreativeManager.Service
{
    public class VoucherService
    {
        private readonly RepositoryService repositoryService;

        public VoucherService(RepositoryService repositoryService)
        {
            this.repositoryService = repositoryService;
        }

        public async Task<bool> IsValid(string code)
        {
            var vouchers = await repositoryService.GetVouchers();

            return vouchers.Any(v => v.Code == code && v.Used == null);
        }

        public async Task<PersistentData.Voucher> Redeem(Guid id)
        {
            var vouchers = await repositoryService.GetVouchers();
            var voucher = vouchers.FirstOrDefault(v => v.Id == id && v.Used == null);

            if (voucher == null)
            {
                throw new Exception($"Unable to redeem voucher '{id}'");
            }

            voucher.Used = DateTime.Now;

            await repositoryService.SetVoucher(voucher);

            return voucher;
        }

        public Task<IEnumerable<PersistentData.Voucher>?> GetVouchers() => repositoryService.GetVouchers();
        
        public async Task ResetVouchers()
        {
            var vouchers = await repositoryService.GetVouchers();
            if (vouchers == null) return;
            foreach (var voucher in vouchers)
            {
                voucher.Used = null;

                await repositoryService.SetVoucher(voucher);
            }
        }
    }
}
