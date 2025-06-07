using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class PolicyTempDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long DetailID { get; set; }

        public string PolicyNo { get; set; }

        public string ENDTNum { get; set; }

        public string SectionID { get; set; }

        public string ContentSection { get; set; }

        public string Location { get; set; }

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

        public string Field21 { get; set; }

        public string Field22 { get; set; }

        public string Field23 { get; set; }

        public string Field24 { get; set; }

        public string Field25 { get; set; }

        public string Field26 { get; set; }

        public string Field27 { get; set; }

        public string Field28 { get; set; }

        public string Field29 { get; set; }

        public string Field30 { get; set; }

        public decimal? RiskSum { get; set; }

        public decimal? OtherSum { get; set; }

        public decimal? SectionPremium { get; set; }

        public DateTime? TStartDate { get; set; }

        public DateTime? TEndDate { get; set; }

        public decimal? A1 { get; set; }

        public decimal? A2 { get; set; }

        public decimal? A3 { get; set; }

        public decimal? A4 { get; set; }

        public long? A5 { get; set; }

        public decimal? A6 { get; set; }

        public decimal? A7 { get; set; }

        public decimal? A8 { get; set; }

        public decimal? A9 { get; set; }

        public decimal? A10 { get; set; }

        public decimal? A11 { get; set; }

        public decimal? A12 { get; set; }

        public decimal? A13 { get; set; }

        public decimal? A14 { get; set; }

        public decimal? A15 { get; set; }

        public decimal? A16 { get; set; }

        public double? A17 { get; set; }

        public decimal? A18 { get; set; }

        public decimal? A19 { get; set; }

        public decimal? A20 { get; set; }

        public decimal? A21 { get; set; }

        public decimal? A22 { get; set; }

        public decimal? A23 { get; set; }

        public decimal? A24 { get; set; }

        public decimal? A25 { get; set; }

        public decimal? A26 { get; set; }

        public decimal? A27 { get; set; }

        public decimal? A28 { get; set; }

        public decimal? A29 { get; set; }

        public decimal? A30 { get; set; }

        public decimal? A31 { get; set; }

        public decimal? A32 { get; set; }

        public decimal? A33 { get; set; }

        public decimal? A34 { get; set; }

        public decimal? A35 { get; set; }

        public decimal? A36 { get; set; }

        public decimal? A37 { get; set; }

        public decimal? A38 { get; set; }

        public decimal? A39 { get; set; }

        public decimal? A40 { get; set; }

        public decimal? A41 { get; set; }

        public decimal? A42 { get; set; }

        public decimal? A43 { get; set; }

        public decimal? A44 { get; set; }

        public decimal? A45 { get; set; }

        public decimal? A46 { get; set; }

        public decimal? A47 { get; set; }

        public decimal? A48 { get; set; }

        public decimal? A49 { get; set; }

        public decimal? A50 { get; set; }

        public string Tag { get; set; }

        public string TransGUID { get; set; }

        public string SubmitBy { get; set; }

        public DateTime? SubmitOn { get; set; }

        public string ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
    }
}
