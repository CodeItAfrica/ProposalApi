using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gibs.Domain.Entities
{
    [Table("SubRiskSections")]
    public class SubRiskSection
    {
        [Key]
        public long SectionID { get; set; }

        public string? SectionCode { get; set; }

        public string? SubRiskID { get; set; }

        public string? SectionName { get; set; }

        public string? SubRiskName { get; set; }

        public string? Field1 { get; set; }

        public string? Field2 { get; set; }

        public decimal? Rates { get; set; }

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

    public class RiskSectionDto
    {
        public long? SectionID { get; set; }
        public string? SectionCode { get; set; }
        public string? SubRiskID { get; set; }
        public string? SectionName { get; set; }
        public string? SubRiskName { get; set; }
        public string? Field1 { get; set; }
        public string? Field2 { get; set; }
        public decimal? Rates { get; set; }
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

    public class SubRiskSectionDto
    {
        public long? SectionID { get; set; }
        public string? SectionCode { get; set; }
        public string? SubRiskID { get; set; }
        public string? SectionName { get; set; }
        public string? SubRiskName { get; set; }
        public string? Field1 { get; set; }
        public string? Field2 { get; set; }
        public decimal? Rates { get; set; }
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

    public class CreateSubRiskSectionDto
    {
        public string? SectionCode { get; set; }
        public string? SubRiskID { get; set; }
        public string? SectionName { get; set; }
        public string? SubRiskName { get; set; }
        public string? Field1 { get; set; }
        public string? Field2 { get; set; }
        public decimal? Rates { get; set; }
        public decimal? A1 { get; set; }
        public decimal? A2 { get; set; }
        public decimal? A3 { get; set; }
        public decimal? A4 { get; set; }
        public decimal? A5 { get; set; }
        public string? SubmittedBy { get; set; }
    }

    public class UpdateSubRiskSectionDto
    {
        public string? SectionCode { get; set; }
        public string? SubRiskID { get; set; }
        public string? SectionName { get; set; }
        public string? SubRiskName { get; set; }
        public string? Field1 { get; set; }
        public string? Field2 { get; set; }
        public decimal? Rates { get; set; }
        public decimal? A1 { get; set; }
        public decimal? A2 { get; set; }
        public decimal? A3 { get; set; }
        public decimal? A4 { get; set; }
        public decimal? A5 { get; set; }
        public byte Active { get; set; }
        public string? ModifiedBy { get; set; }
    }
    public class SubRiskSectionQueryDto
    {
        public string? Search { get; set; }
    }

}
