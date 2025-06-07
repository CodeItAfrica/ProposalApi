using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class PolicyAutoNumber
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long NumID { get; set; }

        [Key]
        public string NumType { get; set; }
        [Key]
        public string RiskID { get; set; }
        [Key]
        public string BranchID { get; set; }
        [Key]
        public string CompanyID { get; set; }


        public long? NextValue { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string Format { get; set; }
        public string Sample { get; set; }
    }
}
