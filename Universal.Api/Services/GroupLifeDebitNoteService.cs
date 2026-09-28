using Dapper;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Infrastructure.Common;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;          // existing DNCNNote entity
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services.GroupLife
{
    /// <summary>
    /// Port of webroot/web/modules/underwriting/postings_dnotes_edit.aspx.vb —
    /// the group life debit note posting screen, including its co-insurance
    /// participation split. Facultative outward is a separate module.
    ///
    /// Behavioural differences from the legacy page, all deliberate:
    ///  1. One real ADO transaction wraps every write. The VB opened a transaction
    ///     and then did all its work through cls_* helpers that used their own
    ///     connections, so Trans.Rollback() never rolled anything back.
    ///  2. Column set verified against the DNCNNotes DDL rather than the model:
    ///     VatAmount (bigint) and Z_NAICOM_STATUS do exist and are written, while
    ///     VatRate, A1-A10, Field1-10, RIClass, TopMostValue, PMLValue and CRRefDate
    ///     genuinely do not. See MIGRATION-NOTES.md and DNCNNote-model-changes.md.
    ///  3. dbo.UpdatePolicyDetailsDNCNNo is called as a proc, not reimplemented —
    ///     it also stamps Field19 = 'DEBITED'.
    ///  4. Posting side effects (coinsurance notes, ORC notes, commission CN, leader
    ///     row) were commented out in the VB. They are implemented here but gated
    ///     behind explicit flags on the request.
    /// </summary>
    public class GroupLifeDebitNoteService
    {
        private readonly DataContext _db;
        private readonly AutoNumberService _autoNumber;
        //private readonly IAuditTrailService _audit;
        private readonly GibsSettings _settings;
        private readonly GroupLifeOptions _options;
        private readonly ILogger<GroupLifeDebitNoteService> _logger;

        // ---- Schema constants (confirmed against the live model/DDL) ---------
        // Legacy "trans-id" is PolicyDetails.DetailID (bigint identity).
        private const string PolicyDetailKeyColumn = "DetailID";
        private const string MembersTable = "GrpMembers";
        // ---------------------------------------------------------------------

        public GroupLifeDebitNoteService(
            DataContext db,
            AutoNumberService autoNumber,
            //IAuditTrailService audit,
            IOptions<GibsSettings> settings,
            IOptions<GroupLifeOptions> options,
            ILogger<GroupLifeDebitNoteService> logger)
        {
            _db = db;
            _autoNumber = autoNumber;
            //_audit = audit;
            _settings = settings?.Value ?? new GibsSettings();
            _options = options?.Value ?? new GroupLifeOptions();
            _logger = logger;
        }

        // =================================================================
        // READ
        // =================================================================

        public async Task<DebitNotePrefillDto> GetPrefillAsync(
            string policyNo, long? detailId, string riskClass, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                throw new BusinessRuleException("Policy number is required.");

            var conn = await OpenConnectionAsync(ct);

            var policy = await GetPolicyAsync(conn, null, policyNo);
            if (policy == null)
                throw new NotFoundException($"Policy '{policyNo}' was not found.");

            PolicyDetailRow detail = null;
            if (detailId.HasValue)
            {
                detail = await GetPolicyDetailAsync(conn, null, detailId.Value);
                if (detail == null)
                    throw new NotFoundException($"Policy detail '{detailId}' was not found.");

                if (!string.Equals(detail.PolicyNo, policy.PolicyNo, StringComparison.OrdinalIgnoreCase))
                    throw new BusinessRuleException($"Policy detail '{detailId}' does not belong to policy '{policyNo}'.");
            }
            else
            {
                // No specific endorsement requested — default to the latest section on this
                // policy. Without this, the prefill silently fell back to Policies.SumInsured/
                // GrossPremium, which are always 0 for group life (the real figures live on
                // PolicyDetails), producing a form that looked valid but was all zeros.
                detail = await GetLatestPolicyDetailAsync(conn, null, policyNo);
            }

            var dto = new DebitNotePrefillDto
            {
                DetailId = detail?.DetailID,
                DebitNoteNo = "[AUTO]",
                PolicyNo = policy.PolicyNo,
                CoPolicyNo = policy.CoPolicyNo,
                ReceiptNo = detail?.EndorsementNo,
                BranchId = policy.BranchID,
                BizSource = policy.BizSource,
                BizOption = "NEW",
                NoteType = "DN",
                SubRiskId = policy.SubRiskID,
                SubRisk = policy.SubRisk,
                RiskClass = riskClass,

                BillingDate = detail?.EntryDate ?? DateTime.Today,
                StartDate = detail?.StartDate ?? policy.StartDate,
                EndDate = detail?.EndDate ?? policy.EndDate,

                PartyId = policy.PartyID,
                PartyName = policy.Party,
                MktStaffId = policy.MktStaffID,
                MktStaffName = policy.MktStaff,
                InsuredId = policy.InsuredID,
                InsuredName = policy.InsuredFullName,

                TotalRiskSumInsured = detail?.TotalRiskValue ?? 0m,
                TotalRiskPremium = detail?.A20 ?? 0m,
                TotalTrackingCost = detail?.A18 ?? 0m,
                TopMostLocationValue = detail?.A10 ?? 0m,
                MplValue = detail?.A2 ?? 0m,

                SumInsured = detail?.SumInsured ?? 0m,
                GrossPremium = detail?.GrossPremium ?? 0m,
                OurSharePercent = (decimal)(detail?.ProportionRate ?? 100d),

                ExRate = detail?.ExRate ?? policy.ExRate ?? 0d,
                ExCurrency = detail?.ExCurrency ?? policy.ExCurrency,
                SumInsuredFrgn = detail?.SumInsuredFrgn ?? 0m,
                GrossPremiumFrgn = detail?.GrossPremiumFrgn ?? 0m,

                Narration = "BEING FIRST PREMIUM DUE ON POLICY"
            };

            var isDirectSource = IsDirectSourceType(policy.SourceType);
            var orcRate = policy.BizSource == BusinessSource.Accepted ? _options.DefaultOrcRate : 0m;

            dto.Premium = GroupLifeCalculator.ApplyRates(
                grossPremium: dto.GrossPremium,
                commissionRate: 0m,
                whtRate: 0m,
                orcRate: orcRate);

            dto.Ui = BuildUiFlags(policy.BizSource, policy.SourceType, riskClass,
                dto.ExCurrency, approval: null, isDirectSource);

            // Legacy loaded the saved coinsurance grid keyed on the endorsement no.
            if (detail != null)
            {
                // No note number exists yet, so the grid is prefilled from the most
                // recent participation split on this policy.
                var priorSplit = await GetLatestParticipationsForPolicyAsync(conn, null, detail.PolicyNo);
                dto.CoinsuranceLines = priorSplit.Select(MapParticipation).ToList();
            }

            // BindGridDescription(): 40 blank rows seeded with default rates.
            if (dto.Ui.ShowDistributionSection)
                dto.DistributionTemplate = BuildDistributionTemplate(orcRate);

            return dto;
        }

        /// <summary>
        /// Paged list. Head notes only by default: a child note (commission CN,
        /// participant CN, ORC DN) has refDNCNNo pointing at its parent, so
        /// including them makes totals on a list screen double-count.
        /// </summary>
        public async Task<Models.PagedResult<DebitNoteListItemDto>> ListAsync(
            DebitNoteListQuery query, CancellationToken ct = default)
        {
            query ??= new DebitNoteListQuery();

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 25 : (query.PageSize > 200 ? 200 : query.PageSize);

            var conn = await OpenConnectionAsync(ct);

            var sql = $@"
                SELECT  COUNT(*) OVER()                       AS TotalCount,
                        DNCNNo, refDNCNNo, PolicyNo, ReceiptNo, NoteType, BizSource, BizOption,
                        PaymentType, BranchID, BillingDate, PartyID, Party, InsuredName,
                        ISNULL(SumInsured, 0)   AS SumInsured,
                        ISNULL(GrossPremium, 0) AS GrossPremium,
                        ISNULL(Commission, 0)   AS Commission,
                        ISNULL(NetAmount, 0)    AS NetAmount,
                        Approval, SubmittedBy, SubmittedOn
                FROM    DNCNNotes
                WHERE   ISNULL(Deleted, 0) = 0
                  AND   (@NoteType     IS NULL OR NoteType    = @NoteType)
                  AND   (@PolicyNo     IS NULL OR PolicyNo    = @PolicyNo)
                  AND   (@BranchId     IS NULL OR BranchID    = @BranchId)
                  AND   (@BizSource    IS NULL OR BizSource   = @BizSource)
                  AND   (@PaymentType  IS NULL OR PaymentType = @PaymentType)
                  AND   (@Approval     IS NULL OR ISNULL(Approval, 0) = @Approval)
                  AND   (@BillingFrom  IS NULL OR BillingDate >= @BillingFrom)
                  AND   (@BillingToExclusive IS NULL OR BillingDate < @BillingToExclusive)
                  AND   (@HeadNotesOnly = 0 OR ISNULL(refDNCNNo, DNCNNo) = DNCNNo)
                  AND   (@PendingOnly  IS NULL
                         OR (@PendingOnly = 1 AND DNCNNo LIKE '{PendingPrefix}%')
                         OR (@PendingOnly = 0 AND DNCNNo NOT LIKE '{PendingPrefix}%'))
                  AND   (@SearchLike IS NULL
                         OR DNCNNo      LIKE @SearchLike
                         OR Party       LIKE @SearchLike
                         OR InsuredName LIKE @SearchLike
                         OR PolicyNo    LIKE @SearchLike)
                ORDER BY SubmittedOn DESC, DNCNNo DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var rows = (await conn.QueryAsync<DebitNoteListRow>(new CommandDefinition(sql, new
            {
                NoteType = Blank(query.NoteType),
                PolicyNo = Blank(query.PolicyNo),
                BranchId = Blank(query.BranchId),
                BizSource = Blank(query.BizSource),
                PaymentType = Blank(query.PaymentType),
                query.Approval,
                query.BillingFrom,
                // Callers pass a day, not an instant, so the end date is inclusive.
                BillingToExclusive = query.BillingTo?.Date.AddDays(1),
                HeadNotesOnly = query.HeadNotesOnly ? 1 : 0,
                PendingOnly = query.PendingOnly.HasValue ? (query.PendingOnly.Value ? 1 : 0) : (int?)null,
                SearchLike = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search.Trim()}%",
                Offset = (page - 1) * pageSize,
                PageSize = pageSize
            }, cancellationToken: ct))).ToList();

            return new Models.PagedResult<DebitNoteListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = rows.Count == 0 ? 0 : rows[0].TotalCount,
                Items = rows.Select(MapListItem).ToList()
            };
        }

        /// <summary>Child notes raised against a head note.</summary>
        public async Task<List<DebitNoteListItemDto>> ListChildrenAsync(
            string dncnNo, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dncnNo))
                throw new BusinessRuleException("Debit note number is required.");

            var conn = await OpenConnectionAsync(ct);

            var rows = await conn.QueryAsync<DebitNoteListRow>(new CommandDefinition(@"
                SELECT  0 AS TotalCount,
                        DNCNNo, refDNCNNo, PolicyNo, ReceiptNo, NoteType, BizSource, BizOption,
                        PaymentType, BranchID, BillingDate, PartyID, Party, InsuredName,
                        ISNULL(SumInsured, 0)   AS SumInsured,
                        ISNULL(GrossPremium, 0) AS GrossPremium,
                        ISNULL(Commission, 0)   AS Commission,
                        ISNULL(NetAmount, 0)    AS NetAmount,
                        Approval, SubmittedBy, SubmittedOn
                FROM    DNCNNotes
                WHERE   refDNCNNo = @dncnNo AND DNCNNo <> @dncnNo AND ISNULL(Deleted, 0) = 0
                ORDER BY NoteType, PaymentType, DNCNNo",
                new { dncnNo }, cancellationToken: ct));

            return rows.Select(MapListItem).ToList();
        }

        private static string Blank(string v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private static DebitNoteListItemDto MapListItem(DebitNoteListRow r) => new DebitNoteListItemDto
        {
            DebitNoteNo = r.DNCNNo,
            RefDebitNoteNo = r.refDNCNNo,
            PolicyNo = r.PolicyNo,
            ReceiptNo = r.ReceiptNo,
            NoteType = r.NoteType,
            BizSource = r.BizSource,
            BizOption = r.BizOption,
            PaymentType = r.PaymentType,
            BranchId = r.BranchID,
            BillingDate = r.BillingDate,
            PartyId = r.PartyID,
            PartyName = r.Party,
            InsuredName = r.InsuredName,
            SumInsured = r.SumInsured,
            GrossPremium = r.GrossPremium,
            Commission = r.Commission,
            NetAmount = r.NetAmount,
            Approval = r.Approval,
            ApprovalStatus = DescribeApproval(r.Approval),
            IsInvoice = r.DNCNNo != null &&
                        r.DNCNNo.StartsWith(PendingPrefix, StringComparison.OrdinalIgnoreCase),
            IsHeadNote = string.Equals(r.refDNCNNo ?? r.DNCNNo, r.DNCNNo, StringComparison.OrdinalIgnoreCase),
            SubmittedBy = r.SubmittedBy,
            SubmittedOn = r.SubmittedOn
        };

        private static string DescribeApproval(byte? approval) => approval switch
        {
            null => "Pending",
            0 => "Pending",
            1 => "Approved",
            2 => "Posted to ledger",
            _ => $"Status {approval}"
        };

        private class DebitNoteListRow
        {
            public int TotalCount { get; set; }
            public string DNCNNo { get; set; }
            public string refDNCNNo { get; set; }
            public string PolicyNo { get; set; }
            public string ReceiptNo { get; set; }
            public string NoteType { get; set; }
            public string BizSource { get; set; }
            public string BizOption { get; set; }
            public string PaymentType { get; set; }
            public string BranchID { get; set; }
            public DateTime? BillingDate { get; set; }
            public string PartyID { get; set; }
            public string Party { get; set; }
            public string InsuredName { get; set; }
            public decimal SumInsured { get; set; }
            public decimal GrossPremium { get; set; }
            public decimal Commission { get; set; }
            public decimal NetAmount { get; set; }
            public byte? Approval { get; set; }
            public string SubmittedBy { get; set; }
            public DateTime? SubmittedOn { get; set; }
        }

        public async Task<DebitNoteDto> GetAsync(
            string dncnNo, string riskClass = null, CancellationToken ct = default)
        {
            var conn = await OpenConnectionAsync(ct);

            var note = await GetNoteAsync(conn, null, dncnNo);
            if (note == null)
                throw new NotFoundException($"Debit note '{dncnNo}' was not found.");

            var policy = await GetPolicyAsync(conn, null, note.PolicyNo);
            var dto = MapNote(note, policy, riskClass);

            var participations = await GetParticipationsAsync(conn, null, note.DNCNNo);
            dto.CoinsuranceLines = participations.Select(MapParticipation).ToList();

            return dto;
        }

        public async Task<DebitNoteDto> RefreshFromPolicyDetailAsync(
            string dncnNo, string riskClass = null, CancellationToken ct = default)
        {
            var conn = await OpenConnectionAsync(ct);

            var note = await GetNoteAsync(conn, null, dncnNo);
            if (note == null)
                throw new NotFoundException($"Debit note '{dncnNo}' was not found.");

            var policy = await GetPolicyAsync(conn, null, note.PolicyNo);
            var detail = await GetPolicyDetailByInvoiceAsync(conn, null, note.DNCNNo);

            var dto = MapNote(note, policy, riskClass);

            // cmdVerify overrode the money fields from PolicyDetails and the
            // broker from the policy header — that is the whole point of the button.
            if (detail != null)
            {
                dto.SumInsured = detail.SumInsured ?? 0m;
                dto.GrossPremium = detail.GrossPremium ?? 0m;
                dto.TotalRiskSumInsured = detail.SumInsured ?? 0m;
                dto.TotalRiskPremium = detail.GrossPremium ?? 0m;
                dto.Premium.GrossPremium = dto.GrossPremium;
            }

            if (policy != null)
            {
                dto.PartyId = policy.PartyID;
                dto.PartyName = policy.Party;
            }

            var participations = await GetParticipationsAsync(conn, null, note.DNCNNo);
            dto.CoinsuranceLines = participations.Select(MapParticipation).ToList();

            return dto;
        }

        // =================================================================
        // CALCULATE
        // =================================================================

        public PremiumBreakdownDto Calculate(CalculatePremiumRequest request)
        {
            if (request == null)
                throw new BusinessRuleException("Request body is required.");

            var breakdown = GroupLifeCalculator.ApplyRates(
                request.GrossPremium,
                request.CommissionRate,
                request.WhtRate,
                request.OrcRate,
                request.NcdfRate,
                request.NaicomLevyRate,
                request.DeductOrcFromNet);

            return breakdown;
        }

        public DistributionResultDto Distribute(DistributeRequest request)
        {
            if (request == null)
                throw new BusinessRuleException("Request body is required.");

            var result = GroupLifeCalculator.Distribute(
                request.TotalRiskSumInsured,
                request.TotalRiskPremium,
                request.OurSharePercent,
                request.Lines,
                request.OurGrossPremium);

            // Still a 200: the client needs the computed lines even when unbalanced,
            // exactly as the grid re-bound before showing the message box.
            return result;
        }

        // =================================================================
        // CREATE  (btnOk_Click -> DoInsert, "new" branch)
        // =================================================================

        public async Task<CreateDebitNoteResponse> CreateAsync(
            CreateDebitNoteRequest req, string username, CancellationToken ct = default)
        {
            if (req == null)
                throw new BusinessRuleException("Request body is required.");

            // ---- btnOk_Click guards -------------------------------------
            if (string.IsNullOrWhiteSpace(req.PaymentType) ||
                req.PaymentType.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Payment option cannot be empty.");

            if (string.IsNullOrWhiteSpace(req.CreditRefNo))
                throw new BusinessRuleException("Broker's credit advice or credit note number cannot be empty.");

            // CurrentAccountPeriod is nullable: null means the period check is off.
            if (_settings.CurrentAccountPeriod.HasValue &&
                _settings.CurrentAccountPeriod.Value.Date > req.BillingDate.Date)
            {
                throw new BusinessRuleException(
                    "The accounting period has been closed. Please check the transaction date.");
            }

            // EnableRetryOnFailure is configured on the DbContext, so a
            // transaction we begin ourselves must run inside the execution
            // strategy: on a transient fault the WHOLE delegate is retried as
            // one retriable unit. Everything below is therefore re-runnable —
            // the transaction rolls back before each retry, so a retry consumes
            // a fresh autonumber and the previous one is left as a gap.
            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                var conn = await OpenConnectionAsync(ct);

                // One transaction, begun on the DbContext so that AutoNumberService's
                // SaveChangesAsync enlists in it. Begun via Dapper instead, the number
                // would commit independently and survive a rolled-back posting.
                await using var efTx = await _db.Database.BeginTransactionAsync(ct);
                var tx = efTx.GetDbTransaction();

                var policy = await GetPolicyAsync(conn, tx, req.PolicyNo);
                if (policy == null)
                    throw new NotFoundException($"Policy '{req.PolicyNo}' was not found.");

                PolicyDetailRow detail = null;
                if (req.DetailId.HasValue)
                {
                    detail = await GetPolicyDetailAsync(conn, tx, req.DetailId.Value);
                    if (detail == null)
                        throw new NotFoundException($"Policy detail '{req.DetailId}' was not found.");

                    if (!string.Equals(detail.PolicyNo, policy.PolicyNo, StringComparison.OrdinalIgnoreCase))
                        throw new BusinessRuleException($"Policy detail '{req.DetailId}' does not belong to policy '{req.PolicyNo}'.");

                    // Group member premium reconciliation. Only enforced when the
                    // endorsement actually has member rows (individual/non-group
                    // business returns NULL and is skipped, matching the VB).
                    var membersTotal = await GetMemberPremiumTotalAsync(
                        conn, tx, detail.PolicyNo, detail.EndorsementNo);

                    if (membersTotal.HasValue &&
                        GroupLifeCalculator.R2(req.GrossPremium) != GroupLifeCalculator.R2(membersTotal.Value))
                    {
                        throw new BusinessRuleException("The premium on this debit note does not match the sum of member premiums. " +
                            $"Submitted: {GroupLifeCalculator.R2(req.GrossPremium)}, " +
                            $"System: {GroupLifeCalculator.R2(membersTotal.Value)}.");
                    }
                }

                // Legacy recalculation: when no commission was captured the page
                // recomputed WHT/ORC/net server-side before saving, and that branch
                // is the one that nets ORC off the premium.
                var premium = req.CommissionAmount == 0m
                    ? GroupLifeCalculator.ApplyRates(req.GrossPremium, 0m, req.WhtRate, req.OrcRate,
                        deductOrcFromNet: true)
                    : new PremiumBreakdownDto
                    {
                        GrossPremium = GroupLifeCalculator.R2(req.GrossPremium),
                        CommissionRate = req.CommissionRate,
                        CommissionAmount = GroupLifeCalculator.R2(req.CommissionAmount),
                        WhtRate = req.WhtRate,
                        WhtAmount = GroupLifeCalculator.R2(req.WhtAmount),
                        OrcRate = req.OrcRate,
                        OrcAmount = GroupLifeCalculator.R2(req.OrcAmount),
                        NetPremium = GroupLifeCalculator.R2(req.NetPremium)
                    };

                // Outward split must balance before anything is posted.
                var coinsuranceLines = new DistributionResultDto();
                var hasCoinsuranceLines = req.CoinsuranceLines != null && req.CoinsuranceLines.Any(
                    c => !string.IsNullOrWhiteSpace(c.ParticipantId));

                if (hasCoinsuranceLines)
                {
                    coinsuranceLines = GroupLifeCalculator.Distribute(
                        req.TotalRiskSumInsured, req.TotalRiskPremium, req.OurSharePercent,
                        req.CoinsuranceLines, req.GrossPremium);

                    foreach (var warning in coinsuranceLines.Warnings)
                        _logger.LogWarning("Policy {PolicyNo}: {Warning}", req.PolicyNo, warning);

                    if (!coinsuranceLines.IsBalanced)
                        throw new BusinessRuleException(string.Join(" ", coinsuranceLines.Errors));
                }

                // ---- AutoNumber -----------------------------------------
                // Runs on this transaction (same DataContext), so a rolled-back
                // posting also rolls back the consumed sequence.
                //
                // CONCURRENCY: AutoNumberService reads the row, increments in
                // memory and saves. Two concurrent postings on the same branch can
                // read the same NextValue and produce a duplicate DNCNNo. Nothing
                // in the schema stops it — DNCNNo is the PK, so the loser gets a
                // PK violation and their whole posting is rolled back. See
                // MIGRATION-NOTES.md for the UPDLOCK fix.
                var debitNoteNo = await _autoNumber.GetNextAsync(
                    AutoNumberType.DNote, req.BranchId, policy.SubRiskID, ct);

                if (string.IsNullOrWhiteSpace(debitNoteNo))
                {
                    throw new BusinessRuleException("AutoNumber settings are invalid. Data was NOT saved.");
                }

                // Raised as an invoice; the prefix flips to DN on approval.
                debitNoteNo = ToPendingNumber(debitNoteNo);

                var endorsementNo = detail?.EndorsementNo ?? string.Empty;

                var note = new DNCNNote
                {
                    DNCNNo = debitNoteNo,
                    refDNCNNo = debitNoteNo,          // legacy: self-referencing on the head note
                    ReceiptNo = endorsementNo,
                    PolicyNo = req.PolicyNo,
                    CoPolicyNo = policy.CoPolicyNo,
                    BranchID = req.BranchId,
                    BizSource = req.BizSource,
                    BizOption = string.IsNullOrWhiteSpace(req.BizOption) ? "NEW" : req.BizOption,
                    NoteType = "DN",
                    BillingDate = req.BillingDate,
                    SubRiskID = policy.SubRiskID,
                    SubRisk = policy.SubRisk,
                    PartyID = policy.PartyID,
                    Party = policy.Party,
                    PartyRate = premium.CommissionRate,
                    InsuredID = policy.InsuredID,
                    InsuredName = req.InsuredName ?? policy.InsuredFullName,
                    StartDate = req.StartDate,
                    EndDate = req.EndDate,
                    MktStaffID = policy.MktStaffID,
                    MktStaff = policy.MktStaff,
                    SumInsured = GroupLifeCalculator.R2(req.SumInsured),
                    GrossPremium = premium.GrossPremium,
                    Commission = premium.CommissionAmount,
                    // VatAmount is bigint: WHT is stored to the naira.
                    VatAmount = (long)Math.Round(premium.WhtAmount, 0, MidpointRounding.AwayFromZero),
                    PropRate = (double)req.OurSharePercent,
                    ProRataDays = 0,
                    ProRataPremium = premium.GrossPremium,
                    NetAmount = premium.NetPremium,
                    RetProp = premium.OrcRate,
                    RetPremium = premium.OrcAmount,
                    RetValue = 0m,
                    TotalRiskValue = GroupLifeCalculator.R2(req.TotalRiskSumInsured),
                    TotalPremium = GroupLifeCalculator.R2(req.TotalRiskPremium),
                    Narration = req.Narration,
                    ExRate = req.ExRate,
                    ExCurrency = req.ExCurrency,
                    SumInsuredFrgn = GroupLifeCalculator.R2(req.SumInsuredFrgn),
                    GrossPremiumFrgn = GroupLifeCalculator.R2(req.GrossPremiumFrgn),
                    PaymentType = req.PaymentType,
                    ChequeNo = req.CreditRefNo,
                    Transguid = req.TransGuid,
                    HasTreaty = 0,
                    Deleted = 0,
                    Active = 1,
                    Remarks = "INVOICE",
                    Approval = 0,
                    // Legacy Rec.Z_NAICOM_STATUS = "QUEUED": this is what the NAICOM
                    // submission job picks up. Dropping it silently stops regulatory
                    // reporting for every note raised through this endpoint.
                    Z_NAICOM_STATUS = "QUEUED",
                    SubmittedBy = username,
                    SubmittedOn = DateTime.Now,
                    DBDate = req.BillingDate
                };

                await InsertNoteAsync(conn, tx, note);

                var response = new CreateDebitNoteResponse
                {
                    DebitNoteNo = debitNoteNo,
                    PolicyNo = req.PolicyNo,
                    EndorsementNo = endorsementNo
                };

                // ---- Optional postings ----------------------------------
                if (req.PostCommissionCreditNote && premium.CommissionAmount > 0m)
                {
                    response.CommissionCreditNote = await PostCommissionCreditNoteAsync(
                        conn, tx, ct, note, policy, premium, username);
                }

                if (hasCoinsuranceLines && req.PostCoinsuranceCreditNotes)
                {
                    response.CoinsuranceCreditNotes = await PostCoinsuranceCreditNotesAsync(
                        conn, tx, ct, note, policy, coinsuranceLines.Lines, username);
                }

                if (hasCoinsuranceLines && req.PostCoinsuranceOrcDebitNotes)
                {
                    response.CoinsuranceOrcDebitNotes = await PostCoinsuranceOrcDebitNotesAsync(
                        conn, tx, ct, note, policy, coinsuranceLines.Lines, username);
                }

                if (hasCoinsuranceLines && req.PostLeadOrcCreditNotes)
                {
                    response.LeadOrcCreditNotes = await PostLeadOrcCreditNotesAsync(
                        conn, tx, ct, note, policy, coinsuranceLines.Lines, username);
                }

                // UNCONDITIONAL when there are lines. Coins_Leaders is the only
                // record of who carries the risk, so the split is never optional —
                // the child credit notes are the money, these rows are the split.
                // A fully retained direct note has no lines and writes nothing.
                if (hasCoinsuranceLines)
                {
                    // One row per participant only. Our own retention is NOT written
                    // here — it already lives on the head note (SumInsured,
                    // GrossPremium, PropRate), and duplicating it produced a second
                    // LEAD row that made Tag useless for identifying the lead carrier.
                    foreach (var line in coinsuranceLines.Lines)
                    {
                        await InsertLeaderRowAsync(conn, tx, new CoinsLeaderRow
                        {
                            DNCNNo = debitNoteNo,
                            PolicyNo = req.PolicyNo,
                            LeaderID = line.ParticipantId,
                            Leader = line.ParticipantName,
                            TotalRiskValue = GroupLifeCalculator.R2(req.TotalRiskSumInsured),
                            TotalPremium = GroupLifeCalculator.R2(req.TotalRiskPremium),
                            RetValue = line.ShareSumInsured,
                            RetPremium = line.SharePremium,
                            RetProp = line.SharePercent,
                            Remarks = "INVOICE",
                            // LEAD marks the lead carrier among the participants,
                            // FOLLOWING the rest. Exactly one line should be flagged;
                            // the calculator rejects more than one.
                            Tag = line.IsLead ? "LEAD" : "FOLLOWING"
                        });
                        response.ParticipationRowsWritten++;
                    }
                }

                // ---- Policy header + detail sync ------------------------
                await UpdatePolicyInsuredDetailsAsync(conn, tx, note);

                if (!string.IsNullOrWhiteSpace(endorsementNo))
                {
                    var stamped = await UpdatePolicyDetailNoteNoAsync(
                        conn, tx, req.PolicyNo, endorsementNo, debitNoteNo);

                    // The VB read the context method's return as an error code and
                    // rolled back on non-zero. Here the value is rows affected, so
                    // the meaningful failure is zero: the endorsement was not
                    // stamped DEBITED and the note would be orphaned.
                    if (stamped == 0)
                    {
                        throw new BusinessRuleException($"Endorsement '{endorsementNo}' on policy '{req.PolicyNo}' could not be " +
                            "marked as debited. No changes were made.");
                    }
                }

                //await SafeAuditAsync("NEW", "UNDERWRITING", "DNCN_NOTES",
                //    $"Debit note {debitNoteNo} raised on policy {req.PolicyNo}, gross premium {premium.GrossPremium}",
                //    username, conn, tx);

                await efTx.CommitAsync(ct);
                return response;
            });
        }

        // =================================================================
        // UPDATE  (btnOk_Click -> DoInsert, "edit" branch)
        // =================================================================

        public async Task<string> UpdateAsync(
            string dncnNo, UpdateDebitNoteRequest req, string username, CancellationToken ct = default)
        {
            if (req == null)
                throw new BusinessRuleException("Request body is required.");

            if (string.IsNullOrWhiteSpace(req.PaymentType) ||
                req.PaymentType.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Payment option cannot be empty.");

            if (string.IsNullOrWhiteSpace(req.CreditRefNo))
                throw new BusinessRuleException("Broker's credit advice or credit note number cannot be empty.");

            // CurrentAccountPeriod is nullable: null means the period check is off.
            if (_settings.CurrentAccountPeriod.HasValue &&
                _settings.CurrentAccountPeriod.Value.Date > req.BillingDate.Date)
            {
                throw new BusinessRuleException(
                    "The accounting period has been closed. Please check the transaction date.");
            }

            // EnableRetryOnFailure is configured on the DbContext, so a
            // transaction we begin ourselves must run inside the execution
            // strategy: on a transient fault the WHOLE delegate is retried as
            // one retriable unit. Everything below is therefore re-runnable —
            // the transaction rolls back before each retry, so a retry consumes
            // a fresh autonumber and the previous one is left as a gap.
            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                var conn = await OpenConnectionAsync(ct);

                // One transaction, begun on the DbContext so that AutoNumberService's
                // SaveChangesAsync enlists in it. Begun via Dapper instead, the number
                // would commit independently and survive a rolled-back posting.
                await using var efTx = await _db.Database.BeginTransactionAsync(ct);
                var tx = efTx.GetDbTransaction();

                var existing = await GetNoteAsync(conn, tx, dncnNo);
                if (existing == null)
                    throw new NotFoundException($"Debit note '{dncnNo}' was not found.");

                // Legacy: Approval = 2 means it is already in the ledger.
                if (existing.Approval == 2)
                    throw new BusinessRuleException("This transaction has been processed into the ledger and cannot be edited. Please reverse it.");

                var auditDetails = BuildAuditDelta(existing, req);

                existing.BranchID = req.BranchId;
                existing.BillingDate = req.BillingDate;
                existing.StartDate = req.StartDate;
                existing.EndDate = req.EndDate;
                existing.InsuredName = req.InsuredName;
                existing.Narration = req.Narration;
                existing.Transguid = req.TransGuid;
                existing.PartyID = req.PartyId ?? existing.PartyID;
                existing.Party = req.PartyName ?? existing.Party;
                existing.MktStaffID = req.MktStaffId ?? existing.MktStaffID;
                existing.TotalRiskValue = GroupLifeCalculator.R2(req.TotalRiskSumInsured);
                existing.TotalPremium = GroupLifeCalculator.R2(req.TotalRiskPremium);
                existing.SumInsured = GroupLifeCalculator.R2(req.SumInsured);
                existing.GrossPremium = GroupLifeCalculator.R2(req.GrossPremium);
                existing.Commission = GroupLifeCalculator.R2(req.CommissionAmount);
                existing.VatAmount = (long)Math.Round(req.WhtAmount, 0, MidpointRounding.AwayFromZero);
                existing.PropRate = (double)req.OurSharePercent;
                existing.RetProp = req.OrcRate;
                existing.RetPremium = GroupLifeCalculator.R2(req.OrcAmount);
                existing.NetAmount = GroupLifeCalculator.R2(req.NetPremium);
                existing.ProRataDays = 0;
                existing.ProRataPremium = GroupLifeCalculator.R2(req.GrossPremium);
                existing.ExRate = req.ExRate;
                existing.ExCurrency = req.ExCurrency;
                existing.SumInsuredFrgn = GroupLifeCalculator.R2(req.SumInsuredFrgn);
                existing.GrossPremiumFrgn = GroupLifeCalculator.R2(req.GrossPremiumFrgn);
                existing.PaymentType = req.PaymentType;
                existing.ChequeNo = req.CreditRefNo;
                existing.ModifiedBy = username;
                existing.ModifiedOn = DateTime.Now;

                var rows = await UpdateNoteAsync(conn, tx, existing);
                if (rows == 0)
                {
                    throw new BusinessRuleException("No rows were updated.");
                }

                await UpdatePolicyInsuredDetailsAsync(conn, tx, existing);

                //await SafeAuditAsync("EDIT", "UNDERWRITING", "DNCN_NOTES",
                //    auditDetails, username, conn, tx);

                await efTx.CommitAsync(ct);
                return dncnNo;
            });
        }

        // =================================================================
        // APPROVE  (btnApprove — flips INV to DN)
        // =================================================================

        public async Task<ApproveDebitNoteResponse> ApproveAsync(
            string dncnNo, byte approvalStatus, string username, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dncnNo))
                throw new BusinessRuleException("Debit note number is required.");

            if (approvalStatus == 0)
                throw new BusinessRuleException("Approval status must be greater than zero.");

            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                var conn = await OpenConnectionAsync(ct);
                await using var efTx = await _db.Database.BeginTransactionAsync(ct);
                var tx = efTx.GetDbTransaction();

                var note = await GetNoteAsync(conn, tx, dncnNo);
                if (note == null)
                    throw new NotFoundException($"Debit note '{dncnNo}' was not found.");

                if (note.NoteType != "DN")
                    throw new BusinessRuleException(
                        $"'{dncnNo}' is a {note.NoteType}, not a debit note. Only debit notes carry the INV prefix.");

                if (note.Approval >= approvalStatus)
                    throw new BusinessRuleException(
                        $"'{dncnNo}' is already at approval status {note.Approval}.");

                var approvedNo = ToApprovedNumber(dncnNo);
                var renamed = !string.Equals(approvedNo, dncnNo, StringComparison.Ordinal);

                if (renamed)
                {
                    // The number is the primary key and is copied into other tables,
                    // so this is a rename, not an update. Every known holder moves in
                    // the same transaction. See MIGRATION-NOTES.md for the query that
                    // finds holders this code does not know about.
                    var clash = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                        "SELECT COUNT(*) FROM DNCNNotes WHERE DNCNNo = @approvedNo",
                        new { approvedNo }, tx));

                    if (clash > 0)
                        throw new BusinessRuleException(
                            $"Cannot approve: '{approvedNo}' already exists. The INV and DN sequences have collided.");

                    // Children first, while they can still be found by the old number.
                    await conn.ExecuteAsync(new CommandDefinition(
                        "UPDATE DNCNNotes SET refDNCNNo = @approvedNo WHERE refDNCNNo = @dncnNo",
                        new { approvedNo, dncnNo }, tx));

                    await conn.ExecuteAsync(new CommandDefinition(
                        "UPDATE PolicyDetails SET DNCNNo = @approvedNo WHERE DNCNNo = @dncnNo",
                        new { approvedNo, dncnNo }, tx));

                    await conn.ExecuteAsync(new CommandDefinition(
                        "UPDATE Coins_Leaders SET DNCNNo = @approvedNo WHERE DNCNNo = @dncnNo",
                        new { approvedNo, dncnNo }, tx));

                    // The key itself last.
                    await conn.ExecuteAsync(new CommandDefinition(
                        "UPDATE DNCNNotes SET DNCNNo = @approvedNo WHERE DNCNNo = @dncnNo",
                        new { approvedNo, dncnNo }, tx));
                }

                var rows = await conn.ExecuteAsync(new CommandDefinition(@"
                    UPDATE DNCNNotes
                    SET    Approval   = @approvalStatus,
                           ApprovedBy = @username,
                           ApprovedOn = @approvedOn
                    WHERE  DNCNNo = @approvedNo",
                    new { approvalStatus, username, approvedOn = DateTime.Now, approvedNo }, tx));

                if (rows == 0)
                    throw new BusinessRuleException($"Approval of '{dncnNo}' updated no rows.");

                //await SafeAuditAsync("APPROVE", "UNDERWRITING", "DNCN_NOTES",
                //    renamed
                //        ? $"[{dncnNo}] approved at status {approvalStatus} and renumbered to {approvedNo}"
                //        : $"[{dncnNo}] approved at status {approvalStatus}",
                //    username, conn, tx);

                await efTx.CommitAsync(ct);

                return new ApproveDebitNoteResponse
                {
                    PreviousDebitNoteNo = dncnNo,
                    DebitNoteNo = approvedNo,
                    Renumbered = renamed,
                    ApprovalStatus = approvalStatus,
                    ApprovedBy = username
                };
            });
        }

        // =================================================================
        // POSTING SIDE EFFECTS
        // =================================================================

        /// <summary>DoInsertCommissionCreditNote: broker commission CN against the head note.</summary>
        private async Task<string> PostCommissionCreditNoteAsync(
            IDbConnection conn, IDbTransaction tx, CancellationToken ct,
            DNCNNote head, PolicyRow policy, PremiumBreakdownDto premium, string username)
        {
            var cnNo = await _autoNumber.GetNextAsync(
                AutoNumberType.CNote, head.BranchID, head.SubRiskID, ct);

            if (string.IsNullOrWhiteSpace(cnNo))
                throw new InvalidOperationException(
                    "AutoNumber settings are invalid for credit notes. Data was NOT saved.");

            var cn = CloneForChildNote(head);
            cn.DNCNNo = cnNo;
            cn.refDNCNNo = head.DNCNNo;
            cn.NoteType = "CN";
            cn.BizOption = "NEW";
            cn.PaymentType = "COMMISSION";
            cn.Remarks = "COMMISSION";
            cn.SumInsured = 0m;
            cn.GrossPremium = premium.CommissionAmount;
            cn.Commission = premium.CommissionAmount;
            cn.ProRataPremium = premium.CommissionAmount;
            cn.NetAmount = premium.CommissionAmount;
            cn.RetProp = 0m;
            cn.RetPremium = 0m;
            cn.TotalRiskValue = 0m;
            cn.TotalPremium = 0m;
            cn.Narration =
                "Being commission credit note element of business proportion on the above policy no.";
            cn.SubmittedBy = username;
            cn.SubmittedOn = DateTime.Now;

            await InsertNoteAsync(conn, tx, cn);
            return cnNo;
        }

        /// <summary>DoCoinsuranceInsert: one CN per outward participant for their share of premium.</summary>
        private async Task<List<string>> PostCoinsuranceCreditNotesAsync(
            IDbConnection conn, IDbTransaction tx, CancellationToken ct,
            DNCNNote head, PolicyRow policy, IEnumerable<CoinsuranceLineDto> lines, string username)
        {
            var created = new List<string>();

            foreach (var line in lines)
            {
                var cnNo = await _autoNumber.GetNextAsync(
                    AutoNumberType.CNote, policy.BranchID, policy.SubRiskID, ct);

                if (string.IsNullOrWhiteSpace(cnNo))
                    throw new InvalidOperationException(
                        "AutoNumber settings are invalid for credit notes. Data was NOT saved.");

                var cn = CloneForChildNote(head);
                cn.DNCNNo = cnNo;
                cn.refDNCNNo = head.DNCNNo;
                cn.NoteType = "CN";
                cn.BizSource = _options.CoinsuranceBizSource;
                cn.BizOption = "NEW";
                cn.PaymentType = "NORMAL";
                cn.ChequeNo = string.Empty;
                cn.LeaderID = _settings.CompanyAbbrev;
                cn.Leader = CompanyDisplayName;
                cn.PartyID = line.ParticipantId;
                cn.Party = line.ParticipantName;
                cn.PartyRate = line.CommissionRate;
                cn.SumInsured = line.ShareSumInsured;
                cn.GrossPremium = line.SharePremium;
                cn.Commission = line.CommissionAmount;
                // Without this the line's WHT is deducted from NetAmount with no
                // record of it — the same gap the head note had before VatAmount
                // was found on the DDL. Column is bigint, so whole naira.
                cn.VatAmount = (long)Math.Round(line.WhtAmount, 0, MidpointRounding.AwayFromZero);
                cn.RetProp = line.OrcRate;
                cn.RetPremium = line.OrcAmount;
                cn.RetValue = 0m;
                cn.PropRate = (double)line.SharePercent;
                cn.ProRataDays = 0;
                cn.ProRataPremium = 0m;
                cn.NetAmount = line.NetPremium;
                cn.SubmittedBy = username;
                cn.SubmittedOn = DateTime.Now;

                await InsertNoteAsync(conn, tx, cn);
                created.Add(cnNo);
            }

            return created;
        }

        /// <summary>DoCoinsuranceORCInsert: one DN per participant for the ORC receivable.</summary>
        private async Task<List<string>> PostCoinsuranceOrcDebitNotesAsync(
            IDbConnection conn, IDbTransaction tx, CancellationToken ct,
            DNCNNote head, PolicyRow policy, IEnumerable<CoinsuranceLineDto> lines, string username)
        {
            var created = new List<string>();

            foreach (var line in lines)
            {
                var dnNo = await _autoNumber.GetNextAsync(
                    AutoNumberType.DNote, policy.BranchID, policy.SubRiskID, ct);

                if (string.IsNullOrWhiteSpace(dnNo))
                    throw new InvalidOperationException(
                        "AutoNumber settings are invalid for debit notes. Data was NOT saved.");

                // Legacy: Replace(strAutoNumber, "INV", "DN", 1, 3)
                dnNo = ReplaceOccurrences(dnNo, "INV", "DN", 3);

                var dn = CloneForChildNote(head);
                dn.DNCNNo = dnNo;
                dn.refDNCNNo = head.DNCNNo;
                dn.NoteType = "DN";
                dn.BizSource = _options.CoinsuranceBizSource;
                dn.BizOption = "NEW";
                dn.PaymentType = "ORC";
                dn.ChequeNo = string.Empty;
                dn.LeaderID = _settings.CompanyAbbrev;
                dn.Leader = CompanyDisplayName;
                dn.PartyID = line.ParticipantId;
                dn.Party = line.ParticipantName;
                dn.PartyRate = line.OrcRate;
                dn.SumInsured = 0m;
                dn.GrossPremium = line.OrcAmount;
                dn.Commission = line.OrcAmount;
                dn.RetProp = line.OrcRate;
                dn.RetPremium = line.OrcAmount;
                dn.RetValue = 0m;
                dn.PropRate = (double)line.SharePercent;
                dn.ProRataDays = 0;
                dn.ProRataPremium = 0m;
                dn.NetAmount = line.OrcAmount;
                dn.TotalRiskValue = 0m;
                dn.TotalPremium = 0m;
                dn.Narration = $"Being your share {line.OrcRate}% of ORC due on the above policy no.";
                dn.SubmittedBy = username;
                dn.SubmittedOn = DateTime.Now;

                await InsertNoteAsync(conn, tx, dn);
                created.Add(dnNo);
            }

            return created;
        }

        /// <summary>
        /// DoCoinsuranceORCInsertCN: ORC credit note raised to the lead underwriter.
        /// The VB used Exit Function on the first non-lead row, which aborted the whole
        /// loop; here non-lead rows are simply skipped.
        /// </summary>
        private async Task<List<string>> PostLeadOrcCreditNotesAsync(
            IDbConnection conn, IDbTransaction tx, CancellationToken ct,
            DNCNNote head, PolicyRow policy, IEnumerable<CoinsuranceLineDto> lines, string username)
        {
            var created = new List<string>();

            foreach (var line in lines.Where(l => l.IsLead))
            {
                var cnNo = await _autoNumber.GetNextAsync(
                    AutoNumberType.CNote, policy.BranchID, policy.SubRiskID, ct);

                if (string.IsNullOrWhiteSpace(cnNo))
                    throw new InvalidOperationException(
                        "AutoNumber settings are invalid for credit notes. Data was NOT saved.");

                var cn = CloneForChildNote(head);
                cn.DNCNNo = cnNo;
                cn.refDNCNNo = head.DNCNNo;
                cn.NoteType = "CN";
                cn.BizSource = BusinessSource.Accepted;
                cn.BizOption = "NEW";
                cn.PaymentType = "ORC";
                cn.ChequeNo = string.Empty;
                cn.LeaderID = _settings.CompanyAbbrev;
                cn.Leader = CompanyDisplayName;
                cn.PartyID = line.ParticipantId;
                cn.Party = line.ParticipantName;
                cn.PartyRate = line.OrcRate;
                cn.SumInsured = 0m;
                cn.GrossPremium = line.OrcAmount;
                cn.Commission = line.OrcAmount;
                cn.RetProp = line.OrcRate;
                cn.RetPremium = line.OrcAmount;
                cn.RetValue = 0m;
                cn.PropRate = (double)line.SharePercent;
                cn.ProRataDays = 0;
                cn.ProRataPremium = 0m;
                cn.NetAmount = line.OrcAmount;
                cn.TotalRiskValue = 0m;
                cn.TotalPremium = 0m;
                cn.Narration = $"Being your share {line.OrcRate}% of ORC due on the above policy no.";
                cn.SubmittedBy = username;
                cn.SubmittedOn = DateTime.Now;

                await InsertNoteAsync(conn, tx, cn);
                created.Add(cnNo);
            }

            return created;
        }

        // =================================================================
        // DATA ACCESS
        // =================================================================

        private const string NoteColumns = @"
            DNCNNo, refDNCNNo, ReceiptNo, PolicyNo, CoPolicyNo, BranchID, BizSource, BizOption, NoteType,
            BillingDate, SubRiskID, SubRisk, PartyID, Party, PartyRate, InsuredID, InsuredName,
            StartDate, EndDate, MktStaffID, MktStaff, SumInsured, GrossPremium, Commission, VatAmount,
            PropRate, ProRataDays, ProRataPremium, NetAmount, Narration, ExRate, ExCurrency,
            SumInsuredFrgn, GrossPremiumFrgn, Approval, HasTreaty, Remarks, Transguid, PaymentType,
            ChequeNo, Deleted, Active, SubmittedBy, SubmittedOn, ModifiedBy, ModifiedOn, ApprovedBy,
            ApprovedOn, TotalRiskValue, TotalPremium, LeaderID, Leader, RetProp, RetValue, RetPremium,
            DBDate, Z_NAICOM_STATUS";

        private static Task<DNCNNote> GetNoteAsync(IDbConnection c, IDbTransaction t, string dncnNo) =>
            c.QueryFirstOrDefaultAsync<DNCNNote>(new CommandDefinition(
                $"SELECT {NoteColumns} FROM DNCNNotes WHERE DNCNNo = @dncnNo AND ISNULL(Deleted, 0) = 0",
                new { dncnNo }, t));

        private async Task<int> InsertNoteAsync(IDbConnection c, IDbTransaction t, DNCNNote n)
        {
            NormalizeForSchema(n);

            return await c.ExecuteAsync(new CommandDefinition($@"
                INSERT INTO DNCNNotes ({NoteColumns})
                VALUES (
                    @DNCNNo, @refDNCNNo, @ReceiptNo, @PolicyNo, @CoPolicyNo, @BranchID, @BizSource,
                    @BizOption, @NoteType, @BillingDate, @SubRiskID, @SubRisk, @PartyID, @Party,
                    @PartyRate, @InsuredID, @InsuredName, @StartDate, @EndDate, @MktStaffID, @MktStaff,
                    @SumInsured, @GrossPremium, @Commission, @VatAmount, @PropRate, @ProRataDays,
                    @ProRataPremium, @NetAmount, @Narration, @ExRate, @ExCurrency, @SumInsuredFrgn,
                    @GrossPremiumFrgn, @Approval, @HasTreaty, @Remarks, @Transguid, @PaymentType,
                    @ChequeNo, @Deleted, @Active, @SubmittedBy, @SubmittedOn, @ModifiedBy,
                    @ModifiedOn, @ApprovedBy, @ApprovedOn, @TotalRiskValue, @TotalPremium, @LeaderID,
                    @Leader, @RetProp, @RetValue, @RetPremium, @DBDate, @Z_NAICOM_STATUS)", n, t));
        }

        private async Task<int> UpdateNoteAsync(IDbConnection c, IDbTransaction t, DNCNNote n)
        {
            NormalizeForSchema(n);

            return await c.ExecuteAsync(new CommandDefinition(@"
                UPDATE DNCNNotes SET
                    BranchID         = @BranchID,
                    BillingDate      = @BillingDate,
                    StartDate        = @StartDate,
                    EndDate          = @EndDate,
                    PartyID          = @PartyID,
                    Party            = @Party,
                    MktStaffID       = @MktStaffID,
                    MktStaff         = @MktStaff,
                    InsuredName      = @InsuredName,
                    SumInsured       = @SumInsured,
                    GrossPremium     = @GrossPremium,
                    Commission       = @Commission,
                    VatAmount        = @VatAmount,
                    PropRate         = @PropRate,
                    ProRataDays      = @ProRataDays,
                    ProRataPremium   = @ProRataPremium,
                    NetAmount        = @NetAmount,
                    TotalRiskValue   = @TotalRiskValue,
                    TotalPremium     = @TotalPremium,
                    RetProp          = @RetProp,
                    RetPremium       = @RetPremium,
                    Narration        = @Narration,
                    ExRate           = @ExRate,
                    ExCurrency       = @ExCurrency,
                    SumInsuredFrgn   = @SumInsuredFrgn,
                    GrossPremiumFrgn = @GrossPremiumFrgn,
                    PaymentType      = @PaymentType,
                    ChequeNo         = @ChequeNo,
                    Transguid        = @Transguid,
                    ModifiedBy       = @ModifiedBy,
                    ModifiedOn       = @ModifiedOn
                WHERE DNCNNo = @DNCNNo AND ISNULL(Approval, 0) <> 2", n, t));
        }

        /// <summary>
        /// Last stop before any write. The DNCNNotes column types lose data
        /// silently if handed values they cannot hold, so the loss is made
        /// explicit and logged here rather than happening inside the driver.
        /// </summary>
        private void NormalizeForSchema(DNCNNote n)
        {
            // RetProp is decimal(18,0) but holds a *rate* — 2.5% becomes 3.
            if (n.RetProp.HasValue)
            {
                var rounded = Math.Round(n.RetProp.Value, 0, MidpointRounding.AwayFromZero);
                if (rounded != n.RetProp.Value)
                {
                    _logger.LogWarning(
                        "DNCNNotes.RetProp is decimal(18,0): ORC rate {Actual}% will be stored as " +
                        "{Stored}% on note {DNCNNo}. It will not reconcile against RetPremium, " +
                        "which was computed at the true rate.",
                        n.RetProp.Value, rounded, n.DNCNNo);
                }
                n.RetProp = rounded;
            }

            // VatAmount is bigint — kobo are lost on the WHT figure.
            // (Requires `public long? VatAmount` on the DNCNNote model.)

            n.SubRisk = Truncate(n.SubRisk, 64);
            n.Party = Truncate(n.Party, 256);
            n.InsuredName = Truncate(n.InsuredName, 256);
            n.MktStaff = Truncate(n.MktStaff, 256);
            n.Narration = Truncate(n.Narration, 1028);
            n.Remarks = Truncate(n.Remarks, 256);
            n.Transguid = Truncate(n.Transguid, 50);
            n.ChequeNo = Truncate(n.ChequeNo, 64);
            n.ExCurrency = Truncate(n.ExCurrency, 16);
            n.LeaderID = Truncate(n.LeaderID, 64);
            n.Leader = Truncate(n.Leader, 512);
            n.SubmittedBy = Truncate(n.SubmittedBy, 128);
            n.ModifiedBy = Truncate(n.ModifiedBy, 128);
        }

        private static Task<PolicyRow> GetPolicyAsync(IDbConnection c, IDbTransaction t, string policyNo) =>
            c.QueryFirstOrDefaultAsync<PolicyRow>(new CommandDefinition(@"
                SELECT PolicyNo, CoPolicyNo, BranchID, BizSource, SourceType, SubRiskID, SubRisk,
                       PartyID, Party, MktStaffID, MktStaff, MktGroupID, MktGroup,
                       InsuredID, InsSurname, InsFirstname,
                       InsOthernames, TrackID, SumInsured, GrossPremium, StartDate, EndDate,
                       ExRate, ExCurrency
                FROM Policies
                WHERE PolicyNo = @policyNo", new { policyNo }, t));

        private static readonly string PolicyDetailColumns = @"
            DetailID, PolicyNo, CoPolicyNo, EndorsementNo, BizOption, DNCNNo, InsuredName, Field1,
            StartDate, EndDate, EntryDate, SumInsured, GrossPremium, NetAmount, TotalRiskValue,
            SumInsuredFrgn, GrossPremiumFrgn, ProportionRate, ExRate, ExCurrency, A2, A10, A18, A20";

        private static Task<PolicyDetailRow> GetPolicyDetailAsync(
            IDbConnection c, IDbTransaction t, long detailId) =>
            c.QueryFirstOrDefaultAsync<PolicyDetailRow>(new CommandDefinition(
                $"SELECT {PolicyDetailColumns} FROM PolicyDetails WHERE {PolicyDetailKeyColumn} = @detailId",
                new { detailId }, t));

        private static Task<PolicyDetailRow> GetPolicyDetailByInvoiceAsync(
            IDbConnection c, IDbTransaction t, string dncnNo) =>
            c.QueryFirstOrDefaultAsync<PolicyDetailRow>(new CommandDefinition(
                $@"SELECT TOP(1) {PolicyDetailColumns} FROM PolicyDetails
                   WHERE DNCNNo = @dncnNo AND ISNULL(Deleted, 0) = 0
                   ORDER BY DetailID DESC",
                new { dncnNo }, t));

        /// <summary>
        /// Used by GetPrefillAsync when no detailId is supplied: defaults the
        /// prefill to the most recent section/endorsement on the policy instead
        /// of falling back to Policies.SumInsured/GrossPremium, which are always
        /// zero for group life (those columns are deliberately not used by that
        /// module — see PolicyService.MapCommonFields).
        /// </summary>
        private static Task<PolicyDetailRow> GetLatestPolicyDetailAsync(
            IDbConnection c, IDbTransaction t, string policyNo) =>
            c.QueryFirstOrDefaultAsync<PolicyDetailRow>(new CommandDefinition(
                $@"SELECT TOP(1) {PolicyDetailColumns} FROM PolicyDetails
                   WHERE PolicyNo = @policyNo AND ISNULL(Deleted, 0) = 0
                   ORDER BY DetailID DESC",
                new { policyNo }, t));

        /// <summary>
        /// Wraps dbo.UpdatePolicyDetailsDNCNNo. The proc sets DNCNNo *and*
        /// Field19 = 'DEBITED' — the second half is what marks the endorsement as
        /// invoiced, so this must not be reimplemented as a plain UPDATE.
        /// </summary>
        private static Task<int> UpdatePolicyDetailNoteNoAsync(
            IDbConnection c, IDbTransaction t, string policyNo, string endorsementNo, string dncnNo) =>
            c.ExecuteAsync(new CommandDefinition(
                "dbo.UpdatePolicyDetailsDNCNNo",
                new { PolicyNo = policyNo, EndorsementNo = endorsementNo, DNCNNo = dncnNo },
                t, commandType: CommandType.StoredProcedure));

        private static Task<int> UpdatePolicyInsuredDetailsAsync(
            IDbConnection c, IDbTransaction t, DNCNNote n) =>
            c.ExecuteAsync(new CommandDefinition(@"
                UPDATE Policies SET
                    SumInsured   = @SumInsured,
                    GrossPremium = @GrossPremium,
                    StartDate    = @StartDate,
                    EndDate      = @EndDate,
                    MktStaffID   = @MktStaffID,
                    MktStaff     = @MktStaff,
                    PartyID      = @PartyID,
                    Party        = @Party,
                    BranchID     = @BranchID,
                    ExCurrency   = @ExCurrency,
                    ExRate       = @ExRate
                WHERE PolicyNo = @PolicyNo",
                new
                {
                    n.SumInsured,
                    n.GrossPremium,
                    n.StartDate,
                    n.EndDate,
                    n.MktStaffID,
                    n.MktStaff,
                    n.PartyID,
                    n.Party,
                    n.BranchID,
                    n.ExCurrency,
                    n.ExRate,
                    n.PolicyNo
                }, t));

        /// <summary>
        /// Participations for a posted note: one Coins_Leaders row per co-insurance
        /// participant, keyed on DNCNNo. Our own retention is not stored there, it
        /// is on the head note. The LeaderID filter is defensive — notes raised by
        /// the legacy screen may carry a row for our own company.
        /// </summary>
        private async Task<IReadOnlyList<CoinsLeaderRow>> GetParticipationsAsync(
            IDbConnection c, IDbTransaction t, string dncnNo)
        {
            if (string.IsNullOrWhiteSpace(dncnNo))
                return Array.Empty<CoinsLeaderRow>();

            var rows = await c.QueryAsync<CoinsLeaderRow>(new CommandDefinition(@"
                SELECT CoinsID, DNCNNo, PolicyNo, TotalRiskValue, TotalPremium,
                       LeaderID, Leader, RetProp, RetValue, RetPremium, Remarks, Tag
                FROM   Coins_Leaders
                WHERE  DNCNNo = @dncnNo
                  AND  ISNULL(LeaderID, '') <> @ourId
                ORDER BY CoinsID",
                new { dncnNo, ourId = _settings.CompanyAbbrev ?? string.Empty }, t));

            return rows.ToList();
        }

        /// <summary>
        /// Participations from the most recent note on a policy, used to prefill a
        /// new note's grid. Coins_Leaders has no endorsement column, so "most
        /// recent" is the highest CoinsID group rather than a specific endorsement.
        /// </summary>
        private async Task<IReadOnlyList<CoinsLeaderRow>> GetLatestParticipationsForPolicyAsync(
            IDbConnection c, IDbTransaction t, string policyNo)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                return Array.Empty<CoinsLeaderRow>();

            var rows = await c.QueryAsync<CoinsLeaderRow>(new CommandDefinition(@"
                WITH latest AS (
                    SELECT TOP (1) DNCNNo
                    FROM   Coins_Leaders
                    WHERE  PolicyNo = @policyNo
                    ORDER BY CoinsID DESC
                )
                SELECT cl.CoinsID, cl.DNCNNo, cl.PolicyNo, cl.TotalRiskValue, cl.TotalPremium,
                       cl.LeaderID, cl.Leader, cl.RetProp, cl.RetValue, cl.RetPremium,
                       cl.Remarks, cl.Tag
                FROM   Coins_Leaders cl
                JOIN   latest l ON l.DNCNNo = cl.DNCNNo
                WHERE  ISNULL(cl.LeaderID, '') <> @ourId
                ORDER BY cl.CoinsID",
                new { policyNo, ourId = _settings.CompanyAbbrev ?? string.Empty }, t));

            return rows.ToList();
        }

        /// <summary>
        /// CoinsID is an identity column and is never supplied. RetProp is
        /// decimal(18,0): the value is rounded here so the loss is visible in the
        /// log instead of happening silently inside SQL Server.
        /// </summary>
        private async Task<long> InsertLeaderRowAsync(
            IDbConnection c, IDbTransaction t, CoinsLeaderRow row)
        {
            var storedRetProp = Math.Round(row.RetProp, 0, MidpointRounding.AwayFromZero);
            if (storedRetProp != row.RetProp)
            {
                _logger.LogWarning(
                    "Coins_Leaders.RetProp is decimal(18,0): proportion {Actual}% will be stored as {Stored}% " +
                    "for DNCNNo {DNCNNo}. Widen the column if fractional shares matter.",
                    row.RetProp, storedRetProp, row.DNCNNo);
            }

            return await c.ExecuteScalarAsync<long>(new CommandDefinition(@"
                INSERT INTO Coins_Leaders
                    (DNCNNo, PolicyNo, TotalRiskValue, TotalPremium, LeaderID, Leader,
                     RetProp, RetValue, RetPremium, Remarks, Tag)
                OUTPUT INSERTED.CoinsID
                VALUES
                    (@DNCNNo, @PolicyNo, @TotalRiskValue, @TotalPremium, @LeaderID, @Leader,
                     @RetProp, @RetValue, @RetPremium, @Remarks, @Tag)",
                new
                {
                    row.DNCNNo,
                    row.PolicyNo,
                    row.TotalRiskValue,
                    row.TotalPremium,
                    row.LeaderID,
                    Leader = Truncate(row.Leader, 512),
                    RetProp = storedRetProp,
                    row.RetValue,
                    row.RetPremium,
                    Remarks = Truncate(row.Remarks, 50),
                    Tag = Truncate(row.Tag, 50)
                }, t));
        }

        /// <summary>
        /// GibsSettings has CompanyAbbrev but no CompanyName, while DNCNNotes.Leader
        /// and Coins_Leaders.Leader want a full name. Add `CompanyName` to the
        /// GibsSettings section to populate them properly; until then the abbrev is
        /// used so the column is never left empty.
        /// </summary>
        private string CompanyDisplayName => _settings.CompanyAbbrev;

        private static string Truncate(string value, int maxLength) =>
            string.IsNullOrEmpty(value) || value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);

        private static Task<decimal?> GetMemberPremiumTotalAsync(
            IDbConnection c, IDbTransaction t, string policyNo, string endorsementNo) =>
            c.ExecuteScalarAsync<decimal?>(new CommandDefinition(
                $@"SELECT SUM(GrossPremium) FROM {MembersTable}
                   WHERE PolicyNo = @policyNo AND EndorsementNo = @endorsementNo
                     AND ISNULL(Deleted, 0) = 0",
                new { policyNo, endorsementNo }, t));

        // =================================================================
        // MAPPING / HELPERS
        // =================================================================

        private DebitNoteDto MapNote(DNCNNote n, PolicyRow policy, string riskClass)
        {
            var premium = new PremiumBreakdownDto
            {
                GrossPremium = n.GrossPremium ?? 0m,
                CommissionRate = n.PartyRate ?? 0m,
                CommissionAmount = n.Commission ?? 0m,
                OrcRate = n.RetProp ?? 0m,
                OrcAmount = n.RetPremium ?? 0m,
                NetPremium = n.NetAmount ?? 0m,
                // VatAmount is bigint, so this is whole naira. There is no
                // VatRate column — the rate is not recoverable from the record.
                WhtAmount = n.VatAmount ?? 0L
            };

            var dto = new DebitNoteDto
            {
                DebitNoteNo = n.DNCNNo,
                RefDebitNoteNo = n.refDNCNNo,
                ReceiptNo = n.ReceiptNo,
                PolicyNo = n.PolicyNo,
                CoPolicyNo = n.CoPolicyNo,
                BranchId = n.BranchID,
                BizSource = n.BizSource,
                BizOption = n.BizOption,
                NoteType = n.NoteType,
                SubRiskId = n.SubRiskID,
                SubRisk = n.SubRisk,
                RiskClass = riskClass,
                BillingDate = n.BillingDate,
                StartDate = n.StartDate,
                EndDate = n.EndDate,
                PartyId = n.PartyID,
                PartyName = n.Party,
                MktStaffId = n.MktStaffID,
                MktStaffName = n.MktStaff,
                InsuredId = n.InsuredID,
                InsuredName = n.InsuredName,
                TotalRiskSumInsured = n.TotalRiskValue ?? 0m,
                TotalRiskPremium = n.TotalPremium ?? 0m,
                SumInsured = n.SumInsured ?? 0m,
                GrossPremium = n.GrossPremium ?? 0m,
                OurSharePercent = (decimal)(n.PropRate ?? 0d),
                Premium = premium,
                ExRate = n.ExRate ?? 0d,
                ExCurrency = n.ExCurrency,
                SumInsuredFrgn = n.SumInsuredFrgn ?? 0m,
                GrossPremiumFrgn = n.GrossPremiumFrgn ?? 0m,
                PaymentType = n.PaymentType,
                CreditRefNo = n.ChequeNo,
                Narration = n.Narration,
                TransGuid = n.Transguid,
                Approval = n.Approval,
                SubmittedBy = n.SubmittedBy,
                SubmittedOn = n.SubmittedOn
            };

            dto.Ui = BuildUiFlags(n.BizSource, policy?.SourceType, riskClass, n.ExCurrency,
                n.Approval, IsDirectSourceType(policy?.SourceType));

            return dto;
        }

        /// <summary>
        /// Coins_Leaders row -> grid line. Commission, ORC and WHT rates are NOT
        /// stored on this table, so a line read back carries the split but not the
        /// rates it was computed with; the client must re-enter them before a
        /// recompute. RetProp is decimal(18,0), so a fractional share does not
        /// survive the round trip — see MIGRATION-NOTES.md.
        /// </summary>
        private static CoinsuranceLineDto MapParticipation(CoinsLeaderRow r) => new CoinsuranceLineDto
        {
            ParticipantId = r.LeaderID,
            ParticipantName = r.Leader,
            SharePercent = r.RetProp,
            ShareSumInsured = r.RetValue,
            SharePremium = r.RetPremium,
            IsLead = string.Equals(r.Tag, "LEAD", StringComparison.OrdinalIgnoreCase)
        };

        private List<CoinsuranceLineDto> BuildDistributionTemplate(decimal orcRate)
        {
            var rows = new List<CoinsuranceLineDto>(_options.DistributionGridRows);
            for (var i = 1; i <= _options.DistributionGridRows; i++)
            {
                rows.Add(new CoinsuranceLineDto
                {
                    Sn = i,
                    SharePercent = 0m,
                    CommissionRate = _options.DefaultCommissionRate,
                    OrcRate = orcRate == 0m ? _options.DefaultOrcRate : orcRate,
                    WhtRate = _options.DefaultWhtRate
                });
            }
            return rows;
        }

        /// <summary>
        /// The Page_Load visibility maze, collapsed into one place. Legacy rules:
        ///   SourceType DI/ED (CIP) or D (SA)  -> direct business, no commission/WHT
        ///   BizSource ACCEPTED                -> ORC section + distribution grid
        ///   BizSource CO-INSURANCE LEAD       -> distribution grid + share totals
        ///   BizSource DIRECT                  -> distribution grid hidden
        ///   class V                           -> tracking cost
        ///   class H/O                         -> NCDF / NAICOM levies
        ///   Approval 2                        -> read only
        /// </summary>
        private DebitNoteUiFlagsDto BuildUiFlags(
            string bizSource, string sourceType, string riskClass, string currency,
            byte? approval, bool isDirectSourceType)
        {
            var isDirectBizSource = string.Equals(bizSource, BusinessSource.Direct, StringComparison.OrdinalIgnoreCase);
            var isAccepted = string.Equals(bizSource, BusinessSource.Accepted, StringComparison.OrdinalIgnoreCase);
            var isCoinsLead = string.Equals(bizSource, BusinessSource.CoinsuranceLead, StringComparison.OrdinalIgnoreCase);

            var flags = new DebitNoteUiFlagsDto
            {
                ShowCommissionSection = !isDirectSourceType,
                ShowWhtSection = !isDirectSourceType,
                ShowOrcSection = isAccepted || isCoinsLead,
                ShowDistributionSection = !isDirectBizSource && (isAccepted || isCoinsLead),
                ShowComputeButton = isAccepted || isCoinsLead,
                ShowShareTotals = isCoinsLead,
                ShowTrackingCost = string.Equals(riskClass, "V", StringComparison.OrdinalIgnoreCase),
                ShowLevies = riskClass != null &&
                             (riskClass.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                              riskClass.Equals("O", StringComparison.OrdinalIgnoreCase)),
                ShowCurrencyPanel = !string.IsNullOrWhiteSpace(currency) &&
                                    !currency.Equals("NAIRA", StringComparison.OrdinalIgnoreCase),
                CommissionEditable = !isDirectSourceType,
                Editable = approval != 2
            };

            if (approval == 2)
                flags.LockReason =
                    "This transaction has been processed into the ledger and cannot be edited. Please reverse it.";

            return flags;
        }

        /// <summary>
        /// Legacy direct-business source types, which differ per installation:
        /// CIP used DI/ED, SA used D. Union taken so one API serves both.
        /// </summary>
        private static bool IsDirectSourceType(string sourceType)
        {
            if (string.IsNullOrWhiteSpace(sourceType)) return false;
            switch (sourceType.Trim().ToUpperInvariant())
            {
                case "DI":
                case "ED":
                case "D":
                    return true;
                default:
                    return false;
            }
        }

        private DNCNNote CloneForChildNote(DNCNNote head) => new DNCNNote
        {
            ReceiptNo = head.ReceiptNo,
            PolicyNo = head.PolicyNo,
            CoPolicyNo = head.CoPolicyNo,
            BranchID = head.BranchID,
            BizSource = head.BizSource,
            BillingDate = head.BillingDate,
            SubRiskID = head.SubRiskID,
            SubRisk = head.SubRisk,
            PartyID = head.PartyID,
            Party = head.Party,
            PartyRate = head.PartyRate,
            InsuredID = head.InsuredID,
            InsuredName = head.InsuredName,
            StartDate = head.StartDate,
            EndDate = head.EndDate,
            MktStaffID = head.MktStaffID,
            MktStaff = head.MktStaff,
            PropRate = head.PropRate,
            ProRataDays = 0,
            ProRataPremium = 0m,
            Narration = head.Narration,
            ExRate = head.ExRate,
            ExCurrency = head.ExCurrency,
            SumInsuredFrgn = head.SumInsuredFrgn,
            GrossPremiumFrgn = head.GrossPremiumFrgn,
            TotalRiskValue = head.TotalRiskValue,
            TotalPremium = head.TotalPremium,
            ChequeNo = head.ChequeNo,
            PaymentType = head.PaymentType,
            Transguid = head.Transguid,
            Approval = 0,
            HasTreaty = 0,
            Deleted = 0,
            Active = 1,
            Remarks = head.Remarks,
            DBDate = head.BillingDate
        };

        private static string BuildAuditDelta(DNCNNote before, UpdateDebitNoteRequest after)
        {
            var parts = new List<string>();

            void Compare(string label, object oldValue, object newValue)
            {
                var o = oldValue?.ToString() ?? string.Empty;
                var nv = newValue?.ToString() ?? string.Empty;
                if (!string.Equals(o, nv, StringComparison.Ordinal))
                    parts.Add($"{label}: {o} modified to {nv}");
            }

            Compare("Billing Date", before.BillingDate?.ToString("yyyy-MM-dd"),
                after.BillingDate.ToString("yyyy-MM-dd"));
            Compare("Start Date", before.StartDate?.ToString("yyyy-MM-dd"),
                after.StartDate?.ToString("yyyy-MM-dd"));
            Compare("Expiry Date", before.EndDate?.ToString("yyyy-MM-dd"),
                after.EndDate?.ToString("yyyy-MM-dd"));
            Compare("Narration", before.Narration, after.Narration);
            Compare("SumInsured", before.SumInsured, GroupLifeCalculator.R2(after.SumInsured));
            Compare("GrossPremium", before.GrossPremium, GroupLifeCalculator.R2(after.GrossPremium));
            Compare("NetAmount", before.NetAmount, GroupLifeCalculator.R2(after.NetPremium));

            var details = string.Join(", ", parts);

            // The legacy audit column is narrow; long deltas silently blew up the
            // insert. Truncated here rather than losing the whole update.
            const int maxLength = 500;
            if (details.Length > maxLength)
                details = details.Substring(0, maxLength - 3) + "...";

            return string.IsNullOrEmpty(details)
                ? $"Debit note {before.DNCNNo} saved with no field-level changes"
                : $"[{before.DNCNNo}] {details}";
        }

        //private async Task SafeAuditAsync(
        //    string action, string module, string table, string details, string username,
        //    IDbConnection c, IDbTransaction t)
        //{
        //    // The VB wrapped audit writes in a bare Try/Catch so a failed audit never
        //    // killed the posting. Same intent, but the failure is logged.
        //    //
        //    // Note this swallows the exception without rolling back: the audit row is
        //    // lost while the note stands. That is the legacy behaviour and it is the
        //    // right trade-off here, but it does mean DNCN_NOTES history can have gaps.
        //    try
        //    {
        //        await _audit.WriteAsync(action, module, table, details, username, c, t);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogWarning(ex, "Audit trail write failed for {Table} ({Action})", table, action);
        //    }
        //}

        /// <summary>
        /// Dapper runs on the DbContext's own connection so that raw SQL and EF
        /// (AutoNumberService) share one connection and one transaction. The
        /// connection is owned by the context and is NOT disposed here.
        /// </summary>
        private async Task<DbConnection> OpenConnectionAsync(CancellationToken ct = default)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await _db.Database.OpenConnectionAsync(ct);
            return conn;
        }

        // ---- Debit note number prefix ------------------------------------
        // A debit note is raised as an INVOICE and only becomes a DEBIT NOTE when
        // it is approved. Same sequence number throughout — only the prefix moves,
        // which is why the legacy ORC path did Replace(n, "INV", "DN", 1, 3).
        private const string PendingPrefix = "INV";
        private const string ApprovedPrefix = "DN";
        private const int DncnNoMaxLength = 32;   // DNCNNotes.DNCNNo is varchar(32)

        /// <summary>
        /// Number as stored at creation. Accepts whatever the AutoNumbers Format row
        /// produces: an INV number passes through, a DN number is rewritten, anything
        /// else is prefixed and logged rather than silently mangled.
        /// </summary>
        internal string ToPendingNumber(string number)
        {
            if (string.IsNullOrWhiteSpace(number)) return number;

            string result;
            if (number.StartsWith(PendingPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result = number;
            }
            else if (number.StartsWith(ApprovedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result = PendingPrefix + number.Substring(ApprovedPrefix.Length);
            }
            else
            {
                result = PendingPrefix + number;
                _logger.LogWarning(
                    "AutoNumber returned '{Number}', which starts with neither {Pending} nor " +
                    "{Approved}. Stored as '{Result}'. Check the AutoNumbers Format row for DNOTE.",
                    number, PendingPrefix, ApprovedPrefix, result);
            }

            if (result.Length > DncnNoMaxLength)
                throw new BusinessRuleException(
                    $"Generated debit note number '{result}' exceeds {DncnNoMaxLength} characters. " +
                    "Fix the AutoNumbers Format row rather than allowing a truncated key.");

            return result;
        }

        /// <summary>Number as stored once approved. Only the prefix changes.</summary>
        internal static string ToApprovedNumber(string number) =>
            string.IsNullOrWhiteSpace(number) ||
            !number.StartsWith(PendingPrefix, StringComparison.OrdinalIgnoreCase)
                ? number
                : ApprovedPrefix + number.Substring(PendingPrefix.Length);

        private static string ReplaceOccurrences(string input, string find, string replace, int count)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder(input);
            var done = 0;
            var index = 0;

            while (done < count)
            {
                var found = sb.ToString().IndexOf(find, index, StringComparison.Ordinal);
                if (found < 0) break;

                sb.Remove(found, find.Length).Insert(found, replace);
                index = found + replace.Length;
                done++;
            }

            return sb.ToString();
        }
    }
}