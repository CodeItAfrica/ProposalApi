using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace GibsLifesMicroWebApi.Models
{
    // ============================================================
    // MOTOR-ONLY DTOs
    // Shared types (AdjustmentDetailDto, ProRataCalculation*, PolicyDetailDto,
    // ResponseInfo, etc.) live in the main entities file — same namespace/assembly,
    // so the references below resolve without any using changes.
    // ============================================================

    /// <summary>
    /// Vehicle aggregation breakdown
    /// </summary>
    public class VehicleAggregationDto
    {
        public int TotalVehicles { get; set; }
        public decimal TotalVehicleValue { get; set; }
        public decimal TotalNetPremium { get; set; }
        public List<VehicleSummaryDto> VehicleSummaries { get; set; } = new();
    }

    /// <summary>
    /// Proposal-level adjustment breakdown
    /// </summary>
    public class ProposalAdjustmentBreakdownDto
    {
        public decimal AggregatePremium { get; set; }

        public decimal OtherDiscountRate { get; set; }
        public decimal OtherDiscountAmount { get; set; }
        public string OtherDiscountFormula { get; set; }

        public decimal PremiumAfterDiscount { get; set; }

        public decimal OtherLoadingRate { get; set; }
        public decimal OtherLoadingAmount { get; set; }
        public string OtherLoadingFormula { get; set; }

        public decimal NetPremiumDue { get; set; }
    }

    public class MotorAdjustmentOptionsDto
    {
        public List<string> StandardDiscounts { get; set; } = new()
    {
        "NO CLAIMS DISCOUNT (NCD)",
        "FLEET DISCOUNT",
        "SPECIAL DISCOUNT",
        "LOYALTY DISCOUNT",
        "MULTIPLE VEHICLE DISCOUNT"
    };

        public List<string> StandardLoadings { get; set; } = new()
    {
        "TPPD (Third Party Property Damage)",
        "SRCC (Strike, Riot & Civil Commotion)",
        "FLOOD",
        "EXCESS BUY BACK",
        "TPFT (Third Party Fire & Theft)",
        "YOUNG DRIVER LOADING",
        "HIGH RISK AREA LOADING",
        "VEHICLE AGE LOADING"
    };

        public List<string> ProposalLevelAdjustments { get; set; } = new()
    {
        "OTHER DISCOUNT",
        "OTHER LOADING"
    };
    }

    // Motor DTOs with Dynamic Discounts and Loadings


    /// <summary>
    /// Step 1: Calculate basic premium
    /// Only vehicle essentials needed
    /// </summary>
    public class MotorStep1RequestDto
    {
        public string VehicleType { get; set; }
        public decimal VehicleValue { get; set; }
        public decimal PremiumRate { get; set; }
        public string CoverType { get; set; }
        public string Usage { get; set; }
    }

    /// <summary>
    /// Step 2: Apply discounts
    /// Only basic premium, vehicle value, and discounts needed
    /// </summary>
    public class MotorStep2RequestDto
    {
        public decimal BasicPremium { get; set; }
        public decimal VehicleValue { get; set; }
        public List<MotorAdjustmentDto> Discounts { get; set; } = new();
    }

    /// <summary>
    /// Step 3: Apply loadings
    /// Only premium after discounts, vehicle value, and loadings needed
    /// </summary>
    public class MotorStep3RequestDto
    {
        public decimal PremiumAfterDiscounts { get; set; }
        public decimal VehicleValue { get; set; }
        public List<MotorAdjustmentDto> Loadings { get; set; } = new();
    }

    /// <summary>
    /// Step 4: Add additional costs
    /// Only gross premium and costs needed
    /// </summary>
    public class MotorStep4RequestDto
    {
        public decimal GrossPremium { get; set; }
        public decimal TrackingCost { get; set; } = 0;
        public decimal RescueCost { get; set; } = 0;
    }

    /// <summary>
    /// Step 5: Aggregate all vehicles
    /// Only list of calculated vehicles needed
    /// </summary>
    public class MotorStep5RequestDto
    {
        public List<VehicleSummaryDto> Vehicles { get; set; } = new();
    }

    public class VehicleSummaryDto
    {
        public int ItemNo { get; set; }
        public string VehicleRegNo { get; set; }
        public decimal VehicleValue { get; set; }
        public decimal NetPremium { get; set; }
    }

    /// <summary>
    /// Step 6: Apply proposal-level adjustments
    /// Only aggregate premium and two rates needed
    /// </summary>
    public class MotorStep6RequestDto
    {
        public decimal AggregatePremium { get; set; }
        public decimal OtherDiscountRate { get; set; } = 0;
        public decimal OtherLoadingRate { get; set; } = 0;
    }

    /// <summary>
    /// Step 7: Final calculation with pro-rata and proportion
    /// Only net premium, cover days, and rates needed
    /// </summary>
    public class MotorStep7RequestDto
    {
        public decimal NetPremium { get; set; }
        public decimal TotalVehicleValue { get; set; }
        public int CoverDays { get; set; } = 365;
        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
    }

    /// <summary>
    /// Step 5 Response: Aggregate vehicles
    /// </summary>
    public class MotorAggregateVehiclesResponseDto
    {
        public decimal TotalVehicleValue { get; set; }
        public decimal TotalNetPremium { get; set; }
        public int VehicleCount { get; set; }
        public List<VehicleSummaryDto> VehicleSummaries { get; set; } = new();

        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Step 6 Response: Proposal-level adjustments
    /// </summary>
    public class MotorProposalAdjustmentsResponseDto
    {
        public decimal AggregatePremium { get; set; }
        public decimal OtherDiscountRate { get; set; }
        public decimal OtherDiscountAmount { get; set; }
        public decimal PremiumAfterDiscount { get; set; }
        public decimal OtherLoadingRate { get; set; }
        public decimal OtherLoadingAmount { get; set; }
        public decimal NetPremiumDue { get; set; }

        public string OtherDiscountFormula { get; set; }
        public string OtherLoadingFormula { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Step 7 Response: Final calculation
    /// </summary>
    public class MotorFinalCalculationResponseDto
    {
        public decimal NetPremium { get; set; }
        public decimal TotalVehicleValue { get; set; }

        // Pro-rata calculation
        public int CoverDays { get; set; }
        public decimal ProRataPremium { get; set; }
        public string ProRataFormula { get; set; }

        // Proportion calculation
        public decimal ProportionRate { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }

        // Foreign currency
        public decimal ExchangeRate { get; set; }
        public string Currency { get; set; }
        public decimal ForeignSumInsured { get; set; }
        public decimal ForeignPremium { get; set; }

        // Final amount
        public decimal FinalPremiumDue { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }


    /// <summary>
    /// Complete calculated vehicle with all steps
    /// </summary>
    public class CalculatedMotorRiskItemDto : MotorRiskItemInputDto
    {
        public string CertificateNo { get; set; }

        // Step 1: Basic premium
        public MotorBasicPremiumResponseDto Step1_BasicPremium { get; set; }

        // Step 2: Discounts
        public MotorDiscountsResponseDto Step2_Discounts { get; set; }

        // Step 3: Loadings
        public MotorLoadingsResponseDto Step3_Loadings { get; set; }

        // Step 4: Final premium
        public MotorFinalPremiumResponseDto Step4_FinalPremium { get; set; }

        // Final values for easy access
        public decimal BasicPremium => Step1_BasicPremium?.BasicPremium ?? 0;
        public decimal GrossPremium => Step3_Loadings?.GrossPremium ?? 0;
        public decimal NetPremium => Step4_FinalPremium?.NetPremium ?? 0;
        public decimal TotalDiscountsAmount => Step2_Discounts?.TotalDiscountAmount ?? 0;
        public decimal TotalLoadingsAmount => Step3_Loadings?.TotalLoadingAmount ?? 0;

        // Share calculations
        public decimal ShareValue { get; set; }
        public decimal SharePremium { get; set; }
    }

    /// <summary>
    /// Request for step-by-step calculation
    /// </summary>
    public class MotorStepCalculationRequestDto
    {
        public string ProposalNo { get; set; }
        public MotorRiskItemInputDto Vehicle { get; set; }
        public decimal ProportionRate { get; set; } = 100;
    }

    /// <summary>
    /// Complete motor calculation request
    /// </summary>
    public class MotorCalculationRequestDto
    {
        public string ProposalNo { get; set; }
        public List<MotorRiskItemInputDto> Vehicles { get; set; } = new();

        // Proposal-level adjustments (NEW)
        public decimal OtherDiscountRate { get; set; } = 0;
        public decimal OtherLoadingRate { get; set; } = 0;

        // Final calculation parameters
        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;

        public string CalculatedBy { get; set; } = "SYSTEM";
    }

    /// <summary>
    /// Complete motor calculation response
    /// </summary>
    public class MotorCalculationResponseDto
    {
        public string ProposalNo { get; set; }
        public List<CalculatedMotorRiskItemDto> CalculatedVehicles { get; set; } = new();

        // Vehicle-level totals (before proposal adjustments)
        public decimal TotalVehicleValue { get; set; }
        public string CertOrDocNo { get; set; }
        public decimal TotalGrossPremium { get; set; }
        public decimal TotalNetPremium { get; set; }

        // Proposal-level adjustments (NEW)
        public MotorProposalAdjustmentsResponseDto ProposalAdjustments { get; set; }
        public decimal NetPremiumAfterProposalAdjustments { get; set; }

        // Final calculations
        public decimal ProRataPremium { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public decimal FinalPremiumDue { get; set; }

        // Foreign currency
        public decimal ForeignSumInsured { get; set; }
        public decimal ForeignPremium { get; set; }

        // Metadata
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime CalculatedOn { get; set; }
    }
    public class MotorCalculationInputsDto
    {
        public decimal ProportionRate { get; set; }
        public decimal ExchangeRate { get; set; }
        public string Currency { get; set; }
        public int CoverDays { get; set; }
        public int VehicleCount { get; set; }
        public decimal OtherDiscountRate { get; set; }
        public decimal OtherLoadingRate { get; set; }
    }
    /// <summary>
    /// Breakdown showing calculation steps for motor
    /// </summary>
    public class MotorCalculationBreakdownDto
    {
        public string ProposalNo { get; set; }
        public DateTime CalculatedOn { get; set; }
        public string CalculatedBy { get; set; }

        // Input parameters
        public MotorCalculationInputsDto Inputs { get; set; }

        // Vehicle-by-vehicle breakdown
        public List<VehicleCalculationBreakdownDto> VehicleCalculations { get; set; } = new();

        // Vehicle aggregation
        public VehicleAggregationDto VehicleAggregation { get; set; }

        // Proposal-level adjustments
        public ProposalAdjustmentBreakdownDto ProposalAdjustments { get; set; }

        // Final results
        public MotorCalculationResultsDto FinalResults { get; set; }
    }
    public class MotorCalculationResultsDto
    {
        public decimal TotalVehicleValue { get; set; }
        public decimal TotalNetPremium { get; set; }
        public decimal NetPremiumAfterProposalAdjustments { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal ShareSumInsured { get; set; }
        public decimal SharePremium { get; set; }
        public decimal ForeignSumInsured { get; set; }
        public decimal ForeignPremium { get; set; }
        public string ForeignCurrency { get; set; }
        public decimal FinalPremiumDue { get; set; }
    }

    /// <summary>
    /// Detailed breakdown for a single vehicle
    /// </summary>
    public class VehicleCalculationBreakdownDto
    {
        public int ItemNo { get; set; }
        public string VehicleRegNo { get; set; }
        public string CertificateNo { get; set; }
        public string VehicleType { get; set; }
        public string CoverType { get; set; }
        public string VehicleUser { get; set; }
        public string VehicleMake { get; set; }
        public string VehicleModel { get; set; }
        public string ChassisNo { get; set; }
        public string EngineNo { get; set; }
        public string Color { get; set; }
        public int ModelYear { get; set; }
        public int SeatCapacity { get; set; }
        public string State { get; set; }
        public string Usage { get; set; }
        public string WaxCode { get; set; }
        // Input values
        public decimal VehicleValue { get; set; }
        public decimal PremiumRate { get; set; }

        // Calculation steps
        public CalculationStepDto Step1_BasicPremium { get; set; }
        public CalculationStepDto Step2_AfterDiscounts { get; set; }
        public CalculationStepDto Step3_AfterLoadings { get; set; }
        public CalculationStepDto Step4_FinalPremium { get; set; }

        // Share calculations
        public decimal ShareValue { get; set; }
        public decimal SharePremium { get; set; }
    }


    public class CalculationStepDto
    {
        public string StepName { get; set; }
        public decimal StartingAmount { get; set; }
        public List<AdjustmentDetailDto> Adjustments { get; set; } = new();
        public decimal TotalAdjustment { get; set; }
        public decimal ResultingAmount { get; set; }
        public string Formula { get; set; }
    }
    /// <summary>
    /// Update an existing motor calculation identified by DetailID
    /// </summary>
    public class UpdateMotorCalculationRequestDto
    {
        [Required]
        public string DetailID { get; set; }

        public List<MotorRiskItemInputDto> Vehicles { get; set; } = new();

        public decimal OtherDiscountRate { get; set; } = 0;
        public decimal OtherLoadingRate { get; set; } = 0;

        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
        public int CoverDays { get; set; } = 365;

        public string RecalculatedBy { get; set; } = "SYSTEM";
        public string Remarks { get; set; }
    }

    /// <summary>
    /// List item for all motor calculations on a policy
    /// </summary>
    public class MotorCalculationListItemDto
    {
        public string DetailID { get; set; }
        public string ProposalNo { get; set; }
        public string CertOrDocNo { get; set; }
        public DateTime CalculatedOn { get; set; }
        public string CalculatedBy { get; set; }
        public string CalculationType { get; set; }   // BizOption
        public decimal TotalSumInsured { get; set; }
        public decimal TotalPremium { get; set; }
        public decimal NetPremium { get; set; }
        public decimal ProRataPremium { get; set; }
        public int VehicleCount { get; set; }
        public string Status { get; set; }             // ACTIVE / SUPERSEDED / DELETED
        public string Remarks { get; set; }
    }
    public class VehicleBasePremiumDto
    {
        public bool IsThirdPartyOrFlat { get; set; }
        public decimal FlatPremiumAmount { get; set; } // If third party
        public string BasicPremiumFormula { get; set; }
        public decimal BasicPremium { get; set; }
    }
    /// <summary>
    /// Application basis for adjustments
    /// </summary>
    public enum AdjustmentBasis
    {
        SumInsured,  // Apply rate on vehicle value
        Premium      // Apply rate on current premium
    }

    public static class MotorAdjustmentTypes
    {
        public static readonly List<string> StandardDiscounts = new()
    {
        "NO CLAIMS DISCOUNT (NCD)",
        "FLEET DISCOUNT",
        "SPECIAL DISCOUNT",
        "LOYALTY DISCOUNT",
        "MULTIPLE VEHICLE DISCOUNT"
    };

        public static readonly List<string> StandardLoadings = new()
    {
        "TPPD (Third Party Property Damage)",
        "SRCC (Strike, Riot & Civil Commotion)",
        "FLOOD",
        "EXCESS BUY BACK",
        "TPFT (Third Party Fire & Theft)",
        "YOUNG DRIVER LOADING",
        "HIGH RISK AREA LOADING",
        "VEHICLE AGE LOADING"
    };
    }


    /// <summary>
    /// Enhanced adjustment DTO with basis selection
    /// </summary>
    public class MotorAdjustmentDto
    {
        public string AdjustmentName { get; set; }
        public string AdjustmentType { get; set; } // "DISCOUNT" or "LOADING"
        public decimal Rate { get; set; }
        public AdjustmentBasis AppliedOn { get; set; } // SumInsured or Premium
        public decimal BaseAmount { get; set; } // The amount the rate was applied on (calculated)
        public decimal Amount { get; set; } // Calculated amount
        public int SequenceOrder { get; set; }
        public string Formula { get; set; } // For display: "5% of Premium" or "2% of Sum Insured"
    }

    /// <summary>
    /// Input for a single motor vehicle - SIMPLIFIED
    /// </summary>
    public class MotorRiskItemInputDto
    {
        // Vehicle identification
        public int ItemNo { get; set; }
        public string VehicleRegNo { get; set; }
        public string VehicleUser { get; set; }
        public string VehicleType { get; set; }
        public string VehicleMake { get; set; }
        public string VehicleModel { get; set; }
        public string ChassisNo { get; set; }
        public string EngineNo { get; set; }
        public string Color { get; set; }
        public int ModelYear { get; set; }
        public string CoverType { get; set; } // COMPREHENSIVE, THIRD_PARTY, etc.
        public string Usage { get; set; } // PRIVATE, COMMERCIAL, etc.

        // Financial details
        public decimal VehicleValue { get; set; }
        public decimal PremiumRate { get; set; }

        // Location details
        public string State { get; set; }
        public int SeatCapacity { get; set; }
        public string WaxCode { get; set; }
        public string Location { get; set; }

        // Cover period
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Additional costs (optional)
        public decimal TrackingCost { get; set; } = 0;
        public decimal RescueCost { get; set; } = 0;

        // Dynamic adjustments - DISCOUNTS FIRST, THEN LOADINGS
        public List<MotorAdjustmentDto> Discounts { get; set; } = new();
        public List<MotorAdjustmentDto> Loadings { get; set; } = new();
    }

    /// <summary>
    /// Step 1 Response: Basic premium calculation
    /// </summary>
    public class MotorBasicPremiumResponseDto
    {
        public int ItemNo { get; set; }
        public string VehicleRegNo { get; set; }
        public string CoverType { get; set; }

        // Basic calculation
        public bool IsThirdPartyOrFlat { get; set; }
        public decimal VehicleValue { get; set; }
        public decimal PremiumRate { get; set; }
        public decimal BasicPremium { get; set; }
        public string CalculationFormula { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Step 2 Response: After applying discounts
    /// </summary>
    public class MotorDiscountsResponseDto
    {
        public int ItemNo { get; set; }
        public decimal BasicPremium { get; set; }
        public decimal VehicleValue { get; set; }

        public List<MotorAdjustmentDto> DiscountsApplied { get; set; } = new();
        public decimal TotalDiscountAmount { get; set; }
        public decimal PremiumAfterDiscounts { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Step 3 Response: After applying loadings
    /// </summary>
    public class MotorLoadingsResponseDto
    {
        public int ItemNo { get; set; }
        public decimal PremiumBeforeLoadings { get; set; }
        public decimal VehicleValue { get; set; }

        public List<MotorAdjustmentDto> LoadingsApplied { get; set; } = new();
        public decimal TotalLoadingAmount { get; set; }
        public decimal GrossPremium { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Step 4 Response: After adding additional costs
    /// </summary>
    public class MotorFinalPremiumResponseDto
    {
        public int ItemNo { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal TrackingCost { get; set; }
        public decimal RescueCost { get; set; }
        public decimal NetPremium { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Excel upload DTO
    /// </summary>
    public class MotorExcelUploadDto
    {
        public string ProposalNo { get; set; }
        public IFormFile ExcelFile { get; set; }
        public decimal ProportionRate { get; set; } = 100;
        public decimal ExchangeRate { get; set; } = 1;
        public string Currency { get; set; } = "NAIRA";
    }

    public class AddMotorVehicleRequestDto
    {
        public MotorRiskItemInputDto Vehicle { get; set; }
        public decimal ProportionRate { get; set; } = 100;
    }

    public class MotorAdjustmentTypesDto
    {
        public List<string> StandardDiscounts { get; set; }
        public List<string> StandardLoadings { get; set; }
        public List<string> ProposalLevelAdjustments { get; set; }
        public List<string> CoverTypes { get; set; }
        public List<string> UsageTypes { get; set; }
    }

    /// <summary>
    /// A vehicle from a source motor calculation that can be renewed.
    /// SelectionToken is the vehicle's CertificateNo (ENDTNum) — pass it back in the renew request.
    /// </summary>
    public class RenewableVehicleDto
    {
        public string SelectionToken { get; set; }   // source vehicle CertificateNo (ENDTNum)
        public string CertificateNo { get; set; }
        public string VehicleRegNo { get; set; }
        public string VehicleType { get; set; }
        public string VehicleUser { get; set; }
        public decimal VehicleValue { get; set; }
        public decimal NetPremium { get; set; }
    }

    /// <summary>
    /// Request to renew selected vehicles of a source motor calculation into a NEW calculation.
    /// Empty/null SelectedVehicleTokens = renew ALL vehicles of the source.
    /// </summary>
    public class RenewMotorCalculationRequestDto
    {
        public List<string> SelectedVehicleTokens { get; set; } = new();  // source vehicle CertificateNos
        public string RenewedBy { get; set; } = "SYSTEM";
        public string Remarks { get; set; }
    }

    public class RenewMotorCalculationResponseDto
    {
        public string ProposalNo { get; set; }
        public string SourceDetailID { get; set; }
        public string SourceCertOrDocNo { get; set; }
        public string NewCertOrDocNo { get; set; }
        public string NewBizOption { get; set; } = "RENEWAL";
        public string Status { get; set; } = "UNCOMPLETED";   // stored in Field50
        public int RenewedVehicleCount { get; set; }
        public decimal TotalVehicleValue { get; set; }
        public decimal TotalNetPremium { get; set; }
        public decimal ProRataPremium { get; set; }
        public decimal FinalPremiumDue { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public string NextStep { get; set; }
    }

    public class MotorTPDPremiumDto
    {
        public string VehicleType { get; set; }
        public string Usage { get; set; }
        public string CoverType { get; set; }
        public decimal FlatPremium { get; set; }
    }
    // ============================================================
    // MOTOR — BULK VEHICLE UPLOAD DTOs (Excel / CSV)
    //
    // Parsing produces a MotorVehicleUploadPreviewDto: one row per data row in
    // the file, each carrying either a fully-mapped MotorRiskItemInputDto or
    // the issues that stopped it mapping. Nothing here touches the database —
    // the upload endpoint feeds Vehicles into the SAME calculation paths the
    // JSON endpoints use, so file-sourced and API-sourced vehicles are
    // calculated and persisted identically.
    //
    // An upload ADDS VEHICLES to the proposal's declaration by default — see
    // MotorVehicleUploadMode.
    //
    // NOTE: this supersedes the older MotorExcelUploadDto /
    // ProcessMotorExcelUploadAsync pair, whose ReadMotorExcelFileAsync is an
    // empty stub that returns zero vehicles — it "succeeds" while importing
    // nothing. Delete that path or point it here.
    // ============================================================

    public enum MotorVehicleUploadMode
    {
        /// <summary>
        /// DEFAULT. Incremental append: every valid row is added to whatever is
        /// already persisted for the declaration, then declaration totals are
        /// recomputed once (Motor's Other Discount/Loading rates re-applied
        /// against the new combined total, proportion+FX redone, header
        /// updated). Same semantics as POST /motor/vehicles, batched into a
        /// single transaction. Nothing already on the proposal is deleted.
        /// </summary>
        Append = 0,

        /// <summary>
        /// Full replace — OPT-IN, destructive. The file becomes THE vehicle list
        /// for the proposal: every existing MOTOR row is deleted first, then
        /// rewritten from the file. Runs through CalculateMotorCompleteAsync +
        /// SaveMotorCalculationAsync(deleteExisting: true). Only use this when
        /// the file genuinely contains every vehicle that should exist.
        /// </summary>
        Replace = 1
    }

    public class MotorVehicleUploadIssueDto
    {
        /// <summary>1-based row number as it appears in the sheet/file (0 = file-level issue).</summary>
        public int RowNumber { get; set; }

        /// <summary>Column header the issue relates to, or null for a whole-row issue.</summary>
        public string Column { get; set; }

        /// <summary>"ERROR" (row skipped) or "WARNING" (row imported, worth reviewing).</summary>
        public string Severity { get; set; } = "ERROR";

        public string Message { get; set; }
    }

    public class MotorVehicleUploadRowDto
    {
        public int RowNumber { get; set; }
        public bool IsValid { get; set; }

        /// <summary>Populated only when IsValid — otherwise see Issues.</summary>
        public MotorRiskItemInputDto Vehicle { get; set; }

        public List<MotorVehicleUploadIssueDto> Issues { get; set; } = new();
    }

    public class MotorVehicleUploadPreviewDto
    {
        public string FileName { get; set; }
        public string FileType { get; set; }      // CSV / XLSX / XLS
        public string SheetName { get; set; }     // Excel only
        public int HeaderRowNumber { get; set; }

        public int TotalDataRows { get; set; }
        public int ValidRowCount { get; set; }
        public int InvalidRowCount { get; set; }
        public int WarningCount { get; set; }

        /// <summary>Column headers that were understood and what they mapped to.</summary>
        public List<string> RecognizedColumns { get; set; } = new();

        /// <summary>
        /// Column headers that were ignored. Check this first when values come
        /// through blank — a typo'd header lands here rather than failing loudly.
        /// </summary>
        public List<string> UnrecognizedColumns { get; set; } = new();

        public List<MotorVehicleUploadRowDto> Rows { get; set; } = new();

        /// <summary>Issues with the file as a whole (unreadable, no header, empty, too many rows).</summary>
        public List<MotorVehicleUploadIssueDto> FileIssues { get; set; } = new();

        /// <summary>The vehicles ready to be calculated — valid rows only, in file order.</summary>
        public List<MotorRiskItemInputDto> Vehicles =>
            Rows.Where(r => r.IsValid && r.Vehicle != null).Select(r => r.Vehicle).ToList();

        /// <summary>Rows that could NOT be imported, with their reasons.</summary>
        public List<MotorVehicleUploadRowDto> SkippedRows =>
            Rows.Where(r => !r.IsValid).ToList();

        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class MotorVehicleUploadResultDto
    {
        public string ProposalNo { get; set; }
        public MotorVehicleUploadMode Mode { get; set; }

        /// <summary>
        /// What the parser made of the file — always returned. Upload.SkippedRows
        /// is what the UI should surface after an import: those rows are the ones
        /// the user still needs to key in or fix and re-upload.
        /// </summary>
        public MotorVehicleUploadPreviewDto Upload { get; set; }

        public int VehiclesImported { get; set; }
        public int VehiclesSkipped { get; set; }

        /// <summary>
        /// CERTNO of each vehicle actually written, in file order. These are the
        /// handles for the per-vehicle edit/remove endpoints
        /// (PUT/DELETE motor/vehicles?certificateNo=…), so the user can review
        /// and correct each imported vehicle individually.
        /// </summary>
        public List<string> ImportedCertificateNos { get; set; } = new();

        /// <summary>Declaration state after the import — same shape the JSON endpoints return.</summary>
        public MotorCalculationResponseDto Calculation { get; set; }

        public bool Success { get; set; }
        public string Message { get; set; }
    }
}