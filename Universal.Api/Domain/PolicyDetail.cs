using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class PolicyDetail
    {
        [Key]

        public long DetailID { get; set; }
        [ForeignKey("Policy")]
        public string? PolicyNo { get; set; }
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

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A1 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A2 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A3 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A4 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A5 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A6 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A7 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A8 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A9 { get; set; }
        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A20 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A15 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A10 { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A19 { get; set; }
        [Column(TypeName = "decimal(18, 4)")]
        public decimal? A18 { get; set; }
        public decimal? TotalRiskValue { get; set; }

        public virtual Policy Policy { get; set; }

    }


    /// <summary>
    /// GrpMembers entity - Group policy members
    /// </summary>
    [Table("GrpMembers")]
    public class GrpMembers
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long MemberID { get; set; }

        [StringLength(32)]
        public string PolicyNo { get; set; }

        [StringLength(32)]
        public string QuotationNo { get; set; }

        [StringLength(64)]
        public string EndorsementNo { get; set; }

        [StringLength(32)]
        public string BizOption { get; set; }

        [StringLength(32)]
        public string SerialNo { get; set; }

        [StringLength(32)]
        public string MembersNo { get; set; }

        [StringLength(128)]
        public string Surname { get; set; }

        [StringLength(128)]
        public string OtherNames { get; set; }

        public DateTime? DOBDate { get; set; }

        public DateTime? HireDate { get; set; }

        [StringLength(32)]
        public string Sex { get; set; }

        public long? Age { get; set; }

        [Column(TypeName = "money")]
        public decimal? BasicSalary { get; set; }

        [Column(TypeName = "money")]
        public decimal? Housing { get; set; }

        [Column(TypeName = "money")]
        public decimal? Transport { get; set; }

        [Column(TypeName = "money")]
        public decimal? OtherAmount { get; set; }

        [Column(TypeName = "money")]
        public decimal? TEmolument { get; set; }

        [Column(TypeName = "money")]
        public decimal? SumInsured { get; set; }

        public float? PremiumRate { get; set; }

        [Column(TypeName = "money")]
        public decimal? GrossPremium { get; set; }

        public float? PDERate { get; set; }

        [Column(TypeName = "money")]
        public decimal? PDEPremium { get; set; }

        public float? TDRate { get; set; }

        [Column(TypeName = "money")]
        public decimal? TDPremium { get; set; }

        public float? MERate { get; set; }

        [Column(TypeName = "money")]
        public decimal? MEPremium { get; set; }

        public float? FURate { get; set; }

        [Column(TypeName = "money")]
        public decimal? FUPremium { get; set; }

        public float? CRRate { get; set; }

        [Column(TypeName = "money")]
        public decimal? CRPremium { get; set; }

        [Column(TypeName = "money")]
        public decimal? RentEXP { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A1 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A2 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A3 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A4 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A5 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A6 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A7 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A8 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A9 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? A10 { get; set; }

        [Column(TypeName = "image")]
        public byte[]? photo { get; set; }

        public byte? Deleted { get; set; }

        public byte? Active { get; set; }

        [StringLength(128)]
        public string? SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        [StringLength(128)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }

        [StringLength(128)]
        public string? Field1 { get; set; }

        [StringLength(32)]
        public string? Field2 { get; set; }

        [StringLength(32)]
        public string? Field3 { get; set; }

        [StringLength(32)]
        public string? Field4 { get; set; }

        [StringLength(32)]
        public string? Field5 { get; set; }

        [StringLength(32)]
        public string? Field6 { get; set; }

        [StringLength(32)]
        public string? Field7 { get; set; }

        [StringLength(32)]
        public string? Field8 { get; set; }

        [StringLength(64)]
        public string? Field9 { get; set; }

        [StringLength(64)]
        public string? Field10 { get; set; }
    }
}
