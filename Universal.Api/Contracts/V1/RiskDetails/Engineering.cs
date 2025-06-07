using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
{
    public class PolicyAsEngineering : RiskDetail
    {
        public string ContractorName { get; set; }
        public string ScopeOfContract { get; set; }
        public string ProjectConsultant { get; set; }
        public string PrincipalName { get; set; }
        public string RiskDescription { get; set; }
        public string ContractAwardDate { get; set; }
        public string AnyOneYear { get; set; }
        public string RiskClassification { get; set; }
        public string PropertyDescription { get; set; }
        public string EstimatedContractTerms { get; set; }
        public string PrincipalAddress { get; set; }
        public string RiskAddress { get; set; }
        public string MinorExcess { get; set; }
        public string IndustryId { get; set; }
        public string Remarks { get; set; }
        public string AnyOneLimit { get; set; }
        public bool SurveyRequired { get; set; }
        public string MaintenanceFrom { get; set; }
        public string MaintenanceTo { get; set; }

        public override void FromPolicyDetail(PolicyDetail pd)
        {
            throw new System.NotImplementedException();
        }

        public override Models.PolicyDetail ToPolicyDetail()
        {
            return new Models.PolicyDetail
            {

            };
        }
    }
}
