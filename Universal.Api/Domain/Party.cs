using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class Party
    {
        public string PartyID { get; set; }
        //public string ApiId { get; set; }
        //public string ApiPassword { get; set; }

        public string StateID { get; set; }

        [Column("Party")]
        public string PartyName { get; set; }

        //public string FirstName { get; set; }
        //public string OtherName { get; set; }

        public string Description { get; set; }

        public string PartyType { get; set; }

        public string Address { get; set; }

        public string mobilePhone { get; set; }

        public string LandPhone { get; set; }

        public string Email { get; set; }

        public string Fax { get; set; }

        public string InsContact { get; set; }

        public string FinContact { get; set; }

        public decimal? CreditLimit { get; set; }

        public decimal? ComRate { get; set; }

        public string Remarks { get; set; }
        //public string ApiStatus { get; set; }

        public byte? Deleted { get; set; }

        public byte? Active { get; set; }

        public string SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public string ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
    }
}
