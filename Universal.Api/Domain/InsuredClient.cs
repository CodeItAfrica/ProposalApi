using System;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Models
{
    public class InsuredClient
    {
        [Key]
        public string InsuredID { get; set; }
        //public string ApiId { get; set; }
        //public string ApiPassword { get; set; }

        public string Surname { get; set; }

        public string FullName { get; set; }

        public string OtherNames { get; set; }

        public string Occupation { get; set; }
        public string NationalID { get; set; }
        public string ClientType { get; set; }

        public string Address { get; set; }

        public string MobilePhone { get; set; }

        public string LandPhone { get; set; }

        public string Email { get; set; }
        //public string ApiStatus { get; set; }


        public string Remarks { get; set; }

        public byte? Deleted { get; set; }

        public byte? Active { get; set; }
        public string SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public string ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }

    }
}
