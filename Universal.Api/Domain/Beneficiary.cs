using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Domain
{
    [Table("Beneficiaries")]
    public class Beneficiary
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int?  TransID { get; set; }

        [StringLength(64)]
        public string? ProposalNo { get; set; }

         
        [StringLength(255)]
        public string? Surname { get; set; }

         
        [StringLength(255)]
        public string? OtherNames { get; set; }

         
        [StringLength(50)]
        public string? Age { get; set; }

         
        [StringLength(50)]
        public string? Sex { get; set; }


        [Column(TypeName = "datetime")]
        public DateTime? DOB { get; set; }

        [StringLength(20)]
        public string? Relation { get; set; }

         
        [Column(TypeName = "money")]
        public decimal? Percentage { get; set; }

         
        [StringLength(20)]
        public string? Type { get; set; }

         
        [StringLength(10)]
        public string? TransFlag { get; set; }

        public byte[]? Photo { get; set; }

         
        [StringLength(50)]
        public string? PolicyNo { get; set; }

         
        [StringLength(255)]
        public string? Address { get; set; }

        [StringLength(255)]
        public string? Remark { get; set; }

        [StringLength(255)]
        public string? Tag { get; set; }

        public byte? Deleted { get; set; }

        public byte? Active { get; set; } 

        [StringLength(128)]
        public string? SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        [StringLength(128)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
    }
    public class BeneficiaryCreateDto
    {
        public string? ProposalNo { get; set; }

         
        public string Surname { get; set; }

         
        public string OtherNames { get; set; }

         
        public string Age { get; set; }

         
        public string Sex { get; set; }

         
        public DateTime DOB { get; set; }

         
        public string Relation { get; set; }

         
        public decimal Percentage { get; set; }

         
        public string Type { get; set; }

         
        public string TransFlag { get; set; }

        // Support both file upload and base64 string
        public IFormFile Photo { get; set; }
        public string PhotoBase64 { get; set; }

         
        public string PolicyNo { get; set; }

         
        public string Address { get; set; }

        public string Remark { get; set; }
        public string Tag { get; set; }
    }

    public class BeneficiaryUpdateDto
    {
        public string? ProposalNo { get; set; }
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public string? Age { get; set; }
        public string? Sex { get; set; }
        public DateTime? DOB { get; set; }
        public string? Relation { get; set; }
        public decimal? Percentage { get; set; }
        public string? Type { get; set; }
        public string? TransFlag { get; set; }

        // Support both file upload and base64 string
        public IFormFile? Photo { get; set; }
        public string? PhotoBase64 { get; set; }
                     
        public string?PolicyNo { get; set; }
        public string?Address { get; set; }
        public string?Remark { get; set; }
        public string?Tag { get; set; }
        public byte? Active { get; set; }
    }

    public class BeneficiaryReadDto
    {
        public int? TransID { get; set; }
        public string? ProposalNo { get; set; }
        public string? Surname { get; set; }
        public string? OtherNames { get; set; }
        public string? Age { get; set; }
        public string? Sex { get; set; }
        public DateTime? DOB { get; set; }
        public string? Relation { get; set; }
        public decimal? Percentage { get; set; }
        public string? Type { get; set; }
        public string? TransFlag { get; set; }

        // Return photo as base64 string
        public string? PhotoBase64 { get; set; }
        public bool? HasPhoto { get; set; }

        public string? PolicyNo { get; set; }
        public string? Address { get; set; }
        public string? Remark { get; set; }
        public string? Tag { get; set; }
        public byte? Deleted { get; set; }
        public byte? Active { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }


}
