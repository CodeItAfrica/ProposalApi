using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class PolicyMaster
    {
        [Key]
        public long PolicyID { get; set; }
        public string? PolicyNo { get; set; }
        public string? ProposalNo { get; set; }
        public string? BrCode { get; set; }
        public DateTime? TDate { get; set; }
        public DateTime? PropDate { get; set; }
        public DateTime? AssDate { get; set; }
        public string? CoverCode { get; set; }
        public string? Covertype { get; set; }
        public string? Title { get; set; }
        public string? AssuredCode { get; set; }
        public string? SurName { get; set; }
        public string? OtherNames { get; set; }
        public string? FullName { get; set; }
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
        public string? Agentcode { get; set; }
        public string? AgentDescription { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? MaturityDate { get; set; }
        public double? Duration { get; set; }
        public DateTime? DateofBirth { get; set; }
        public string? FOP { get; set; }
        public decimal? Age { get; set; }
        public string? MOP { get; set; }
        public decimal? SumAssured { get; set; }
        public decimal? Premium1 { get; set; }
        public decimal? Premium2 { get; set; }
        public decimal? Premium3 { get; set; }
        public decimal? BasicPremium { get; set; }
        public decimal? IntrestRate { get; set; }
        public decimal? TotalPremium { get; set; }

        public decimal? Loading { get; set; }
        public decimal? SUPPSA { get; set; }
        public decimal? XMas { get; set; }
        public decimal? ADBSA { get; set; }
        public decimal? ToAllocate { get; set; }
        public string? PaymentType { get; set; }
        public decimal? PaymentAmount { get; set; }
        public DateTime? Dateofpayment { get; set; }
        public string? ReceiptNo { get; set; }
        public string? BankName { get; set; }
        public string? Status { get; set; }
        public string? AccountNo { get; set; }
        public string? TransSTATUS { get; set; }
        public string? TransGUID { get; set; }
        public string? Remarks { get; set; }
        public string? Tag { get; set; }
        public string? ContactName { get; set; }
        public string? ContactAddress { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactJob { get; set; }
        public string? CustomerType { get; set; }
        public string   MeansID { get; set; }
        public string? IDNo { get; set; }
        public string? ChronicIllness { get; set; }
        public string? CardNo { get; set; }

        public long? isProposal { get; set; }
        public long? Deleted { get; set; }

        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? SpouseName { get; set; }
        public string? GovtOfOrigin { get; set; }
        public string? ResAddress { get; set; }
        public string? LandMark { get; set; }
        public string? NIN { get; set; }
        public string? BVN { get; set; }
        public string? EmploymentType { get; set; }
        public string? EmployerName { get; set; }
        public string? EmployerAddress { get; set; }
        public string? EmployerNumber { get; set; }
        public string? EmployerEmail { get; set; }
        public DateTime? DateEmployed { get; set; }
        public string? Height { get; set; }
        public string? weight { get; set; }
        public string? Sport { get; set; }
        public string? Infirmity { get; set; }
        public string? stroke { get; set; }
        public string? Treatment { get; set; }
        public string? WorkAbesent { get; set; }
        public string? Smoke { get; set; }
        public string? Aviation { get; set; }
        public string? Relgion { get; set; }
        public string? Designation { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    public class PolicyInvRequestDto
    {
        //public string? PolicyNo { get; set; }
        //public string? ProposalNo { get; set; }
        public string? Title { get; set; }
        public string? BrCode { get; set; }
        public string? AssuredCode { get; set; }
        public string? SurName { get; set; }
        public string? OtherNames { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? Landphone { get; set; }
        public string? MobilePhone { get; set; }
        public string? Email { get; set; }
        public string? NationalID { get; set; }
        public string? Occupation { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int Duration { get; set; }
        public string Frequency { get; set; }
        public decimal RegularSaving { get; set; }
        public string ProductCode { get; set; }
        public decimal? ExtraRate { get; set; }
        public decimal? LoadingRate { get; set; }

        // Additional proposal fields:
        public string? MaritalStatus { get; set; }
        public string? Country { get; set; }
        public string? AgentCode { get; set; }
        public string? AgentDescription { get; set; }
        public string? RiskID { get; set; }

      
        public string MeansID { get; set; }
        public string? IDNo { get; set; }
        public string? ChronicIllness { get; set; }
        public string? Mop { get; set; }
        public string? PaymentType { get; set; }
        public decimal? PaymentAmount { get; set; }
        public DateTime? Dateofpayment { get; set; }
        public string? ReceiptNo { get; set; }
        public string? BankName { get; set; }
        public string? AccountNo { get; set; }

        public string? PlaceOfBirth { get; set; }
        public string? SpouseName  { get; set; }
        public string? GovtOfOrigin { get; set; }
        public string? ResAddress { get; set; }
        public string? LandMark { get; set; }
        public string? NIN { get; set; }
        public string? BVN { get; set; }
        public string? EmploymentType { get; set; }
        public string? EmployerName { get; set; }
        public string? EmployerAddress { get; set; }
        public string? EmployerNumber { get; set; }
        public string? EmployerEmail { get; set; }
        public DateTime? DateEmployed { get; set; }
        public string? Height { get; set; }
        public string? weight { get; set; }
        public string? Sport { get; set; }
        public string? Infirmity { get; set; }
        public string? stroke { get; set; }
        public string? Treatment { get; set; }
        public string? WorkAbesent { get; set; }
        public string? Smoke { get; set; }
        public string? Aviation { get; set; }
        public string? Religion { get; set; }
        public string? Designation { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

    

    }

    public class convertTopolicyRequest
    {
        //public string? PolicyNo { get; set; }
        //public string? ProposalNo { get; set; }
           public string? PolicyStatus { get; set; }
   
    }

    public class ProposalRequest
    {
        //public string? PolicyNo { get; set; }
        //public string? ProposalNo { get; set; }
        public string? Title { get; set; }
        public string? AssuredCode { get; set; }
        public string? SurName { get; set; }
        public string? OtherNames { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? Landphone { get; set; }
        public string? MobilePhone { get; set; }
        public string? Email { get; set; }
        public string? NationalID { get; set; }
        public string? Occupation { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int Duration { get; set; }
        public string Frequency { get; set; }
        public decimal RegularSaving { get; set; }
        public string ProductCode { get; set; }
        public decimal? ExtraRate { get; set; }
        public decimal? LoadingRate { get; set; }

        // Additional proposal fields:
        public string? MaritalStatus { get; set; }
        public string? Country { get; set; }
        public string? AgentCode { get; set; }
        public string? AgentDescription { get; set; }
        public string? RiskID { get; set; }


        public string MeansID { get; set; }
        public string? IDNo { get; set; }
        public string? ChronicIllness { get; set; }
        public string? Mop { get; set; }
        public string? PaymentType { get; set; }
        public decimal? PaymentAmount { get; set; }
        public DateTime? Dateofpayment { get; set; }
        public string? ReceiptNo { get; set; }
        public string? BankName { get; set; }
        public string? AccountNo { get; set; }

        public string? PlaceOfBirth { get; set; }
        public string? SpouseName { get; set; }
        public string? GovtOfOrigin { get; set; }
        public string? ResAddress { get; set; }
        public string? LandMark { get; set; }
        public string? NIN { get; set; }
        public string? BVN { get; set; }
        public string? EmploymentType { get; set; }
        public string? EmployerName { get; set; }
        public string? EmployerAddress { get; set; }
        public string? EmployerNumber { get; set; }
        public string? EmployerEmail { get; set; }
        public DateTime? DateEmployed { get; set; }
        public string? Height { get; set; }
        public string? weight { get; set; }
        public string? Sport { get; set; }
        public string? Infirmity { get; set; }
        public string? stroke { get; set; }
        public string? Treatment { get; set; }
        public string? WorkAbesent { get; set; }
        public string? Smoke { get; set; }
        public string? Aviation { get; set; }
        public string? Religion { get; set; }
        public string? Designation { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }



    }

    public class TradProposalRequest
    {
        //public string? PolicyNo { get; set; }
        //public string? ProposalNo { get; set; }
        //public string? BrCode { get; set; }
        public string? Title { get; set; }
        public string? AssuredCode { get; set; }
        public string? SurName { get; set; }
        public string? OtherNames { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? Landphone { get; set; }
        public string? MobilePhone { get; set; }
        public string? Email { get; set; }
        //public string? NationalID { get; set; }
        public string? RiskID { get; set; }

        public string? Occupation { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int Duration { get; set; }
        public string Frequency { get; set; }
        public string Remark { get; set; }
        public decimal RegularSaving { get; set; }
        public string ProductCode { get; set; }
        public decimal? FirstPremium { get; set; }
        public decimal? SumAssured { get; set; }

        // Additional proposal fields:
        public string? MaritalStatus { get; set; }
        public string? Country { get; set; }
        public string? AgentCode { get; set; }
        public string? AgentDescription { get; set; }


        public string MeansID { get; set; }
        public string? IDNo { get; set; }
        public string? ChronicIllness { get; set; }

        public string? Mop { get; set; }
        public string? PaymentType { get; set; }
        public decimal? PaymentAmount { get; set; }
        public DateTime? Dateofpayment { get; set; }
        public string? ReceiptNo { get; set; }
        public string? BankName { get; set; }
        public string? AccountNo { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? SpouseName { get; set; }
        public string? GovtOfOrigin { get; set; }
        public string? ResAddress { get; set; }
        public string? LandMark { get; set; }
        public string? NIN { get; set; }
        public string? BVN { get; set; }
        public string? EmploymentType { get; set; }
        public string? EmployerName { get; set; }
        public string? EmployerAddress { get; set; }
        public string? EmployerNumber { get; set; }
        public string? EmployerEmail { get; set; }
        public DateTime? DateEmployed { get; set; }
        public string? Height { get; set; }
        public string? weight { get; set; }
        public string? Sport { get; set; }
        public string? Infirmity { get; set; }
        public string? stroke { get; set; }
        public string? Treatment { get; set; }
        public string? WorkAbesent { get; set; }
        public string? Smoke { get; set; }
        public string? Aviation { get; set; }
        public string? Religion { get; set; }
        public string? Designation { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class PolicyTradRequestDto
    {
        //public string? PolicyNo { get; set; }
        //public string? ProposalNo { get; set; }
        //public string? BrCode { get; set; }
        public string? Title { get; set; }
        public string? BrCode { get; set; }
        public string? AssuredCode { get; set; }
        public string? SurName { get; set; }
        public string? OtherNames { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? Landphone { get; set; }
        public string? MobilePhone { get; set; }
        public string? Email { get; set; }
        //public string? NationalID { get; set; }
        public string? RiskID { get; set; }

        public string? Occupation { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int Duration { get; set; }
        public string Frequency { get; set; }

        public int Age { get; set; }
        public string RateCode { get; set; }

        public string NationalID { get; set; }
        public string Remark { get; set; }
        public decimal RegularSaving { get; set; }
        public string ProductCode { get; set; }
        public decimal? FirstPremium { get; set; }
        public decimal? SumAssured { get; set; }

        // Additional proposal fields:
        public string? MaritalStatus { get; set; }
        public string? Country { get; set; }
        public string? AgentCode { get; set; }
        public string? AgentDescription { get; set; }

        
        public string MeansID { get; set; }
        public string? IDNo { get; set; }
        public string? ChronicIllness { get; set; }

        public string? Mop { get; set; }
        public string? PaymentType { get; set; }
        public decimal? PaymentAmount { get; set; }
        public DateTime? Dateofpayment { get; set; }
        public string? ReceiptNo { get; set; }
        public string? BankName { get; set; }
        public string? AccountNo { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? SpouseName { get; set; }
        public string? GovtOfOrigin { get; set; }
        public string? ResAddress { get; set; }
        public string? LandMark { get; set; }
        public string? NIN { get; set; }
        public string? BVN { get; set; }
        public string? EmploymentType { get; set; }
        public string? EmployerName { get; set; }
        public string? EmployerAddress { get; set; }
        public string? EmployerNumber { get; set; }
        public string? EmployerEmail { get; set; }
        public DateTime? DateEmployed { get; set; }
        public string? Height { get; set; }
        public string? weight { get; set; }
        public string? Sport { get; set; }
        public string? Infirmity { get; set; }
        public string? stroke { get; set; }
        public string? Treatment { get; set; }
        public string? WorkAbesent { get; set; }
        public string? Smoke { get; set; }
        public string? Aviation { get; set; }
        public string? Religion { get; set; }
        public string? Designation { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }


    public class PolicyIndListItemDto
    {
        public string PolicyNo { get; set; } = default!;
        public bool IsProposal { get; set; }
        public string? CoverCode { get; set; }
        public string? Covertype { get; set; }
        public string? BrCode { get; set; }
        public string? AssuredCode { get; set; }
        public string? InsuredName { get; set; }
        public DateTime? TDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? MaturityDate { get; set; }
        public decimal? SumAssured { get; set; }
        public decimal? BasicPremium { get; set; }
        public string? Status { get; set; }
    }

    public class PolicyIndQueryDto
    {
        /// <summary>Restrict to a specific sub-risk / product (e.g. "GLA" for Group Life).</summary>
        public string? CoverCode { get; set; }
        public string? BrCode { get; set; }
        public string? AssuredCode { get; set; }
        public string? Status { get; set; }

        /// <summary>false = policies only, true = proposals only, null = both.</summary>
        public bool? IsProposal { get; set; }

        /// <summary>Matches against policy number or insured name (contains).</summary>
        public string? Search { get; set; }

        /// <summary>Filter on start date (inclusive).</summary>
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

 
}
