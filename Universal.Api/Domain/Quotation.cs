using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Domain
{
    public class QuotationRequest
    {
        public string? QuoteNo { get; set; }
        //public string? AssuredCode { get; set; }
        public string? SurName { get; set; }
        public string? OtherNames { get; set; }
        public DateTime? QuotationDate { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? BranchID { get; set; }
        public string? MobileNumber { get; set; }

        public string? Email { get; set; }
        public long Duration { get; set; }
        public string? Frequency { get; set; }
        public decimal? RegularSaving { get; set; }
        public string? ProductCode { get; set; }
        public string? AgentCode { get; set; }
        public string? Product { get; set; }
        public decimal? ExtraRate { get; set; }
        public decimal? LoadingRate { get; set; }
        public string? Status { get; set; }
    }
    public class TraditionalQuotationRequest
    {
        public string? QuoteNo { get; set; }
        //public string? AssuredCode { get; set; }
        public string? SurName { get; set; }
        public string? OtherNames { get; set; }
        public string? AgentCode { get; set; }

        public DateTime? QuotationDate { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? BranchID { get; set; }
        public string? MobileNumber { get; set; }
        public string? Email { get; set; }
        public long Duration { get; set; }
        public string? Frequency { get; set; }
        public decimal? RegularSaving { get; set; }
        public string? ProductCode { get; set; }
        public long Age { get; set; }
        public string RateCode { get; set; }
        public string? Product { get; set; }
        public decimal? FirstPremium { get; set; }
        public decimal? SumAssured { get; set; }
        public string? Status { get; set; }
    }

    public class InterestRate
    {
        public int Id { get; set; }
        public string CoverCode { get; set; }
        public string Covertype { get; set; }
        public long Age { get; set; }
        public long Duration { get; set; }
        public double IntRate { get; set; }
        public string Premtype { get; set; }
    }

    public class QuoteMasterDto
    {
        public DateTime TransactionDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime ExpiryDate { get; set; }

        public string InsuredType { get; set; }
        public string SchemeType { get; set; }

        public string ClientName { get; set; }
        public decimal GroupLifeFactor { get; set; }
        public decimal GroupFactorAmount { get; set; }

        public string SubmittedBy { get; set; }

        public List<QuoteMemberDto> Members { get; set; } = new();
    }

    public class GroupLifeQuotationRequest
    {
        public string ClientName { get; set; }
        public string SchemeType { get; set; }
        public string InsuredType { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime ExpiryDate { get; set; }

        public decimal GroupLifeFactor { get; set; }
        public decimal GroupFactorAmount { get; set; }

        public bool UseFixedRate { get; set; }
        public decimal? FixedRate { get; set; }

        public bool UseAverageAge { get; set; }

        public decimal? LoadingPercent { get; set; }
        public decimal? DiscountPercent { get; set; }

        public List<MemberRequest> Members { get; set; }
    }


    public class MemberRequest
    {
        public string Surname { get; set; }
        public string OtherNames { get; set; }
        public int ANB { get; set; } // Age Next Birthday
        public decimal Emolument { get; set; }

        public bool HasPDE { get; set; }
        public bool HasTTD { get; set; }
        public bool HasME { get; set; }
        public bool HasFUN { get; set; }
        public bool HasCRL { get; set; }

        public decimal? PDE_Rate { get; set; }
        public decimal? TTD_Rate { get; set; }
        public decimal? ME_Rate { get; set; }
        public decimal? FUN_Rate { get; set; }
        public decimal? CRL_Rate { get; set; }

        public int? CoverDays { get; set; }
    }


    public class QuoteMemberDto
    {
        public bool UseFixedRate { get; set; }
        public decimal? FixedRate { get; set; }

        public bool UseAverageAge { get; set; }
        public int? AverageAge { get; set; }

        public string Surname { get; set; }
        public string OtherNames { get; set; }
        public int ANB { get; set; }

        public decimal Emolument { get; set; }

        public bool UseGpaRates { get; set; }

        // GPA Component Rates (manually set if not GPA mode)
        public decimal? TPD_Rate { get; set; }
        public decimal? TTD_Rate { get; set; }
        public decimal? MedicalRate { get; set; }
        public decimal? FuneralRate { get; set; }
        public decimal? CIRate { get; set; }

        public decimal? LoadingRate { get; set; }
        public decimal? ReductionRate { get; set; }

        public List<MemberExtensionRateDto> Extensions { get; set; } = new();
    }
    public class MemberExtensionRateDto
    {
        public string Description { get; set; } // e.g., "Permanent Disability"
        public decimal Rate { get; set; }
        public decimal ItemValue { get; set; }
    }


    public class CreateQuoteRequest
    {
        public string? CoverCode { get; set; } = "GL";          // Product code
        public string? CoverType { get; set; } = "GROUP LIFE ONLY";
        public string? ClientCode { get; set; }                 // Existing client code (optional)
        public string? ClientSurname { get; set; }
        public string? ClientOtherNames { get; set; }

        public DateTime TransactionDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime ExpiryDate { get; set; }

        public decimal GroupFactor { get; set; }                // e.g., 3x salary
        public string? CustomerType { get; set; }               // Field10

        public decimal? LoadingPercent { get; set; }            // optional
        public decimal? ReductionPercent { get; set; }          // optional

        public bool UseFixedRate { get; set; }
        public decimal? FixedRate { get; set; }                 // if UseFixedRate

        public int? CoverDays { get; set; }                     // for proration (optional)

        public List<MemberDto> Members { get; set; } = new();
    }

    public enum ExtensionType
    {
        PDE,
        TTD, // Temporary Total Disability (aka TDE in legacy)
        ME,
        FUN,
        CRL
    }


    public class MemberDto
    {
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public int ANB { get; set; }                    // Age Next Birthday
        public decimal Emolument { get; set; }          // Annual Salary

        public bool UseAverageAge { get; set; }
        public int? AverageAge { get; set; }

        // Extension rates (% per 1000 of SA) - optional
        public decimal? PDERate { get; set; }
        public decimal? TDRate { get; set; }    // TTD
        public decimal? MERate { get; set; }
        public decimal? FUNRate { get; set; }
        public decimal? CRLRate { get; set; }
    }

    public class UndPremRate
    {
        [Key]
        public int RateID { get; set; }
        public string CoverCode { get; set; }
        public string Covertype { get; set; }
        public int Term1 { get; set; }
        public int Term2 { get; set; }
        public int Yr { get; set; }
        public decimal Rate { get; set; }
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
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Remark { get; set; }
        public string Tag { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedOn { get; set; }
    }
    public class QuoteWithMembersDto
    {
        public QuotesMaster Master { get; set; }
        public List<QuoteMember> Members { get; set; } = new List<QuoteMember>();
    }


    // Models/QuoteMember.cs

    [Table("QuoteMembers")]
    public class QuoteMember
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long MemberID { get; set; } 
        public string? QuotationNo { get; set; }
        public string? EndorsementNo { get; set; }
        public string? BizOption { get; set; }
        public string? SerialNo { get; set; }
        public string? MembersNo { get; set; }
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public DateTime? DOBDate { get; set; }
        public DateTime? HireDate { get; set; }
        public string? Sex { get; set; }
        public long? Age { get; set; }
        public decimal? BasicSalary { get; set; }
        public decimal? Housing { get; set; }
        public decimal? Transport { get; set; }
        public decimal? OtherAmount { get; set; }
        public decimal? TEmolument { get; set; }
        public decimal? SumInsured { get; set; }
        public double? PremiumRate { get; set; }
        public decimal? GrossPremium { get; set; }
        public double? PDERate { get; set; }
        public decimal? PDEPremium { get; set; }
        public decimal? PDESumInsured { get; set; }
        public double? TDRate { get; set; }
        public decimal? TDPremium { get; set; }
        public decimal? TDSuminsured { get; set; }
        public double? MERate { get; set; }
        public decimal? MEPremium { get; set; }
        public decimal? MESumInsured { get; set; }
        public decimal? A1 { get; set; }
        public decimal? A2 { get; set; }
        public decimal? A3 { get; set; }
        public decimal? A4 { get; set; }
        public decimal? A5 { get; set; }
        public decimal? A6 { get; set; }
        public decimal? A7 { get; set; }
        public decimal? A8 { get; set; }
        public decimal? A9 { get; set; }
        public decimal? A10 { get; set; }
        public byte? Deleted { get; set; }
        public byte? Active { get; set; } = 1;
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? Field1 { get; set; }
        public string? Field2 { get; set; }
        public string? Field3 { get; set; }
        public string? Field4 { get; set; }
        public string? Field5 { get; set; }
        public string? Field6 { get; set; }
        public string? Field7 { get; set; }
        public string? Field8 { get; set; }
        public string? Field9 { get; set; }
        public string? Field10 { get; set; } // CustomerType
    }

    public class QuotationResult
    {
        public string? QuoteNo { get; set; }

        public decimal? AnnualPremium { get; set; }
        public decimal? SumAssured { get; set; }
        public decimal? LifeCover { get; set; }
        public decimal? Loading { get; set; }
        public decimal? ExtraPremium { get; set; }
        public decimal? TotalPremium { get; set; }
    }

    public class PagedResult<T>
    {
        public int TotalCount { get; set; }
        public IEnumerable<T> Items { get; set; }
    }
    public class ProductSettings
    {
        [Key]
        public string? ProductID { get; set; }
        public long? Min_Age { get; set; }
        public long? Max_Age { get; set; }
        public long? Min_Duration { get; set; }
        public long? Max_Duration { get; set; }
        public decimal? Min_Contribution { get; set; }
        public decimal? Max_Contribution { get; set; }
        public decimal? RateDiv_factor { get; set; }
        public decimal? Min_SA { get; set; }
        public decimal? Max_SA { get; set; }
        public string? Multiplier { get; set; }
        public decimal? A1 { get; set; }
        public decimal? A2 { get; set; }
        public string? Remarks { get; set; }
        public string? Tag { get; set; }
    }

    public class UploadMembersResult
    {
        public int Total { get; set; }
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    public class QuotesMaster
    {
        [Key]
        public long QuoteID { get; set; }
        public string? QuoteNo { get; set; }
        public string? ProposalNo { get; set; }
        public string? BrCode { get; set; }
        public DateTime? TDate { get; set; }
        public DateTime? AssDate { get; set; }
        public string? CoverCode { get; set; } // ProductCode
        public string? Covertype { get; set; }
        public string? AssuredCode { get; set; }
        public string? SurName { get; set; }
        public string? OtherNames { get; set; }
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? Landphone { get; set; }
        public string? MobilePhone { get; set; }
        public string? Email { get; set; }
        public string? NationalID { get; set; }
        public string? Occupation { get; set; }
        public string? Sex { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Country { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? MaturityDate { get; set; }
        public double? Duration { get; set; }
        public DateTime? DateofBirth { get; set; }
        public decimal? SumAssured { get; set; }
        public string? CustomerType { get; set; }
        public string? CardNo { get; set; }
        public string? Status { get; set; }
        public string? Medical { get; set; }

        public decimal? Premium1 { get; set; } // Annual Premium
        public decimal? Premium2 { get; set; } // Extra Premium
        public decimal? Premium3 { get; set; } // Loading
        public double? A1 { get; set; }
        public double? A2 { get; set; }

        public double? TPDRate { get; set; }
        public double? TTDRAte { get; set; }
        public double? CIRate { get; set; }
        public double? MedicalRate { get; set; }
        public double? FuneralRate { get; set; }
               
        public decimal? SUPPSA { get; set; }
        public decimal? ToAllocate { get; set; }

        public decimal? Loading { get; set; }
        public decimal? XMas { get; set; }
        public decimal? ADBSA { get; set; }

        public decimal? BasicPremium { get; set; } // Same as Premium1
        public decimal? IntrestRate { get; set; }
        public decimal? TotalPremium { get; set; }

        public string? TransSTATUS { get; set; }
        public string? TransGUID { get; set; }
        public string? Remarks { get; set; }
        public string? Tag { get; set; }

        public long? isQuote { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        //public ICollection<QuoteMember> QuoteMembers { get; set; }
        //public virtual ICollection<QuoteMember> Members { get; set; } = new List<QuoteMember>();


    }


    public static class FrequencyFactor
    {
        public static readonly Dictionary<string, int> Values = new()
    {
        { "Single", 1 },
        { "Monthly", 12 },
        { "Quarterly", 4 },
        { "Half-Yearly", 2 },
        { "Annually", 1 },
        { "Weekly", 52 },
        { "Fortnightly", 26 },
        { "Daily", 365 },
        { "Daily (5 Days)", 261 },
        { "Daily (6 Days)", 313 },
        { "Daily (7 Days)", 365 }
    };
    }


    public class GroupLifeQuotationDto
    {
        public string QuotationNumber { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string InsuredType { get; set; }
        public string SchemeType { get; set; }
        public string ClientName { get; set; }
        public decimal GroupLifeFactor { get; set; }
        public decimal GroupFactorAmount { get; set; }
        public bool UseFixedRate { get; set; }
        public decimal? FixedRate { get; set; }
        public bool UseAverageAge { get; set; }
        public decimal? AverageAge { get; set; }
        public decimal LoadingRate { get; set; }
        public decimal ReductionRate { get; set; }
        public int CoverDays { get; set; } = 365;
        public List<MemberQuotationDto> Members { get; set; } = new List<MemberQuotationDto>();
        public ExtensionRatesDto ExtensionRates { get; set; } = new ExtensionRatesDto();
    }

    public class MemberQuotationDto
    {
        public long MemberID { get; set; }
        public string Surname { get; set; }
        public string OtherNames { get; set; }
        public int ANB { get; set; } // Age Next Birthday
        public decimal TotalEmolument { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal Housing { get; set; }
        public decimal Transport { get; set; }
        public decimal OtherAmount { get; set; }
        public string Sex { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }

    public class ExtensionRatesDto
    {
        public decimal PDERate { get; set; } // Permanent Disability Extension
        public decimal TDERate { get; set; } // Temporary Total Disability
        public decimal MERate { get; set; } // Medical Expenses
        public decimal FUNRate { get; set; } // Funeral Extension
        public decimal CRLRate { get; set; } // Critical Illness
    }

    public class QuotationCalculationResultDto
    {
        public string QuotationNumber { get; set; }
        public decimal TotalSumAssured { get; set; }
        public decimal TotalBasicPremium { get; set; }
        public decimal TotalExtensionPremiums { get; set; }
        public decimal TotalLoading { get; set; }
        public decimal TotalReduction { get; set; }
        public decimal NetPremium { get; set; }
        public List<MemberCalculationDto> MemberCalculations { get; set; } = new List<MemberCalculationDto>();
    }

    //public class MemberCalculationDto
    //{
    //    public long MemberID { get; set; }
    //    public string Surname { get; set; }
    //    public string OtherNames { get; set; }
    //    public int ANB { get; set; }
    //    public decimal TotalEmolument { get; set; }
    //    public decimal SumAssured { get; set; }
    //    public decimal PremiumRate { get; set; }
    //    public decimal ActualPremium { get; set; }
    //    public decimal PDEPremium { get; set; }
    //    public decimal TDEPremium { get; set; }
    //    public decimal MEPremium { get; set; }
    //    public decimal FUNPremium { get; set; }
    //    public decimal CRLPremium { get; set; }
    //    public decimal TotalExtensionPremiums { get; set; }
    //    public decimal Loading { get; set; }
    //    public decimal Reduction { get; set; }
    //    public decimal NetPremium { get; set; }
    //}


    public class QuotationRequestDto
    {
        public string QuotationNumber { get; set; }
        [Required]
        public DateTime TransactionDate { get; set; }
        [Required]
        public DateTime EffectiveDate { get; set; }
        [Required]
        public DateTime ExpiryDate { get; set; }
        [Required]
        public string InsuredType { get; set; }
        [Required]
        public string SchemeType { get; set; }
        [Required]
        public string ClientName { get; set; }
        [Required]
        public decimal GroupLifeFactor { get; set; }
        [Required]
        public decimal GroupFactorAmount { get; set; }
        public bool UseFixedRate { get; set; }
        public decimal FixedRate { get; set; }
        public bool UseAverageAge { get; set; }
        public int AverageAge { get; set; }
        public decimal LoadingRate { get; set; }
        public decimal ReductionRate { get; set; }
        public int CoverDays { get; set; } = 365;
        public List<MemberDetailsDto> Members { get; set; } = new List<MemberDetailsDto>();
    }

    public class MemberDetailsDto
    {
        public string StaffNo { get; set; }
        [Required]
        public string EmployeeNames { get; set; }
        [Required]
        public string Sex { get; set; }
        [Required]
        public DateTime DateOfBirth { get; set; }
        public int ANB => CalculateANB(DateOfBirth);
        [Required]
        public decimal BasicSalary { get; set; }
        public decimal HousingAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal TotalEmolument => BasicSalary + HousingAllowance + TransportAllowance;
        public DateTime HireDate { get; set; }

        // Extension rates per member
        public decimal PermanentDisabilityRate { get; set; }
        public decimal TemporaryDisabilityRate { get; set; }
        public decimal MedicalExpensesRate { get; set; }
        public decimal FuneralExtensionRate { get; set; }
        public decimal CriticalIllnessRate { get; set; }

        private static int CalculateANB(DateTime dateOfBirth)
        {
            if (dateOfBirth == DateTime.MinValue) return 0;

            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;

            // Check if birthday has occurred this year
            if (dateOfBirth.Date > today.AddYears(-age))
                age--;

            return age + 1; // Age Next Birthday
        }
    }

    //public class GroupLifeExtensionsDto
    //{
    //    public bool UsePDE { get; set; }
    //    public decimal PDERate { get; set; }

    //    public bool UseTDE { get; set; }
    //    public decimal TDERate { get; set; }

    //    public bool UseME { get; set; }
    //    public decimal MERate { get; set; }

    //    public bool UseFUN { get; set; }
    //    public decimal FUNRate { get; set; }

    //    public bool UseCRL { get; set; }
    //    public decimal CRLRate { get; set; }
    //}

    public class QuotationResponseDto
    {
        public string? QuotationNumber { get; set; }
        public decimal? TotalSumInsured { get; set; }
        public decimal? TotalBasicPremium { get; set; }
        public decimal? TotalExtensionPremiums { get; set; }
        public decimal? LoadingAmount { get; set; }
        public decimal? ReductionAmount { get; set; }
        public decimal? NetPremium { get; set; }
        public List<MemberCalculationDto>? MemberCalculations { get; set; } = new List<MemberCalculationDto>();
        public ExtensionSummaryDto? ExtensionSummary { get; set; } = new ExtensionSummaryDto();
    }

    public class MemberCalculationDto
    {
        public string?  StaffNo { get; set; }
        public string? EmployeeNames { get; set; }
        public string? Sex { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? ANB { get; set; }
        public decimal? BasicSalary { get; set; }
        public decimal? HousingAllowance { get; set; }
        public decimal? TransportAllowance { get; set; }
        public decimal? TotalEmolument { get; set; }
        public decimal? SumAssured { get; set; }
        public decimal? PremiumRate { get; set; }
        public decimal? ActualPremium { get; set; }
        public decimal? PDEPremium { get; set; }
        public decimal? TDEPremium { get; set; }
        public decimal? MEPremium { get; set; }
        public decimal? FUNPremium { get; set; }
        public decimal? CRLPremium { get; set; }
        public decimal? TotalMemberPremium { get; set; }
        public DateTime? HireDate { get; set; }
    }

    public class ExtensionSummaryDto
    {
        public decimal? TotalPDEPremium { get; set; }
        public decimal? TotalTDEPremium { get; set; }
        public decimal? TotalMEPremium { get; set; }
        public decimal? TotalFUNPremium { get; set; }
        public decimal? TotalCRLPremium { get; set; }
    }

}
