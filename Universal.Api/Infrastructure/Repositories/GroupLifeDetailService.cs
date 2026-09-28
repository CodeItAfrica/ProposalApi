using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Infrastructure.Common;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{


    public class GroupLifeDetailService
    {
        private readonly DataContext _db;
        private readonly AutoNumberService _autoNumber;
        private readonly GroupLifeMemberService _members; // add to constructor

        public GroupLifeDetailService(DataContext db, AutoNumberService autoNumber, GroupLifeMemberService members)
        {
            _db = db;
            _autoNumber = autoNumber;
            _members = members;
        }
        public async Task<PolicyResponseDto?> GetAsync(string policyNo, CancellationToken ct = default)
        {
            var rec = await _db.Policies.AsNoTracking().FirstOrDefaultAsync(p => p.PolicyNo == policyNo, ct);
            return rec is null ? null : ToResponse(rec);
        }
        private static PolicyResponseDto ToResponse(Policy rec) => new()
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
            Status = rec.TransSTATUS
        };
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
                // isProposal is 1 for proposals, 0 for policies (legacy convention).
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
        public async Task<GroupLifeDetailDto> EnsureEnvelopeAsync(string policyNo, string user, CancellationToken ct = default)
        {
            var policy = await GetPolicyAsync(policyNo, ct);

            // SelectThisPOlicyDetailsNew(...).BizOption == "" -> create.
            var existing = await _db.PolicyDetails
                .Where(d => d.PolicyNo == policyNo && (d.Deleted == null || d.Deleted == 0))
                .OrderByDescending(d => d.DetailID)
                .FirstOrDefaultAsync(ct);

            if (existing is not null && !string.IsNullOrEmpty(existing.BizOption))
                return ToDto(existing, created: false);

            var rec = await BuildAndSaveEnvelopeAsync(policy, user, "NEW", ct);
            return ToDto(rec, created: true);
        }

        public async Task<GroupLifeDetailDto> CreateEnvelopeAsync(string policyNo, string user, string? bizOption = null,
            CancellationToken ct = default)
        {
            var policy = await GetPolicyAsync(policyNo, ct);

            // Every section is an independent peer; creating another one neither relabels nor closes
            // the existing ones. Default to "NEW" like the first section unless the caller specifies
            // a different BizOption (e.g. an actual endorsement).
            var option = string.IsNullOrWhiteSpace(bizOption)
                ? "NEW"
                : bizOption.Trim().ToUpperInvariant();

            var rec = await BuildAndSaveEnvelopeAsync(policy, user, option, ct);
            return ToDto(rec, created: true);
        }
        public async Task<GroupLifeDetailDto> CreateEndorsementAsync(
      string policyNo, string sourceEndorsementNo, EndorsementType type, string user,
      CancellationToken ct = default)
        {
            var policy = await GetPolicyAsync(policyNo, ct);

            var source = await _db.PolicyDetails
                .FirstOrDefaultAsync(d => d.PolicyNo == policyNo
                    && d.EndorsementNo == sourceEndorsementNo
                    && (d.Deleted == null || d.Deleted == 0), ct)
                ?? throw new NotFoundException($"Detail '{sourceEndorsementNo}' not found on policy '{policyNo}'.");

            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    PolicyDetail rec;

                    if (type == EndorsementType.Additional)
                    {
                        rec = await BuildAndSaveEnvelopeAsync(policy, user, "ADDITIONAL", ct);
                    }
                    else
                    {
                        var bizOption = type == EndorsementType.Renewal ? "RENEWAL" : "REVERSAL";

                        rec = await CloneDetailAsync(source, policy, user, bizOption, ct);
                        await _members.DuplicateMembersAsync(policyNo, sourceEndorsementNo, rec.EndorsementNo, bizOption, user, ct);

                        source.BizOption = "EXPIRED";
                        source.ModifiedBy = user;
                        source.ModifiedOn = DateTime.Now;
                        await _db.SaveChangesAsync(ct);

                        // The member rollup inside DuplicateMembersAsync ran while the source was still
                        // live. Now that it is EXPIRED, recompute the policy header so it counts live
                        // sections only: a REVERSAL nets the pair to zero, a RENEWAL counts once.
                        await _members.UpdatePolicyHeaderTotalsAsync(policyNo, user, ct);
                    }

                    await tx.CommitAsync(ct);
                    return ToDto(rec, created: true);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });
        }

        // Clones a PolicyDetail's figures onto a new endorsement number — used by RENEWAL/REVERSAL,
        // which are re-running the same calculation under a new section rather than starting at zero.
        private async Task<PolicyDetail> CloneDetailAsync(
            PolicyDetail source, Policy policy, string user, string bizOption, CancellationToken ct)
        {
            var endorseNo = await _autoNumber.GetNextAsync(
                AutoNumberType.EndorsementNo, policy.BranchID ?? string.Empty, policy.SubRiskID, ct);

            var rec = new PolicyDetail
            {
                PolicyNo = source.PolicyNo,
                EndorsementNo = endorseNo,
                CertOrDocNo = endorseNo,
                InsuredName = source.InsuredName,
                EntryDate = DateTime.Now,
                SubmittedBy = user,
                SubmittedOn = DateTime.Now,
                BizOption = bizOption,
                StartDate = source.StartDate,
                EndDate = source.EndDate,
                ExRate = source.ExRate,
                ExCurrency = source.ExCurrency,
                PremiumRate = source.PremiumRate,
                ProportionRate = source.ProportionRate,
                SumInsured = source.SumInsured,
                TotalRiskValue = source.TotalRiskValue,
                A20 = source.A20,
                A15 = source.A15,
                GrossPremium = source.GrossPremium,
                NetAmount = source.NetAmount,
                SumInsuredFrgn = source.SumInsuredFrgn,
                GrossPremiumFrgn = source.GrossPremiumFrgn,
                A6 = source.A6,
                A2 = source.A2,
                A3 = source.A3,
                A4 = source.A4,
                A5 = source.A5,
                ProRataDays = source.ProRataDays,
                Deleted = 0,
                Active = 1,
                A10 = source.A10,
                A19 = source.A19,
                A7 = source.A7,
                Field16 = source.Field16,
                Field17 = source.Field17,
                Field10 = Guid.NewGuid().ToString(),
                Field20 = "Uncompleted",
                A18 = source.A18
            };

            _db.PolicyDetails.Add(rec);
            await _db.SaveChangesAsync(ct);
            return rec;
        }
        public async Task<GroupLifeDetailDto?> GetEnvelopeAsync(string policyNo, CancellationToken ct = default)
        {
            var rec = await _db.PolicyDetails.AsNoTracking()
                .Where(d => d.PolicyNo == policyNo && (d.Deleted == null || d.Deleted == 0))
                .OrderByDescending(d => d.DetailID)
                .FirstOrDefaultAsync(ct);

            return rec is null ? null : ToDto(rec, created: false);
        }

        public async Task<IReadOnlyList<GroupLifeDetailDto>> ListEnvelopesAsync(string policyNo, CancellationToken ct = default)
        {
            return await _db.PolicyDetails.AsNoTracking()
                .Where(d => d.PolicyNo == policyNo && (d.Deleted == null || d.Deleted == 0))
                .OrderByDescending(d => d.DetailID)
                .Select(d => new GroupLifeDetailDto
                {
                    DetailID = (int)d.DetailID,
                    PolicyNo = d.PolicyNo,
                    EndorsementNo = d.EndorsementNo,
                    CertOrDocNo = d.CertOrDocNo,
                    InsuredName = d.InsuredName,
                    EndorsementType = d.BizOption,
                    CompletionStatus = d.Field20,
                    EffectiveDate = d.StartDate,
                    ExpiryDate = d.EndDate,
                    SumInsured = (decimal)d.SumInsured,
                    GrossPremium = (decimal)d.GrossPremium,
                    NetPremium = (decimal)d.NetAmount,
                    SubmittedBy = d.SubmittedBy,
                    SubmittedOn = d.SubmittedOn,
                    Created = false
                })
                .ToListAsync(ct);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private async Task<Policy> GetPolicyAsync(string policyNo, CancellationToken ct) =>
            await _db.Policies.FirstOrDefaultAsync(p => p.PolicyNo == policyNo, ct)
            ?? throw new NotFoundException($"Policy '{policyNo}' not found.");

        private async Task<PolicyDetail> BuildAndSaveEnvelopeAsync(Policy policy, string user, string bizOption,
            CancellationToken ct)
        {
            var endorseNo = await _autoNumber.GetNextAsync(
                AutoNumberType.EndorsementNo, policy.BranchID ?? string.Empty, policy.SubRiskID, ct);

            var rec = new PolicyDetail
            {
                PolicyNo = policy.PolicyNo,
                EndorsementNo = endorseNo,
                CertOrDocNo = endorseNo,
                InsuredName = policy.InsFullname,
                EntryDate = DateTime.Now,
                SubmittedBy = user,
                SubmittedOn = DateTime.Now,
                BizOption = bizOption,
                StartDate = policy.StartDate,
                EndDate = policy.EndDate,
                ExRate = policy.ExRate,
                ExCurrency = policy.ExCurrency,
                PremiumRate = 0,
                ProportionRate = policy.ProportionRate,
                SumInsured = 0,
                TotalRiskValue = 0,
                A20 = 0,
                A15 = 0,
                GrossPremium = 0,
                NetAmount = 0,
                SumInsuredFrgn = 0,
                GrossPremiumFrgn = 0,
                A6 = 0,
                A2 = 0,
                A3 = 0,
                A4 = 0,
                A5 = 0,
                ProRataDays = 0,
                Deleted = 0,
                Active = 1,
                A10 = 1,        // running serial seed for member numbering
                A19 = 0,        // loading rate
                A7 = 0,         // discount rate
                Field16 = string.Empty,
                Field17 = string.Empty,
                Field10 = Guid.NewGuid().ToString(),
                Field20 = "Uncompleted",
                A18 = 0
            };

            _db.PolicyDetails.Add(rec);
            await _db.SaveChangesAsync(ct);
            return rec;
        }

        private static GroupLifeDetailDto ToDto(PolicyDetail d, bool created) => new()
        {
            DetailID = (int)d.DetailID,
            PolicyNo = d.PolicyNo,
            EndorsementNo = d.EndorsementNo,
            CertOrDocNo = d.CertOrDocNo,
            InsuredName = d.InsuredName,
            EndorsementType = d.BizOption,
            CompletionStatus = d.Field20,
            EffectiveDate = d.StartDate,
            ExpiryDate = d.EndDate,
            SumInsured = (decimal)d.SumInsured,
            GrossPremium = (decimal)d.GrossPremium,
            NetPremium = (decimal)d.NetAmount,
            SubmittedBy = d.SubmittedBy,
            SubmittedOn = d.SubmittedOn,
            Created = created
        };


    }
}