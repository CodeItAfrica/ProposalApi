using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Domain
{
    // ---------------------------------------------------------------------
    // Read models. These are deliberately lean: every SELECT in the service
    // lists its columns explicitly, so these only carry what the legacy page
    // actually touched. No SELECT *, no surprises from the generic Field/A
    // columns.
    // ---------------------------------------------------------------------

    /// <summary>Subset of Policies used by the debit note posting screen.</summary>
    public class PolicyRow
    {
        public string PolicyNo { get; set; }
        public string CoPolicyNo { get; set; }
        public string BranchID { get; set; }
        public string BizSource { get; set; }      // DIRECT / ACCEPTED / CO-INSURANCE LEAD / BANK_BIZ
        public string SourceType { get; set; }     // DI / ED / D ...
        public string SubRiskID { get; set; }
        public string SubRisk { get; set; }
        public string PartyID { get; set; }
        public string Party { get; set; }
        public string MktStaffID { get; set; }
        public string MktStaff { get; set; }
        public string InsuredID { get; set; }
        public string InsSurname { get; set; }
        public string InsFirstname { get; set; }
        public string InsOthernames { get; set; }
        public long? TrackID { get; set; }         // legacy "our account share" flag (100 == full)
        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double? ExRate { get; set; }
        public string ExCurrency { get; set; }

        public string InsuredFullName =>
            $"{InsSurname} {InsFirstname} {InsOthernames}".Replace("  ", " ").Trim();
    }

    /// <summary>
    /// Subset of PolicyDetails (the endorsement/transaction row addressed by the
    /// legacy "trans-id" query string, which is the DetailID identity key).
    /// </summary>
    public class PolicyDetailRow
    {
        public long DetailID { get; set; }
        public string PolicyNo { get; set; }
        public string CoPolicyNo { get; set; }
        public string EndorsementNo { get; set; }
        public string BizOption { get; set; }
        public string DNCNNo { get; set; }
        public string InsuredName { get; set; }
        public string Field1 { get; set; }          // legacy RI class
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? EntryDate { get; set; }
        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }
        public decimal? NetAmount { get; set; }
        public decimal? TotalRiskValue { get; set; }
        public decimal? SumInsuredFrgn { get; set; }
        public decimal? GrossPremiumFrgn { get; set; }
        public double? ProportionRate { get; set; }
        public double? ExRate { get; set; }
        public string ExCurrency { get; set; }
        public decimal? A2 { get; set; }            // MPL value
        public decimal? A10 { get; set; }           // top-most location value
        public decimal? A18 { get; set; }           // total tracking cost
        public decimal? A20 { get; set; }           // total risk premium
    }

    /// <summary>
    /// Coins_Leaders (plural) — lead/following participation header written on
    /// posting. Schema confirmed:
    ///   CoinsID        bigint         (identity, never written)
    ///   DNCNNo         varchar(32)
    ///   PolicyNo       varchar(32)
    ///   TotalRiskValue money
    ///   TotalPremium   money
    ///   LeaderID       varchar(64)
    ///   Leader         varchar(512)
    ///   RetProp        decimal(18,0)  <-- ZERO decimal places, see below
    ///   RetValue       money
    ///   RetPremium     money
    ///   Remarks        varchar(50)
    ///   Tag            varchar(50)
    /// </summary>
    public class CoinsLeaderRow
    {
        public long CoinsID { get; set; }
        public string DNCNNo { get; set; }
        public string PolicyNo { get; set; }
        public string LeaderID { get; set; }
        public string Leader { get; set; }
        public decimal TotalRiskValue { get; set; }
        public decimal TotalPremium { get; set; }
        public decimal RetValue { get; set; }
        public decimal RetPremium { get; set; }

        /// <summary>
        /// Retained/ceded proportion in percent. The column is decimal(18,0), so
        /// SQL Server rounds this to a whole number on insert — 12.5% lands as 13.
        /// The service rounds explicitly and logs when precision is lost rather
        /// than letting the driver do it silently.
        /// </summary>
        public decimal RetProp { get; set; }

        public string Remarks { get; set; }
        public string Tag { get; set; }             // LEAD / FOLLOWING
    }


    /// <summary>Tunables that were hard-coded literals in the ASPX code-behind.</summary>
    public class GroupLifeOptions
    {
        /// <summary>BizSource stamped on coinsurance notes. Legacy value: COINSURANCE_EXT.</summary>
        public string CoinsuranceBizSource { get; set; } = "COINSURANCE_EXT";

        /// <summary>Default ORC rate pre-filled for ACCEPTED business.</summary>
        public decimal DefaultOrcRate { get; set; } = 2.5m;

        /// <summary>Default rates seeded into an empty distribution grid.</summary>
        public decimal DefaultCommissionRate { get; set; } = 5m;
        public decimal DefaultWhtRate { get; set; } = 10m;

        /// <summary>Number of blank distribution rows the legacy grid rendered.</summary>
        public int DistributionGridRows { get; set; } = 40;
    }

    // ---------------------------------------------------------------------
    // Result envelope
    // ---------------------------------------------------------------------

    public class ServiceResult<T>
    {
        public bool Succeeded { get; private set; }
        public T Data { get; private set; }
        public string Message { get; private set; }
        public int StatusCode { get; private set; }

        public static ServiceResult<T> Ok(T data) =>
            new ServiceResult<T> { Succeeded = true, Data = data, StatusCode = 200 };

        public static ServiceResult<T> Fail(string message, int statusCode = 400) =>
            new ServiceResult<T> { Succeeded = false, Message = message, StatusCode = statusCode };
    }

    // =====================================================================
    // Shared shapes
    // =====================================================================

    /// <summary>
    /// One outward participation row (the DataGrid3 line in the legacy page).
    /// Rates come in, amounts go out.
    /// </summary>
    public class CoinsuranceLineDto
    {
        public int Sn { get; set; }

        /// <summary>Participant/reinsurer account code (ddItemtype.SelectedValue).</summary>
        public string ParticipantId { get; set; }

        public string ParticipantName { get; set; }

        /// <summary>Share of the risk ceded to this participant, in percent.</summary>
        public decimal SharePercent { get; set; }

        public decimal CommissionRate { get; set; }
        public decimal OrcRate { get; set; }
        public decimal WhtRate { get; set; }

        public bool IsLead { get; set; }

        // Computed by the service — ignored on input.
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal OrcAmount { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal NetPremium { get; set; }
    }

    /// <summary>Which sections the legacy page showed/hid for this policy.</summary>
    public class DebitNoteUiFlagsDto
    {
        public bool ShowCommissionSection { get; set; }      // PlaceHolder2
        public bool ShowWhtSection { get; set; }             // PlaceHolder1
        public bool ShowOrcSection { get; set; }             // PlaceHolder3
        public bool ShowDistributionSection { get; set; }    // PlaceHolder5 / Panel10
        public bool ShowComputeButton { get; set; }          // cmdCalcfire
        public bool ShowTrackingCost { get; set; }           // class = "V"
        public bool ShowLevies { get; set; }                 // class = "H" or "O"
        public bool ShowCurrencyPanel { get; set; }          // non-NAIRA
        public bool ShowShareTotals { get; set; }            // CO-INSURANCE LEAD
        public bool CommissionEditable { get; set; }
        public bool Editable { get; set; }                   // false once Approval = 2
        public string LockReason { get; set; }
    }

    /// <summary>Premium arithmetic result (cmdApply / btnOk recalculation).</summary>
    public class PremiumBreakdownDto
    {
        public decimal GrossPremium { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal WhtRate { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal OrcRate { get; set; }
        public decimal OrcAmount { get; set; }
        public decimal NetPremium { get; set; }

        public decimal NcdfRate { get; set; }
        public decimal NcdfValue { get; set; }
        public decimal NcdfNetValue { get; set; }

        public decimal NaicomLevyRate { get; set; }
        public decimal NaicomLevyValue { get; set; }
        public decimal NaicomLevyNetValue { get; set; }

        /// <summary>True when ORC was subtracted from the net (posting-time rule).</summary>
        public bool OrcDeductedFromNet { get; set; }
    }

    // =====================================================================
    // GET responses
    // =====================================================================

    public class DebitNoteDto
    {
        public string DebitNoteNo { get; set; }
        public string RefDebitNoteNo { get; set; }
        public string ReceiptNo { get; set; }          // endorsement no
        public string PolicyNo { get; set; }
        public string CoPolicyNo { get; set; }
        public string BranchId { get; set; }
        public string BizSource { get; set; }
        public string BizOption { get; set; }
        public string NoteType { get; set; }
        public string SubRiskId { get; set; }
        public string SubRisk { get; set; }
        public string RiskClass { get; set; }

        public DateTime? BillingDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string PartyId { get; set; }
        public string PartyName { get; set; }
        public string MktStaffId { get; set; }
        public string MktStaffName { get; set; }
        public string InsuredId { get; set; }
        public string InsuredName { get; set; }

        public decimal TotalRiskSumInsured { get; set; }
        public decimal TotalRiskPremium { get; set; }
        public decimal TotalTrackingCost { get; set; }
        public decimal TopMostLocationValue { get; set; }
        public decimal MplValue { get; set; }

        public decimal SumInsured { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal OurSharePercent { get; set; }

        public PremiumBreakdownDto Premium { get; set; }

        public double ExRate { get; set; }
        public string ExCurrency { get; set; }
        public decimal SumInsuredFrgn { get; set; }
        public decimal GrossPremiumFrgn { get; set; }

        public string PaymentType { get; set; }
        public string CreditRefNo { get; set; }        // ChequeNo
        public string Narration { get; set; }
        public string TransGuid { get; set; }

        public byte? Approval { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }

        public DebitNoteUiFlagsDto Ui { get; set; }

        /// <summary>
        /// Participations read from Coins_Leaders for this note, excluding our own
        /// retention row. Share figures are populated; commission/ORC/WHT rates are
        /// not stored on that table and come back as zero.
        /// </summary>
        public List<CoinsuranceLineDto> CoinsuranceLines { get; set; } = new List<CoinsuranceLineDto>();
    }

    /// <summary>Prefill for a brand new debit note (legacy "new" branch of Page_Load).</summary>
    public class DebitNotePrefillDto : DebitNoteDto
    {
        /// <summary>
        /// PolicyDetails.DetailID this prefill was built from. Echoed back because
        /// the POST needs it and the client would otherwise have to re-derive it
        /// from the endorsement number.
        /// </summary>
        public long? DetailId { get; set; }

        /// <summary>Blank distribution rows, pre-seeded with default rates.</summary>
        public List<CoinsuranceLineDto> DistributionTemplate { get; set; } = new List<CoinsuranceLineDto>();
    }

    // =====================================================================
    // Listing
    //
    // The legacy list screens (postings_dnotes.aspx / postings_coins.aspx) were
    // not part of the port, so these are designed rather than ported. Filters
    // follow what the edit screens key on.
    // =====================================================================

    //public class PagedResult<T>
    //{
    //    public List<T> Items { get; set; } = new List<T>();
    //    public int Page { get; set; }
    //    public int PageSize { get; set; }
    //    public int TotalCount { get; set; }
    //    public int TotalPages => PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
    //    public bool HasMore => Page < TotalPages;
    //}

    /// <summary>Row shape for list screens — deliberately lighter than the full note.</summary>
    public class DebitNoteListItemDto
    {
        public string DebitNoteNo { get; set; }
        public string RefDebitNoteNo { get; set; }
        public string PolicyNo { get; set; }
        public string ReceiptNo { get; set; }
        public string NoteType { get; set; }
        public string BizSource { get; set; }
        public string BizOption { get; set; }
        public string PaymentType { get; set; }
        public string BranchId { get; set; }
        public DateTime? BillingDate { get; set; }
        public string PartyId { get; set; }
        public string PartyName { get; set; }
        public string InsuredName { get; set; }
        public decimal SumInsured { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal Commission { get; set; }
        public decimal NetAmount { get; set; }
        public byte? Approval { get; set; }

        /// <summary>0 pending, 1 approved, 2 posted to the ledger.</summary>
        public string ApprovalStatus { get; set; }

        /// <summary>True while the number still carries the INV prefix.</summary>
        public bool IsInvoice { get; set; }

        /// <summary>True when this row is a head note rather than a child.</summary>
        public bool IsHeadNote { get; set; }

        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
    }

    public class DebitNoteListQuery
    {
        public string PolicyNo { get; set; }
        public string BranchId { get; set; }
        public string BizSource { get; set; }
        public string PaymentType { get; set; }

        /// <summary>DN or CN. Defaults to DN — this is the debit note list.</summary>
        public string NoteType { get; set; } = "DN";

        public byte? Approval { get; set; }

        /// <summary>Filters on the note number prefix: true = INV only, false = DN only.</summary>
        public bool? PendingOnly { get; set; }

        /// <summary>
        /// True (default) returns only head notes — rows where refDNCNNo equals
        /// DNCNNo. False includes the child notes raised against them.
        /// </summary>
        public bool HeadNotesOnly { get; set; } = true;

        public DateTime? BillingFrom { get; set; }
        public DateTime? BillingTo { get; set; }

        /// <summary>Matches note number, party or insured name.</summary>
        public string Search { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    // =====================================================================
    // POST/PUT requests
    // =====================================================================

    public class CalculatePremiumRequest
    {
        [Required] public decimal GrossPremium { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal WhtRate { get; set; }
        public decimal OrcRate { get; set; }
        public decimal NcdfRate { get; set; }
        public decimal NaicomLevyRate { get; set; }

        /// <summary>
        /// false = screen "Apply" rule (net = gross - commission - WHT).
        /// true  = posting rule used when commission is zero (net also nets off ORC).
        /// </summary>
        public bool DeductOrcFromNet { get; set; }
    }

    public class DistributeRequest
    {
        [Required] public decimal TotalRiskSumInsured { get; set; }
        [Required] public decimal TotalRiskPremium { get; set; }

        /// <summary>Our retained share, in percent (txtBusProp).</summary>
        public decimal OurSharePercent { get; set; } = 100m;

        /// <summary>
        /// Optional. The gross premium on the head note (our retained share in
        /// money). When supplied, the split is reconciled against the total risk
        /// premium as well as the percentages. The legacy screen never checked
        /// this, so a mismatch is a warning, not an error.
        /// </summary>
        public decimal? OurGrossPremium { get; set; }

        [Required] public List<CoinsuranceLineDto> Lines { get; set; } = new List<CoinsuranceLineDto>();
    }

    public class DistributionResultDto
    {
        public List<CoinsuranceLineDto> Lines { get; set; } = new List<CoinsuranceLineDto>();
        public decimal TotalCededPercent { get; set; }
        public decimal TotalCededSumInsured { get; set; }
        public decimal TotalCededPremium { get; set; }

        /// <summary>Legacy txtshareprop — must land on 100 for the note to be saveable.</summary>
        public decimal ShareProportion { get; set; }
        public bool IsBalanced { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Non-blocking. Percentages can balance while the money does not — see
        /// OurGrossPremium on the request.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class CreateDebitNoteRequest
    {
        [Required] public string PolicyNo { get; set; }

        /// <summary>
        /// Legacy "trans-id": PolicyDetails.DetailID, the endorsement row this note
        /// is raised against. Null for a note not tied to a detail row.
        /// </summary>
        public long? DetailId { get; set; }

        /// <summary>Legacy "class" query string (V, F, M, H, O, B, E, G).</summary>
        public string RiskClass { get; set; }

        [Required] public string BranchId { get; set; }
        [Required] public string BizSource { get; set; }
        public string BizOption { get; set; } = "NEW";

        [Required] public DateTime BillingDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string InsuredName { get; set; }
        public string Narration { get; set; }
        public string TransGuid { get; set; }

        [Required] public string PaymentType { get; set; }
        [Required] public string CreditRefNo { get; set; }

        public decimal TotalRiskSumInsured { get; set; }
        public decimal TotalRiskPremium { get; set; }

        public decimal SumInsured { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal OurSharePercent { get; set; } = 100m;

        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal WhtRate { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal OrcRate { get; set; }
        public decimal OrcAmount { get; set; }
        public decimal NetPremium { get; set; }

        public double ExRate { get; set; }
        public string ExCurrency { get; set; } = "NAIRA";
        public decimal SumInsuredFrgn { get; set; }
        public decimal GrossPremiumFrgn { get; set; }

        /// <summary>Outward participations. Empty for pure DIRECT business.</summary>
        public List<CoinsuranceLineDto> CoinsuranceLines { get; set; } = new List<CoinsuranceLineDto>();

        // --- Posting switches. All of these were commented out in the legacy
        // page; they are opt-in here so you can enable them one at a time.
        public bool PostCommissionCreditNote { get; set; }
        public bool PostCoinsuranceCreditNotes { get; set; }
        public bool PostCoinsuranceOrcDebitNotes { get; set; }
        public bool PostLeadOrcCreditNotes { get; set; }
        // NOTE: there is deliberately no flag for the Coins_Leaders write. If
        // coinsuranceLines are supplied the split is ALWAYS persisted — it is the
        // only record of who carries the risk, and making it opt-in meant a client
        // could enter a full distribution and have it silently thrown away.
    }

    public class UpdateDebitNoteRequest
    {
        [Required] public string BranchId { get; set; }
        [Required] public DateTime BillingDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string InsuredName { get; set; }
        public string Narration { get; set; }
        public string TransGuid { get; set; }

        public string PartyId { get; set; }
        public string PartyName { get; set; }
        public string MktStaffId { get; set; }

        public decimal TotalRiskSumInsured { get; set; }
        public decimal TotalRiskPremium { get; set; }
        public decimal SumInsured { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal OurSharePercent { get; set; }

        public decimal CommissionAmount { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal OrcRate { get; set; }
        public decimal OrcAmount { get; set; }
        public decimal NetPremium { get; set; }

        public double ExRate { get; set; }
        public string ExCurrency { get; set; }
        public decimal SumInsuredFrgn { get; set; }
        public decimal GrossPremiumFrgn { get; set; }

        [Required] public string PaymentType { get; set; }
        [Required] public string CreditRefNo { get; set; }
    }

    public class ApproveDebitNoteResponse
    {
        /// <summary>The INV number the note was raised under.</summary>
        public string PreviousDebitNoteNo { get; set; }

        /// <summary>The number after approval — DN-prefixed if it was renumbered.</summary>
        public string DebitNoteNo { get; set; }

        /// <summary>
        /// True when the prefix changed. False means the note was already
        /// DN-prefixed, so approval only moved the status.
        /// </summary>
        public bool Renumbered { get; set; }

        public byte ApprovalStatus { get; set; }
        public string ApprovedBy { get; set; }
    }

    public class CreateDebitNoteResponse
    {
        public string DebitNoteNo { get; set; }
        public string PolicyNo { get; set; }
        public string EndorsementNo { get; set; }

        /// <summary>Coinsurance credit notes raised against participants.</summary>
        public List<string> CoinsuranceCreditNotes { get; set; } = new List<string>();

        /// <summary>ORC debit notes raised against participants.</summary>
        public List<string> CoinsuranceOrcDebitNotes { get; set; } = new List<string>();

        /// <summary>ORC credit notes raised to the lead underwriter.</summary>
        public List<string> LeadOrcCreditNotes { get; set; } = new List<string>();

        public string CommissionCreditNote { get; set; }

        /// <summary>
        /// Rows written to Coins_Leaders — one per co-insurance participant. Equals
        /// the number of lines supplied; zero when there are none. Our own retention
        /// is not written there, it is on the head note itself.
        /// </summary>
        public int ParticipationRowsWritten { get; set; }
    }

}