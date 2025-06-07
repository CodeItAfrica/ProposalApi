using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Data
{
    [Table("Users")]
    public class ApiUser
    {
        [Key]
        [Column("UserID")]
        public long CompanyID { get; set; }
        [Column("Username")]
        public string CompanyName { get; set; }

        public string Password { get; set; }



        //public string Status { get; set; }

        //public string Remarks { get; set; }

        //public string Tag { get; set; }

        //public string Deleted { get; set; }

    }
}
