using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
{
    public class PolicyAsMarineCargo : RiskDetail
    {
        public string CertificateType { get; set; } //Enum (Single transit Or Open Transit)
        public string ConveyanceId { get; set; } //Enum (Sea,Air Or Sea/Air)
        public string VesselDescription { get; set; }
        public string SubjectMatter { get; set; }
        public string FromCountryId { get; set; }
        public string ToCountryId { get; set; }
        public string LienClause { get; set; }
        public string CertificateNo { get; set; } //Auto Gen
        public string TINNumber { get; set; }
        public string PackageNum { get; set; } //Enum Containerised Or Non Contenerised
        public string NatureofCargo { get; set; } ///' Enum General Merchandise -360 Days Or Machinery 720 Days
        public string ProformaInvoiceNo { get; set; }
        public string MarksAndNumbers { get; set; }
        public double PremiumRate { get; set; }
        public string BasisOfValuation { get; set; }

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
