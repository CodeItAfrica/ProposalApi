using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class Claim
    {
        [Key]
        public string ClaimNo { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ClaimReservedID { get; set; }
        public string BranchID { get; set; }
        public DateTime? EntryDate { get; set; }
        public DateTime? NotifyDate { get; set; }
        public DateTime? LossDate { get; set; }
        public string PolicyNo { get; set; }
        public string CoPolicyNo { get; set; }
        public string refDNCNNo { get; set; }
        public decimal? PropRate { get; set; }
        public int? UndYear { get; set; }
        public string InsuredName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string SubRiskID { get; set; }
        public string SubRisk { get; set; }
        public string PartyID { get; set; }
        public string Party { get; set; }
        public string InsuredID { get; set; }
        public string LossType { get; set; }
        public string LossDetails { get; set; }
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
        public decimal? AmtReserved { get; set; }
        public decimal? AmtPaid { get; set; }

        //public double? ExRate { get; set; }

        //public string ExCurrency { get; set; }

        //public decimal? ClaimReservedFrgn { get; set; }
        public long? DetailID { get; set; }
        public byte? Approval { get; set; }
        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public decimal? A1 { get; set; }
        public decimal? A2 { get; set; }
        public decimal? A3 { get; set; }
        public decimal? A4 { get; set; }
        public decimal? A5 { get; set; }
        public double? A6 { get; set; }
        public double? A7 { get; set; }
        public double? A8 { get; set; }
        public double? A9 { get; set; }
        public string LeadID { get; set; }
        public string Leader { get; set; }
        public string PropType { get; set; }
        public decimal? LeadProp { get; set; }
    }
}
