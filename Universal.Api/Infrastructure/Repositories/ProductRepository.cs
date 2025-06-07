using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Contracts.V1;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {
        public Task<Branch> BranchSelectThisAsync(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
                throw new ArgumentNullException(nameof(appId));

            return _db.Branches.FirstOrDefaultAsync(x => x.StateID == appId);
        }

        public Task<SubRisk> ProductSelectThisAsync(string productId)
        {
          

            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentNullException(nameof(productId));

            return _db.SubRisks.FirstOrDefaultAsync(x => x.SubRiskID == productId);
        }

        public Task<List<SubRisk>> ProductSelectAsync(FilterPaging filter)
        {
            if (filter == null)
                filter = new FilterPaging();

            var query = _db.SubRisks.AsQueryable();

            foreach (string item in filter.SearchTextItems)
                query = query.Where(x => x.SubRiskName.Contains(item)).AsQueryable();

            return query.OrderBy(x => x.SubRiskName)
                        //.Skip(filter.SkipCount)
                        .Take(filter.PageSize)
                        .ToListAsync();
        }

        public async Task<bool> UpdateA6Async(string subRiskId, SubRiskA6UpdateDto dto)
        {
            var subRisk = await _db.SubRisks.FindAsync(subRiskId);
            if (subRisk == null) return false;

            subRisk.A6 = dto.Type;
            subRisk.ModifiedBy = dto.ModifiedBy;
            subRisk.ModifiedOn = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }
    }
}
