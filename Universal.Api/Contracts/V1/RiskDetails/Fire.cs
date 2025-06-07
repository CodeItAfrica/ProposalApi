using System.Collections.Generic;
using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
{
    public class PolicyAsFire : RiskDetail
    {
        //public int SectionID { get; set; }// section ID is serial number 1, 2, auto generated
        //public string Section { get; set; }// 'Enum :- List all product for selection e.g fire, burglary
        //public string RiskSMIID { get; set; }//
        //public double FireRate { get; set; }
        //public double PerilsRate { get; set; }
        //-----------------------------------------------------------------//

        public string CoverType { get; set; }
        public string PropertyType { get; set; }
        public string PropertyAddress { get; set; }
        public string PropertyDescription { get; set; }//
        public string PropertyLengthOfStay { get; set; } //how long in property
        public string PropertyOccupants { get; set; }//no of staff
        public string TypeOfGoods { get; set; }
        public decimal EstimatedValue { get; set; }

        // generic fields
        public List<NameValue> Values { get; set; }

        public override void FromPolicyDetail(PolicyDetail pd)
        {
        }

        //public string MaterialWall { get; set; }
        //public string MaterialRoof { get; set; }
        //public string WindowsBuglary { get; set; }


        public override PolicyDetail ToPolicyDetail()
        {
            return new PolicyDetail
            {

            };
        }
    }

}
