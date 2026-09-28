using GibsLifesMicroWebApi.Infrastructure.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace GibsLifesMicroWebApi.Models
{

    [Table("GrpMembers")]
    public class GrpMember
    {
        [Key]
        public long MemberID { get; set; }

        [ForeignKey("Policy")]
        [StringLength(32)]
        public string PolicyNo { get; set; }

        [StringLength(32)]
        public string QuotationNo { get; set; }

        [StringLength(64)]
        public string EndorsementNo { get; set; }

        [StringLength(32)]
        public string BizOption { get; set; }

        [StringLength(32)]
        public string SerialNo { get; set; }

        [StringLength(32)]
        public string MembersNo { get; set; }

        [StringLength(128)]
        public string Surname { get; set; }

        [StringLength(128)]
        public string OtherNames { get; set; }

        public DateTime? DOBDate { get; set; }
        public DateTime? HireDate { get; set; }

        [StringLength(32)]
        public string Sex { get; set; }

        [Column(TypeName = "bigint")]  // Changed from int to bigint to match database
        public long Age { get; set; }

        [Column(TypeName = "money")]
        public decimal? BasicSalary { get; set; }

        [Column(TypeName = "money")]
        public decimal? Housing { get; set; }

        [Column(TypeName = "money")]
        public decimal? Transport { get; set; }

        [Column(TypeName = "money")]
        public decimal? OtherAmount { get; set; }

        [Column(TypeName = "money")]
        public decimal? TEmolument { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A1 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A2 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A3 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A4 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A5 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A6 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A7 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A8 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A9 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A10 { get; set; }

        [Column(TypeName = "money")]
        public decimal? SumInsured { get; set; }

        [Column(TypeName = "float")]
        public float? PremiumRate { get; set; }

        [Column(TypeName = "money")]
        public decimal? GrossPremium { get; set; }

        [Column(TypeName = "float")]  // Changed from decimal to float to match database
        public float? PDERate { get; set; }

        [Column(TypeName = "money")]
        public decimal? PDEPremium { get; set; }

        [Column(TypeName = "float")]  // Changed from decimal to float to match database
        public float? TDRate { get; set; }

        [Column(TypeName = "money")]
        public decimal? TDPremium { get; set; }

        [Column(TypeName = "float")]  // Changed from decimal to float to match database
        public float? MERate { get; set; }

        [Column(TypeName = "money")]
        public decimal? MEPremium { get; set; }

        [Column(TypeName = "float")]  // Changed from decimal to float to match database
        public float? FURate { get; set; }

        [Column(TypeName = "money")]
        public decimal? FUPremium { get; set; }

        [Column(TypeName = "float")]  // Changed from decimal to float to match database
        public float? CRRate { get; set; }

        [Column(TypeName = "money")]
        public decimal? CRPremium { get; set; }

        [Column(TypeName = "money")]
        public decimal? RentEXP { get; set; }

        [Column("photo")]
        public byte[] Photo { get; set; }

        public byte? Deleted { get; set; }
        public byte? Active { get; set; }

        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }

        [StringLength(128)]
        public string Field1 { get; set; }

        [StringLength(32)]
        public string Field2 { get; set; }

        [StringLength(32)]
        public string Field3 { get; set; }

        [StringLength(32)]
        public string Field4 { get; set; }

        [StringLength(32)]
        public string Field5 { get; set; }

        [StringLength(32)]
        public string Field6 { get; set; }

        [StringLength(32)]
        public string Field7 { get; set; }

        [StringLength(32)]
        public string Field8 { get; set; }

        [StringLength(64)]
        public string Field9 { get; set; }

        [StringLength(64)]
        public string Field10 { get; set; }

        // Navigation property
        [NotMapped]
        public Policy Policy { get; set; }
    }


    /// <summary>
    /// Add or update a single group-life member (member.ascx "Save &amp; Update Details").
    /// The service computes emolument, sums and premiums internally.
    /// </summary>
    public class MemberUpsertDto
    {
        public string PolicyNo { get; set; } = default!;
        public string? EndorsementNo { get; set; }

        /// <summary>AUTO or MANUAL. AUTO generates the certificate/members number.</summary>
        public string NumberType { get; set; } = "AUTO";

        /// <summary>Members number when NumberType is MANUAL.</summary>
        public string? MembersNo { get; set; }

        public string? SerialNo { get; set; }
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public string? Sex { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? DateOfBirth { get; set; }

        /// <summary>Frequency of payment (W / M) — used by the duration helper.</summary>
        public string? FreqOfPayment { get; set; }
        public decimal Duration { get; set; }
        public long Age { get; set; }

        // ---- premium inputs ----
        public decimal FixedPremiumRate { get; set; }
        public decimal OurSharePercent { get; set; } = 100m;

        /// <summary>Loan amount maps to BasicSalary in the legacy GL Calc.</summary>
        public decimal BasicSalary { get; set; }
        public decimal Housing { get; set; }
        public decimal Transport { get; set; }
        public decimal OtherAmount { get; set; }
    }

    public class MemberResponseDto
    {
        public long MemberID { get; set; }
        public string? MembersNo { get; set; }
        public string? SerialNo { get; set; }
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public string? Sex { get; set; }
        public long? Age { get; set; }
        public decimal? TotalEmolument { get; set; }
        public decimal? TotalSumInsured { get; set; }
        public decimal? TotalPremium { get; set; }
        public decimal? ShareSumInsured { get; set; }
        public decimal? SharePremium { get; set; }
        public decimal? Duration { get; set; }
    }

    /// <summary>Pure premium calculation request (no persistence) — "Calc.." button.</summary>
    public class PremiumCalcRequestDto
    {
        public decimal BasicSalary { get; set; }
        public decimal Housing { get; set; }
        public decimal Transport { get; set; }
        public decimal OtherAmount { get; set; }
        public decimal FixedPremiumRate { get; set; }
        public decimal OurSharePercent { get; set; } = 100m;
    }

    public class PremiumCalcResultDto
    {
        public decimal TotalEmolument { get; set; }
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
    }

    /// <summary>Aggregate premium totals across all members of a policy/endorsement.</summary>
    public class AggregatePremiumDto
    {
        public decimal TotalRiskValue { get; set; }
        public decimal TotalRiskPremium { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public int MemberCount { get; set; }
    }

    /// <summary>One row of a bulk member upload (mirrors the Excel/JSON column headers).</summary>
    public class BulkMemberRowDto
    {
        public string? SerialNo { get; set; }

        /// <summary>Customer / loan account number (LAPO CUST_NO). Stored on QuotationNo.</summary>
        public string? CustomerNo { get; set; }

        public string? CustomerName { get; set; }
        public string? CustomerAddress { get; set; }
        public string? PhoneNo { get; set; }
        public string? Gender { get; set; }
        public string? Occupation { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Bvn { get; set; }
        public string? BranchName { get; set; }
        public string? States { get; set; }

        /// <summary>Regional grouping (LAPO AREA). Falls back into the state/LGA field.</summary>
        public string? Area { get; set; }

        public string? Zones { get; set; }
        public string? NextOfKin { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public DateTime? MaturityDate { get; set; }
        public string? ProductDescription { get; set; }
        public decimal Tenor { get; set; }
        public string? LoanType { get; set; }
        public decimal DisbursementAmount { get; set; }
    }
    /// <summary>
    /// Result of parsing an uploaded workbook: the ready-to-push payload plus a summary of what
    /// was read, skipped, and any columns that couldn't be mapped.
    /// </summary>
    public class ExcelParseResultDto
    {
        /// <summary>The assembled payload, ready to POST to the bulk endpoint.</summary>
        public BulkMemberUploadDto Payload { get; set; } = new();

        public int DataRowsRead { get; set; }
        public int RowsMapped { get; set; }
        public int RowsSkipped { get; set; }

        /// <summary>Workbook headers that didn't map to a known field (informational).</summary>
        public List<string> UnmappedColumns { get; set; } = new();

        /// <summary>Row-level notes (e.g. unparseable dates, blank rows skipped).</summary>
        public List<string> Warnings { get; set; } = new();
    }

    public class BulkMemberUploadDto
    {
        public string PolicyNo { get; set; } = default!;
        public string? EndorsementNo { get; set; }

        /// <summary>PolicyRate (A10 * amount) or FixedRate (rate + loading/discount).</summary>
        public BulkPricingMode PricingMode { get; set; } = BulkPricingMode.PolicyRate;

        /// <summary>Used only when PricingMode = FixedRate.</summary>
        public decimal FixedPremiumRate { get; set; }
        public decimal LoadingRate { get; set; }
        public decimal DiscountRate { get; set; }
        public string? BizOption { get; set; }
        public List<BulkMemberRowDto> Rows { get; set; } = new();
    }

    public class BulkMemberUploadResultDto
    {
        public string? UploadBatchID { get; set; }
        public int InsertedCount { get; set; }
        public int SkippedCount { get; set; }
        public AggregatePremiumDto Aggregate { get; set; } = new();
    }

    public class AnbRequestDto
    {
        public DateTime DateOfBirth { get; set; }
        public DateTime StartDate { get; set; }
    }

    public class DurationRequestDto
    {
        public string FreqOfPayment { get; set; } = FrequencyOfPayment.Monthly;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

}
