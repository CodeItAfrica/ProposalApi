using System;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Models
{
    public class Branch
    {
        [Key]
        public string BranchID { get; set; }

        public string RegionID { get; set; }

        public string StateID { get; set; }

        public string BranchID2 { get; set; }

        public string Description { get; set; }

        public string Manager { get; set; }

        public string Address { get; set; }

        public string MobilePhone { get; set; }

        public string LandPhone { get; set; }

        public string Email { get; set; }

        public string Fax { get; set; }

        public byte? Deleted { get; set; }

        public byte? Active { get; set; }

        public string Remarks { get; set; }

        public string SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public string ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
    }
}
