using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{

    [Table("PolicyDocs")]
    public class Document
    {
        [Key, Column("ID")]
        public long DocumentId { get; set; }

        [Column("OptionType")]
        public string OwnerType { get; set; } //POLICY, CLAIMS etc
        [Column("PolicyNo")]
        public string OwnerRefId { get; set; } //PolicyNo, ClaimNo
        [Column("EndorsementNo")]
        public string DocumentName { get; set; }
        [Column("RawBytes")]
        public byte[] Content { get; set; }
        //public string ContentType { get; set; }
        [Column("SubmittedOn")] 
        public DateTime SubmitDate { get; set; }

    }

    [Table("AttachDocs")] // Replace with your actual table name
    public class AttachDocument
    {
        [Key]
        [Display(Name = "Attachment ID")]
        public long AttachID { get; set; }

        [Display(Name = "Document Number")]
        [StringLength(64)]
        public string? DocNo { get; set; }

        [Display(Name = "Entry Date")]
        [DataType(DataType.DateTime)]
        public DateTime? EntryDate { get; set; }

        [Display(Name = "Document Types")]
        [StringLength(256)]
        public string? DocTypes { get; set; }

        [Display(Name = "Document From")]
        [StringLength(256)]
        public string? DocFrom { get; set; }

        [Display(Name = "Document Details")]
        [StringLength(512)]
        public string? DocDetails { get; set; }

        [Display(Name = "Documents")]
        public byte[]? zDocuments { get; set; }

        [Display(Name = "Deleted")]
        public byte? Deleted { get; set; }

        [Display(Name = "Active")]
        public byte? Active { get; set; }

        [Display(Name = "Option Type")]
        [StringLength(50)]
        public string? optionType { get; set; }

        [Display(Name = "Document Email 1")]
        [StringLength(64)]
        [EmailAddress]
        public string? DocEmail1 { get; set; }

        [Display(Name = "Document Email 2")]
        [StringLength(64)]
        [EmailAddress]
        public string? DocEmail2 { get; set; }

        [Display(Name = "Mail Status")]
        [StringLength(64)]
        public string? MailStatus { get; set; }

        [Display(Name = "Submitted By")]
        [StringLength(128)]
        public string? SubmittedBy { get; set; }

        [Display(Name = "Submitted On")]
        [DataType(DataType.DateTime)]
        public DateTime? SubmittedOn { get; set; }

        [Display(Name = "Modified By")]
        [StringLength(128)]
        public string? ModifiedBy { get; set; }

        [Display(Name = "Modified On")]
        [DataType(DataType.DateTime)]
        public DateTime? ModifiedOn { get; set; }
    }
}
