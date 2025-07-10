using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

}
