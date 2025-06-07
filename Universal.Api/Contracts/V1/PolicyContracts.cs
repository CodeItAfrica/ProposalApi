using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1
{
    public class CreateNew<T> where T : RiskDetail
    {
        public string PolicyNo { get; set; }
        [Required]
        public string AgentID { get; set; }
        //[Required]
        public string CustomerID { get; set; }
        //[Required]
        public string ProductID { get; set; }
        [Required]
        public DateTime EntryDate { get; set; }
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }

        public string PaymentProviderID { get; set; }

        public string PaymentReferenceID { get; set; }
        //[Required]
        public CreateNewCustomerRequest Insured { get; set; }

        [Required]
        public List<T> PolicySections { get; set; }
    }

    public abstract class RiskDetail
    {
        [Required]
        public string CertificateNo { get; set; }
        [Required]
        public decimal SectionSumInsured { get; set; }
        [Required]
        public decimal SectionPremium { get; set; }


        public abstract void FromPolicyDetail(PolicyDetail pd);

        public abstract PolicyDetail ToPolicyDetail();
    }

    public class PolicyResult
    {
        public PolicyResult(Policy p)
        {
            var dn = p.DebitNote;

            if (dn  == null)
            {
                PolicyNo = p.PolicyNo;
                AgentID = p.PartyID;
                CustomerID = p.InsuredID;
                ProductID = p.SubRiskID;
                //ProductClass = policy.
                //NaicomID = p.Z_NAICOM_UID;
                EntryDate = p.TransDate.Value;
                StartDate = p.StartDate.Value;
                EndDate = p.EndDate.Value;
            }
            else
            {
                PolicyNo = dn.PolicyNo;
                AgentID = dn.PartyID;
                CustomerID = dn.InsuredID;
                ProductID = dn.SubRiskID;
                //ProductClass = policy.
                //NaicomID = dn.Z_NAICOM_UID;
                EntryDate = dn.BillingDate.Value;
                StartDate = dn.StartDate.Value;
                EndDate = dn.EndDate.Value;
            }

            Insured = new CustomerResult(p);
        }

        public string PolicyNo { get; set; }
        public string AgentID { get; set; }
        public string CustomerID { get; set; }
        public string ProductID { get; set; }
        public string ProductClass { get; set; }
        public string NaicomID { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public CustomerResult Insured { get; set; }
    }

    public class PolicyResult<T> : PolicyResult where T : RiskDetail
    {
        public PolicyResult(Policy policy) : base(policy)
        {
            PolicySections = new List<T>();

            foreach (var pd in policy.PolicyDetails)
            {
                var t = Activator.CreateInstance(typeof(T)) as T;
                t.FromPolicyDetail(pd);
                PolicySections.Add(t);
            }
        }

        public List<T> PolicySections { get; set; }
    }
}
