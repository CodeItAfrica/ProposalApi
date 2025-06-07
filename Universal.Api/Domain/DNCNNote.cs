using System;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Models
{
    public class DNCNNote
    {
        [Key]
        public string DNCNNo { get; set; }

        public string refDNCNNo { get; set; }

        public string ReceiptNo { get; set; }

        public string PolicyNo { get; set; }

        public string CoPolicyNo { get; set; }

        public string BranchID { get; set; }

        //public string CompanyID { get; set; }

        public string BizSource { get; set; }

        public string BizOption { get; set; }

        public string NoteType { get; set; }

        public DateTime? BillingDate { get; set; }

        public string SubRiskID { get; set; }

        public string SubRisk { get; set; }

        public string PartyID { get; set; }

        public string Party { get; set; }

        public decimal? PartyRate { get; set; }

        public string InsuredID { get; set; }

        public string InsuredName { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string MktStaffID { get; set; }

        public string MktStaff { get; set; }

        public decimal? SumInsured { get; set; }

        public decimal? GrossPremium { get; set; }

        public decimal? Commission { get; set; }

        public double? PropRate { get; set; }

        public long? ProRataDays { get; set; }

        public decimal? ProRataPremium { get; set; }

        //public double? VatRate { get; set; }

        //public Decimal? VatAmount { get; set; }

        public decimal? NetAmount { get; set; }

        public string Narration { get; set; }

        public double? ExRate { get; set; }

        public string ExCurrency { get; set; }

        public decimal? SumInsuredFrgn { get; set; }

        public decimal? GrossPremiumFrgn { get; set; }

        public byte? Approval { get; set; }

        public byte? HasTreaty { get; set; }

        public string Remarks { get; set; }

        public string Transguid { get; set; }

        //public string SourceType { get; set; }

        //public string RIClass { get; set; }

        //public Decimal? TopMostValue { get; set; }

        //public Decimal? PMLValue { get; set; }

        public string PaymentType { get; set; }

        public string ChequeNo { get; set; }

        //public string Field1 { get; set; }

        //public string Field2 { get; set; }

        //public string Field3 { get; set; }

        //public string Field4 { get; set; }

        //public string Field5 { get; set; }

        //public string Field6 { get; set; }

        //public string Field7 { get; set; }

        //public string Field8 { get; set; }

        //public string Field9 { get; set; }

        //public string Field10 { get; set; }

        public byte? Deleted { get; set; }

        //public string DeletedBy { get; set; }

        //public DateTime? DeletedOn { get; set; }

        public byte? Active { get; set; }

        public string SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public string ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }

        public string ApprovedBy { get; set; }

        public DateTime? ApprovedOn { get; set; }

        public decimal? TotalRiskValue { get; set; }

        public decimal? TotalPremium { get; set; }

        public string LeaderID { get; set; }

        public string Leader { get; set; }

        public decimal? RetProp { get; set; }

        public decimal? RetValue { get; set; }

        public decimal? RetPremium { get; set; }

        public DateTime? DBDate { get; set; }

        //public DateTime? CRRefDate { get; set; }

        //public string MktUnitID { get; set; }

        //public string mktUnit { get; set; }

        //public string CoverType { get; set; }

        //public Decimal? A1 { get; set; }

        //public Decimal? A2 { get; set; }

        //public Decimal? A3 { get; set; }

        //public Decimal? A4 { get; set; }

        //public Decimal? A5 { get; set; }

        //public Decimal? A6 { get; set; }

        //public Decimal? A7 { get; set; }

        //public Decimal? A8 { get; set; }

        //public Decimal? A9 { get; set; }

        //public Decimal? A10 { get; set; }

        //public DateTime? ExtraDate1 { get; set; }

        //public DateTime? ExtraDate2 { get; set; }


        //public string Z_NAICOM_UID { get; set; }
        //public string Z_NAICOM_STATUS { get; set; }
        //public DateTime? Z_NAICOM_SENT_ON { get; set; }
        //public string Z_NAICOM_ERROR { get; set; }
        //public string Z_NAICOM_JSON { get; set; }
    }
}
