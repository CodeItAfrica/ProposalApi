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
}
