//using System.Text.Json;
//using System.Collections.Generic;
//using GibsLifesMicroWebApi.Models;

//namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
//{
//    public class PolicyAsAccident : RiskDetail
//    {
//        public string CoverType { get; set; }
//        public string Location { get; set; }
//        public string Description { get; set; }
//        public string ScopeofCover { get; set; }
//        public string LienClauses { get; set; }

//        //public List<NameValue> Attributes { get; set; }
//        public List<PersonRequest> Members { get; set; }

//        public override void FromPolicyDetail(PolicyDetail pd)
//        {
//            CoverType = pd.Field11;
//            Location = pd.Field12;
//            Description = pd.Field13;
//            ScopeofCover = pd.Field14;
//            LienClauses = pd.Field15;

//            if (pd.Field17 != null)
//                Members = JsonSerializer.Deserialize<List<PersonRequest>>(pd.Field17);
//        }

//        public override Models.PolicyDetail ToPolicyDetail()
//        {
//            return new Models.PolicyDetail
//            {
//                Field11 = CoverType,
//                Field12 = Location,
//                Field13 = Description,
//                Field14 = ScopeofCover,
//                Field15 = LienClauses,
//                Field17 = JsonSerializer.Serialize(Members),
//            };
//        }
//    }
//}
