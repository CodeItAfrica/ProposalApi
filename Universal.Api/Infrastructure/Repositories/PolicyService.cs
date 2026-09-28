
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Infrastructure.Common;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Models.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories;


public class PolicyService 
{
    private readonly DataContext _db;
    private readonly AutoNumberService _autoNumber;
    private readonly LookupService _lookup;
    private readonly GibsSettings _settings;

    public PolicyService(DataContext db, AutoNumberService autoNumber, LookupService lookup,
        IOptions<GibsSettings> settings)
    {
        _db = db;
        _autoNumber = autoNumber;
        _lookup = lookup;
        _settings = settings.Value;
    }

    public async Task<PolicyResponseDto> CreateAsync(PolicyCreateDto dto, string user, CancellationToken ct = default)
    {
        await ValidateAsync(dto, ct);

        var subRisk = await _lookup.GetSubRiskAsync(dto.SubRiskId, ct)
                      ?? throw new BusinessRuleException("Please select the sub class of business");
        var insured = await _lookup.GetInsuredAsync(dto.InsuredId, ct)
                      ?? throw new BusinessRuleException("Pls check your insured code?");
        var broker = string.IsNullOrWhiteSpace(dto.AgentBrokerId)
            ? null
            : await _lookup.GetAgentAsync(dto.AgentBrokerId!, ct)
              ?? throw new BusinessRuleException("Pls check your Agent/Broker Codes?");

        var isProposal = string.Equals(dto.TransactionType, TransactionType.Proposal, StringComparison.OrdinalIgnoreCase);

        // ---------- AUTONUMBER ----------
        var seq = await _autoNumber.GetNextAsync(AutoNumberType.Policy, dto.BranchId, dto.SubRiskId, ct);
        var policyNo = isProposal ? $"PR/{seq}" : seq;

        if (await _db.Policies.AnyAsync(p => p.PolicyNo == policyNo, ct))
            throw new BusinessRuleException("PolicyNo already Exist in the System?");

        var rec = new Policy { PolicyNo = policyNo };
        // Legacy quirk preserved: proposal -> isProposal 0, policy -> isProposal 1.
        rec.isProposal = (byte)(isProposal ? 0 : 1);

        MapCommonFields(rec, dto, subRisk, insured, broker);

        rec.SourceType = dto.TransactionType;
        rec.PremiumRate = 0;
        rec.SumInsuredFrgn = 0;
        rec.GrossPremiumFrgn = 0;
        rec.ProRataDays = 365;
        rec.ProRataPremium = 0;
        rec.Remarks = string.Empty;
        rec.TransSTATUS = "PENDING";
        rec.Deleted = 0;
        rec.Active = 1;
        rec.SubmittedBy = user;
        rec.SubmittedOn = DateTime.Now;

        _db.Policies.Add(rec);
        await _db.SaveChangesAsync(ct);

        return ToResponse(rec);
    }

    public async Task<PolicyResponseDto> UpdateAsync(string policyNo, PolicyUpdateDto dto, string user,
        CancellationToken ct = default)
    {
        var rec = await _db.Policies.FirstOrDefaultAsync(p => p.PolicyNo == policyNo, ct)
                  ?? throw new NotFoundException($"Policy '{policyNo}' not found.");

        await ValidateAsync(dto, ct);

        var subRisk = await _lookup.GetSubRiskAsync(dto.SubRiskId, ct)
                      ?? throw new BusinessRuleException("Please select the sub class of business");
        var insured = await _lookup.GetInsuredAsync(dto.InsuredId, ct)
                      ?? throw new BusinessRuleException("Pls check your insured code?");
        var broker = string.IsNullOrWhiteSpace(dto.AgentBrokerId)
            ? null
            : await _lookup.GetAgentAsync(dto.AgentBrokerId!, ct)
              ?? throw new BusinessRuleException("Pls check your Agent/Broker Codes?");

        MapCommonFields(rec, dto, subRisk, insured, broker);
        rec.ModifiedBy = user;
        rec.ModifiedOn = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        return ToResponse(rec);
    }

    public async Task<PolicyResponseDto?> GetAsync(string policyNo, CancellationToken ct = default)
    {
        var rec = await _db.Policies.AsNoTracking().FirstOrDefaultAsync(p => p.PolicyNo == policyNo, ct);
        if (rec is null) return null;

        var dto = ToResponse(rec);

        // Look up the dropdown text for the stored ids (fill only when not already stored).
        if (string.IsNullOrWhiteSpace(dto.AccountExecName))
            dto.AccountExecName = await _lookup.ResolveAccountExecNameAsync(dto.AccountExecId, ct);

        if (string.IsNullOrWhiteSpace(dto.BusChannelName))
            dto.BusChannelName = await _lookup.ResolveChannelNameAsync(dto.BusChannelId, ct);

        return dto;
    }

    public async Task<PagedResultDto<PolicyListItemDto>> ListAsync(PolicyQueryDto query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 500 ? 50 : query.PageSize;

        var q = _db.Policies.AsNoTracking()
            .Where(p => p.Deleted == null || p.Deleted == 0);

        if (!string.IsNullOrWhiteSpace(query.SubRiskId))
            q = q.Where(p => p.SubRiskID == query.SubRiskId);

        if (!string.IsNullOrWhiteSpace(query.BranchId))
            q = q.Where(p => p.BranchID == query.BranchId);

        if (!string.IsNullOrWhiteSpace(query.InsuredId))
            q = q.Where(p => p.InsuredID == query.InsuredId);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(p => p.TransSTATUS == query.Status);

        if (query.IsProposal.HasValue)
        {
            // isProposal is 0 for proposals, 1 for policies (legacy convention).
            byte flag = (byte)(query.IsProposal.Value ? 0 : 1);
            q = q.Where(p => p.isProposal == flag);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(p => (p.PolicyNo != null && p.PolicyNo.Contains(term))
                             || (p.InsFullname != null && p.InsFullname.Contains(term)));
        }

        if (query.FromDate.HasValue)
            q = q.Where(p => p.StartDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(p => p.StartDate <= query.ToDate.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(p => p.TransDate)
            .ThenByDescending(p => p.PolicyNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PolicyListItemDto
            {
                PolicyNo = p.PolicyNo!,
                IsProposal = p.isProposal == 0,
                SubRiskId = p.SubRiskID,
                SubRiskName = p.SubRiskName,
                BranchId = p.BranchID,
                InsuredId = p.InsuredID,
                InsuredName = p.InsFullname,
                EntryDate = p.TransDate,
                StartDate = p.StartDate,
                ExpiryDate = p.EndDate,
                SumInsured = p.SumInsured,
                GrossPremium = p.GrossPremium,
                Status = p.TransSTATUS
            })
            .ToListAsync(ct);

        return new PagedResultDto<PolicyListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    // ------------------------------------------------------------------
    // Shared field mapping (create + update).
    // ------------------------------------------------------------------
    private static void MapCommonFields(Policy rec, PolicyCreateDto dto, SubRisk subRisk,
        InsuredClients insured, Agents? broker)
    {
        rec.TransDate = dto.EntryDate;
        rec.BranchID = dto.BranchId;
        rec.BizSource = dto.BusinessSource;
        rec.SubRiskID = subRisk.SubRiskID;
        rec.SubRiskName = subRisk.SubRiskName;

        if (broker is not null)
        {
            rec.PartyID = broker.AgentID;
            rec.Party = broker.Agent;
        }

        rec.InsuredID = insured.InsuredID;
        rec.InsSurname = insured.SurName;
        rec.InsOthernames = insured.OtherNames;
        rec.InsAddress = insured.Address;
        rec.InsStateID = string.Empty;
        rec.InsMobilePhone = insured.MobilePhone;
        rec.InsLandPhone = insured.Landphone;
        rec.InsEmail = insured.Email;
        rec.InsOccupation = insured.Occupation;

        rec.StartDate = dto.StartDate;
        rec.EndDate = dto.ExpiryDate;
        rec.ExCurrency = dto.CurrencyName;
        rec.ExRate = dto.ExchangeRate;

        rec.ProportionRate = (double)dto.ProportionPercent;
        rec.SumInsured = 0;         // group-life open-cover fields are not used
        rec.GrossPremium = 0;

        rec.MktGroupID = dto.SubChannelId;
        // MktGroup (sub-channel text) would come from a lookup; left null unless supplied.

        // Account executive now has its own column instead of colliding with the channel.
        rec.MktStaffID = dto.AccountExecutiveId;

        // Marine-style Field1..Field6 are blank for group life.
        rec.Field1 = string.Empty;  // cert type
        rec.Field2 = string.Empty;  // ship mode
        rec.Field4 = string.Empty;  // excess
        rec.Field5 = string.Empty;  // cover type
        rec.Field6 = string.Empty;  // subject matter

        // Business channel id (text resolved on read via the channel lookup).
        rec.Field7 = dto.BusChannelId;

        rec.A1 = 0;
        rec.A2 = 0;
        rec.A3 = 0;
        rec.A4 = 0;
        rec.A5 = 0;
        rec.A10 = dto.PremiumRatePercent / 100m; // stored as a fraction

        if (long.TryParse(dto.AccountTypeId, out var trackId))
            rec.TrackID = trackId;
    }

    // ------------------------------------------------------------------
    // Validation — union of the btnSave / btnSaveGotoDetails / DoInsert guards.
    // ------------------------------------------------------------------
    private async Task ValidateAsync(PolicyCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.SubRiskId))
            throw new BusinessRuleException("Please select the sub class of business");

        if (string.IsNullOrWhiteSpace(dto.BusinessSource))
            throw new BusinessRuleException("Please select the appropriate source of the business");

        if (dto.StartDate > dto.ExpiryDate)
            throw new BusinessRuleException("Start Date Can not be greater than the End Date. Pls check the dates?");

        if (_settings.CurrentAccountPeriod is { } period && period > dto.EntryDate)
            throw new BusinessRuleException("Account period has been closed pls.check the date of this transaction?");

        if (string.IsNullOrWhiteSpace(dto.BranchId))
            throw new BusinessRuleException("Branch Name can not empty?");

        if (string.IsNullOrWhiteSpace(dto.AccountTypeId) || dto.AccountTypeId == "NULL")
            throw new BusinessRuleException("Account types can not be empty?");

        if (dto.ProportionPercent == 0)
            throw new BusinessRuleException("Pls supply the business proportion?");

        var isHundredPercentAccount = dto.AccountTypeId == "100";

        if (!isHundredPercentAccount && dto.ProportionPercent == 100)
            throw new BusinessRuleException("Share account Percentage can not be 100?");

        if (dto.BusinessSource == BusinessSource.Direct && dto.ProportionPercent != 100)
            throw new BusinessRuleException("Direct business Percentage can not be less than 100?");

        if (dto.BusinessSource == BusinessSource.CoinsuranceLead && dto.ProportionPercent == 100)
            throw new BusinessRuleException("Direct with coinsurance Percentage can not be 100?");

        if (dto.BusinessSource == BusinessSource.Accepted && dto.ProportionPercent == 100)
            throw new BusinessRuleException("Share account Percentage can not be 100?");

        await Task.CompletedTask;
    }

    private PolicyResponseDto ToResponse(Policy rec) => new()
    {
        PolicyNo = rec.PolicyNo,
        IsProposal = rec.isProposal == 0,
        SubRiskId = rec.SubRiskID,
        SubRiskName = rec.SubRiskName,
        BranchId = rec.BranchID,
        BusinessSource = rec.BizSource,
        InsuredId = rec.InsuredID,
        InsuredName = rec.InsFullname,
        AgentBrokerId = rec.PartyID,
        EntryDate = rec.TransDate,
        StartDate = rec.StartDate,
        ExpiryDate = rec.EndDate,
        ProportionPercent = rec.ProportionRate.HasValue ? (decimal)rec.ProportionRate.Value : (decimal?)null,
        PremiumRateFraction = rec.A10,
        CurrencyName = rec.ExCurrency,
        ExchangeRate = rec.ExRate,
        Status = rec.TransSTATUS,

        // Dropdown ids + text resolvable without a DB round-trip.
        AccountExecId = rec.MktStaffID,
        AccountExecName = rec.MktStaff,                                   // stored text (GET refills if null)
        BusinessSourceText = _lookup.ResolveBusinessSourceText(rec.BizSource),
        AccountTypeId = rec.TrackID?.ToString(),
        AccountTypeText = _lookup.ResolveAccountTypeText(rec.TrackID?.ToString()),
        BusChannelId = rec.Field7,
        BusChannelName = rec.Field8,                                     // stored text (GET refills if null)
        SubChannelId = rec.MktGroupID,
        SubChannelName = rec.MktGroup
    };
}