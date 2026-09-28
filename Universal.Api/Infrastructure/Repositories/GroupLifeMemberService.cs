using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Infrastructure.Common;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static GibsLifesMicroWebApi.Data.Repositories.Repository;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories

{

    public class GroupLifeMemberService
    {
        private readonly DataContext _db;
        private readonly PremiumCalculator _calc;
        private readonly AutoNumberService _autoNumber;
        private readonly BulkUploadErrorService _bulkUploadErrorService;
        public GroupLifeMemberService(DataContext db, PremiumCalculator calc, AutoNumberService autoNumber, BulkUploadErrorService bulkUploadErrorService)
        {
            _db = db;
            _calc = calc;
            _autoNumber = autoNumber;
            _bulkUploadErrorService = bulkUploadErrorService;
        }

        public async Task<IReadOnlyList<GrpMember>> ListAsync(string policyNo, string? endorsementNo,
            CancellationToken ct = default)
        {
            var q = _db.GrpMembers.AsNoTracking()
                .Where(m => m.PolicyNo == policyNo && (m.Deleted == null || m.Deleted == 0));

            if (!string.IsNullOrWhiteSpace(endorsementNo))
                q = q.Where(m => m.EndorsementNo == endorsementNo);

            return await q.OrderBy(m => m.SerialNo).ThenBy(m => m.MemberID).ToListAsync(ct);
        }

        // ------------------------------------------------------------------
        // "Calc.." button — pure computation, no persistence.
        // ------------------------------------------------------------------
        public Task<PremiumCalcResultDto> CalcAsync(PremiumCalcRequestDto dto, CancellationToken ct = default)
        {
            var emolument = _calc.TotalEmolument(dto.BasicSalary, dto.Transport, dto.Housing, dto.OtherAmount);
            var p = _calc.ComputeMember(emolument, dto.FixedPremiumRate, dto.OurSharePercent);

            return Task.FromResult(new PremiumCalcResultDto
            {
                TotalEmolument = emolument,
                TotalSumInsured = p.TotalSumInsured,
                TotalPremium = p.TotalPremium,
                ShareSumInsured = p.ShareSumInsured,
                SharePremium = p.SharePremium
            });
        }

        // ------------------------------------------------------------------
        // Save & Update Details -> AddItemSectionTempGridDetails + InsertItemCoverGridDetails.
        // ------------------------------------------------------------------
        public async Task<MemberResponseDto> UpsertAsync(MemberUpsertDto dto, string user, CancellationToken ct = default)
        {
            var policy = await _db.Policies.FirstOrDefaultAsync(p => p.PolicyNo == dto.PolicyNo, ct)
                         ?? throw new NotFoundException($"Policy '{dto.PolicyNo}' not found.");

            // The section (PolicyDetail envelope) must already exist, otherwise the totals
            // rollup has nothing to update and the members end up orphaned.
            var endorsementNo = await EnsureSectionExistsAsync(dto.PolicyNo, dto.EndorsementNo, ct);

            var emolument = _calc.TotalEmolument(dto.BasicSalary, dto.Transport, dto.Housing, dto.OtherAmount);
            var premium = _calc.ComputeMember(emolument, dto.FixedPremiumRate, dto.OurSharePercent);

            // Resolve the members/certificate number.
            var membersNo = dto.MembersNo;
            if (string.Equals(dto.NumberType, "AUTO", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(membersNo))
                membersNo = await _autoNumber.GetNextAsync(AutoNumberType.CertNo, policy.BranchID ?? "01", ct: ct);

            // Insert vs. update: match on PolicyNo + EndorsementNo + MembersNo (SelectThisProposalStaffNo).
            // EndorsementNo is part of the match because RENEWAL/REVERSAL sections clone members
            // with the same MembersNo, so PolicyNo + MembersNo alone is no longer unique.
            var rec = await _db.GrpMembers.FirstOrDefaultAsync(
                m => m.PolicyNo == dto.PolicyNo
                     && m.EndorsementNo == endorsementNo
                     && m.MembersNo == membersNo
                     && (m.Deleted == null || m.Deleted == 0), ct);

            var isNew = rec is null;
            if (rec is null)
            {
                rec = new GrpMember
                {
                    QuotationNo = dto.PolicyNo,
                    PolicyNo = dto.PolicyNo,
                    EndorsementNo = endorsementNo,
                    MembersNo = membersNo,
                    BizOption = "NEW",
                    SerialNo = dto.SerialNo ?? await NextSerialAsync(dto.PolicyNo, endorsementNo, ct),
                    SubmittedBy = user,
                    SubmittedOn = DateTime.Now,
                    Deleted = 0
                };
                _db.GrpMembers.Add(rec);
            }
            else
            {
                rec.ModifiedBy = user;
                rec.ModifiedOn = DateTime.Now;
            }

            rec.Surname = dto.Surname;
            rec.OtherNames = dto.OtherNames;
            rec.Sex = dto.Sex;
            rec.DOBDate = dto.DateOfBirth;
            rec.HireDate = dto.StartDate;          // member cover start (legacy HireDate = TstartDate)
                                                   // rec.DisbursedDate = dto.ExpiryDate;    // member cover end   (legacy DisbursedDate = TEndDate)
            rec.Age = dto.Age;
            rec.BasicSalary = dto.BasicSalary;
            rec.Housing = dto.Housing;
            rec.Transport = dto.Transport;
            rec.OtherAmount = dto.OtherAmount;
            rec.TEmolument = emolument;
            rec.PremiumRate = (float)dto.FixedPremiumRate;
            rec.SumInsured = premium.ShareSumInsured;   // share
            rec.GrossPremium = premium.SharePremium;    // share
            rec.A9 = premium.TotalSumInsured;           // 100%
            rec.A10 = premium.TotalPremium;             // 100%
            rec.A1 = dto.Duration;
            rec.Field7 = dto.FreqOfPayment;

            await _db.SaveChangesAsync(ct);

            // Persist per-cover breakdown for this policy/endorsement.
            //await RebuildCoverDetailsAsync(policy, endorsementNo, emolument, dto.FixedPremiumRate, user, ct);

            // Roll the member totals up onto the PolicyDetail envelope.
            await UpdateEnvelopeTotalsAsync(dto.PolicyNo, endorsementNo, user, ct);

            return new MemberResponseDto
            {
                MemberID = rec.MemberID,
                MembersNo = rec.MembersNo,
                SerialNo = rec.SerialNo,
                Surname = rec.Surname,
                OtherNames = rec.OtherNames,
                Sex = rec.Sex,
                Age = rec.Age,
                TotalEmolument = emolument,
                TotalSumInsured = premium.TotalSumInsured,
                TotalPremium = premium.TotalPremium,
                ShareSumInsured = premium.ShareSumInsured,
                SharePremium = premium.SharePremium,
                Duration = dto.Duration
            };
        }

        public async Task<bool> RemoveAsync(long memberId, CancellationToken ct = default)
        {
            var rec = await _db.GrpMembers.FirstOrDefaultAsync(m => m.MemberID == memberId, ct);
            if (rec is null) return false;

            // Soft delete, matching the legacy DeleteRec behaviour.
            rec.Deleted = 1;
            await _db.SaveChangesAsync(ct);

            // Keep the envelope totals in sync after removing the member.
            if (!string.IsNullOrWhiteSpace(rec.PolicyNo))
                await UpdateEnvelopeTotalsAsync(rec.PolicyNo!, rec.EndorsementNo, rec.ModifiedBy ?? "system", ct);

            return true;
        }

        public async Task<AggregatePremiumDto> GetAggregateAsync(string policyNo, string? endorsementNo,
            CancellationToken ct = default)
        {
            IReadOnlyList<GrpMember> members;

            if (string.IsNullOrWhiteSpace(endorsementNo))
            {
                // Whole-policy view: live sections only, so an EXPIRED source and its REVERSAL
                // (or RENEWAL) clone are never counted together.
                var live = await GetLiveEndorsementNosAsync(policyNo, ct);
                var all = await ListAsync(policyNo, null, ct);
                members = all.Where(m => m.EndorsementNo != null && live.Contains(m.EndorsementNo)).ToList();
            }
            else
            {
                // An explicit section is returned as asked, even if it is EXPIRED or a REVERSAL.
                members = await ListAsync(policyNo, endorsementNo, ct);
            }

            // TotalRisk* use the 100% figures (A9 / A10); Share* use the member share fields.
            var totalRiskValue = members.Sum(m => m.A9 ?? 0m);
            var totalRiskPremium = members.Sum(m => m.A10 ?? 0m);
            var shareSum = members.Sum(m => m.SumInsured ?? 0m);
            var sharePrem = members.Sum(m => m.GrossPremium ?? 0m);

            return new AggregatePremiumDto
            {
                TotalRiskValue = totalRiskValue,
                TotalRiskPremium = totalRiskPremium,
                ShareSumInsured = shareSum,
                SharePremium = sharePrem,
                MemberCount = members.Count
            };
        }

        // ------------------------------------------------------------------
        // Bulk upload — unifies cmduploadtodb_Click (PolicyRate) and UploadDataExcel_GOXI (FixedRate).
        //// ------------------------------------------------------------------
        //public async Task<BulkMemberUploadResultDto> BulkUploadAsync(BulkMemberUploadDto dto, string user,
        //    CancellationToken ct = default)
        //{
        //    if (string.IsNullOrWhiteSpace(dto.PolicyNo))
        //        throw new BusinessRuleException("policyNo is required for a bulk upload.");

        //    if (dto.Rows is null || dto.Rows.Count == 0)
        //        throw new BusinessRuleException("No member rows were supplied.");

        //    var policy = await _db.Policies.FirstOrDefaultAsync(p => p.PolicyNo == dto.PolicyNo, ct)
        //                 ?? throw new NotFoundException($"Policy '{dto.PolicyNo}' not found.");

        //    var proportion = (decimal)(policy.ProportionRate ?? 100d);
        //    var policyRate = policy.A10 ?? 0m;    // stored rate fraction

        //    decimal totalSum = 0, totalPrem = 0;
        //    var inserted = 0;
        //    var serial = 1;

        //    foreach (var row in dto.Rows)
        //    {
        //        if (string.IsNullOrWhiteSpace(row.CustomerName)) continue;

        //        var sumAssured = row.DisbursementAmount;

        //        var premium = dto.PricingMode switch
        //        {
        //            BulkPricingMode.PolicyRate => policyRate * sumAssured,
        //            BulkPricingMode.FixedRate => _calc.GroupLifePremium(
        //                sumAssured, dto.FixedPremiumRate, dto.LoadingRate, dto.DiscountRate),
        //            _ => policyRate * sumAssured
        //        };

        //        var certNo = await _autoNumber.GetNextAsync(AutoNumberType.CertNo, policy.BranchID ?? "01", ct: ct);

        //        var member = new GrpMember
        //        {
        //            // QuotationNo carries the loan/customer account number (LAPO CUST_NO) when
        //            // supplied; it otherwise just echoed PolicyNo, so no information is lost.
        //            QuotationNo = Trunc(string.IsNullOrWhiteSpace(row.CustomerNo) ? dto.PolicyNo : row.CustomerNo, 32),
        //            PolicyNo = Trunc(dto.PolicyNo, 32),
        //            EndorsementNo = Trunc(dto.EndorsementNo, 64),
        //            BizOption = "POLY",
        //            SerialNo = Trunc(string.IsNullOrWhiteSpace(row.SerialNo) ? serial.ToString() : row.SerialNo, 32),
        //            MembersNo = Trunc($"{policy.SubRiskID}{certNo}", 32),
        //            Surname = Trunc(row.CustomerName, 128),
        //            OtherNames = string.Empty,
        //            //Address = Trunc(row.CustomerAddress, 255),
        //            DOBDate = row.DateOfBirth,
        //            HireDate = row.MaturityDate,
        //            //DisbursedDate = row.DisbursementDate,
        //            Sex = Trunc(row.Gender, 32),
        //            SumInsured = sumAssured,
        //            GrossPremium = premium,
        //            A1 = row.Tenor,
        //            Field1 = Trunc(row.PhoneNo, 128),
        //            Field2 = Trunc(row.Occupation, 32),
        //            Field3 = Trunc(row.BranchName, 32),
        //            // Prefer an explicit state; fall back to the regional AREA when state is blank.
        //            Field4 = Trunc(string.IsNullOrWhiteSpace(row.States) ? row.Area : row.States, 32),
        //            Field5 = Trunc(row.NextOfKin, 32),
        //            Field6 = Trunc(row.ProductDescription, 32),
        //            Field7 = "M",
        //            Field8 = Trunc(row.Bvn, 32),
        //            Field9 = Trunc(row.Zones, 64),
        //            Field10 = Guid.NewGuid().ToString(),
        //            SubmittedBy = Trunc(user, 64),
        //            SubmittedOn = DateTime.Now,
        //            Deleted = 0
        //        };

        //        _db.GrpMembers.Add(member);

        //        totalSum += sumAssured;
        //        totalPrem += premium;
        //        inserted++;
        //        serial++;
        //    }

        //    await _db.SaveChangesAsync(ct);

        //    // Roll the uploaded totals up onto the PolicyDetail envelope.
        //    await UpdateEnvelopeTotalsAsync(dto.PolicyNo, dto.EndorsementNo, user, ct, dto.LoadingRate);

        //    return new BulkMemberUploadResultDto
        //    {
        //        InsertedCount = inserted,
        //        Aggregate = new AggregatePremiumDto
        //        {
        //            TotalRiskValue = totalSum,
        //            TotalRiskPremium = totalPrem,
        //            ShareSumInsured = totalSum * proportion / 100m,
        //            SharePremium = totalPrem * proportion / 100m,
        //            MemberCount = inserted
        //        }
        //    };
        //}

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        public async Task<BulkMemberUploadResultDto> BulkUploadAsync(
      BulkMemberUploadDto dto,
      string user,
      CancellationToken ct = default)
        {
            var uploadBatchId = Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(dto.PolicyNo))
                throw new BusinessRuleException("Policy Number is required.");

            if (dto.Rows == null || dto.Rows.Count == 0)
                throw new BusinessRuleException("No member rows were supplied.");

            var policy = await _db.Policies
                .FirstOrDefaultAsync(x => x.PolicyNo == dto.PolicyNo, ct)
                ?? throw new NotFoundException($"Policy '{dto.PolicyNo}' not found.");

            if (policy.A10 == null)
                throw new BusinessRuleException("Policy premium rate has not been configured.");

            // The section (PolicyDetail envelope) must already exist, otherwise the totals
            // rollup has nothing to update and the members end up orphaned.
            var endorsementNo = await EnsureSectionExistsAsync(dto.PolicyNo, dto.EndorsementNo, ct);

            var proportion = (decimal)(policy.ProportionRate ?? 100d);
            var policyRate = policy.A10.Value;

            decimal totalSum = 0;
            decimal totalPremium = 0;

            int inserted = 0;
            int skipped = 0;
            int serial = 1;

            async Task LogErrorAsync(BulkMemberRowDto row, string reason)
            {
                skipped++;

                await _bulkUploadErrorService.InsertAsync(
                    new BulkUploadErrorCreateDto
                    {
                        UploadBatchID = uploadBatchId,
                        PolicyNo = dto.PolicyNo,
                        MemberNo = row.CustomerNo,
                        MemberName = row.CustomerName,
                        ErrorReason = reason,
                        RawData = System.Text.Json.JsonSerializer.Serialize(row),
                        UploadedBy = user,
                        UploadedOn = DateTime.Now,
                        IsResolved = "N"
                    },
                    ct);
            }



            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var trx = await _db.Database.BeginTransactionAsync(ct);

                try
                {
                    _db.ChangeTracker.AutoDetectChangesEnabled = false;

                    foreach (var row in dto.Rows)
                    {
                        try
                        {
                            //--------------------------------------------------
                            // Validation
                            //--------------------------------------------------

                            if (string.IsNullOrWhiteSpace(row.CustomerName))
                            {
                                await LogErrorAsync(row, "Customer Name is required.");
                                serial++;
                                continue;
                            }

                            if (row.DisbursementAmount <= 0)
                            {
                                await LogErrorAsync(row, "Invalid Disbursement Amount.");
                                serial++;
                                continue;
                            }

                            //--------------------------------------------------
                            // Duplicate Check
                            //--------------------------------------------------

                            //if (!string.IsNullOrWhiteSpace(row.CustomerNo))
                            //{
                            //    bool exists = await _db.GrpMembers.AnyAsync(x =>
                            //        x.PolicyNo == dto.PolicyNo &&
                            //        x.QuotationNo == row.CustomerNo.Trim(),
                            //        ct);

                            //    if (exists)
                            //    {
                            //        await LogErrorAsync(row, "Customer already exists for this policy.");
                            //        serial++;
                            //        continue;
                            //    }
                            //}

                            //--------------------------------------------------
                            // Premium Calculation
                            //--------------------------------------------------

                            decimal premium = dto.PricingMode switch
                            {
                                BulkPricingMode.PolicyRate =>
                                    policyRate * row.DisbursementAmount,

                                BulkPricingMode.FixedRate =>
                                    _calc.GroupLifePremium(
                                        row.DisbursementAmount,
                                        dto.FixedPremiumRate,
                                        dto.LoadingRate,
                                        dto.DiscountRate),

                                _ =>
                                    policyRate * row.DisbursementAmount
                            };

                            // Same convention as UpsertAsync: A9/A10 hold the 100% figures,
                            // SumInsured/GrossPremium hold our share of them.
                            var shareSumInsured = row.DisbursementAmount * proportion / 100m;
                            var sharePremium = premium * proportion / 100m;

                            //--------------------------------------------------
                            // Certificate Number
                            //--------------------------------------------------

                            var certNo = await _autoNumber.GetNextAsync(
                                AutoNumberType.CertNo,
                                policy.BranchID ?? "01",
                                ct: ct);

                            //--------------------------------------------------
                            // Member Entity
                            //--------------------------------------------------

                            var member = new GrpMember
                            {
                                QuotationNo = Trunc(
                                    string.IsNullOrWhiteSpace(row.CustomerNo)
                                        ? dto.PolicyNo
                                        : row.CustomerNo.Trim(),
                                    32),

                                PolicyNo = Trunc(dto.PolicyNo.Trim(), 32),
                                EndorsementNo = Trunc(endorsementNo, 64),

                                BizOption = dto.BizOption,

                                SerialNo = Trunc(
                                    string.IsNullOrWhiteSpace(row.SerialNo)
                                        ? serial.ToString()
                                        : row.SerialNo.Trim(),
                                    32),

                                MembersNo = Trunc($"{policy.SubRiskID}{certNo}", 32),

                                Surname = Trunc(row.CustomerName.Trim(), 128),
                                OtherNames = string.Empty,

                                DOBDate = row.DateOfBirth,
                                HireDate = row.MaturityDate,
                                Sex = Trunc(row.Gender?.Trim(), 32),

                                SumInsured = shareSumInsured,   // share
                                GrossPremium = sharePremium,    // share
                                A9 = row.DisbursementAmount,    // 100% sum insured
                                A10 = premium,                  // 100% premium

                                A1 = row.Tenor,

                                Field1 = Trunc(row.PhoneNo?.Trim(), 128),
                                Field2 = Trunc(row.Occupation?.Trim(), 32),
                                Field3 = Trunc(row.BranchName?.Trim(), 32),
                                Field4 = Trunc(
                                    string.IsNullOrWhiteSpace(row.States)
                                        ? row.Area?.Trim()
                                        : row.States.Trim(),
                                    32),
                                Field5 = Trunc(row.NextOfKin?.Trim(), 32),
                                Field6 = Trunc(row.ProductDescription?.Trim(), 32),
                                Field7 = "M",
                                Field8 = Trunc(row.Bvn?.Trim(), 32),
                                Field9 = Trunc(row.Zones?.Trim(), 64),

                                Field10 = Guid.NewGuid().ToString(),

                                SubmittedBy = Trunc(user, 64),
                                SubmittedOn = DateTime.Now,
                                Deleted = 0
                            };

                            _db.GrpMembers.Add(member);

                            totalSum += row.DisbursementAmount;
                            totalPremium += premium;
                            inserted++;
                        }
                        catch (Exception ex)
                        {
                            await LogErrorAsync(row, ex.Message);
                        }

                        serial++;
                    }

                    _db.ChangeTracker.AutoDetectChangesEnabled = true;

                    await _db.SaveChangesAsync(ct);

                    await UpdateEnvelopeTotalsAsync(
                        dto.PolicyNo,
                        endorsementNo,
                        user,
                        ct,
                        dto.LoadingRate);

                    await trx.CommitAsync(ct);

                    return new BulkMemberUploadResultDto
                    {
                        UploadBatchID = uploadBatchId,
                        InsertedCount = inserted,
                        SkippedCount = skipped,
                        Aggregate = new AggregatePremiumDto
                        {
                            TotalRiskValue = totalSum,
                            TotalRiskPremium = totalPremium,
                            ShareSumInsured = totalSum * proportion / 100m,
                            SharePremium = totalPremium * proportion / 100m,
                            MemberCount = inserted
                        }
                    };
                }
                catch
                {
                    await trx.RollbackAsync(ct);
                    throw;
                }
                finally
                {
                    _db.ChangeTracker.AutoDetectChangesEnabled = true;
                }
            });
        }



        /// <summary>
        /// Clips a string to the underlying column width so a long value (e.g. a verbose
        /// PRODUCT_DESC) can't trigger SQL error 8152 "String or binary data would be truncated".
        /// </summary>
        private static string? Trunc(string? value, int max)
            => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];


        /// <summary>
        /// Guards the member write paths (Upsert / BulkUpload). Members must be saved against a
        /// section (PolicyDetail envelope) that already exists on the policy. Without this, an
        /// arbitrary or blank EndorsementNo is stored as-is, the envelope rollup finds nothing to
        /// update (its totals stay 0), and later RENEWAL/REVERSAL clones find no members to copy.
        /// Returns the trimmed, validated endorsement number for the caller to use.
        /// </summary>
        private async Task<string> EnsureSectionExistsAsync(string policyNo, string? endorsementNo, CancellationToken ct)
        {
            var endorsement = endorsementNo?.Trim();

            if (string.IsNullOrWhiteSpace(endorsement))
                throw new BusinessRuleException("Endorsement number is required.");

            var exists = await _db.PolicyDetails.AsNoTracking().AnyAsync(d =>
                d.PolicyNo == policyNo
                && d.EndorsementNo == endorsement
                && (d.Deleted == null || d.Deleted == 0), ct);

            if (!exists)
                throw new NotFoundException($"Section '{endorsement}' not found on policy '{policyNo}'.");

            return endorsement;
        }


        /// <summary>
        /// Rolls the current members' totals up onto the PolicyDetail "envelope" row, mirroring
        /// how the legacy screen wrote section totals back to PolicyDetails. Field mapping:
        ///   TotalRiskValue = sum of A9 (100% sum insured)
        ///   A20            = sum of A10 (100% premium)   [TotalRiskPremium]
        ///   SumInsured     = sum of member SumInsured    [our share]
        ///   GrossPremium   = sum of member GrossPremium  [our share]
        ///   NetAmount      = share premium (same as GrossPremium here)
        ///   A18            = member count
        ///   A19            = loading rate (when supplied)
        /// The live GetAggregateAsync remains the source of truth; this just persists a snapshot
        /// so downstream accounting/invoicing can read totals straight off the envelope.
        /// </summary>
        private async Task UpdateEnvelopeTotalsAsync(string policyNo, string? endorsementNo, string user,
            CancellationToken ct, decimal? loadingRate = null)
        {
            var envelopeQuery = _db.PolicyDetails
                .Where(d => d.PolicyNo == policyNo && (d.Deleted == null || d.Deleted == 0));

            if (!string.IsNullOrWhiteSpace(endorsementNo))
                envelopeQuery = envelopeQuery.Where(d => d.EndorsementNo == endorsementNo);

            var envelope = await envelopeQuery
                .OrderByDescending(d => d.DetailID)
                .FirstOrDefaultAsync(ct);

            // Update the per-endorsement envelope when one exists (members can be added
            // without a detail row, in which case only the policy header is refreshed).
            if (envelope is not null)
            {
                var members = await _db.GrpMembers.AsNoTracking()
                    .Where(m => m.PolicyNo == policyNo
                                && (endorsementNo == null || m.EndorsementNo == endorsementNo)
                                && (m.Deleted == null || m.Deleted == 0))
                    .ToListAsync(ct);

                envelope.TotalRiskValue = members.Sum(m => m.A9 ?? 0m);      // 100% sum insured
                envelope.A20 = members.Sum(m => m.A10 ?? 0m);               // 100% premium
                envelope.SumInsured = members.Sum(m => m.SumInsured ?? 0m); // our share
                envelope.GrossPremium = members.Sum(m => m.GrossPremium ?? 0m);
                envelope.NetAmount = envelope.GrossPremium;
                envelope.A18 = members.Count;
                if (loadingRate.HasValue)
                    envelope.A19 = loadingRate.Value;
                envelope.ModifiedBy = user;
                envelope.ModifiedOn = DateTime.Now;

                await _db.SaveChangesAsync(ct);
            }

            // Always roll the whole-policy totals up to the Policies header.
            await UpdatePolicyHeaderTotalsAsync(policyNo, user, ct);
        }

        /// <summary>
        /// Section BizOptions that must NOT count towards whole-policy totals:
        ///   EXPIRED  - a source section that has been renewed or reversed (its members were cloned
        ///              onto the new section, so counting both would double the figures).
        ///   REVERSAL - a section that cancels the one it was cloned from; the pair nets to zero.
        /// Everything else (NEW, ADDITIONAL, RENEWAL, ...) is a live section.
        /// </summary>
        private static readonly string[] NonLiveBizOptions = { "EXPIRED", "REVERSAL" };

        /// <summary>Endorsement numbers of the policy's live (non-deleted, non-expired, non-reversal) sections.</summary>
        private async Task<List<string>> GetLiveEndorsementNosAsync(string policyNo, CancellationToken ct)
        {
            return await _db.PolicyDetails.AsNoTracking()
                .Where(d => d.PolicyNo == policyNo
                            && (d.Deleted == null || d.Deleted == 0)
                            && d.EndorsementNo != null
                            && (d.BizOption == null || !NonLiveBizOptions.Contains(d.BizOption)))
                .Select(d => d.EndorsementNo!)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Recomputes the Policies header totals from the policy's LIVE sections only and saves them.
        /// Unlike the per-endorsement envelope, the header is a whole-policy figure, so it spans
        /// sections but skips EXPIRED and REVERSAL ones (see <see cref="NonLiveBizOptions"/>).
        ///   Policies.SumInsured   = sum of member SumInsured   (our share)
        ///   Policies.GrossPremium = sum of member GrossPremium (our share)
        /// Public because GroupLifeDetailService must call it again after it marks a source section
        /// EXPIRED: the member rollup runs BEFORE that flag is set, so without a second pass the
        /// header would still hold the source's figures (and, for a RENEWAL, source + clone).
        /// NOTE: inferred from the schema — the legacy header rollup / posting step wasn't in the
        /// supplied source. Confirm whether the header should hold share or 100% figures, and
        /// whether it updates live (as here) or only when the transaction is completed/posted.
        /// </summary>
        public async Task UpdatePolicyHeaderTotalsAsync(string policyNo, string user, CancellationToken ct)
        {
            var policy = await _db.Policies.FirstOrDefaultAsync(p => p.PolicyNo == policyNo, ct);
            if (policy is null) return;

            var live = await GetLiveEndorsementNosAsync(policyNo, ct);

            var liveMembers = _db.GrpMembers.AsNoTracking()
                .Where(m => m.PolicyNo == policyNo
                            && (m.Deleted == null || m.Deleted == 0)
                            && live.Contains(m.EndorsementNo));

            policy.SumInsured = await liveMembers.SumAsync(m => m.SumInsured ?? 0m, ct);     // our share
            policy.GrossPremium = await liveMembers.SumAsync(m => m.GrossPremium ?? 0m, ct); // our share
            policy.ModifiedBy = user;
            policy.ModifiedOn = DateTime.Now;

            await _db.SaveChangesAsync(ct);
        }

        private async Task<string> NextSerialAsync(string policyNo, string? endorsementNo, CancellationToken ct)
        {
            var count = await _db.GrpMembers.CountAsync(
                m => m.PolicyNo == policyNo && (endorsementNo == null || m.EndorsementNo == endorsementNo)
                     && (m.Deleted == null || m.Deleted == 0), ct);
            return (count + 1).ToString();
        }

        // ------------------------------------------------------------------
        // Duplicates every member from one endorsement onto a new one, used by
        // RENEWAL/REVERSAL endorsements which re-run an existing section rather
        // than starting from a blank envelope. Copies every rated field verbatim —
        // no premium recalculation — and rolls the new envelope's totals once at
        // the end instead of per member.
        //
        // Throws when the source section has no members: a RENEWAL/REVERSAL of an
        // empty section would otherwise "succeed" and silently leave the new
        // envelope with zeros. The caller runs this inside a transaction, so the
        // cloned envelope is rolled back along with the failure.
        // ------------------------------------------------------------------
        public async Task<int> DuplicateMembersAsync(
            string policyNo, string fromEndorsementNo, string toEndorsementNo, string bizOption, string user,
            CancellationToken ct = default)
        {
            var source = await _db.GrpMembers.AsNoTracking()
                .Where(m => m.PolicyNo == policyNo
                            && m.EndorsementNo == fromEndorsementNo
                            && (m.Deleted == null || m.Deleted == 0))
                .ToListAsync(ct);

            if (source.Count == 0)
                throw new BusinessRuleException(
                    $"Section '{fromEndorsementNo}' on policy '{policyNo}' has no members to copy for a {bizOption} endorsement.");

            foreach (var src in source)
            {
                var clone = new GrpMember
                {
                    QuotationNo = src.QuotationNo,
                    PolicyNo = src.PolicyNo,
                    EndorsementNo = toEndorsementNo,
                    MembersNo = src.MembersNo,           // same member/cert identity, new section
                    BizOption = bizOption,                // "RENEWAL" or "REVERSAL"
                    SerialNo = src.SerialNo,
                    Surname = src.Surname,
                    OtherNames = src.OtherNames,
                    Sex = src.Sex,
                    DOBDate = src.DOBDate,
                    HireDate = src.HireDate,
                    Age = src.Age,
                    BasicSalary = src.BasicSalary,
                    Housing = src.Housing,
                    Transport = src.Transport,
                    OtherAmount = src.OtherAmount,
                    TEmolument = src.TEmolument,
                    PremiumRate = src.PremiumRate,
                    SumInsured = src.SumInsured,
                    GrossPremium = src.GrossPremium,
                    A9 = src.A9,
                    A10 = src.A10,
                    A1 = src.A1,
                    Field1 = src.Field1,
                    Field2 = src.Field2,
                    Field3 = src.Field3,
                    Field4 = src.Field4,
                    Field5 = src.Field5,
                    Field6 = src.Field6,
                    Field7 = src.Field7,
                    Field8 = src.Field8,
                    Field9 = src.Field9,
                    Field10 = Guid.NewGuid().ToString(),
                    SubmittedBy = user,
                    SubmittedOn = DateTime.Now,
                    Deleted = 0
                };

                _db.GrpMembers.Add(clone);
            }

            await _db.SaveChangesAsync(ct);

            // One rollup for the whole batch, not per member.
            await UpdateEnvelopeTotalsAsync(policyNo, toEndorsementNo, user, ct);

            return source.Count;
        }

        ///// <summary>
        ///// Rebuilds PolicyCoverDetails for the policy/endorsement from the product's cover catalogue.
        ///// Mirrors InsertItemCoverGridDetails: rate = fixed rate, value = emolument, premium = value*rate/100.
        ///// </summary>
        //private async Task RebuildCoverDetailsAsync(Policy policy, string? endorsementNo, decimal emolument,
        //    decimal fixedRate, string user, CancellationToken ct)
        //{
        //    var existing = await _db.PolicyCoverDetails
        //        .Where(c => c.PolicyID == policy.PolicyNo && c.Field2 == endorsementNo)
        //        .ToListAsync(ct);
        //    if (existing.Count > 0)
        //        _db.PolicyCoverDetails.RemoveRange(existing);

        //    var covers = await _db.SubRiskCovers.AsNoTracking()
        //        .Where(c => c.SubRiskID == policy.SubRiskID
        //                    && (c.Category == null || c.Category == "C")
        //                    && (c.Deleted == null || c.Deleted == 0))
        //        .ToListAsync(ct);

        //    foreach (var cover in covers)
        //    {
        //        var premium = _calc.CoverPremium(emolument, fixedRate);
        //        _db.PolicyCoverDetails.Add(new PolicyCoverDetail
        //        {
        //            PolicyID = policy.PolicyNo,
        //            CoverCode = cover.CoverCode,
        //            CoverName = cover.CoverName,
        //            Field1 = string.Empty,
        //            Field2 = endorsementNo,
        //            Field3 = string.Empty,
        //            A1 = emolument,
        //            A2 = fixedRate,
        //            A3 = premium,
        //            SubmittedBy = user,
        //            SubmittedOn = DateTime.Now
        //        });
        //    }

        //    await _db.SaveChangesAsync(ct);
        //}
    }
}