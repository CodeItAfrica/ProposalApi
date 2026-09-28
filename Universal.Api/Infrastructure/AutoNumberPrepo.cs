using System;
using System.Linq;
using System.Threading.Tasks;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GibsLifesMicroWebApi.Infrastructure
{
    public class AutoNumberPrepo
    {
        private readonly DataContext _db;

        public AutoNumberPrepo(DataContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Generates next policy number and increments the serial in the DB.
        /// </summary>
        public async Task<string> GeneratePolicyNumberAsync(
            string branchId,
            string subRiskId,
            string endorsementType = "",
            DateTime? date = null)
        {
            var today = date ?? DateTime.Today;

            // 1️⃣ Resolve AutoNumber row
            var auto = await ResolveAutoNumberAsync("POLICY", branchId, subRiskId);
            if (auto == null) throw new Exception("AutoNumber setup for POLICY not found.");

            // 2️⃣ Increment serial
            long nextNo = auto.NextValue ?? 1;
            auto.NextValue = nextNo + 1;

            await _db.SaveChangesAsync(); // ✅ Safe inside transaction

            // 3️⃣ Build formatted policy number
            return await BuildNumberAsync(auto.Format, nextNo, branchId, subRiskId, endorsementType, today);
        }

        private async Task<AutoNumber> ResolveAutoNumberAsync(string numType, string branchId, string riskId)
        {
            var query = _db.AutoNumbers.Where(x => x.NumType == numType);

            // 1️⃣ ALL branch
            var all = await query.FirstOrDefaultAsync(x => x.BranchID == "ALL");
            if (all != null) return all;

            // 2️⃣ Branch + Risk
            if (!string.IsNullOrEmpty(riskId))
            {
                var branchRisk = await query.FirstOrDefaultAsync(x => x.BranchID == branchId && x.RiskID == riskId);
                if (branchRisk != null) return branchRisk;
            }

            // 3️⃣ Branch only
            return await query.FirstOrDefaultAsync(x => x.BranchID == branchId);
        }

        private async Task<string> BuildNumberAsync(
            string format,
            long serial,
            string branchId,
            string subRiskId,
            string endorsementType,
            DateTime date)
        {
            if (string.IsNullOrEmpty(format))
                throw new Exception("Invalid AutoNumber format.");

            int zeroCount = format.IndexOf("0$") - format.IndexOf("$0");
            zeroCount = Math.Max(zeroCount, 3);
            string zeros = new string('0', zeroCount);

            format = format.Replace($"${zeros}$", serial.ToString(zeros))
                           .Replace("$SR$", subRiskId ?? "")
                           .Replace("$YR$", date.ToString("yy"))
                           .Replace("$MO$", date.ToString("MM"))
                           .Replace("$BR$", branchId ?? "")
                           .Replace("$P$", GetEndorsementCode(endorsementType));

            if (format.Contains("$RC$"))
            {
                var risk = await _db.SubRisks.FirstOrDefaultAsync(x => x.SubRiskID == subRiskId);
                format = format.Replace("$RC$", risk?.RiskID ?? "");
            }

            if (format.Contains("$BR2$"))
            {
                var branch = await _db.Branches.FirstOrDefaultAsync(x => x.BranchID == branchId);
                format = format.Replace("$BR2$", branch?.BranchID2 ?? "");
            }

            return format.ToUpper();
        }

        private string GetEndorsementCode(string type) => type?.ToUpper() switch
        {
            "DELETE" or "DELETED" or "REVERSAL" or "RETURN" => "C",
            "RENEW" or "RENEWAL" => "R",
            "ADDITIONAL" => "E",
            "NIL" or "REDO" => "N",
            _ => "A",
        };
    }
}