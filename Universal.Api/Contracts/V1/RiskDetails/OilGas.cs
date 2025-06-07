using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
{
    public class PolicyAsOilGas : RiskDetail
    {
        public string ProjectPeriodFrom { get; set; }
        public string ProjectPeriodTo { get; set; }
        public decimal InterestCover { get; set; }
        public decimal Deductibles { get; set; }
        public string Conditions { get; set; }
        public string Remarks { get; set; }
        public string Situation { get; set; }
        public string InsuredSubscription { get; set; }
        public decimal DeductionFromPremium { get; set; }

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
