
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Models.Customer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{


    /// <summary>
    /// Wraps the legacy cls_Insured / cls_Parties / cls_SubRisks lookups.
    /// </summary>
    public class LookupService
    {
        private readonly DataContext _db;

        public LookupService(DataContext db) => _db = db;

        // Static option maps (mirror FillBizSource / FillBizShareType).
        private static readonly (string Value, string Text)[] BusinessSourceOptions =
        {
        ("DIRECT", "Direct Business"),
        ("ACCEPTED", "Co-Insurance (Following)"),
        ("CO-INSURANCE LEAD", "Co-Insurance (Leading)")
    };

        private static readonly (string Value, string Text)[] AccountTypeOptions =
        {
        ("100", "100% Accounting"),
        ("0", "Our Share Accounting")
    };

        // ---- Entity lookups ----
      

        // ---- Dropdown sources ----
        public async Task<IReadOnlyList<LookupItemDto>> GetAccountExecutivesAsync(CancellationToken ct = default)
            => await _db.MktStaffs.AsNoTracking()
                .OrderBy(s => s.StaffName)
                .Select(s => new LookupItemDto { Value = s.MktStaffID, Text = s.StaffName })
                .ToListAsync(ct);

        public async Task<IReadOnlyList<LookupItemDto>> GetBusinessChannelsAsync(CancellationToken ct = default)
            => await _db.MktChannels.AsNoTracking()
                .OrderBy(c => c.Channels)
                .Select(c => new LookupItemDto { Value = c.ChannelID, Text = c.Channels })
                .ToListAsync(ct);

        public async Task<IReadOnlyList<CurrencyOptionDto>> GetCurrenciesAsync(CancellationToken ct = default)
            => await _db.ExchangeRates.AsNoTracking()
                .Where(r => r.ExCurrency != null)
                .Select(r => new CurrencyOptionDto { Currency = r.ExCurrency!, Rate = r.Rates ?? 1 })
                .ToListAsync(ct);

        public IReadOnlyList<LookupItemDto> GetBusinessSources()
            => BusinessSourceOptions.Select(o => new LookupItemDto { Value = o.Value, Text = o.Text }).ToList();

        public IReadOnlyList<LookupItemDto> GetAccountTypes()
            => AccountTypeOptions.Select(o => new LookupItemDto { Value = o.Value, Text = o.Text }).ToList();

        // ---- Text resolution for GET ----
        public async Task<string?> ResolveAccountExecNameAsync(string? mktStaffId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(mktStaffId) || mktStaffId == "NULL") return null;
            return await _db.MktStaffs.AsNoTracking()
                .Where(s => s.MktStaffID == mktStaffId)
                .Select(s => s.StaffName)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<string?> ResolveChannelNameAsync(string? channelId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(channelId) || channelId == "NULL") return null;
            return await _db.MktChannels.AsNoTracking()
                .Where(c => c.ChannelID == channelId)
                .Select(c => c.Channels)
                .FirstOrDefaultAsync(ct);
        }

        public string? ResolveBusinessSourceText(string? source)
        {
            if (string.IsNullOrWhiteSpace(source)) return null;
            foreach (var o in BusinessSourceOptions)
                if (string.Equals(o.Value, source, StringComparison.OrdinalIgnoreCase)) return o.Text;
            return source;
        }

        public string? ResolveAccountTypeText(string? accountTypeId)
        {
            if (string.IsNullOrWhiteSpace(accountTypeId)) return null;
            foreach (var o in AccountTypeOptions)
                if (o.Value == accountTypeId) return o.Text;
            return accountTypeId;
        }
        public Task<InsuredClients?> GetInsuredAsync(string insuredId, CancellationToken ct = default)
            => _db.InsuredClients.FirstOrDefaultAsync(x => x.InsuredID == insuredId, ct);

        public Task<Agents?> GetAgentAsync(string agentId, CancellationToken ct = default)
            => _db.Agents.FirstOrDefaultAsync(x => x.AgentID == agentId, ct);

        public Task<SubRisk?> GetSubRiskAsync(string subRiskId, CancellationToken ct = default)
            => _db.SubRisks.FirstOrDefaultAsync(x => x.SubRiskID == subRiskId, ct);
    }
}