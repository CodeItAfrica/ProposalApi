using System;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Contracts.V1
{

    public class CreateNewCustomerRequestMaster
    {


        public string Surname { get; set; }

        public string FullName { get; set; }

        public string OtherNames { get; set; }

        public string Occupation { get; set; }
        public string NationalID { get; set; }


        public string Address { get; set; }

        public string MobilePhone { get; set; }

        public string LandPhone { get; set; }

        public string Email { get; set; }
        //public string ApiStatus { get; set; }



     //[Required]
        //public string Address { get; set; }
        //[Required, Phone]
        //public string PhoneLine1 { get; set; }
        //[Phone]
        //public string PhoneLine2 { get; set; }
        public string CityLGA { get; set; }
        [Required]
        public string StateID { get; set; }
        [Required]
        public string Nationality { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public KycTypeEnum? KycType { get; set; }

        public string KycNumber { get; set; }

        public DateTime? KycIssueDate { get; set; }

        public DateTime? KycExpiryDate { get; set; }

        public PersonRequest NextOfKin { get; set; }
    }
    public class CreateNewCustomerRequest : PersonRequest
    {
        [Required]
        public bool IsOrg { get; set; }

        public string OrgName { get; set; }

        public string OrgRegNumber { get; set; }

        public DateTime? OrgRegDate { get; set; }
        [Required]
        public string Password { get; set; }
        //[Required]
        //public string FirstName { get; set; }
        //[Required]
        //public string LastName { get; set; }
        //public string OtherName { get; set; }
        //[Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        //[Required]
        //public string Address { get; set; }
        //[Required, Phone]
        //public string PhoneLine1 { get; set; }
        //[Phone]
        //public string PhoneLine2 { get; set; }
        [Required]
        public string CityLGA { get; set; }
        [Required]
        public string StateID { get; set; }
        [Required]
        public string Nationality { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public KycTypeEnum? KycType { get; set; }

        public string KycNumber { get; set; }

        public DateTime? KycIssueDate { get; set; }

        public DateTime? KycExpiryDate { get; set; }

        public PersonRequest NextOfKin { get; set; }
    }

    public class CustomerResult : CreateNewCustomerRequest
    {
        public CustomerResult(Models.InsuredClient client)
        {
            CustomerID = client.InsuredID;
            IsOrg = client.Occupation == "CORPORATE";

            if (IsOrg)
            {
                //OrgName = client.FullName;
                //OrgRegDate = client.DateOfBirth;
                //OrgRegNumber = client.KycIdNumber;
            }

            CustomerID = client.InsuredID;
            FirstName = client.FullName;
            LastName = client.Surname;
            OtherName = client.OtherNames;
            Email = client.Email;
            Address = client.Address;
            PhoneLine2 = client.LandPhone;
            PhoneLine1 = client.MobilePhone;

            //IsCorporate = false;
            Password = null;
            Title = null;

            CityLGA = null;
            StateID = null;
            Nationality = null;
            //DateOfBirth = null;
            KycType = null;
            KycNumber = null;
            //Industry = client.Occupation;
        }

        public CustomerResult(Models.Policy policy)
        {
            CustomerID = policy.InsuredID;
            IsOrg = policy.InsOccupation == "CORPORATE";

            if (IsOrg)
            {
                //OrgName = policy.FullName;
                //OrgRegDate = policy.DateOfBirth;
                //OrgRegNumber = policy.KycIdNumber;
            }
            Password = null;
            Title = null;

            //CustomerId = policy.InsFaxNo; //ApiId
            LastName = policy.InsSurname;
            FirstName = policy.InsFirstname;
            OtherName = policy.InsOthernames;
            Address = policy.InsAddress;
            PhoneLine1 = policy.InsMobilePhone;
            PhoneLine2 = policy.InsLandPhone;
            Email = policy.InsEmail;
            //Industry = policy.InsOccupation;
            StateID = policy.InsStateID;

            CityLGA = null;
            //StateOfOrigin = null;
            Nationality = null;
            //DateOfBirth = null;
            KycType = null;
            KycNumber = null;
            //Industry = client.Occupation;
        }

        public string CustomerID { get; set; }
    }

    public class PersonRequest
    {
        public string Title { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        public string FirstName { get; set; }

        public string OtherName { get; set; }

        public GenderEnum? Gender { get; set; }
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Address { get; set; }
        [Required]
        public string PhoneLine1 { get; set; }
    
        public string PhoneLine2 { get; set; }
    }

}
