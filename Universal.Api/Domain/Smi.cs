using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gibs.Domain.Entities.others
{
    [Table("SubRiskSMIs")]
    public class SubRiskSMI
    {
        [Key]
        public long SID { get; set; }

        public string? SMICode { get; set; }

        public string? SectionCode { get; set; }

        public string? SubRiskID { get; set; }

        public string? SMIDetails { get; set; }

        public string? SectionName { get; set; }

        public string? SubRiskName { get; set; }

        public string? Field1 { get; set; }

        public string? Field2 { get; set; }

        public decimal? Rates { get; set; }

        public byte? AddSI { get; set; }

        public byte? StockItem { get; set; }

        public byte? Multiplier { get; set; }

        public decimal? A1 { get; set; }

        public decimal? A2 { get; set; }

        public decimal? A3 { get; set; }

        public decimal? A4 { get; set; }

        public decimal? A5 { get; set; }

        public byte? Active { get; set; }

        public string? SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
    }

    public class SubRiskSMIDto
    {
        public long? SID { get; set; }
        public string? SMICode { get; set; }
        public string? SectionCode { get; set; }
        public string? SubRiskID { get; set; }
        public string? SMIDetails { get; set; }
        public string? SectionName { get; set; }
        public string? SubRiskName { get; set; }
        public string? Field1 { get; set; }
        public string? Field2 { get; set; }
        public decimal? Rates { get; set; }
        public byte? AddSI { get; set; }
        public byte? StockItem { get; set; }
        public byte? Multiplier { get; set; }
        public decimal? A1 { get; set; }
        public decimal? A2 { get; set; }
        public decimal? A3 { get; set; }
        public decimal? A4 { get; set; }
        public decimal? A5 { get; set; }
        public byte? Active { get; set; } = 1;
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    public class CreateSubRiskSMIDto
    {
        public string? SMICode { get; set; }
        public string? SectionCode { get; set; }
        public string? SubRiskID { get; set; }
        public string? SMIDetails { get; set; }
        public string? SectionName { get; set; }
        public string? SubRiskName { get; set; }
        public string? Field1 { get; set; }
        public string? Field2 { get; set; }
        public decimal? Rates { get; set; }
        public byte? AddSI { get; set; }
        public byte? StockItem { get; set; }
        public byte? Multiplier { get; set; }
        public decimal? A1 { get; set; }
        public decimal? A2 { get; set; }
        public decimal? A3 { get; set; }
        public decimal? A4 { get; set; }
        public decimal? A5 { get; set; }
        public byte? Active { get; set; } 
        public string? SubmittedBy { get; set; }
    }

    public class UpdateSubRiskSMIDto
    {
        public string? SMICode { get; set; }
        public string? SectionCode { get; set; }
        public string? SubRiskID { get; set; }
        public string? SMIDetails { get; set; }
        public string? SectionName { get; set; }
        public string? SubRiskName { get; set; }
        public string? Field1 { get; set; }
        public string? Field2 { get; set; }
        public decimal? Rates { get; set; }
        public byte? AddSI { get; set; }
        public byte? StockItem { get; set; }
        public byte? Multiplier { get; set; }
        public decimal? A1 { get; set; }
        public decimal? A2 { get; set; }
        public decimal? A3 { get; set; }
        public decimal? A4 { get; set; }
        public decimal? A5 { get; set; }
        public byte? Active { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class SubRiskSMIQueryDto
    {
        public string? Search { get; set; }
    }

}
