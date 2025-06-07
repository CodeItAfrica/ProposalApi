using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace GibsLifesMicroWebApi.Contracts.V1
{
    public class CreateNewAgentRequest /*: PersonRequest*/
    {
        //public int INT { get; set; }
        //public string AgentID { get; set; }
        public string UnitID { get; set; }
        public string Agent { get; set; }
        public string Address { get; set; }
        public string Area { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Phone1 { get; set; }
        public string Phone2 { get; set; }
        public string FaxNo { get; set; }
        public string Email { get; set; }
        public string InsPerson { get; set; }
        public string FinPerson { get; set; }
        public decimal Balance { get; set; }
        public decimal CreditLimit { get; set; }
        public long ComRate { get; set; }
        public string Remark { get; set; }
        public string AccountNo { get; set; }
        public string Bankname { get; set; }
        public string Tag { get; set; }

        public string Password { get; set; }

        public List<IFormFile>? Attachments { get; set; }

        //public byte Deleted { get; set; }
        //public string SubmittedBy { get; set; }
        //public DateTime SubmittedOn { get; set; }
        //public string ModifiedBy { get; set; }
        //public DateTime ModifiedOn { get; set; }
    }

    //public class AgentResult : CreateNewAgentRequest
    //{
    //    public AgentResult(Models.Party party)
    //    {
    //        AgentID = party.PartyID;
    //        AgentName = party.PartyName;

    //        //split into 3
    //        //LastName = party.PartyName;
    //        //FirstName = party.FirstName;
    //        //OtherName = party.OtherName;

    //        //Gender = party.Gender;
    //        Email = party.Email;
    //        Address = party.Address;
    //        //PhoneLine1 = party.PhoneNumber1;
    //        //PhoneLine2 = party.PhoneNumber2;

    //        //CityLGA = party.CityLGA;
    //        StateID = party.StateID;
    //        //Nationality = party.Nationality;
    //        //DateOfBirth = party.DateOfBirth;

    //        //KycType = party.KycIdType;
    //        //KycNumber = party.KycIdNumber;
    //        //KycIssueDate = party.KycIdIssueDate;
    //        //KycExpiryDate = party.KycIdExpiryDate;

    //        //NextOfKin = party.NextOfKin;
    //    }

    //    public string AgentID { get; set; }
    //}
}
