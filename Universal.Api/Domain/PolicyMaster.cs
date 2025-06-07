using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GibsLifesMicroWebApi.Models
{
    public class PolicyMaster
    {
        [Key]
        public long     PolicyID          {get; set;}
        public string  PolicyNo        {get; set;}    
        public string ProposalNo       {get; set;}
        public string BrCode           {get; set;}
        public DateTime? TDate           {get; set;}
        public DateTime? PropDate         {get; set;}
        public DateTime? AssDate          {get; set;}
        public string CoverCode        {get; set;}
        public string Covertype        {get; set;}
        public string Title            {get; set;}
        public string AssuredCode      {get; set;}
        public string SurName        {get; set;}
        public string OtherNames       {get; set;}
        public string Address          {get; set;}
        public string FullName         {get; set;}
        public string State         {get; set;}
        public string Landphone       {get; set;}
        public string MobilePhone      {get; set;}
        public string Email            {get; set;}
        public string NationalID       {get; set;}
        public string Occupation       {get; set;}
        public string Sex              {get; set;}
        public string MaritalStatus    {get; set;}
        public string Country         {get; set;}
        public string AgentDescription {get; set;}
        public DateTime? StartDate      {get; set;}
        public DateTime? MaturityDate     {get; set;}
        public double Duration { get; set; }
        public DateTime? DateofBirth { get; set; }
        public string FOP { get; set; }
        public decimal Age { get; set; }
        public string MOP { get; set; }        
        public string TransSTATUS { get; set; }        
        public string AgentCode { get; set; }        
        public decimal BasicPremium { get; set; }
        public decimal SumAssured { get; set; }
        public long isProposal { get; set; }
        public long Deleted { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }

    }
}
