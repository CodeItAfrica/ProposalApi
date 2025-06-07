using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
{
    public class PolicyAsAviation : RiskDetail
    {
        public string AircraftID { get; set; }
        public string AircraftMake { get; set; }
        public string AircraftModel { get; set; }
        public string RegMarks { get; set; }
        public string SpareEquipments { get; set; }
        public int MaximumCrew { get; set; }
        public int PassengerSeating { get; set; }
        public int LicensedPassengers { get; set; }
        public string YearOfMfg { get; set; }
        public string CrewPersonalAccidents { get; set; }
        public string EngineType { get; set; }
        public string Usage { get; set; }
        public string GeographicalArea { get; set; }
        public int DeclaredPassengers { get; set; }
        public int NumberOfEngines { get; set; }
        public int NumberOfPilots { get; set; }
        public string Deductibles { get; set; }
        public bool NightFlight { get; set; }

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
