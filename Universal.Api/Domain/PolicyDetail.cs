using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class PolicyDetail
    {
        [Key]

        public long DetailID { get; set; }
        //[ForeignKey("PolicyNo")]
        //public string? PolicyNo { get; set; }
        public string CoPolicyNo { get; set; }

        public DateTime? EntryDate { get; set; }

        public string EndorsementNo { get; set; }

        public string BizOption { get; set; }

        public string DNCNNo { get; set; }

        public string CertOrDocNo { get; set; }

        public string InsuredName { get; set; }

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

        public decimal? NetAmount { get; set; }

        public byte? Deleted { get; set; }

        public byte? Active { get; set; }

        public string SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public string ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }

        public string Field1 { get; set; }

        public string Field2 { get; set; }

        public string Field3 { get; set; }

        public string Field4 { get; set; }

        public string Field5 { get; set; }

        public string Field6 { get; set; }

        public string Field7 { get; set; }

        public string Field8 { get; set; }

        public string Field9 { get; set; }

        public string Field10 { get; set; }

        public string Field11 { get; set; }

        public string Field12 { get; set; }

        public string Field13 { get; set; }

        public string Field14 { get; set; }

        public string Field15 { get; set; }

        public string Field16 { get; set; }

        public string Field17 { get; set; }

        public string Field18 { get; set; }

        public string Field19 { get; set; }

        public string Field20 { get; set; }

        public decimal? TotalRiskValue { get; set; }

        public virtual Policy Policy { get; set; }

    }
}
