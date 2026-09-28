using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.AspNetCore.Http;

namespace GibsLifesMicroWebApi.Models
{
   
    /// <summary>
    /// Preview of policy data available for renewal
    /// Shows current policy details that can be edited
    /// </summary>
    public class RenewalPreviewDto
    {
        public string OriginalPolicyNo { get; set; }
        public DateTime OriginalStartDate { get; set; }
        public DateTime OriginalEndDate { get; set; }
        public string SubRiskID { get; set; }
        public string SubRisk { get; set; }
        public string BranchID { get; set; }
        public string Branch { get; set; }
        public string PartyID { get; set; }
        public string Party { get; set; }
        public string MktStaffID { get; set; }
        public string MktStaff { get; set; }

        // Insured details (editable)
        public string InsuredID { get; set; }
        public string InsSurname { get; set; }
        public string InsFirstname { get; set; }
        public string InsOthernames { get; set; }
        public string InsAddress { get; set; }
        public string InsMobilePhone { get; set; }
        public string InsEmail { get; set; }
        public string InsOccupation { get; set; }

        // Financial details from last policy
        public decimal? LastSumInsured { get; set; }
        public decimal? LastGrossPremium { get; set; }
        public decimal ProportionRate { get; set; }
        public string Currency { get; set; }
        public decimal ExchangeRate { get; set; }

        // Business details
        public string BizSource { get; set; }
        public string SourceType { get; set; }
        public string BusinessType { get; set; }
        public string AccountType { get; set; }
        public string BusChannel { get; set; }
        public string SubChannel { get; set; }

        // Last calculation details
        public LastCalculationSummaryDto LastCalculation { get; set; }

        // Suggested renewal dates
        public DateTime SuggestedStartDate { get; set; }
        public DateTime SuggestedEndDate { get; set; }
    }

    /// <summary>
    /// Response after creating policy renewal
    /// </summary>
    public class PolicyRenewalResponseDto
    {
        public string OriginalPolicyNo { get; set; }
        public string SourceDetailID { get; set; } // Which calculation was used as basis
        public string NewPolicyNo { get; set; }
        public DateTime RenewalDate { get; set; }
        public DateTime NewStartDate { get; set; }
        public DateTime NewEndDate { get; set; }
        public string InsuredName { get; set; }
        public string SubRisk { get; set; }
        public string Branch { get; set; }

        // Summary of source calculation
        public decimal SourceTotalSumInsured { get; set; }
        public decimal SourceTotalPremium { get; set; }
        public DateTime SourceCalculatedOn { get; set; }
        public string SourceCalculatedBy { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> ValidationErrors { get; set; } = new();

        // Instructions for next steps
        public string NextStep { get; set; }
        public string CalculationEndpoint { get; set; }
    }

    public class CanRenewResponseDto
    {
        public bool CanRenew { get; set; }
        public string Reason { get; set; }
        public List<string> ValidationMessages { get; set; } = new();
        public DateTime? PolicyEndDate { get; set; }
        public bool IsExpired { get; set; }
        public bool AlreadyRenewed { get; set; }
        public string ExistingRenewalPolicyNo { get; set; }
    }
    /// <summary>
    /// Summary of last calculation for renewal preview
    /// </summary>
    public class LastCalculationSummaryDto
    {
        public string CalculationType { get; set; } // "FIRE" or "MOTOR"
        public int SectionCount { get; set; }
        public int RiskItemCount { get; set; }
        public int VehicleCount { get; set; }
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
        public DateTime CalculatedOn { get; set; }
        public string CalculatedBy { get; set; }
    }

    /// <summary>
    /// Request to create a policy renewal
    /// </summary>
    public class CreatePolicyRenewalRequestDto
    {
        [Required]
        public string OriginalPolicyNo { get; set; }

        // New policy period
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        // Editable insured details
        public string InsSurname { get; set; }
        public string InsFirstname { get; set; }
        public string InsOthernames { get; set; }
        public string InsAddress { get; set; }
        public string InsMobilePhone { get; set; }
        public string InsEmail { get; set; }
        public string InsOccupation { get; set; }

        // Business details (can be changed)
        public string BizSource { get; set; }
        public string BusinessType { get; set; }
        public string AccountType { get; set; }
        public string BusChannel { get; set; }
        public string SubChannel { get; set; }

        // Financial parameters
        public decimal ProportionRate { get; set; } = 100;
        public string Currency { get; set; } = "NAIRA";
        public decimal ExchangeRate { get; set; } = 1;

        public string Remarks { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
    }

    public class ProposalDto
    {
        public string? ProposalNo { get; set; }
        public DateTime? TransDate { get; set; }
        public string? CoPolicyNo { get; set; }
        public string? CompanyID { get; set; }
        public string? BranchID { get; set; }
        public string? Branch { get; set; }
        public string? BizSource { get; set; }
        public string? SourceType { get; set; }
        public string? SubRiskID { get; set; }
        public string? SubRisk { get; set; }
        public string? RiskID { get; set; }

        public string? PartyID { get; set; }
        public string? Party { get; set; }
        public string? MktStaffID { get; set; }
        public string? MktStaff { get; set; }
        public string? InsuredID { get; set; }
        public string? InsSurname { get; set; }
        public string? InsFirstname { get; set; }
        public string? InsOthernames { get; set; }
        public string? InsAddress { get; set; }
        public string? InsStateID { get; set; }
        public string? InsMobilePhone { get; set; }
        public string? InsLandPhone { get; set; }
        public string? InsEmail { get; set; }
        public string? InsFaxNo { get; set; }
        public string? InsOccupation { get; set; }
        public byte? IsProposal { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double? ExRate { get; set; }
        public string? ExCurrency { get; set; }
        public double? PremiumRate { get; set; }
        public double? ProportionRate { get; set; }
        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }
        public decimal? SumInsuredFrgn { get; set; }
        public decimal? GrossPremiumFrgn { get; set; }
        public int? ProRataDays { get; set; }
        public decimal? ProRataPremium { get; set; }
        public string? Remarks { get; set; }
        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        public string? UndEmail1 { get; set; }
        public string? UndEmail2 { get; set; }
        public string? TransSTATUS { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? LeaderID { get; set; }
        public string? Leader { get; set; }
        public decimal? CommissionRate { get; set; }
    }

    /// <summary>
    /// Currency and exchange rate saved on a policy's header row -- the rate the
    /// policy was opened with, used to convert amounts throughout its calculations.
    /// </summary>
    public class PolicyExchangeRateDto
    {
        public string ProposalNo { get; set; }
        public string Currency { get; set; }
        public decimal ExchangeRate { get; set; }
    }

    public class PolicyDetailDto
    {
        public string DetailID { get; set; }
        public string PolicyNo { get; set; }
        public string CoPolicyNo { get; set; }
        public DateTime EntryDate { get; set; }
        public string EndorsementNo { get; set; }
        public string BizOption { get; set; }
        public string DNCNNo { get; set; }
        public string CertOrDocNo { get; set; }
        public string InsuredName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? LTAStartDate { get; set; }
        public DateTime? LTAEndDate { get; set; }
        public string ExRateID { get; set; }
        public decimal ExRate { get; set; }
        public string ExCurrency { get; set; }
        public decimal PremiumRate { get; set; }
        public decimal ProportionRate { get; set; }
        public decimal SumInsured { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal SumInsuredFrgn { get; set; }
        public decimal GrossPremiumFrgn { get; set; }
        public int ProRataDays { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal NetAmount { get; set; }
        public int Deleted { get; set; }
        public int Active { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public decimal TotalRiskValue { get; set; }

        // A1-A20 fields for calculations
        public decimal A1 { get; set; }
        public decimal A2 { get; set; }
        public decimal A3 { get; set; }
        public decimal A4 { get; set; }
        public decimal A5 { get; set; }
        public decimal A6 { get; set; }
        public decimal A7 { get; set; }
        public decimal A8 { get; set; }
        public decimal A9 { get; set; }
        public decimal A10 { get; set; }
        public decimal A11 { get; set; }
        public decimal A12 { get; set; }
        public decimal A13 { get; set; }
        public decimal A14 { get; set; }
        public decimal A15 { get; set; }
        public decimal A16 { get; set; }
        public decimal A17 { get; set; }
        public decimal A18 { get; set; }
        public decimal A19 { get; set; }
        public decimal A20 { get; set; }

        // Field1-Field50 for additional data
        public string Field1 { get; set; }
        public string Field2 { get; set; }
        public string Field3 { get; set; }
        public string Field4 { get; set; }
        public string Field5 { get; set; }
        // ... Additional fields 6-50 would follow same pattern
    }
    // Add these new DTOs to your Gibs.Domain.Entities namespace

    /// <summary>
    /// Response DTO for listing all fire calculations for a proposal
    /// </summary>
    public class FireCalculationListItemDto
    {
        public string DetailID { get; set; }
        public string ProposalNo { get; set; }
        public string CertOrDocNo { get; set; }
        public DateTime CalculatedOn { get; set; }
        public string CalculatedBy { get; set; }
        public string CalculationType { get; set; } // "INITIAL", "ADDITIONAL"
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
        public decimal NetPremium { get; set; }
        public decimal ProRataPremium { get; set; }
        public int SectionCount { get; set; }
        public string Status { get; set; } // "ACTIVE", "SUPERSEDED", "DELETED"
        public string Remarks { get; set; }
    }

    /// <summary>
    /// Request DTO for creating a new fire calculation (not the first one)
    /// </summary>
    public class CreateAdditionalFireCalculationRequestDto
    {
        public string ProposalNo { get; set; }

        // Sections with their risk items
        public List<FireSectionDto> Sections { get; set; } = new();

        // Global parameters
        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;

        // ONLY proposal-level adjustments
        public decimal OtherDiscountsRate { get; set; } = 0;
        public decimal OtherLoadingsRate { get; set; } = 0;

        // Metadata
        public string CalculatedBy { get; set; } = "SYSTEM";
        public string Remarks { get; set; }
        public string CalculationType { get; set; } = "ADDITIONAL";
    }
    public class CreateRenewalFireCalculationRequestDto
    {
        [Required]
        public string NewPolicyNo { get; set; }

        [Required]
        public string OriginalPolicyNo { get; set; }
        public string ProposalNo { get; set; }

        // Sections with their risk items
        public List<FireSectionDto> Sections { get; set; } = new();

        // Global parameters
        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;

        // ONLY proposal-level adjustments
        public decimal OtherDiscountsRate { get; set; } = 0;
        public decimal OtherLoadingsRate { get; set; } = 0;

        // Metadata
        public string CalculatedBy { get; set; } = "SYSTEM";
        public string Remarks { get; set; }
    }

    /// <summary>
    /// Request DTO for updating/recalculating a specific fire calculation
    /// </summary>
    public class UpdateFireCalculationRequestDto
    {
        public string DetailID { get; set; } // Which calculation to update

        // Sections with their risk items
        public List<FireSectionDto> Sections { get; set; } = new();

        // Global parameters
        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;

        // ONLY proposal-level adjustments
        public decimal OtherDiscountsRate { get; set; } = 0;
        public decimal OtherLoadingsRate { get; set; } = 0;

        // Metadata
        public string RecalculatedBy { get; set; } = "SYSTEM";
        public string Remarks { get; set; }
    }

    // Input DTO - only user-provided data, no calculated fields
    // Updated Risk Item with SectionID
    public class CleanRiskItemInputDto
    {
        public int ItemNo { get; set; }
        public string SectionID { get; set; } // Foreign key to section
        public string SMICode { get; set; }
        public string SMIDetail { get; set; } // PolicyPremDetails.SMIDetail (column follows SMICode)
        public string ItemDescription { get; set; }
        public decimal ActualValue { get; set; }
        public decimal ItemRate { get; set; }
        public decimal MultiplyRate { get; set; } = 0;
        public string Location { get; set; }

        // NEW: Optional stock item
        public StockItemDto? StockItem { get; set; } // Can be null

        public decimal FeaDiscountRate { get; set; } = 0;

        /// <summary>
        /// NEW: dynamic item-level adjustments (name + basis), applied to this item's
        /// TOTAL gross premium (main + stock) before it rolls up into the section.
        /// Leave both empty to keep the legacy FeaDiscountRate-only behaviour.
        /// </summary>
        public List<FireAdjustmentDto> Discounts { get; set; } = new();
        public List<FireAdjustmentDto> Loadings { get; set; } = new();
    }

    // Response DTO - includes all calculated fields
    public class CalculatedRiskItemDto : CleanRiskItemInputDto
    {
        // Calculated fields
        public decimal ActualPremium { get; set; }
        public decimal ShareValue { get; set; }
        public decimal PremiumValue { get; set; }
        // NEW: Stock item calculations (if StockItem is provided)
        public decimal StockSumInsured { get; set; }
        public decimal StockGrossPremium { get; set; }
        public decimal StockDiscountAmount { get; set; }
        public decimal StockNetPremium { get; set; }

        // NEW: Aggregated totals
        public decimal TotalSumInsured { get; set; } // ActualValue + StockSumInsured
        public decimal TotalGrossPremium { get; set; } // PremiumValue + StockNetPremium
        public decimal FeaDiscountAmount { get; set; }
        public decimal NetPremiumAfterDiscounts { get; set; }

        // NEW: what was actually applied at item level (amounts + formulas)
        public List<FireAdjustmentDto> AppliedLoadings { get; set; } = new();
        public List<FireAdjustmentDto> AppliedDiscounts { get; set; } = new();
        public decimal TotalItemLoadingAmount { get; set; }
        public decimal TotalItemDiscountAmount { get; set; }
    }
    /// <summary>
    /// Request for the item-level dynamic adjustment path — parallel to
    /// FireSectionDynamicAdjustmentRequestDto, one level down.
    /// </summary>
    public class FireRiskItemDynamicAdjustmentRequestDto
    {
        public decimal ItemSumInsured { get; set; }   // main + stock, unproportioned
        public decimal ItemGrossPremium { get; set; } // PremiumValue + StockNetPremium
        public List<FireAdjustmentDto> Discounts { get; set; } = new();
        public List<FireAdjustmentDto> Loadings { get; set; } = new();
    }

    public class FireRiskItemDynamicAdjustmentResponseDto
    {
        public decimal StartingPremium { get; set; }
        public List<FireAdjustmentDto> LoadingsApplied { get; set; } = new();
        public List<FireAdjustmentDto> DiscountsApplied { get; set; } = new();
        public decimal TotalLoadingAmount { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public decimal NetPremiumDue { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class RiskItemCalculationRequestDto
    {
        public string? SubRisk { get; set; }
        public List<CleanRiskItemInputDto> RiskItems { get; set; } = new();
        public decimal ProportionRate { get; set; } = 100;
    }

    public class RiskItemCalculationResponseDto
    {
        public string? Subrisk { get; set; }
        public List<CalculatedRiskItemDto> CalculatedItems { get; set; } = new();
        public decimal TotalActualValue { get; set; }
        public decimal TotalActualPremium { get; set; }
        public decimal TotalShareValue { get; set; }
        public decimal TotalSharePremium { get; set; }
        public decimal TotalNetPremiumAfterDiscounts { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class SectionAggregateRequestDto
    {
        public List<CalculatedRiskItemDto> CalculatedItems { get; set; } = new();
    }

    public class SectionAggregateResponseDto
    {
        public decimal AggregateSumInsured { get; set; }
        public decimal AggregatePremium { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class SectionAdjustmentRequestDto
    {
        public decimal AggregatePremium { get; set; }

        // Discount rates (user input)
        public decimal SpecialDiscountRate { get; set; } = 0;
        public decimal DeductibleDiscountRate { get; set; } = 0;
        public decimal SpreadDiscountRate { get; set; } = 0;
        public decimal LtaDiscountRate { get; set; } = 0;
        public decimal OtherDiscountsRate { get; set; } = 0;

        // Loading rates (user input)
        public decimal TheftLoadingRate { get; set; } = 0;
        public decimal SrccLoadingRate { get; set; } = 0;
        public decimal OtherLoading2Rate { get; set; } = 0;
    }

    public class SectionAdjustmentResponseDto
    {
        public decimal StartingPremium { get; set; }

        // Section-level discount amounts and net amounts
        public decimal SpecialDiscountAmount { get; set; }
        public decimal SpecialDiscountNetAmount { get; set; }
        public decimal DeductibleDiscountAmount { get; set; }
        public decimal DeductibleDiscountNetAmount { get; set; }
        public decimal SpreadDiscountAmount { get; set; }
        public decimal SpreadDiscountNetAmount { get; set; }
        public decimal LtaDiscountAmount { get; set; }
        public decimal LtaDiscountNetAmount { get; set; }

        // Section-level loading amounts and net amounts
        public decimal TheftLoadingAmount { get; set; }
        public decimal TheftLoadingNetAmount { get; set; }
        public decimal SrccLoadingAmount { get; set; }
        public decimal SrccLoadingNetAmount { get; set; }
        public decimal OtherLoading2Amount { get; set; }
        public decimal OtherLoading2NetAmount { get; set; }
        public decimal NetPremiumDue { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
    // New DTO for proposal-level adjustments (only 2 rates)
    public class ProposalLevelAdjustmentResponseDto
    {
        public decimal StartingPremium { get; set; }

        public decimal OtherDiscountsAmount { get; set; }
        public decimal OtherDiscountsNetAmount { get; set; }

        public decimal OtherLoadingsAmount { get; set; }
        public decimal OtherLoadingsNetAmount { get; set; }

        public decimal NetPremiumDue { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class ProRataCalculationRequestDto
    {
        public decimal NetPremiumDue { get; set; }
        public int CoverDays { get; set; } = 365;
    }

    public class ProRataCalculationResponseDto
    {
        public decimal NetPremiumDue { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal PremiumDue { get; set; }
        public int CoverDays { get; set; }
        public bool IsProRated { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }




    public class ResponseInfo
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ExtraMessage { get; set; }
        public int TotalSuccess { get; set; }
    }

    public class EnhancedRiskItemDto
    {
        public int ItemNo { get; set; }
        public string SMICode { get; set; }
        public string SMIDetail { get; set; }
        public string ItemDescription { get; set; }
        public decimal ActualValue { get; set; }
        public decimal ItemRate { get; set; }
        public decimal ActualPremium { get; set; }
        public decimal MultiplyRate { get; set; }
        public decimal ShareValue { get; set; }
        public decimal PremiumValue { get; set; }
        public string SectionId { get; set; }
        public string Location { get; set; }

        // Enhanced discount options at item level
        public decimal StockDiscountRate { get; set; }
        public decimal StockDiscountAmount { get; set; }
        public decimal FeaDiscountRate { get; set; }
        public decimal FeaDiscountAmount { get; set; }
        public decimal NetPremiumAfterDiscounts { get; set; }
    }


    // Updated Complete Fire Calculation Request
    public class CompleteFireCalculationRequestDto
    {
        public string ProposalNo { get; set; }

        // Sections with their risk items
        public List<FireSectionDto> Sections { get; set; } = new();

        // Global parameters
        public decimal ProportionRate { get; set; } = 100; // Default to 100
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;

        // ONLY proposal-level adjustments (applied to aggregate of all sections)
        public decimal OtherDiscountsRate { get; set; } = 0;
        public decimal OtherLoadingsRate { get; set; } = 0;

        // Calculation metadata
        public string CalculatedBy { get; set; } = "SYSTEM";
        public string Remarks { get; set; }
    }
    /// <summary>
    /// Available calculation option for renewal
    /// </summary>
    public class RenewalCalculationOptionDto
    {
        public string DetailID { get; set; }
        public string CertOrDocNo { get; set; }
        public DateTime CalculatedOn { get; set; }
        public string CalculatedBy { get; set; }
        public string CalculationType { get; set; }
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
        public int SectionCount { get; set; }
        public string Status { get; set; }
        public bool IsRecommended { get; set; }
    }

    /// <summary>
    /// Request to calculate fire calculation for renewal
    /// </summary>
    public class RenewalFireCalculationRequestDto
    {
        [Required]
        public string NewPolicyNo { get; set; }

        [Required]
        public string OriginalPolicyNo { get; set; }
        /// <summary>
        /// The DetailID of the specific calculation to use as the basis for renewal.
        /// If not provided, uses the latest active calculation.
        /// </summary>
        public string SourceDetailID { get; set; }

        // Sections with their risk items (can be edited from original)
        [Required]
        public List<FireSectionDto> Sections { get; set; } = new();

        // Global parameters
        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;

        // Proposal-level adjustments (only 2 rates)
        public decimal OtherDiscountsRate { get; set; } = 0;
        public decimal OtherLoadingsRate { get; set; } = 0;

        // Metadata
        public string CalculatedBy { get; set; } = "SYSTEM";
        public string Remarks { get; set; }
    }
    // Section Calculation Response
    public class SectionCalculationResponseDto
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public List<CalculatedRiskItemDto> CalculatedItems { get; set; } = new();
        public decimal SectionSumInsured { get; set; }
        public decimal SectionGrossPremium { get; set; }
        public decimal SectionNetPremium { get; set; }
        public SectionAdjustmentResponseDto SectionAdjustments { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // Updated Complete Calculation Response
    public class CompleteCalculationResponseDto
    {
        public string ProposalNo { get; set; }
        public string Subrisk { get; set; }
        public string PolicyDetailId { get; set; }

        // Section-wise calculations (with section-level adjustments)
        public List<SectionCalculationResponseDto> SectionCalculations { get; set; } = new();

        // Aggregated final values across all sections
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
        public decimal NetPremium { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public decimal ForeignSumInsured { get; set; }
        public decimal ForeignPremium { get; set; }

        // Individual calculation components
        public RiskItemCalculationResponseDto ItemCalculations { get; set; }
        public SectionAggregateResponseDto AggregateCalculations { get; set; }
        public ProposalLevelAdjustmentResponseDto ProposalLevelAdjustments { get; set; }
        public ProRataCalculationResponseDto ProRataCalculations { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public DateTime CalculatedOn { get; set; }
    }
    // Updated InsuredDetailsData class
    public class InsuredDetailsData
    {
        public string InsuredID { get; set; }
        public string Surname { get; set; }
        public string FirstName { get; set; }
        public string OtherNames { get; set; }
        public string Address { get; set; }
        public string MobilePhone { get; set; }
        public string LandPhone { get; set; }
        public string Email { get; set; }
        public string Occupation { get; set; }
        public string InsuredType { get; set; }
    }

    // Enhanced Create Proposal Request DTO
    public class EnhancedCreateProposalRequestDto
    {
        [Required]
        public string SubriskID { get; set; }

        [Required]
        public string BranchID { get; set; }

        [Required]
        public string PartyID { get; set; }

        [Required]
        public string MktStaffID { get; set; }

        /// <summary>
        /// Use InsuredID to auto-populate insured details from InsuredClients table.
        /// If provided, InsuredName and IsOrg are ignored.
        /// </summary>
        public string? InsuredID { get; set; }

        public string? Surname { get; set; }

        /// <summary>
        /// First name of the insured (for new customers).
        /// Can be used for both INDIVIDUAL and CORPORATE.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Other names of the insured (for new customers).
        /// Can be used for both INDIVIDUAL and CORPORATE.
        /// </summary>
        public string? OtherNames { get; set; }

        /// <summary>
        /// Indicates if the insured is an organization/corporate entity.
        /// True = CORPORATE, False = INDIVIDUAL.
        /// Only used when creating a new customer (InsuredID is null).
        /// </summary>
        public bool? IsOrg { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(365);

        // Additional insured details (for new customers or override)
        public string? InsAddress { get; set; }
        public string? InsMobilePhone { get; set; }
        public string? InsEmail { get; set; }
        public string? InsOccupation { get; set; }

        public string? BizSource { get; set; } = "DIRECT";
        public string? BusinessType { get; set; }
        public string? AccountType { get; set; }
        public string? BusChannel { get; set; }
        public string? SubChannel { get; set; }
        public decimal ProportionRate { get; set; } = 100;
        public string Currency { get; set; } = "NAIRA";
        public decimal ExRate { get; set; } = 1;
    }


    public class CurrentCalculationDto
    {
        public string ProposalNo { get; set; }
        public string CurrentVersionId { get; set; }
        public DateTime LastCalculatedOn { get; set; }
        public string LastCalculatedBy { get; set; }

        // Current parameters
        public decimal ProportionRate { get; set; }
        public decimal ExchangeRate { get; set; }
        public string Currency { get; set; }
        public int CoverDays { get; set; }

        // Current risk items with calculated values
        public List<CalculatedRiskItemDto> RiskItems { get; set; } = new();

        // Current section adjustments
        public SectionAdjustmentCurrentDto SectionAdjustments { get; set; }

        // Current results
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
        public decimal NetPremium { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public decimal ForeignSumInsured { get; set; }
        public decimal ForeignPremium { get; set; }
    }

    public class SectionAdjustmentCurrentDto
    {
        public decimal AggregatePremium { get; set; }

        // Current discount rates and amounts
        public decimal SpecialDiscountRate { get; set; }
        public decimal SpecialDiscountAmount { get; set; }
        public decimal DeductibleDiscountRate { get; set; }
        public decimal DeductibleDiscountAmount { get; set; }
        public decimal SpreadDiscountRate { get; set; }
        public decimal SpreadDiscountAmount { get; set; }
        public decimal LtaDiscountRate { get; set; }
        public decimal LtaDiscountAmount { get; set; }
        public decimal OtherDiscountsRate { get; set; }
        public decimal OtherDiscountsAmount { get; set; }

        // Current loading rates and amounts
        public decimal TheftLoadingRate { get; set; }
        public decimal TheftLoadingAmount { get; set; }
        public decimal SrccLoadingRate { get; set; }
        public decimal SrccLoadingAmount { get; set; }
        public decimal OtherLoading2Rate { get; set; }
        public decimal OtherLoading2Amount { get; set; }
        public decimal OtherLoadingsRate { get; set; }
        public decimal OtherLoadingsAmount { get; set; }

        public decimal NetPremiumDue { get; set; }
    }





    // DTO for recalculation (PUT operation)
    public class RecalculateFireCalculationRequestDto
    {
        public string ProposalNo { get; set; }

        // Sections with their risk items
        public List<FireSectionDto> Sections { get; set; } = new();

        // Global parameters
        public decimal ProportionRate { get; set; } = 100; // Default to 100
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;
        // ONLY proposal-level adjustments
        public decimal OtherDiscountsRate { get; set; } = 0;
        public decimal OtherLoadingsRate { get; set; } = 0;
        public string Remarks { get; set; }

        // Recalculation metadata
        public string RecalculatedBy { get; set; } = "SYSTEM";
        //public string RecalculationReason { get; set; }
        //public bool OverwriteExisting { get; set; } = true; // true = update existing, false = create new version
    }


    // Additional DTOs needed for section breakdown
    public class SectionInputBreakdownDto
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public string Location { get; set; }
        public decimal SectionSumInsured { get; set; }
        public decimal SectionGrossPremium { get; set; }
        public decimal SectionNetPremium { get; set; }
        public List<CalculatedRiskItemBreakdownDto> RiskItems { get; set; } = new();
        public SectionAdjustmentsBreakdownDto SectionAdjustments { get; set; }
        // NEW: Section adjustment rates
        public decimal SpecialDiscountRate { get; set; }
        public decimal DeductibleDiscountRate { get; set; }
        public decimal SpreadDiscountRate { get; set; }
        public decimal LtaDiscountRate { get; set; }
        public decimal TheftLoadingRate { get; set; }
        public decimal SrccLoadingRate { get; set; }
        public decimal OtherLoading2Rate { get; set; }

        public string? Field26 { get; set; }
        public string? Field7 { get; set; }
        public string? Field8 { get; set; }
    }

    public class SectionCalculationBreakdownDto
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public RiskItemCalculationBreakdownDto RiskItemCalculations { get; set; }
        public SectionAdjustmentsBreakdownDto SectionAdjustments { get; set; }
    }

    public class SectionSummaryDto
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public string Location { get; set; }
        public int RiskItemCount { get; set; }
        public decimal SectionSumInsured { get; set; }
        public decimal SectionGrossPremium { get; set; }
        public decimal SectionNetPremium { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal TotalLoadings { get; set; }
        public DateTime LastCalculated { get; set; }
        public string CalculatedBy { get; set; }

        public string? Field26 { get; set; }
        public string? Field7 { get; set; }
        public string? Field8 { get; set; }
    }
    /// <summary>
    /// A section from a source calculation that can be renewed.
    /// SelectionToken is the section's TransGUID — pass it back in the renew request.
    /// </summary>
    public class RenewableSectionDto
    {
        public string SelectionToken { get; set; }   // source section TransGUID
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public string Location { get; set; }
        public decimal SectionSumInsured { get; set; }
        public decimal SectionNetPremium { get; set; }
        public int RiskItemCount { get; set; }
    }

    /// <summary>
    /// Request to renew selected sections of a source calculation into a NEW calculation.
    /// Empty/null SelectedSectionTokens = renew ALL sections of the source.
    /// </summary>
    public class RenewCalculationSectionsRequestDto
    {
        public List<string> SelectedSectionTokens { get; set; } = new();  // source section TransGUIDs
        public string RenewedBy { get; set; } = "SYSTEM";
        public string Remarks { get; set; }
    }

    public class RenewCalculationResponseDto
    {
        public string ProposalNo { get; set; }
        public string SourceDetailID { get; set; }
        public string SourceCertOrDocNo { get; set; }
        public string NewCertOrDocNo { get; set; }
        public string NewBizOption { get; set; } = "RENEWAL";
        public string Status { get; set; } = "UNCOMPLETED";   // stored in Field50
        public int RenewedSectionCount { get; set; }
        public decimal TotalSumInsured { get; set; }
        public decimal TotalNetPremium { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal FinalPremiumDue { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public string NextStep { get; set; }
    }
    public class ProposalFilterDto
    {
        public string BranchId { get; set; }
        public string Status { get; set; }
        public string PartyId { get; set; }
        public string MktStaffId { get; set; }
        public string RiskClass { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string SearchTerm { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string SortBy { get; set; } = "TransDate";
        public string SortDirection { get; set; } = "DESC";
    }
    // Complete Fire Calculation Breakdown DTO
    public class FireCalculationBreakdownDto
    {
        // Calculation Metadata
        public string ProposalNo { get; set; }
        public DateTime CalculatedOn { get; set; }
        public string CalculatedBy { get; set; }
        public string CalculationType { get; set; } // INITIAL, MODIFICATION, RECALCULATION, REVERSION
        public string Remarks { get; set; }

        // Input Parameters Used in Calculation
        public FireCalculationInputsDto Inputs { get; set; }

        // Step-by-Step Calculation Results
        public FireCalculationStepsDto CalculationSteps { get; set; }

        // Final Results Summary
        public FireCalculationResultsDto FinalResults { get; set; }

    }
    public class FireCalculationInputsDto
    {
        // Global Parameters
        public decimal ProportionRate { get; set; }
        public decimal ExchangeRate { get; set; }
        public string Currency { get; set; }
        public int CoverDays { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Section-based inputs (with section-level adjustments)
        public List<SectionInputBreakdownDto> SectionInputs { get; set; } = new();

        // Proposal-level adjustment inputs (ONLY 2 rates)
        public ProposalAdjustmentInputsDto ProposalAdjustments { get; set; }
    }

    // NEW: Proposal-level adjustment inputs (only 2 rates)
    public class ProposalAdjustmentInputsDto
    {
        public decimal OtherDiscountsRate { get; set; }
        public decimal OtherLoadingsRate { get; set; }
    }

    public class RiskItemInputBreakdownDto
    {
        public int ItemNo { get; set; }
        public string SMICode { get; set; }
        public string SMIDetail { get; set; }
        public string ItemDescription { get; set; }
        public decimal ActualValue { get; set; }
        public decimal ItemRate { get; set; }
        public decimal MultiplyRate { get; set; }
        public string SectionId { get; set; }
        public string Location { get; set; }
        public decimal StockDiscountRate { get; set; }
        public decimal FeaDiscountRate { get; set; }
    }


    // Updated FireCalculationStepsDto to include both section and proposal adjustments
    public class FireCalculationStepsDto
    {
        // Section-based calculations (with section-level adjustments)
        public List<SectionCalculationBreakdownDto> SectionCalculations { get; set; } = new();

        // Step 1: Risk Items Calculation Results
        public RiskItemCalculationBreakdownDto RiskItemCalculations { get; set; }

        // Step 2: Section Aggregates
        public SectionAggregatesBreakdownDto SectionAggregates { get; set; }

        // Step 3: Section-level Adjustments (already in SectionCalculations)

        // Step 4: Proposal-level Adjustments (only 2 rates)
        public ProposalLevelAdjustmentsBreakdownDto ProposalAdjustments { get; set; }

        // Step 5: Pro-Rata Calculations
        public ProRataBreakdownDto ProRataCalculations { get; set; }
    }

    // New DTO for proposal-level adjustments breakdown (for reporting)
    public class ProposalLevelAdjustmentsBreakdownDto
    {
        public decimal StartingPremium { get; set; }
        public List<AdjustmentDetailDto> DiscountsApplied { get; set; } = new();
        public List<AdjustmentDetailDto> LoadingsApplied { get; set; } = new();
        public decimal FinalNetPremium { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public decimal TotalLoadingAmount { get; set; }
        public decimal NetAdjustmentAmount { get; set; }
    }

    public class RiskItemCalculationBreakdownDto
    {
        public List<CalculatedRiskItemBreakdownDto> Items { get; set; } = new();
        public RiskItemTotalsDto Totals { get; set; }
    }

    public class CalculatedRiskItemBreakdownDto : RiskItemInputBreakdownDto
    {
        // Calculated values for each risk item
        public decimal ActualPremium { get; set; }
        public decimal ShareValue { get; set; }
        public decimal PremiumValue { get; set; }
        // Stock item calculations (NEW - these were missing)
        public decimal StockSumInsured { get; set; }
        public decimal StockGrossPremium { get; set; }
        public decimal StockDiscountRate { get; set; }
        public decimal StockDiscountAmount { get; set; }
        public decimal StockNetPremium { get; set; }
        public decimal FeaDiscountAmount { get; set; }
        public decimal NetPremiumAfterDiscounts { get; set; }

        // Stock item details (NEW - this was missing)
        public StockItemDto? StockItem { get; set; }

        // Calculation formulas used (for transparency)
        public string ActualPremiumFormula { get; set; }
        public string ShareValueFormula { get; set; }
        public string PremiumValueFormula { get; set; }
        // NEW: legacy per-item certificate number (PolicyPremDetails.ENDTNum)
        public string ItemCertNo { get; set; }

        // NEW: item-level adjustment breakdown, read back from PolicyPremDetails
        public List<AdjustmentDetailDto> DiscountsApplied { get; set; } = new();
        public List<AdjustmentDetailDto> LoadingsApplied { get; set; } = new();
    }

    public class RiskItemTotalsDto
    {
        public decimal TotalActualValue { get; set; }
        public decimal TotalActualPremium { get; set; }
        public decimal TotalShareValue { get; set; }
        public decimal TotalSharePremium { get; set; }
        public decimal TotalNetPremiumAfterDiscounts { get; set; }
        public int ItemCount { get; set; }
    }

    public class SectionAggregatesBreakdownDto
    {
        public decimal AggregateSumInsured { get; set; }
        public decimal AggregatePremium { get; set; }
        public string AggregationMethod { get; set; } = "Sum of all risk item net premiums";
    }

    public class SectionAdjustmentsBreakdownDto
    {
        public decimal StartingPremium { get; set; }

        // Applied discounts with details
        public List<AdjustmentDetailDto> DiscountsApplied { get; set; } = new();

        // Applied loadings with details  
        public List<AdjustmentDetailDto> LoadingsApplied { get; set; } = new();

        public decimal FinalNetPremium { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public decimal TotalLoadingAmount { get; set; }
        public decimal NetAdjustmentAmount { get; set; }
    }

    public class AdjustmentDetailDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public int SequenceOrder { get; set; }
        public string Formula { get; set; }
    }

    public class ProRataBreakdownDto
    {
        public decimal NetPremiumBeforeProRata { get; set; }
        public int CoverDays { get; set; }
        public int StandardDays { get; set; } = 365;
        public decimal ProRataFactor { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal FinalPremiumDue { get; set; }
        public bool IsProRated { get; set; }
        public string ProRataFormula { get; set; }
    }

    public class FireCalculationResultsDto
    {
        // Primary Results
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
        public decimal NetPremium { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }

        // Foreign Currency Results
        public decimal ForeignSumInsured { get; set; }
        public decimal ForeignPremium { get; set; }
        public string ForeignCurrency { get; set; }

        // Key Metrics
        public decimal OverallPremiumRate { get; set; } // Premium as % of Sum Insured
        public decimal EffectiveDiscountRate { get; set; } // Net discount applied
        public decimal EffectiveLoadingRate { get; set; } // Net loading applied

        // Rate Analysis
        public decimal GrossPremiumRate { get; set; }
        public decimal NetPremiumRate { get; set; }
        public decimal ProportionShare { get; set; }
    }



    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNext => Page < TotalPages;
        public bool HasPrevious => Page > 1;
    }


    // Updated Section DTO
    public class FireSectionDto
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public string Location { get; set; }
        public decimal SectionSumInsured { get; set; }
        public decimal SectionPremium { get; set; }


        // Risk items belonging to this section
        public List<CleanRiskItemInputDto> RiskItems { get; set; } = new();

        // MOVED HERE: Section-level adjustment rates (applied per section)
        public decimal SpecialDiscountRate { get; set; } = 0;
        public decimal DeductibleDiscountRate { get; set; } = 0;
        public decimal SpreadDiscountRate { get; set; } = 0;
        public decimal LtaDiscountRate { get; set; } = 0;
        public decimal TheftLoadingRate { get; set; } = 0;
        public decimal SrccLoadingRate { get; set; } = 0;
        public decimal OtherLoading2Rate { get; set; } = 0;
        public string? Field26 { get; set; }
        public string? Field7 { get; set; }
        public string? Field8 { get; set; }
        public List<FireAdjustmentDto> Discounts { get; set; } = new();
        public List<FireAdjustmentDto> Loadings { get; set; } = new();
    }

    // ADD THESE DTOs TO YOUR DTO FILE
    /// <summary>
    /// Request for previewing a single section WITH section-level adjustments
    /// </summary>
    public class SectionPreviewWithAdjustmentsRequestDto
    {
        [Required]
        public FireSectionDto Section { get; set; }
        public decimal ProportionRate { get; set; } = 100;
    }

    /// <summary>
    /// Response for section preview WITH section-level adjustments
    /// </summary>
    public class SectionPreviewWithAdjustmentsResponseDto
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public List<CalculatedRiskItemDto> CalculatedItems { get; set; } = new();

        // Aggregated values
        public decimal SectionSumInsured { get; set; }
        public decimal SectionGrossPremium { get; set; }  // Before adjustments
        public decimal SectionNetPremium { get; set; }     // After section adjustments

        // Adjustment details
        public SectionAdjustmentResponseDto SectionAdjustments { get; set; }

        // Share calculations
        public decimal ProportionRate { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }


    public class MultiSectionAggregateAfterAdjustmentsRequestDto
    {
        public List<AdjustedSectionSummary> Sections { get; set; } = new();
    }
    /// <summary>
    /// Request DTO for aggregating sections that have already had section-level adjustments applied
    /// This is used between section adjustments and proposal-level adjustments
    /// </summary>
    public class AggregateAdjustedSectionsRequestDto
    {
        [Required]
        public List<AdjustedSectionDto> AdjustedSections { get; set; } = new();
    }

    /// <summary>
    /// Represents a section that has already had its section-level adjustments applied
    /// Contains the final net premium after all section-level discounts/loadings
    /// </summary>
    public class AdjustedSectionDto
    {
        [Required]
        public string SectionID { get; set; }

        [Required]
        public string SectionName { get; set; }

        /// <summary>
        /// Total sum insured for this section (sum of all risk items)
        /// </summary>
        [Required]
        public decimal SectionSumInsured { get; set; }

        /// <summary>
        /// Net premium after section-level adjustments have been applied
        /// This is the output from ApplySectionAdjustments endpoint
        /// </summary>
        [Required]
        public decimal SectionNetPremium { get; set; }

        /// <summary>
        /// Number of risk items in this section
        /// </summary>
        public int RiskItemCount { get; set; }

        /// <summary>
        /// Optional: Location of the section
        /// </summary>
        public string Location { get; set; }
    }

    /// <summary>
    /// Response DTO for aggregated adjusted sections
    /// This aggregate is what proposal-level adjustments will be applied to
    /// </summary>
    public class AggregateAdjustedSectionsResponseDto
    {
        /// <summary>
        /// Summary of each section included in the aggregate
        /// </summary>
        public List<SectionAggregateDetail> SectionSummaries { get; set; } = new();

        /// <summary>
        /// Total sum insured across all sections
        /// </summary>
        public decimal TotalSumInsured { get; set; }

        /// <summary>
        /// Total aggregate premium (sum of all section net premiums after section-level adjustments)
        /// This is the amount that proposal-level adjustments will be applied to
        /// </summary>
        public decimal TotalAggregatePremium { get; set; }

        /// <summary>
        /// Number of sections aggregated
        /// </summary>
        public int SectionCount { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }
    public class AdjustedSectionSummary
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public decimal SectionSumInsured { get; set; }
        public decimal SectionNetPremium { get; set; } // After section adjustments
        public int RiskItemCount { get; set; }
    }
    /// <summary>
    /// Updated CompleteCalculationPreviewDto to include section adjustments
    /// </summary>
    public class CompleteCalculationPreviewDto
    {
        // Section-by-section breakdown WITH section-level adjustments
        public List<SectionPreviewWithAdjustmentsResponseDto> SectionPreviews { get; set; } = new();

        // Aggregated totals
        public decimal TotalSumInsured { get; set; }
        public decimal TotalGrossPremium { get; set; }  // After section adjustments
        public decimal TotalNetPremium { get; set; }     // After proposal adjustments
        public decimal ProRataPremium { get; set; }
        public decimal FinalPremiumDue { get; set; }

        // Share calculations
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }

        // Foreign currency
        public decimal ForeignSumInsured { get; set; }
        public decimal ForeignPremium { get; set; }

        // Detailed breakdowns
        public ProposalLevelAdjustmentResponseDto ProposalLevelAdjustments { get; set; }
        public ProRataCalculationResponseDto ProRataDetails { get; set; }

        // Parameters used
        public decimal ProportionRate { get; set; }
        public decimal ExchangeRate { get; set; }
        public string Currency { get; set; }
        public int CoverDays { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }
    /// <summary>
    /// Request for calculating aggregate from multiple sections
    /// </summary>
    public class MultiSectionAggregateRequestDto
    {
        public List<SectionWithCalculatedItems> Sections { get; set; } = new();
    }

    public class SectionWithCalculatedItems
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public List<CalculatedRiskItemDto> CalculatedItems { get; set; } = new();
    }

    /// <summary>
    /// Response for multi-section aggregate
    /// </summary>
    public class MultiSectionAggregateResponseDto
    {
        public List<SectionAggregateDetail> SectionAggregates { get; set; } = new();
        public decimal TotalSumInsured { get; set; }
        public decimal TotalAggregatePremium { get; set; }
        public int SectionCount { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class SectionAggregateDetail
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public decimal SectionSumInsured { get; set; }
        public decimal SectionAggregatePremium { get; set; }
        public int RiskItemCount { get; set; }
    }

    /// <summary>
    /// Request for applying proposal-level adjustments
    /// IMPORTANT: This is applied to TOTAL aggregate from ALL sections
    /// </summary>
    public class ProposalAdjustmentRequestDto
    {
        [Required]
        public decimal TotalAggregatePremium { get; set; }

        // ONLY these 2 rates at proposal level
        public decimal OtherDiscountsRate { get; set; } = 0;
        public decimal OtherLoadingsRate { get; set; } = 0;

    }

    /// <summary>
    /// Request for previewing a single section calculation
    /// </summary>
    public class SectionPreviewRequestDto
    {
        [Required]
        public FireSectionDto Section { get; set; }
        public decimal ProportionRate { get; set; } = 100;
    }

    /// <summary>
    /// Response for section preview
    /// </summary>
    public class SectionPreviewResponseDto
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public List<CalculatedRiskItemDto> CalculatedItems { get; set; } = new();
        public decimal SectionSumInsured { get; set; }
        public decimal SectionAggregatePremium { get; set; }
        public decimal ProportionRate { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Request for previewing complete calculation
    /// </summary>
    public class CompleteCalculationPreviewRequestDto
    {
        [Required]
        public List<FireSectionDto> Sections { get; set; } = new();

        // Global parameters
        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;

        // ONLY proposal-level adjustments
        public decimal OtherDiscountsRate { get; set; } = 0;
        public decimal OtherLoadingsRate { get; set; } = 0;
    }



    public class StockItemDto
    {
        public string StockCode { get; set; }
        public string StockDescription { get; set; }
        public decimal StockSumInsured { get; set; }
        public decimal StockRate { get; set; }
        public decimal StockDiscountRate { get; set; } = 0;

        //// Calculated fields
        //public decimal StockGrossPremium { get; set; }
        //public decimal StockDiscountAmount { get; set; }
        //public decimal StockNetPremium { get; set; }
    }

    /// <summary>
    /// Request DTO for updating an existing proposal
    /// </summary>
    public class UpdateProposalRequestDto
    {
        // Branch and party details (can be changed)
        public string? BranchID { get; set; }
        public string? PartyID { get; set; }
        public string? MktStaffID { get; set; }

        // Insured details (can be updated)
        public string? Surname { get; set; }
        public string? FirstName { get; set; }
        public string? OtherNames { get; set; }
        public string? InsAddress { get; set; }
        public string? InsMobilePhone { get; set; }
        public string? InsEmail { get; set; }
        public string? InsOccupation { get; set; }

        // Policy period (can be updated)
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Business details (can be changed)
        public string? BizSource { get; set; }
        public string? BusinessType { get; set; }
        public string? AccountType { get; set; }
        public string? BusChannel { get; set; }
        public string? SubChannel { get; set; }

        // Financial parameters (can be updated)
        public decimal? ProportionRate { get; set; }
        public string? Currency { get; set; }
        public decimal? ExRate { get; set; }

        public string? Remarks { get; set; }
        public string ModifiedBy { get; set; } = "SYSTEM";
    }
    public class InsuredClientDto
    {
        public string InsuredID { get; set; }
        public string Surname { get; set; }
        public string FirstName { get; set; }
        public string OtherNames { get; set; }
        public string FullName { get; set; }
        public string Occupation { get; set; }
        public string Address { get; set; }
        public string MobilePhone { get; set; }
        public string LandPhone { get; set; }
        public string Email { get; set; }
        public string Fax { get; set; }
        public string Remarks { get; set; }
        public string InsuredType { get; set; }
        public DateTime? DOB { get; set; }
        public bool Active { get; set; }
    }

    public class InsuredClientFilterDto
    {
        public string SearchTerm { get; set; }
        public string InsuredType { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class FireAdjustmentDto
    {
        public string AdjustmentName { get; set; }          // input, genuinely required
        public string? AdjustmentType { get; set; }         // output — service sets "DISCOUNT"/"LOADING"
        public decimal Rate { get; set; }                   // input
        public AdjustmentBasis AppliedOn { get; set; }      // input
        public decimal BaseAmount { get; set; }             // output
        public decimal Amount { get; set; }                 // output
        public int SequenceOrder { get; set; }              // input
        public string? Formula { get; set; }                // output
    }

    public static class FireAdjustmentTypes
    {
        public const string FeaDiscount = "FEA Discount";

        public static readonly List<string> StandardItemDiscounts = new()
        {
            FeaDiscount
        };

        public static readonly List<string> StandardItemLoadings = new()
        {
            "Special Perils",
            "Malicious Damage",
            "SRCC"
        };
        public static readonly List<string> StandardLoadings = new()
        {
            "Theft Loading",
            "SRCC Loading",
            "Other Loading 2"
        };

        public static readonly List<string> StandardDiscounts = new()
        {
            "Special Discount",
            "Deductible Discount",
            "Spread Discount",
            "LTA Discount"
        };
    }

    public class FireAdjustmentOptionsDto
    {
        public List<string> StandardItemDiscounts { get; set; } = FireAdjustmentTypes.StandardItemDiscounts;
        public List<string> StandardItemLoadings { get; set; } = FireAdjustmentTypes.StandardItemLoadings;
        public List<string> StandardDiscounts { get; set; } = FireAdjustmentTypes.StandardDiscounts;
        public List<string> StandardLoadings { get; set; } = FireAdjustmentTypes.StandardLoadings;
    }
    /// <summary>
    /// Suggested item-level adjustment names for the risk-item grid dropdowns.
    /// These are suggestions, not a whitelist — the calculation accepts any name.
    /// Only "FEA Discount" has a legacy fixed-column home (A13 rate / A14 amount);
    /// everything else lives solely in the FIRE_ITEM_* PolicyPremDetails rows.
    /// </summary>
    public class FireItemAdjustmentOptionsDto
    {
        public List<string> StandardDiscounts { get; set; } = FireAdjustmentTypes.StandardItemDiscounts;
        public List<string> StandardLoadings { get; set; } = FireAdjustmentTypes.StandardItemLoadings;
    }
    /// <summary>
    /// Request for the new dynamic section-adjustment path — parallel to
    /// SectionAdjustmentRequestDto, not a replacement for it.
    /// </summary>
    public class FireSectionDynamicAdjustmentRequestDto
    {
        public decimal AggregateSumInsured { get; set; }
        public decimal AggregatePremium { get; set; }
        public List<FireAdjustmentDto> Discounts { get; set; } = new();
        public List<FireAdjustmentDto> Loadings { get; set; } = new();
    }

    public class FireSectionDynamicAdjustmentResponseDto
    {
        public decimal StartingPremium { get; set; }
        public List<FireAdjustmentDto> DiscountsApplied { get; set; } = new();
        public List<FireAdjustmentDto> LoadingsApplied { get; set; } = new();
        public decimal TotalDiscountAmount { get; set; }
        public decimal TotalLoadingAmount { get; set; }
        public decimal NetPremiumDue { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Input for adding/editing one Fire Section via the new incremental
    /// endpoints. Mirrors FireSectionDto's shape (SectionName, Location,
    /// RiskItems, the 7 legacy fixed rates for backward compatibility) plus
    /// the new dynamic Discounts/Loadings lists. Kept as its own type here
    /// (rather than requiring FireSectionDto itself to be extended) so this
    /// file doesn't need FireSectionDto to be partial — the one place that
    /// DOES need FireSectionDto itself extended is documented separately
    /// (CompleteFireCalculationAsync's integration point, which reads
    /// section.Discounts/section.Loadings directly off FireSectionDto).
    /// </summary>
    public class FireSectionIncrementalInputDto
    {
        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public string Location { get; set; }
        public List<CleanRiskItemInputDto> RiskItems { get; set; } = new();

        // Legacy fixed rates — kept for backward compatibility with the
        // existing persist path's rate columns (A12-A16/Field11/Field12);
        // populated automatically from recognized names in Discounts/Loadings
        // below if you don't set them directly.
        public decimal SpecialDiscountRate { get; set; } = 0;
        public decimal DeductibleDiscountRate { get; set; } = 0;
        public decimal SpreadDiscountRate { get; set; } = 0;
        public decimal LtaDiscountRate { get; set; } = 0;
        public decimal TheftLoadingRate { get; set; } = 0;
        public decimal SrccLoadingRate { get; set; } = 0;
        public decimal OtherLoading2Rate { get; set; } = 0;
        public string? Field26 { get; set; }
        public string? Field7 { get; set; }
        public string? Field8 { get; set; }

        // NEW: dynamic adjustments with name + basis choice
        public List<FireAdjustmentDto> Discounts { get; set; } = new();
        public List<FireAdjustmentDto> Loadings { get; set; } = new();
    }

    /// <summary>Response for the incremental Section add/edit/remove endpoints.</summary>
    public class FireDeclarationRecomputeResponseDto
    {
        public string ProposalNo { get; set; }
        public string CertOrDocNo { get; set; }
        public decimal TotalSumInsured { get; set; }
        public decimal TotalGrossPremium { get; set; }
        public decimal TotalNetPremium { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public decimal ForeignSumInsured { get; set; }
        public decimal ForeignPremium { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public System.DateTime CalculatedOn { get; set; }
    }
}