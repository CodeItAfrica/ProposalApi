using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
{
    public class PolicyAsMarineHull : RiskDetail
    {
        public string VesselStateId { get; set; }
        public string VesselName { get; set; }
        public string VesselTonne { get; set; }
        public string VesselOperation { get; set; }
        public string YearBuilt { get; set; }
        public string TerritorialLimits { get; set; }
        public int Length { get; set; }
        public int Depth { get; set; }
        public string EngineType { get; set; }
        public string Builder { get; set; }
        public string VesselClass { get; set; }
        public string Construction { get; set; }
        public string Excess { get; set; }
        public string CountryOfMfg { get; set; }
        public int Beam { get; set; }
        public int Draft { get; set; }
        public double WarLoading { get; set; }
        public string RiskSMIId { get; set; }
        public double Rate { get; set; }
        public string Description { get; set; }
        public decimal TotalSumInsured { get; set; }
        public decimal TotalGrossPremium { get; set; }

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
