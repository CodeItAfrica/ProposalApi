using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
{
    public class NameValue
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    //public class PolicyAsAgric : RiskDetail
    //{
    //    public string Data1 { get; set; }
    //    public string Data2 { get; set; }

    //    public override void FromPolicyDetail(PolicyDetail pd)
    //    {

    //    }

    //    public override Models.PolicyDetail ToPolicyDetail()
    //    {
    //        return new Models.PolicyDetail
    //        {
    //            Field1 = Data1,
    //        };
    //    }
    //}

    //public class PolicyAsAviation : RiskDetail
    //{
    //    public string AircraftID { get; set; }
    //    public string AircraftMake { get; set; }
    //    public string AircraftModel { get; set; }
    //    public string RegMarks { get; set; }
    //    public string SpareEquipments { get; set; }
    //    public int MaximumCrew { get; set; }
    //    public int PassengerSeating { get; set; }
    //    public int LicensedPassengers { get; set; }
    //    public string YearOfMfg { get; set; }
    //    public string CrewPersonalAccidents { get; set; }
    //    public string EngineType { get; set; }
    //    public string Usage { get; set; }
    //    public string GeographicalArea { get; set; }
    //    public int DeclaredPassengers { get; set; }
    //    public int NumberOfEngines { get; set; }
    //    public int NumberOfPilots { get; set; }
    //    public string Deductibles { get; set; }
    //    public bool NightFlight { get; set; }

    //    public override void FromPolicyDetail(PolicyDetail pd)
    //    {

    //    }

    //    public override Models.PolicyDetail ToPolicyDetail()
    //    {
    //        return new Models.PolicyDetail
    //        {

    //        };
    //    }
    //}

    //public class PolicyAsBond : RiskDetail
    //{
    //    //public int OurShare { get; set; }
    //    public string PrincipalName { get; set; }
    //    public string ContractorName { get; set; }
    //    public string BorrowerName { get; set; }
    //    public string DirectorName { get; set; }
    //    public decimal TotalBondValue { get; set; }
    //    public string NatureOfContract { get; set; }
    //    public int BondDuration { get; set; }
    //    public string ContractFrom { get; set; }
    //    public string ContractTo { get; set; }
    //    public string PrimaryGuarantor { get; set; }
    //    public string Remarks { get; set; }
    //    public string AddressOfPrincipal { get; set; }
    //    public string AddressOfContractor { get; set; }
    //    public string AddressOfBorrower { get; set; }
    //    public string AwardDate { get; set; }
    //    public string ContractWork { get; set; }
    //    public string BondFrom { get; set; }
    //    public string BondTo { get; set; }
    //    public decimal TotalContractValue { get; set; }
    //    public double PercOfContractValue { get; set; }
    //    public string BondIssueDate { get; set; }
    //    //public string RiskSMIID { get; set; }
    //    public double Rate { get; set; }
    //    //public decimal TotalSumInsured { get; set; }
    //    public string Description { get; set; }

    //    public override void FromPolicyDetail(PolicyDetail pd)
    //    {

    //    }

    //    //public decimal OurShareSumInsured { get; set; }
    //    //public string OurSharePremium { get; set; }
    //    public override Models.PolicyDetail ToPolicyDetail()
    //    {
    //        return new Models.PolicyDetail
    //        {

    //        };
    //    }
    //}

    //public class PolicyAsEngineering : RiskDetail
    //{
    //    public string ContractorName { get; set; }
    //    public string ScopeOfContract { get; set; }
    //    public string ProjectConsultant { get; set; }
    //    public string PrincipalName { get; set; }
    //    public string RiskDescription { get; set; }
    //    public string ContractAwardDate { get; set; }
    //    public string AnyOneYear { get; set; }
    //    public string RiskClassification { get; set; }
    //    public string PropertyDescription { get; set; }
    //    public string EstimatedContractTerms { get; set; }
    //    public string PrincipalAddress { get; set; }
    //    public string RiskAddress { get; set; }
    //    public string MinorExcess { get; set; }
    //    public string IndustryId { get; set; }
    //    public string Remarks { get; set; }
    //    public string AnyOneLimit { get; set; }
    //    public bool SurveyRequired { get; set; }
    //    public string MaintenanceFrom { get; set; }
    //    public string MaintenanceTo { get; set; }

    //    public override void FromPolicyDetail(PolicyDetail pd)
    //    {

    //    }

    //    public override Models.PolicyDetail ToPolicyDetail()
    //    {
    //        return new Models.PolicyDetail
    //        {

    //        };
    //    }
    //}

    //public class PolicyAsMarineCargo : RiskDetail
    //{
    //    public string CertificateType { get; set; } //Enum (Single transit Or Open Transit)
    //    public string ConveyanceId { get; set; } //Enum (Sea,Air Or Sea/Air)
    //    public string VesselDescription { get; set; }
    //    public string SubjectMatter { get; set; }
    //    public string FromCountryId { get; set; }
    //    public string ToCountryId { get; set; }
    //    public string LienClause { get; set; }
    //    public string CertificateNo { get; set; } //Auto Gen
    //    public string TINNumber { get; set; }
    //    public string PackageNum { get; set; } //Enum Containerised Or Non Contenerised
    //    public string NatureofCargo { get; set; } ///' Enum General Merchandise -360 Days Or Machinery 720 Days
    //    public string ProformaInvoiceNo { get; set; }
    //    public string MarksAndNumbers { get; set; }
    //    public double PremiumRate { get; set; }
    //    public string BasisOfValuation { get; set; }

    //    public override void FromPolicyDetail(PolicyDetail pd)
    //    {

    //    }

    //    public override Models.PolicyDetail ToPolicyDetail()
    //    {
    //        return new Models.PolicyDetail
    //        {

    //        };
    //    }
    //}

    //public class PolicyAsMarineHull : RiskDetail
    //{
    //    public string VesselStateId { get; set; }
    //    public string VesselName { get; set; }
    //    public string VesselTonne { get; set; }
    //    public string VesselOperation { get; set; }
    //    public string YearBuilt { get; set; }
    //    public string TerritorialLimits { get; set; }
    //    public int Length { get; set; }
    //    public int Depth { get; set; }
    //    public string EngineType { get; set; }
    //    public string Builder { get; set; }
    //    public string VesselClass { get; set; }
    //    public string Construction { get; set; }
    //    public string Excess { get; set; }
    //    public string CountryOfMfg { get; set; }
    //    public int Beam { get; set; }
    //    public int Draft { get; set; }
    //    public double WarLoading { get; set; }
    //    public string RiskSMIId { get; set; }
    //    public double Rate { get; set; }
    //    public string Description { get; set; }
    //    public decimal TotalSumInsured { get; set; }
    //    public decimal TotalGrossPremium { get; set; }

    //    public override void FromPolicyDetail(PolicyDetail pd)
    //    {

    //    }

    //    public override Models.PolicyDetail ToPolicyDetail()
    //    {
    //        return new Models.PolicyDetail
    //        {

    //        };
    //    }
    //}

    //public class PolicyAsOilGas : RiskDetail
    //{
    //    public string ProjectPeriodFrom { get; set; }
    //    public string ProjectPeriodTo { get; set; }
    //    public decimal InterestCover { get; set; }
    //    public decimal Deductibles { get; set; }
    //    public string Conditions { get; set; }
    //    public string Remarks { get; set; }
    //    public string Situation { get; set; }
    //    public string InsuredSubscription { get; set; }
    //    public decimal DeductionFromPremium { get; set; }

    //    public override void FromPolicyDetail(PolicyDetail pd)
    //    {

    //    }

    //    public override Models.PolicyDetail ToPolicyDetail()
    //    {
    //        return new Models.PolicyDetail
    //        {

    //        };
    //    }
    //}

}
