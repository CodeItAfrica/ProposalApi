using DocumentFormat.OpenXml.Wordprocessing;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{

    public class    policydto
    {
        public string PolicyNo { get; set; }
        public string ProposalNo { get; set; }
        public string FullName { get; set; }
        public string CoverType { get; set; }
        public string AgentCode { get; set; }
        public string AgentDescription { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? MaturityDate { get; set; }
        public decimal SumAssured { get; set; }
        public decimal BasicPremium { get; set; }
        public string Status { get; set; } // Using TransSTATUS as Status
        public string MobilePhone { get; set; }
        public string Email { get; set; }
    }


public class Policiesdto
    {
       
        public CreateNewCustomerRequest Insured { get; set; }
        public string BranchID { get; set; }


        public string CustomerID { get; set; }

        public string ProductID { get; set; }

    
        //public string BizSource { get; set; }
      
        //public string InsSurname { get; set; }
        //public string InsFirstname { get; set; }
        //public string InsOthernames { get; set; }
        //public string InsAddress { get; set; }
        //public string InsStateID { get; set; }
        //public string InsMobilePhone { get; set; }
        //public string InsLandPhone { get; set; }
        //public string InsEmail { get; set; }
        //public string InsFaxNo { get; set; }
        //public string InsOccupation { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }




        //// ✅ Add PolicyDetails as a List
        //public List<PolicyDetail> PolicyDetails { get; set; } = new List<PolicyDetail>();

        //// ✅ Add GrpMembers as a List
        //public List<GrpMemberDto> GrpMembers { get; set; } = new List<GrpMemberDto>();
    }

    public class PolicyModel
    {
        public CreateNewCustomerRequestMaster Insured { get; set; }
        public DateTime TransDate { get; set; }
        //public string BranchID { get; set; }
        public string CustomerID { get; set; }
        public string ProductID { get; set; }
 
        public DateTime StartDate { get; set; }
        public DateTime MaturityDate { get; set; }
        public string FrequencyOfPayment { get; set; }
            public decimal SumAssured { get; set; }
        public decimal BasicPremium { get; set; }
    }
    public class GetPoliciesdto
    {
        [Key]
        public string PolicyNo { get; set; }
        //public long PolicyID { get; set; }
        public string CoPolicyNo { get; set; }
        public CreateNewCustomerRequest Insured { get; set; }
        public string BranchID { get; set; }

        public string AgentID { get; set; }

        public string CustomerID { get; set; }

        public string ProductID { get; set; }

        public string Branch { get; set; }
        public string BizSource { get; set; }
        public string SubRiskID { get; set; }
        public string SubRisk { get; set; }
        public string InsSurname { get; set; }
        public string InsFirstname { get; set; }
        public string InsOthernames { get; set; }
        public string InsAddress { get; set; }
        public string InsStateID { get; set; }
        public string InsMobilePhone { get; set; }
        public string InsLandPhone { get; set; }
        public string InsEmail { get; set; }
        public string InsFaxNo { get; set; }
        public string InsOccupation { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string ExCurrency { get; set; }
        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }
        public string TransSTATUS { get; set; }

        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }



        //// ✅ Add PolicyDetails as a List
        //public List<PolicyDetail> PolicyDetails { get; set; } = new List<PolicyDetail>();

        //// ✅ Add GrpMembers as a List
        //public List<GrpMemberDto> GrpMembers { get; set; } = new List<GrpMemberDto>();
    }

    [Table("Policies")] // Add this - use your actual table name
    public class Policy
    {
        [Key]
        [StringLength(32)]
        public string PolicyNo { get; set; }

        public string? CompanyID { get; set; }
        public string? SourceType { get; set; }
        public string? MktGroupID { get; set; }
        public string? MktGroup { get; set; }
        public long? ExRateID { get; set; }

        [StringLength(255)]
        public string? Field10 { get; set; }

        public DateTime? ExtraDate1 { get; set; }
        public DateTime? ExtraDate2 { get; set; }

        public long? TrackID { get; set; }
        public DateTime? TransDate { get; set; }

        public string CoPolicyNo { get; set; }
        public string BranchID { get; set; }
        public string Branch { get; set; }
        public string BizSource { get; set; }
        public string SubRiskID { get; set; }

        [Column("SubRisk")]
        public string SubRiskName { get; set; }

        public string PartyID { get; set; }

        [StringLength(255)]
        public string Field1 { get; set; }

        [StringLength(255)]
        public string Field2 { get; set; }

        [StringLength(255)]
        public string Field3 { get; set; }

        [StringLength(255)]
        public string Field4 { get; set; }

        public byte? isProposal { get; set; }

        [StringLength(255)]
        public string Field5 { get; set; }

        [StringLength(255)]
        public string Field6 { get; set; }

        [StringLength(255)]
        public string Field7 { get; set; }

        [StringLength(255)]
        public string Field8 { get; set; }

        [StringLength(255)]
        public string Field9 { get; set; }

        public string Party { get; set; }

        [Column(TypeName = "money")]
        public decimal? A1 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A2 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A3 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A4 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A5 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A6 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A7 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A8 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A9 { get; set; }

        [Column(TypeName = "money")]
        public decimal? A10 { get; set; }

        public string MktStaffID { get; set; }
        public string MktStaff { get; set; }
        public string InsuredID { get; set; }
        public string InsSurname { get; set; }
        public string InsFirstname { get; set; }
        public string InsOthernames { get; set; }
        public string InsAddress { get; set; }
        public string InsStateID { get; set; }
        public string InsMobilePhone { get; set; }
        public string InsLandPhone { get; set; }
        public string InsEmail { get; set; }
        public string InsFaxNo { get; set; }
        public string InsOccupation { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public double? ExRate { get; set; }
        public string ExCurrency { get; set; }
        public double? PremiumRate { get; set; }
        public double? ProportionRate { get; set; }

        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }
        public decimal? SumInsuredFrgn { get; set; }
        public decimal? GrossPremiumFrgn { get; set; }

        public int? ProRataDays { get; set; }
        public decimal? ProRataPremium { get; set; }

        public string Remarks { get; set; }
        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        public string TransSTATUS { get; set; }

        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public string LeadID { get; set; }
        public string Leader { get; set; }



        //public string Z_NAICOM_UID { get; set; }
        //public string Z_NAICOM_STATUS { get; set; }
        //public DateTime? Z_NAICOM_SENT_ON { get; set; }
        //public string Z_NAICOM_ERROR { get; set; }
        //public string Z_NAICOM_JSON { get; set; }


        public virtual List<PolicyDetail> PolicyDetails { get; set; }

        public string InsFullname
        {
            get
            {
                return $"{InsSurname} {InsFirstname} {InsOthernames}".Trim();
            }
        }


       
        [NotMapped]
        public DNCNNote DebitNote { get; set; }
        [NotMapped]
        public SubRisk SubRisk { get; set; }
    }
    /// <summary>
    /// Input for creating a group-life policy header (postings_edit "Save &amp; Continue").
    /// </summary>
    public class PolicyCreateDto
    {
        /// <summary>POLY, PROP or RENEWAL. Drives the proposal flag and number prefix.</summary>
        public string TransactionType { get; set; } = Infrastructure.Common.TransactionType.Policy;

        /// <summary>Business class from the legacy querystring (GL for group life).</summary>
        public string Class { get; set; } = "GL";

        /// <summary>Product / sub-class (cdbSubClass).</summary>
        public string SubRiskId { get; set; } = default!;

        public DateTime EntryDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }

        /// <summary>Account executive (cdbAcctExec0).</summary>
        public string? AccountExecutiveId { get; set; }

        public string BranchId { get; set; } = default!;
        public string BusinessSource { get; set; } = default!;

        /// <summary>Agent / broker code (party1).</summary>
        public string? AgentBrokerId { get; set; }

        /// <summary>Insured client code (Insured_code2).</summary>
        public string InsuredId { get; set; } = default!;

        /// <summary>Account type (CdbBusShare → TrackID). "NULL" is rejected.</summary>
        public string AccountTypeId { get; set; } = default!;

        public string? BusChannelId { get; set; }
        public string? SubChannelId { get; set; }

        /// <summary>Proportion% (txtBusProp).</summary>
        public decimal ProportionPercent { get; set; } = 100m;

        /// <summary>Prem-Rate% (txtPreRate). Stored on the policy as a fraction (value / 100).</summary>
        public decimal PremiumRatePercent { get; set; } = 0m;

        /// <summary>Currency name (cdbCurrency text). Defaults to NAIRA.</summary>
        public string CurrencyName { get; set; } = "NAIRA";

        /// <summary>Exchange rate (cdbCurrency value). 1 for local currency.</summary>
        public double ExchangeRate { get; set; } = 1;
    }

    /// <summary>Update payload — same shape as create minus the number/proposal bits.</summary>
    public class PolicyUpdateDto : PolicyCreateDto { }
    /// <summary>Lightweight row for policy list/search results.</summary>
    public class PolicyListItemDto
    {
        public string PolicyNo { get; set; } = default!;
        public bool IsProposal { get; set; }
        public string? SubRiskId { get; set; }
        public string? SubRiskName { get; set; }
        public string? BranchId { get; set; }
        public string? InsuredId { get; set; }
        public string? InsuredName { get; set; }
        public DateTime? EntryDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>Query filters for listing policies (all optional; bound from the query string).</summary>
    public class PolicyQueryDto
    {
        /// <summary>Restrict to a specific sub-risk / product (e.g. "GLA" for Group Life).</summary>
        public string? SubRiskId { get; set; }
        public string? BranchId { get; set; }
        public string? InsuredId { get; set; }
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

    /// <summary>Generic paged result wrapper.</summary>
    public class PagedResultDto<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }


   
    public class PolicyResponseDto
    {
        public string PolicyNo { get; set; } = default!;
        public bool IsProposal { get; set; }
        public string? SubRiskId { get; set; }
        public string? SubRiskName { get; set; }
        public string? BranchId { get; set; }
        public string? BusinessSource { get; set; }
        public string? InsuredId { get; set; }
        public string? InsuredName { get; set; }
        public string? AgentBrokerId { get; set; }
        public DateTime? EntryDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? ProportionPercent { get; set; }
        public decimal? PremiumRateFraction { get; set; }
        public string? CurrencyName { get; set; }
        public double? ExchangeRate { get; set; }
        public string? Status { get; set; }

        // Dropdown values (ids) with their resolved display text.
        public string? AccountExecId { get; set; }
        public string? AccountExecName { get; set; }
        public string? BusinessSourceText { get; set; }
        public string? AccountTypeId { get; set; }
        public string? AccountTypeText { get; set; }
        public string? BusChannelId { get; set; }
        public string? BusChannelName { get; set; }
        public string? SubChannelId { get; set; }
        public string? SubChannelName { get; set; }

        /// <summary>
        /// The member "envelope" created alongside the policy (when createEnvelope is on).
        /// Use this endorsement number when adding members so they attach to the right section.
        /// </summary>
        public long? DetailId { get; set; }
        public string? EndorsementNo { get; set; }
    }

    /// <summary>Response when ensuring the PolicyDetail "envelope" for members.</summary>
    public class GroupLifeDetailDto
    {
        public int DetailID { get; set; }
        public string? PolicyNo { get; set; }
        public string? EndorsementNo { get; set; }
        public string? CertOrDocNo { get; set; }
        public string? InsuredName { get; set; }

        public string? EndorsementType { get; set; }   // was BizOption: NEW / RENEWAL / REVERSAL / ADDITIONAL / EXPIRED
        public string? CompletionStatus { get; set; }  // was Field20: "Uncompleted" / "Completed"
        public DateTime? EffectiveDate { get; set; }    // was StartDate
        public DateTime? ExpiryDate { get; set; }       // was EndDate

        public decimal SumInsured { get; set; }
        public decimal GrossPremium { get; set; }
        public decimal NetPremium { get; set; }         // was NetAmount

        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }

        public bool Created { get; set; }
    }

    public class DropdownItemDto
    {
        public string Text { get; set; }
        public string Value { get; set; }
    }

    /// <summary>Account executives (cls_MktStaffs). Value = MktStaffID, text = staffName.</summary>
    //[Table("MktStaffs")]
    //public class MktStaff
    //{
    //    [Key]
    //    [Column("MktstaffID")]
    //    public string MktStaffID { get; set; } = default!;

    //    [Column("staffName")]
    //    public string? StaffName { get; set; }
    //}

    [Table("MktStaffs")]
    public class MktStaff
    {
        [Key]
        public string MktStaffID { get; set; } = string.Empty;
        public string? MktGrpID { get; set; }
        public string? GroupName { get; set; }
        public string? StaffName { get; set; }
        public string? GroupHead { get; set; }
        public string? mktUnitID { get; set; }
        public string? mktUnit { get; set; }
        public DateTime? Budyear1 { get; set; }
        public DateTime? Budyear2 { get; set; }

        [Column(TypeName = "money")]
        public decimal? CurTarget { get; set; }

        [Column(TypeName = "money")]
        public decimal? PrevTarget { get; set; }

        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    public class MktStaffDto
    {
        public string MktStaffID { get; set; } = string.Empty;
        public string? MktGrpID { get; set; }
        public string? GroupName { get; set; }
        public string? StaffName { get; set; }
        public string? GroupHead { get; set; }
        public string? mktUnitID { get; set; }
        public string? mktUnit { get; set; }
        public DateTime? Budyear1 { get; set; }
        public DateTime? Budyear2 { get; set; }
        public decimal? CurTarget { get; set; }
        public decimal? PrevTarget { get; set; }
        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    public class CreateMktStaffDto
    {
        public string MktStaffID { get; set; } = string.Empty;
        public string? MktGrpID { get; set; }
        public string? GroupName { get; set; }
        public string? StaffName { get; set; }
        public string? GroupHead { get; set; }
        public string? mktUnitID { get; set; }
        public string? mktUnit { get; set; }
        public DateTime? Budyear1 { get; set; }
        public DateTime? Budyear2 { get; set; }
        public decimal? CurTarget { get; set; }
        public decimal? PrevTarget { get; set; }
        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        public string? SubmittedBy { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class UpdateMktStaffDto
    {
        public string? MktGrpID { get; set; }
        public string? GroupName { get; set; }
        public string? StaffName { get; set; }
        public string? GroupHead { get; set; }
        public string? mktUnitID { get; set; }
        public string? mktUnit { get; set; }
        public DateTime? Budyear1 { get; set; }
        public DateTime? Budyear2 { get; set; }
        public decimal? CurTarget { get; set; }
        public decimal? PrevTarget { get; set; }
        public byte Deleted { get; set; }
        public byte Active { get; set; }
        public string? ModifiedBy { get; set; }
    }

    [Table("MktChannels")]
    public class MktChannel
    {
        [Key] public string ChannelID { get; set; }
        public string? Channels { get; set; }
        public string? Description { get; set; }
    }

    public class MktChannelDto
    {
        public string ChannelID { get; set; } = string.Empty;
        public string? Channels { get; set; }
        public string? Description { get; set; }
    }

    public class CreateMktChannelDto
    {
        public string ChannelID { get; set; } = string.Empty;
        public string? Channels { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateMktChannelDto
    {
        public string? Channels { get; set; }
        public string? Description { get; set; }
    }
    /// <summary>Business channels (cls_MktChannels). Value = ChannelID, text = Channels.</summary>
    //[Table("MktChannels")]
    //public class MktChannel
    //{
    //    [Key]
    //    public string ChannelID { get; set; } = default!;

    //    public string? Channels { get; set; }
    //}

    /// <summary>Exchange rates / currencies (cls_ExchangeRates). Text = ExCurrency, value = Rates.</summary>
    [Table("ExchangeRates")]
    [Keyless]
    public class ExchangeRate
    {
        public string? ExCurrency { get; set; }
        public double? Rates { get; set; }
    }


    public enum EndorsementType
    {
        Renewal,
        Reversal,
        Additional
    }
}
