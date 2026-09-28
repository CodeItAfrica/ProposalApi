using GibsLifesMicroWebApi.Infrastructure;
using System;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services
{
    public class AutonumberService
    {
        private readonly AutoNumberPrepo _repo;

        public AutonumberService(AutoNumberPrepo repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public async Task<string> GeneratePolicyNumberAsync(
            string branchId,
            string subRiskId,
            string endorsementType = "",
            DateTime? optionalDate = null)
        {
            return await _repo.GeneratePolicyNumberAsync(branchId, subRiskId, endorsementType, optionalDate);
        }
    }
}