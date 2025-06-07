using System;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Models
{
    public class AutoNumber
    {
        [Key]
        public long NumID { get; set; }

        public string NumType { get; set; }

        public string BranchID { get; set; }

        public string RiskID { get; set; }

        public long? NextValue { get; set; }

        public DateTime? LastUpdated { get; set; }

        public string Format { get; set; }

        public string Sample { get; set; }
    }
}
