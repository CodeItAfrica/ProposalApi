using System;
using System.ComponentModel.DataAnnotations;
namespace GibsLifesMicroWebApi.Models
{
    public class GrpMember
    {
        [Key]
        public long MemberID { get; set; }
        public string PolicyNo { get; set; }
        public string QuotationNo { get; set; }
        public string EndorsementNo { get; set; }
        public string BizOption { get; set; }
        public string SerialNo { get; set; }
        public string MembersNo { get; set; }
        public string Surname { get; set; }
        public string OtherNames { get; set; }
        public string Address { get; set; }
        public DateTime? DOBDate { get; set; }
        public DateTime? HireDate { get; set; }
        public DateTime? DisbursedDate { get; set; }
        public string Sex { get; set; }
        public int Age { get; set; }
        public decimal? BasicSalary { get; set; }
        public decimal? Housing { get; set; }
        public decimal? Transport { get; set; }
        public decimal? OtherAmount { get; set; }
        public decimal? TEmolument { get; set; }
        public decimal? SumInsured { get; set; }
        public double? PremiumRate { get; set; }
        public decimal? GrossPremium { get; set; }
        public decimal? PDERate { get; set; }
        public decimal? PDEPremium { get; set; }
        public decimal? TDRate { get; set; }
        public decimal? TDPremium { get; set; }
        public decimal? MERate { get; set; }
        public decimal? MEPremium { get; set; }
        public decimal? FURate { get; set; }
        public decimal? FUPremium { get; set; }
        public decimal? CRRate { get; set; }
        public decimal? CRPremium { get; set; }
        public decimal? RentEXP { get; set; }
        public byte[] Photo { get; set; }  // Assuming photo is stored as binary
        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

}
