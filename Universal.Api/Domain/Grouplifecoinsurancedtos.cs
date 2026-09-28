using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Dtos
{
    /// <summary>
    /// Premium arithmetic for a single co-insurance note.
    ///
    /// Ported from businessPropChange2() in postings_coins_edit.aspx. Note this
    /// differs from the debit note screen in two ways — see CalculateCoinsRequest.
    /// </summary>
    public class CoinsurancePremiumDto
    {
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }

        /// <summary>This participant's share, in percent.</summary>
        public decimal BusinessPropPercent { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }

        /// <summary>Our own retention, shown for reference on the same screen.</summary>
        public decimal OurPropPercent { get; set; }
        public decimal OurSumInsured { get; set; }
        public decimal OurPremium { get; set; }

        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal WhtRate { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal NetPremium { get; set; }
        public decimal OrcRate { get; set; }
        public decimal OrcAmount { get; set; }

        public bool WhtDeductedFromNet { get; set; }
    }

    public class CalculateCoinsRequest
    {
        [Required] public decimal TotalSumInsured { get; set; }
        [Required] public decimal TotalPremium { get; set; }

        public decimal BusinessPropPercent { get; set; }
        public decimal OurPropPercent { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal WhtRate { get; set; }
        public decimal OrcRate { get; set; } = 2.5m;

        /// <summary>
        /// Legacy net is gross - commission, with WHT calculated but NOT deducted.
        /// Set true to deduct it, matching the debit note screen. Default false
        /// preserves what this page produced.
        /// </summary>
        public bool DeductWhtFromNet { get; set; }
    }

    public class CoinsuranceNoteDto
    {
        public string CreditNoteNo { get; set; }
        public string RefDebitNoteNo { get; set; }
        public string ReceiptNo { get; set; }
        public string PolicyNo { get; set; }
        public string BranchId { get; set; }
        public string BizSource { get; set; }
        public string BizOption { get; set; }
        public string NoteType { get; set; }
        public string SubRiskId { get; set; }
        public string SubRisk { get; set; }

        public DateTime? BillingDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>The participating insurance company (legacy party2 / Agents).</summary>
        public string ParticipantId { get; set; }
        public string ParticipantName { get; set; }

        public string LeaderId { get; set; }
        public string LeaderName { get; set; }

        public string MktStaffId { get; set; }
        public string MktStaffName { get; set; }
        public string InsuredId { get; set; }
        public string InsuredName { get; set; }
        public string Narration { get; set; }

        public CoinsurancePremiumDto Premium { get; set; }

        public double ExRate { get; set; }
        public string ExCurrency { get; set; }
        public decimal SumInsuredFrgn { get; set; }
        public decimal GrossPremiumFrgn { get; set; }

        public byte? Approval { get; set; }
        public bool Editable { get; set; }
        public string LockReason { get; set; }
        public bool ShowOrcSection { get; set; }
        public bool ShowCurrencyPanel { get; set; }

        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
    }

    public class CreateCoinsuranceNoteRequest
    {
        [Required] public string PolicyNo { get; set; }

        /// <summary>Legacy "trans-id": the head debit note this note is raised against.</summary>
        [Required] public string RefDebitNoteNo { get; set; }

        /// <summary>
        /// Legacy "type" query string. "CN" raises a co-insurance credit note;
        /// anything else is treated as a REVERSAL.
        /// </summary>
        public string Type { get; set; } = "CN";

        [Required] public DateTime BillingDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>Participating company. Resolved against Agents.</summary>
        [Required] public string ParticipantId { get; set; }

        public string InsuredName { get; set; }
        public string Narration { get; set; }
        public string BizOption { get; set; }

        public decimal SumInsured { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal BusinessPropPercent { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal NetPremium { get; set; }

        /// <summary>
        /// Optional. When supplied, the note's share is also written to
        /// Coins_Leaders so the participation is queryable — see COINSURANCE-NOTES.md
        /// before turning this on alongside the debit note module.
        /// </summary>
        public bool RecordParticipation { get; set; }

        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
    }

    public class UpdateCoinsuranceNoteRequest
    {
        [Required] public DateTime BillingDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [Required] public string ParticipantId { get; set; }

        public string InsuredName { get; set; }
        public string Narration { get; set; }

        public decimal SumInsured { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal BusinessPropPercent { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal NetPremium { get; set; }
    }

    public class CoinsuranceNoteListItemDto
    {
        public string CreditNoteNo { get; set; }
        public string RefDebitNoteNo { get; set; }
        public string PolicyNo { get; set; }
        public string NoteType { get; set; }
        public string BizOption { get; set; }
        public DateTime? BillingDate { get; set; }
        public string ParticipantId { get; set; }
        public string ParticipantName { get; set; }
        public string InsuredName { get; set; }
        public decimal SharePercent { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public decimal Commission { get; set; }
        public decimal NetPremium { get; set; }
        public byte? Approval { get; set; }
        public string ApprovalStatus { get; set; }
        public bool IsReversal { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
    }

    public class CoinsuranceNoteListQuery
    {
        public string PolicyNo { get; set; }

        /// <summary>Head debit note these notes were raised against.</summary>
        public string RefDebitNoteNo { get; set; }

        public string ParticipantId { get; set; }
        public string BranchId { get; set; }
        public byte? Approval { get; set; }

        /// <summary>True returns only REVERSAL rows, false excludes them.</summary>
        public bool? ReversalsOnly { get; set; }

        public DateTime? BillingFrom { get; set; }
        public DateTime? BillingTo { get; set; }

        /// <summary>Matches note number, participant or insured name.</summary>
        public string Search { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    /// <summary>Totals for one head note's panel — does the split add back up?</summary>
    public class CoinsurancePanelSummaryDto
    {
        public string RefDebitNoteNo { get; set; }
        public string PolicyNo { get; set; }
        public decimal HeadGrossPremium { get; set; }
        public decimal HeadSharePercent { get; set; }
        public int ParticipantCount { get; set; }
        public decimal CededPercent { get; set; }
        public decimal CededPremium { get; set; }
        public decimal TotalPercent { get; set; }
        public decimal TotalPremium { get; set; }

        /// <summary>Total risk premium recorded on the head note, when present.</summary>
        public decimal HeadTotalPremium { get; set; }

        public bool PercentBalances { get; set; }
        public bool PremiumBalances { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class CreateCoinsuranceNoteResponse
    {
        public string CreditNoteNo { get; set; }
        public string RefDebitNoteNo { get; set; }
        public string PolicyNo { get; set; }
        public string ParticipantId { get; set; }
        public string NoteType { get; set; }
        public bool ParticipationRecorded { get; set; }
    }
}