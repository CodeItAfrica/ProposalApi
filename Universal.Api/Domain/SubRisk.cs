using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class SubRisk
    {
        [Key]
        public string? SubRiskID { get; set; }

        public string? RiskID { get; set; }
        [Column("SubRisk")]
        public string? SubRiskName { get; set; }

        public string? Description { get; set; }

        public byte? Deleted { get; set; }

        public byte? Active { get; set; }

        public string? InsuranceTypeID { get; set; }
        public string? A1 { get; set; }
    public string? A2 { get; set; }
    public string? A3 { get; set; }
    public string? A4 { get; set; }
    public string? A5 { get; set; }
    public string? A6 { get; set; } // ← Field to update

    public string? B1 { get; set; }
    public string? B2 { get; set; }
    public string? B3 { get; set; }
    public string? B4 { get; set; }
    public string? B5 { get; set; }
    public string? B6 { get; set; }

    public string? C1 { get; set; }
    public string? C2 { get; set; }
    public string? C3 { get; set; }
    public string? C4 { get; set; }

    public string? D1 { get; set; }
    public string? D2 { get; set; }
    public string? D3 { get; set; }
    public string? D4 { get; set; }

    public string? E1 { get; set; }
    public string? E2 { get; set; }
    public string? E3 { get; set; }
    public string? E4 { get; set; }

        public string? SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
    }

    public class SubRiskA6UpdateDto
    {
        [Column("A6")]

        public string Type { get; set; } = string.Empty;
        public string ModifiedBy { get; set; } = string.Empty;
    }

}
