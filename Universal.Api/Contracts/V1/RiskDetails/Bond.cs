using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
{
    public class PolicyAsBond : RiskDetail
    {
        //public int OurShare { get; set; }
        public string PrincipalName { get; set; }
        public string ContractorName { get; set; }
        public string BorrowerName { get; set; }
        public string DirectorName { get; set; }
        public decimal TotalBondValue { get; set; }
        public string NatureOfContract { get; set; }
        public int BondDuration { get; set; }
        public string ContractFrom { get; set; }
        public string ContractTo { get; set; }
        public string PrimaryGuarantor { get; set; }
        public string Remarks { get; set; }
        public string AddressOfPrincipal { get; set; }
        public string AddressOfContractor { get; set; }
        public string AddressOfBorrower { get; set; }
        public string AwardDate { get; set; }
        public string ContractWork { get; set; }
        public string BondFrom { get; set; }
        public string BondTo { get; set; }
        public decimal TotalContractValue { get; set; }
        public double PercOfContractValue { get; set; }
        public string BondIssueDate { get; set; }
        //public string RiskSMIID { get; set; }
        public double Rate { get; set; }
        //public decimal TotalSumInsured { get; set; }
        public string Description { get; set; }

        public override void FromPolicyDetail(PolicyDetail pd)
        {
            throw new System.NotImplementedException();
        }

        //public decimal OurShareSumInsured { get; set; }
        //public string OurSharePremium { get; set; }
        public override Models.PolicyDetail ToPolicyDetail()
        {
            return new Models.PolicyDetail
            {

            };
        }
    }
}
