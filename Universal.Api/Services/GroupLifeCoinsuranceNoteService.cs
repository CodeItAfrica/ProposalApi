using Dapper;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Dtos;
using GibsLifesMicroWebApi.Infrastructure.Common;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services.GroupLife
{
    public interface IGroupLifeCoinsuranceNoteService
    {
        /// <summary>Prefill from the head debit note (legacy Page_Load, new branch).</summary>
        Task<CoinsuranceNoteDto> GetPrefillAsync(
            string policyNo, string refDebitNoteNo, string type, CancellationToken ct = default);

        Task<CoinsuranceNoteDto> GetAsync(string dncnNo, CancellationToken ct = default);

        /// <summary>Paged list of co-insurance notes.</summary>
        Task<Models.PagedResult<CoinsuranceNoteListItemDto>> ListAsync(
            CoinsuranceNoteListQuery query, CancellationToken ct = default);

        /// <summary>
        /// Every participation raised against one head note, with totals showing
        /// whether the panel adds back up to the whole risk.
        /// </summary>
        Task<CoinsurancePanelSummaryDto> GetPanelAsync(
            string refDebitNoteNo, CancellationToken ct = default);

        CoinsurancePremiumDto Calculate(CalculateCoinsRequest request);

        Task<CreateCoinsuranceNoteResponse> CreateAsync(
            CreateCoinsuranceNoteRequest request, string username, CancellationToken ct = default);

        Task<string> UpdateAsync(
            string dncnNo, UpdateCoinsuranceNoteRequest request, string username, CancellationToken ct = default);
    }

    /// <summary>
    /// Port of postings_coins_edit.aspx.vb — the co-insurance note screen, which
    /// raises ONE credit note per participating company (the page's "Save &amp; Add
    /// New" button was how a full panel got entered).
    ///
    /// This is a sibling of the debit note module, not part of it: that screen
    /// distributes a risk across participants in a grid, this one records a single
    /// participation against an existing head debit note.
    ///
    /// Note the code-behind class is named ..._postings_dnotes_edit even though the
    /// file is postings_coins_edit.aspx.vb — a copy-paste in the legacy, harmless
    /// but confusing when grepping.
    /// </summary>
    public class GroupLifeCoinsuranceNoteService : IGroupLifeCoinsuranceNoteService
    {
        private readonly DataContext _db;
        private readonly AutoNumberService _autoNumber;
        //private readonly IAuditTrailService _audit;
        private readonly GibsSettings _settings;
        private readonly ILogger<GroupLifeCoinsuranceNoteService> _logger;

        private const string CoinsuranceBizSource = "COINSURANCE";   // BizOptionEnum.COINSURANCE
        private const string ReversalBizOption = "REVERSAL";

        public GroupLifeCoinsuranceNoteService(
            DataContext db,
            AutoNumberService autoNumber,
            //IAuditTrailService audit,
            IOptions<GibsSettings> settings,
            ILogger<GroupLifeCoinsuranceNoteService> logger)
        {
            _db = db;
            _autoNumber = autoNumber;
            //_audit = audit;
            _settings = settings?.Value ?? new GibsSettings();
            _logger = logger;
        }

        private string CompanyDisplayName => _settings.CompanyAbbrev;

        // =================================================================
        // READ
        // =================================================================

        public async Task<CoinsuranceNoteDto> GetPrefillAsync(
            string policyNo, string refDebitNoteNo, string type, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                throw new BusinessRuleException("Policy number is required.");
            if (string.IsNullOrWhiteSpace(refDebitNoteNo))
                throw new BusinessRuleException("A reference debit note is required.");

            var conn = await OpenConnectionAsync(ct);

            var policy = await GetPolicyAsync(conn, null, policyNo);
            if (policy == null)
                throw new NotFoundException($"Policy '{policyNo}' was not found.");

            var reference = await GetNoteAsync(conn, null, refDebitNoteNo);
            if (reference == null)
                throw new NotFoundException($"Reference note '{refDebitNoteNo}' was not found.");

            var isCreditNote = string.Equals(type, "CN", StringComparison.OrdinalIgnoreCase);

            var dto = new CoinsuranceNoteDto
            {
                CreditNoteNo = "[AUTO]",
                RefDebitNoteNo = reference.DNCNNo,
                PolicyNo = reference.PolicyNo,
                BranchId = policy.BranchID,
                BizSource = CoinsuranceBizSource,
                BizOption = isCreditNote ? reference.BizOption : ReversalBizOption,
                NoteType = "CN",
                SubRiskId = policy.SubRiskID,
                SubRisk = policy.SubRisk,
                BillingDate = reference.BillingDate,
                StartDate = reference.StartDate,
                EndDate = reference.EndDate,
                InsuredId = reference.InsuredID,
                InsuredName = reference.InsuredName,
                Narration = reference.Narration,
                MktStaffId = reference.MktStaffID,
                MktStaffName = reference.MktStaff,
                ExRate = reference.ExRate ?? 0d,
                ExCurrency = reference.ExCurrency,
                SumInsuredFrgn = reference.SumInsuredFrgn ?? 0m,
                GrossPremiumFrgn = reference.GrossPremiumFrgn ?? 0m,
                LeaderId = _settings.CompanyAbbrev,
                LeaderName = isCreditNote ? CompanyDisplayName : reference.Leader,
                Editable = true
            };

            // The two branches read the totals from different columns on the same
            // reference note, which is why a CN and a REVERSAL prefill differently.
            var totalSumInsured = isCreditNote
                ? reference.SumInsured ?? 0m
                : reference.TotalRiskValue ?? 0m;
            var totalPremium = isCreditNote
                ? reference.GrossPremium ?? 0m
                : reference.TotalPremium ?? 0m;

            dto.Premium = GroupLifeCoinsuranceCalculator.Compute(new CalculateCoinsRequest
            {
                TotalSumInsured = totalSumInsured,
                TotalPremium = totalPremium,
                BusinessPropPercent = 0m,
                OurPropPercent = (decimal)(reference.PropRate ?? 0d),
                CommissionRate = reference.PartyRate ?? 0m,
                WhtRate = 0m,
                OrcRate = 0m
            });

            dto.ShowCurrencyPanel = !string.IsNullOrWhiteSpace(dto.ExCurrency) &&
                                    !dto.ExCurrency.Equals("NAIRA", StringComparison.OrdinalIgnoreCase);
            dto.ShowOrcSection = false;   // the legacy hid this placeholder unconditionally

            return dto;
        }

        public async Task<CoinsuranceNoteDto> GetAsync(string dncnNo, CancellationToken ct = default)
        {
            var conn = await OpenConnectionAsync(ct);

            var note = await GetNoteAsync(conn, null, dncnNo);
            if (note == null)
                throw new NotFoundException($"Co-insurance note '{dncnNo}' was not found.");

            return MapNote(note);
        }

        public async Task<Models.PagedResult<CoinsuranceNoteListItemDto>> ListAsync(
            CoinsuranceNoteListQuery query, CancellationToken ct = default)
        {
            query ??= new CoinsuranceNoteListQuery();

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 25 : (query.PageSize > 200 ? 200 : query.PageSize);

            var conn = await OpenConnectionAsync(ct);

            var rows = (await conn.QueryAsync<CoinsListRow>(new CommandDefinition(@"
                SELECT  COUNT(*) OVER() AS TotalCount,
                        DNCNNo, refDNCNNo, PolicyNo, NoteType, BizOption, BillingDate,
                        PartyID, Party, InsuredName,
                        ISNULL(PropRate, 0)     AS PropRate,
                        ISNULL(SumInsured, 0)   AS SumInsured,
                        ISNULL(GrossPremium, 0) AS GrossPremium,
                        ISNULL(Commission, 0)   AS Commission,
                        ISNULL(NetAmount, 0)    AS NetAmount,
                        Approval, SubmittedBy, SubmittedOn
                FROM    DNCNNotes
                WHERE   ISNULL(Deleted, 0) = 0
                  AND   BizSource = @BizSource
                  AND   (@PolicyNo       IS NULL OR PolicyNo  = @PolicyNo)
                  AND   (@RefDebitNoteNo IS NULL OR refDNCNNo = @RefDebitNoteNo)
                  AND   (@ParticipantId  IS NULL OR PartyID   = @ParticipantId)
                  AND   (@BranchId       IS NULL OR BranchID  = @BranchId)
                  AND   (@Approval       IS NULL OR ISNULL(Approval, 0) = @Approval)
                  AND   (@BillingFrom    IS NULL OR BillingDate >= @BillingFrom)
                  AND   (@BillingToExclusive IS NULL OR BillingDate < @BillingToExclusive)
                  AND   (@ReversalsOnly  IS NULL
                         OR (@ReversalsOnly = 1 AND BizOption = @Reversal)
                         OR (@ReversalsOnly = 0 AND ISNULL(BizOption, '') <> @Reversal))
                  AND   (@SearchLike IS NULL
                         OR DNCNNo      LIKE @SearchLike
                         OR Party       LIKE @SearchLike
                         OR InsuredName LIKE @SearchLike
                         OR PolicyNo    LIKE @SearchLike)
                ORDER BY SubmittedOn DESC, DNCNNo DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", new
            {
                BizSource = CoinsuranceBizSource,
                Reversal = ReversalBizOption,
                PolicyNo = Blank(query.PolicyNo),
                RefDebitNoteNo = Blank(query.RefDebitNoteNo),
                ParticipantId = Blank(query.ParticipantId),
                BranchId = Blank(query.BranchId),
                query.Approval,
                query.BillingFrom,
                BillingToExclusive = query.BillingTo?.Date.AddDays(1),
                ReversalsOnly = query.ReversalsOnly.HasValue
                    ? (query.ReversalsOnly.Value ? 1 : 0) : (int?)null,
                SearchLike = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search.Trim()}%",
                Offset = (page - 1) * pageSize,
                PageSize = pageSize
            }, cancellationToken: ct))).ToList();

            return new Models.PagedResult<CoinsuranceNoteListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = rows.Count == 0 ? 0 : rows[0].TotalCount,
                Items = rows.Select(MapListItem).ToList()
            };
        }

        /// <summary>
        /// The check nobody could do on the legacy screen, because each note was
        /// entered in isolation: does the panel add back up to the whole risk?
        /// </summary>
        public async Task<CoinsurancePanelSummaryDto> GetPanelAsync(
            string refDebitNoteNo, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refDebitNoteNo))
                throw new BusinessRuleException("A reference debit note is required.");

            var conn = await OpenConnectionAsync(ct);

            var head = await GetNoteAsync(conn, null, refDebitNoteNo);
            if (head == null)
                throw new NotFoundException($"Reference note '{refDebitNoteNo}' was not found.");

            var participants = (await conn.QueryAsync<CoinsListRow>(new CommandDefinition(@"
                SELECT  0 AS TotalCount,
                        DNCNNo, refDNCNNo, PolicyNo, NoteType, BizOption, BillingDate,
                        PartyID, Party, InsuredName,
                        ISNULL(PropRate, 0)     AS PropRate,
                        ISNULL(SumInsured, 0)   AS SumInsured,
                        ISNULL(GrossPremium, 0) AS GrossPremium,
                        ISNULL(Commission, 0)   AS Commission,
                        ISNULL(NetAmount, 0)    AS NetAmount,
                        Approval, SubmittedBy, SubmittedOn
                FROM    DNCNNotes
                WHERE   refDNCNNo = @refDebitNoteNo
                  AND   DNCNNo <> @refDebitNoteNo
                  AND   BizSource = @BizSource
                  AND   ISNULL(BizOption, '') <> @Reversal
                  AND   ISNULL(Deleted, 0) = 0
                ORDER BY DNCNNo",
                new { refDebitNoteNo, BizSource = CoinsuranceBizSource, Reversal = ReversalBizOption },
                cancellationToken: ct))).ToList();

            var headPct = (decimal)(head.PropRate ?? 0d);
            var headPremium = head.GrossPremium ?? 0m;
            var cededPct = participants.Sum(p => p.PropRate);
            var cededPremium = GroupLifeCalculator.R2(participants.Sum(p => p.GrossPremium));
            var headTotalPremium = head.TotalPremium ?? 0m;

            var summary = new CoinsurancePanelSummaryDto
            {
                RefDebitNoteNo = head.DNCNNo,
                PolicyNo = head.PolicyNo,
                HeadGrossPremium = headPremium,
                HeadSharePercent = headPct,
                HeadTotalPremium = headTotalPremium,
                ParticipantCount = participants.Count,
                CededPercent = cededPct,
                CededPremium = cededPremium,
                TotalPercent = headPct + cededPct,
                TotalPremium = GroupLifeCalculator.R2(headPremium + cededPremium)
            };

            summary.PercentBalances = summary.TotalPercent == 100m;

            if (!summary.PercentBalances && participants.Count > 0)
                summary.Warnings.Add(
                    $"Retained {headPct}% plus ceded {cededPct}% is {summary.TotalPercent}%, not 100%. " +
                    "Raising a co-insurance note never reduced the head note's share, so a fully " +
                    "retained head note stays at 100% no matter how much is ceded.");

            if (headTotalPremium > 0m)
            {
                summary.PremiumBalances = summary.TotalPremium == GroupLifeCalculator.R2(headTotalPremium);
                if (!summary.PremiumBalances)
                    summary.Warnings.Add(
                        $"Retained plus ceded premium is {summary.TotalPremium}, against a total risk " +
                        $"premium of {GroupLifeCalculator.R2(headTotalPremium)} on the head note.");
            }
            else
            {
                summary.PremiumBalances = true;
                summary.Warnings.Add(
                    "The head note carries no total risk premium, so the money cannot be reconciled.");
            }

            return summary;
        }

        private static string Blank(string v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private static CoinsuranceNoteListItemDto MapListItem(CoinsListRow r) =>
            new CoinsuranceNoteListItemDto
            {
                CreditNoteNo = r.DNCNNo,
                RefDebitNoteNo = r.refDNCNNo,
                PolicyNo = r.PolicyNo,
                NoteType = r.NoteType,
                BizOption = r.BizOption,
                BillingDate = r.BillingDate,
                ParticipantId = r.PartyID,
                ParticipantName = r.Party,
                InsuredName = r.InsuredName,
                SharePercent = r.PropRate,
                ShareSumInsured = r.SumInsured,
                SharePremium = r.GrossPremium,
                Commission = r.Commission,
                NetPremium = r.NetAmount,
                Approval = r.Approval,
                ApprovalStatus = r.Approval switch
                {
                    null or 0 => "Pending",
                    1 => "Approved",
                    2 => "Posted to ledger",
                    _ => $"Status {r.Approval}"
                },
                IsReversal = string.Equals(r.BizOption, ReversalBizOption, StringComparison.OrdinalIgnoreCase),
                SubmittedBy = r.SubmittedBy,
                SubmittedOn = r.SubmittedOn
            };

        private class CoinsListRow
        {
            public int TotalCount { get; set; }
            public string DNCNNo { get; set; }
            public string refDNCNNo { get; set; }
            public string PolicyNo { get; set; }
            public string NoteType { get; set; }
            public string BizOption { get; set; }
            public DateTime? BillingDate { get; set; }
            public string PartyID { get; set; }
            public string Party { get; set; }
            public string InsuredName { get; set; }
            public decimal PropRate { get; set; }
            public decimal SumInsured { get; set; }
            public decimal GrossPremium { get; set; }
            public decimal Commission { get; set; }
            public decimal NetAmount { get; set; }
            public byte? Approval { get; set; }
            public string SubmittedBy { get; set; }
            public DateTime? SubmittedOn { get; set; }
        }

        // =================================================================
        // CALCULATE
        // =================================================================

        public CoinsurancePremiumDto Calculate(CalculateCoinsRequest request)
        {
            if (request == null)
                throw new BusinessRuleException("Request body is required.");

            return GroupLifeCoinsuranceCalculator.Compute(request);
        }

        // =================================================================
        // CREATE
        // =================================================================

        public async Task<CreateCoinsuranceNoteResponse> CreateAsync(
            CreateCoinsuranceNoteRequest req, string username, CancellationToken ct = default)
        {
            if (req == null)
                throw new BusinessRuleException("Request body is required.");

            if (_settings.CurrentAccountPeriod.HasValue &&
                _settings.CurrentAccountPeriod.Value.Date > req.BillingDate.Date)
            {
                throw new BusinessRuleException(
                    "The accounting period has been closed. Please check the transaction date.");
            }

            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                var conn = await OpenConnectionAsync(ct);
                await using var efTx = await _db.Database.BeginTransactionAsync(ct);
                var tx = efTx.GetDbTransaction();

                var policy = await GetPolicyAsync(conn, tx, req.PolicyNo);
                if (policy == null)
                    throw new NotFoundException($"Policy '{req.PolicyNo}' was not found.");

                var reference = await GetNoteAsync(conn, tx, req.RefDebitNoteNo);
                if (reference == null)
                    throw new NotFoundException($"Reference note '{req.RefDebitNoteNo}' was not found.");

                if (!string.Equals(reference.PolicyNo, policy.PolicyNo, StringComparison.OrdinalIgnoreCase))
                    throw new BusinessRuleException(
                        $"Reference note '{req.RefDebitNoteNo}' does not belong to policy '{req.PolicyNo}'.");

                var participant = await GetAgentAsync(conn, tx, req.ParticipantId);
                if (participant == null)
                    throw new NotFoundException(
                        $"Participating company '{req.ParticipantId}' was not found.");

                var isCreditNote = string.Equals(req.Type, "CN", StringComparison.OrdinalIgnoreCase);

                // LEGACY DEFECT, preserved: a REVERSAL draws from the DNOTE sequence
                // but is still stamped NoteType = "CN". So a reversal carries a debit
                // note number on a credit note row. Flagged, not fixed — changing it
                // would renumber reversals and break anything matching on the prefix.
                var numberType = isCreditNote ? AutoNumberType.CNote : AutoNumberType.DNote;

                var noteNo = await _autoNumber.GetNextAsync(
                    numberType, policy.BranchID, policy.SubRiskID, ct);

                if (string.IsNullOrWhiteSpace(noteNo))
                    throw new BusinessRuleException("AutoNumber settings are invalid. Data was NOT saved.");

                var note = new DNCNNote
                {
                    DNCNNo = noteNo,
                    refDNCNNo = req.RefDebitNoteNo,
                    // Legacy: Left(Guid.NewGuid().ToString, 8). Not a receipt number —
                    // a placeholder. 8 hex chars is ~4bn values, so collisions are
                    // unlikely but not impossible, and the debit note module uses this
                    // same column for the endorsement number.
                    ReceiptNo = Guid.NewGuid().ToString("N").Substring(0, 8),
                    PolicyNo = policy.PolicyNo,
                    BranchID = policy.BranchID,
                    BizSource = CoinsuranceBizSource,
                    BizOption = isCreditNote ? req.BizOption : ReversalBizOption,
                    NoteType = "CN",
                    PaymentType = "NORMAL",
                    ExCurrency = "NAIRA",              // hard-coded in the legacy
                    BillingDate = req.BillingDate,
                    SubRiskID = policy.SubRiskID,
                    SubRisk = policy.SubRisk,
                    PartyID = participant.AgentID,
                    Party = participant.Agent,
                    PartyRate = req.CommissionRate,
                    InsuredID = policy.InsuredID,
                    InsuredName = req.InsuredName,
                    StartDate = req.StartDate,
                    EndDate = req.EndDate,
                    // Legacy writes the marketing GROUP into the staff columns.
                    MktStaffID = policy.MktStaffID,
                    MktStaff = policy.MktStaff,
                    SumInsured = GroupLifeCalculator.R2(req.SumInsured),
                    GrossPremium = GroupLifeCalculator.R2(req.GrossPremium),
                    Commission = GroupLifeCalculator.R2(req.CommissionAmount),
                    VatAmount = (long)Math.Round(req.WhtAmount, 0, MidpointRounding.AwayFromZero),
                    PropRate = (double)req.BusinessPropPercent,
                    ProRataDays = 0,
                    ProRataPremium = GroupLifeCalculator.R2(req.GrossPremium),
                    NetAmount = GroupLifeCalculator.R2(req.NetPremium),
                    Narration = req.Narration,
                    Approval = 0,
                    HasTreaty = 0,
                    Deleted = 0,
                    Active = 1,
                    SubmittedBy = username,
                    SubmittedOn = DateTime.Now,
                    DBDate = req.BillingDate
                };

                await InsertNoteAsync(conn, tx, note);

                var participationRecorded = false;
                if (req.RecordParticipation)
                {
                    await InsertParticipationAsync(conn, tx, new CoinsLeaderRow
                    {
                        DNCNNo = req.RefDebitNoteNo,      // hangs off the HEAD note
                        PolicyNo = policy.PolicyNo,
                        LeaderID = participant.AgentID,
                        Leader = participant.Agent,
                        TotalRiskValue = GroupLifeCalculator.R2(req.TotalSumInsured),
                        TotalPremium = GroupLifeCalculator.R2(req.TotalPremium),
                        RetValue = GroupLifeCalculator.R2(req.SumInsured),
                        RetPremium = GroupLifeCalculator.R2(req.GrossPremium),
                        RetProp = req.BusinessPropPercent,
                        Remarks = "COINSURANCE",
                        Tag = "FOLLOWING"
                    });
                    participationRecorded = true;
                }

                //await SafeAuditAsync("NEW", "UNDERWRITING", "DNCN_NOTES",
                //    $"Co-insurance note {noteNo} raised for {participant.Agent} on policy {policy.PolicyNo} " +
                //    $"against {req.RefDebitNoteNo}",
                //    username, conn, tx);

                await efTx.CommitAsync(ct);

                return new CreateCoinsuranceNoteResponse
                {
                    CreditNoteNo = noteNo,
                    RefDebitNoteNo = req.RefDebitNoteNo,
                    PolicyNo = policy.PolicyNo,
                    ParticipantId = participant.AgentID,
                    NoteType = "CN",
                    ParticipationRecorded = participationRecorded
                };
            });
        }

        // =================================================================
        // UPDATE
        // =================================================================

        public async Task<string> UpdateAsync(
            string dncnNo, UpdateCoinsuranceNoteRequest req, string username, CancellationToken ct = default)
        {
            if (req == null)
                throw new BusinessRuleException("Request body is required.");

            if (_settings.CurrentAccountPeriod.HasValue &&
                _settings.CurrentAccountPeriod.Value.Date > req.BillingDate.Date)
            {
                throw new BusinessRuleException(
                    "The accounting period has been closed. Please check the transaction date.");
            }

            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                var conn = await OpenConnectionAsync(ct);
                await using var efTx = await _db.Database.BeginTransactionAsync(ct);
                var tx = efTx.GetDbTransaction();

                var existing = await GetNoteAsync(conn, tx, dncnNo);
                if (existing == null)
                    throw new NotFoundException($"Co-insurance note '{dncnNo}' was not found.");

                if (existing.Approval == 2)
                    throw new BusinessRuleException(
                        "This transaction has been processed into the ledger and cannot be edited. Please reverse it.");

                var policy = await GetPolicyAsync(conn, tx, existing.PolicyNo);
                var participant = await GetAgentAsync(conn, tx, req.ParticipantId);
                if (participant == null)
                    throw new NotFoundException(
                        $"Participating company '{req.ParticipantId}' was not found.");

                var delta = BuildAuditDelta(existing, req, participant.Agent);

                existing.BillingDate = req.BillingDate;
                existing.StartDate = req.StartDate;
                existing.EndDate = req.EndDate;
                existing.InsuredID = policy?.InsuredID ?? existing.InsuredID;
                existing.InsuredName = req.InsuredName;
                existing.PartyID = participant.AgentID;
                existing.Party = participant.Agent;
                existing.PartyRate = req.CommissionRate;
                existing.MktStaffID = policy?.MktStaffID ?? existing.MktStaffID;
                existing.MktStaff = policy?.MktStaff ?? existing.MktStaff;
                existing.SumInsured = GroupLifeCalculator.R2(req.SumInsured);
                existing.GrossPremium = GroupLifeCalculator.R2(req.GrossPremium);
                existing.Commission = GroupLifeCalculator.R2(req.CommissionAmount);
                existing.VatAmount = (long)Math.Round(req.WhtAmount, 0, MidpointRounding.AwayFromZero);
                existing.PropRate = (double)req.BusinessPropPercent;
                existing.ProRataDays = 0;
                existing.ProRataPremium = GroupLifeCalculator.R2(req.GrossPremium);
                existing.NetAmount = GroupLifeCalculator.R2(req.NetPremium);
                existing.Narration = req.Narration;
                existing.ModifiedBy = username;
                existing.ModifiedOn = DateTime.Now;

                var rows = await UpdateNoteAsync(conn, tx, existing);
                if (rows == 0)
                    throw new BusinessRuleException("No rows were updated.");

                //await SafeAuditAsync("EDIT", "UNDERWRITING", "DNCN_NOTES", delta, username, conn, tx);

                await efTx.CommitAsync(ct);
                return dncnNo;
            });
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

        private static Task<int> InsertNoteAsync(IDbConnection c, IDbTransaction t, DNCNNote n) =>
            c.ExecuteAsync(new CommandDefinition($@"
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

        private static Task<int> UpdateNoteAsync(IDbConnection c, IDbTransaction t, DNCNNote n) =>
            c.ExecuteAsync(new CommandDefinition(@"
                UPDATE DNCNNotes SET
                    BillingDate    = @BillingDate,
                    StartDate      = @StartDate,
                    EndDate        = @EndDate,
                    PartyID        = @PartyID,
                    Party          = @Party,
                    PartyRate      = @PartyRate,
                    InsuredID      = @InsuredID,
                    InsuredName    = @InsuredName,
                    MktStaffID     = @MktStaffID,
                    MktStaff       = @MktStaff,
                    SumInsured     = @SumInsured,
                    GrossPremium   = @GrossPremium,
                    Commission     = @Commission,
                    VatAmount      = @VatAmount,
                    PropRate       = @PropRate,
                    ProRataDays    = @ProRataDays,
                    ProRataPremium = @ProRataPremium,
                    NetAmount      = @NetAmount,
                    Narration      = @Narration,
                    ModifiedBy     = @ModifiedBy,
                    ModifiedOn     = @ModifiedOn
                WHERE DNCNNo = @DNCNNo AND ISNULL(Approval, 0) <> 2", n, t));

        private static Task<PolicyRow> GetPolicyAsync(IDbConnection c, IDbTransaction t, string policyNo) =>
            c.QueryFirstOrDefaultAsync<PolicyRow>(new CommandDefinition(@"
                SELECT PolicyNo, CoPolicyNo, BranchID, BizSource, SourceType, SubRiskID, SubRisk,
                       PartyID, Party, MktStaffID, MktStaff, MktGroupID, MktGroup,
                       InsuredID, InsSurname, InsFirstname, InsOthernames, TrackID,
                       SumInsured, GrossPremium, StartDate, EndDate, ExRate, ExCurrency
                FROM Policies WHERE PolicyNo = @policyNo", new { policyNo }, t));

        /// <summary>
        /// Legacy cls_Agents.SelectThis(id) -> Agent entity with AgentID / Agent.
        /// SCHEMA CHECK: confirm the display-name column really is called `Agent`;
        /// the DataContext maps Agents by an INT key with AgentID as an alternate.
        /// </summary>
        private static Task<AgentRow> GetAgentAsync(IDbConnection c, IDbTransaction t, string agentId) =>
            c.QueryFirstOrDefaultAsync<AgentRow>(new CommandDefinition(
                "SELECT TOP (1) AgentID, Agent FROM Agents WHERE AgentID = @agentId",
                new { agentId }, t));

        private static Task<int> InsertParticipationAsync(
            IDbConnection c, IDbTransaction t, CoinsLeaderRow row)
        {
            var stored = Math.Round(row.RetProp, 0, MidpointRounding.AwayFromZero);
            return c.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO Coins_Leaders
                    (DNCNNo, PolicyNo, TotalRiskValue, TotalPremium, LeaderID, Leader,
                     RetProp, RetValue, RetPremium, Remarks, Tag)
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
                    row.Leader,
                    RetProp = stored,
                    row.RetValue,
                    row.RetPremium,
                    row.Remarks,
                    row.Tag
                }, t));
        }

        // =================================================================
        // MAPPING / HELPERS
        // =================================================================

        private CoinsuranceNoteDto MapNote(DNCNNote n) => new CoinsuranceNoteDto
        {
            CreditNoteNo = n.DNCNNo,
            RefDebitNoteNo = n.refDNCNNo,
            ReceiptNo = n.ReceiptNo,
            PolicyNo = n.PolicyNo,
            BranchId = n.BranchID,
            BizSource = n.BizSource,
            BizOption = n.BizOption,
            NoteType = n.NoteType,
            SubRiskId = n.SubRiskID,
            SubRisk = n.SubRisk,
            BillingDate = n.BillingDate,
            StartDate = n.StartDate,
            EndDate = n.EndDate,
            ParticipantId = n.PartyID,
            ParticipantName = n.Party,
            LeaderId = n.LeaderID ?? _settings.CompanyAbbrev,
            LeaderName = n.Leader ?? CompanyDisplayName,
            MktStaffId = n.MktStaffID,
            MktStaffName = n.MktStaff,
            InsuredId = n.InsuredID,
            InsuredName = n.InsuredName,
            Narration = n.Narration,
            ExRate = n.ExRate ?? 0d,
            ExCurrency = n.ExCurrency,
            SumInsuredFrgn = n.SumInsuredFrgn ?? 0m,
            GrossPremiumFrgn = n.GrossPremiumFrgn ?? 0m,
            Approval = n.Approval,
            Editable = n.Approval != 2,
            LockReason = n.Approval == 2
                ? "This transaction has been processed into the ledger and cannot be edited. Please reverse it."
                : null,
            ShowOrcSection = false,
            ShowCurrencyPanel = !string.IsNullOrWhiteSpace(n.ExCurrency) &&
                                !n.ExCurrency.Equals("NAIRA", StringComparison.OrdinalIgnoreCase),
            SubmittedBy = n.SubmittedBy,
            SubmittedOn = n.SubmittedOn,
            Premium = new CoinsurancePremiumDto
            {
                TotalSumInsured = n.TotalRiskValue ?? 0m,
                TotalPremium = n.TotalPremium ?? 0m,
                BusinessPropPercent = (decimal)(n.PropRate ?? 0d),
                ShareSumInsured = n.SumInsured ?? 0m,
                SharePremium = n.GrossPremium ?? 0m,
                CommissionRate = n.PartyRate ?? 0m,
                CommissionAmount = n.Commission ?? 0m,
                WhtAmount = n.VatAmount ?? 0L,
                NetPremium = n.NetAmount ?? 0m
                // WhtRate and OrcRate have no columns — not recoverable from a saved note.
            }
        };

        private static string BuildAuditDelta(
            DNCNNote before, UpdateCoinsuranceNoteRequest after, string participantName)
        {
            var parts = new List<string>();

            void Compare(string label, object o, object n)
            {
                var a = o?.ToString() ?? string.Empty;
                var b = n?.ToString() ?? string.Empty;
                if (!string.Equals(a, b, StringComparison.Ordinal))
                    parts.Add($"{label}: {a} modified to {b}");
            }

            Compare("Billing Date", before.BillingDate?.ToString("yyyy-MM-dd"),
                after.BillingDate.ToString("yyyy-MM-dd"));
            Compare("Start Date", before.StartDate?.ToString("yyyy-MM-dd"),
                after.StartDate?.ToString("yyyy-MM-dd"));
            Compare("Expiry Date", before.EndDate?.ToString("yyyy-MM-dd"),
                after.EndDate?.ToString("yyyy-MM-dd"));
            Compare("Party", before.Party, participantName);
            Compare("Narration", before.Narration, after.Narration);
            Compare("SumInsured", before.SumInsured, GroupLifeCalculator.R2(after.SumInsured));
            Compare("GrossPremium", before.GrossPremium, GroupLifeCalculator.R2(after.GrossPremium));
            Compare("Comm Rate", before.PartyRate, after.CommissionRate);

            var details = string.Join(", ", parts);
            const int max = 500;
            if (details.Length > max) details = details.Substring(0, max - 3) + "...";

            return string.IsNullOrEmpty(details)
                ? $"Co-insurance note {before.DNCNNo} saved with no field-level changes"
                : $"[{before.DNCNNo}] {details}";
        }

        //private async Task SafeAuditAsync(
        //    string action, string module, string table, string details, string username,
        //    IDbConnection c, IDbTransaction t)
        //{
        //    try
        //    {
        //        await _audit.WriteAsync(action, module, table, details, username, c, t);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogWarning(ex, "Audit trail write failed for {Table} ({Action})", table, action);
        //    }
        //}

        private async Task<DbConnection> OpenConnectionAsync(CancellationToken ct = default)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await _db.Database.OpenConnectionAsync(ct);
            return conn;
        }

        private class AgentRow
        {
            public string AgentID { get; set; }
            public string Agent { get; set; }
        }
    }
}