//using System;
//using System.ComponentModel.DataAnnotations;
//using GibsLifesMicroWebApi.Models;

//namespace GibsLifesMicroWebApi.Contracts.V1.RiskDetails
//{
//    public class PolicyAsMotor : RiskDetail
//    {
//        public enum VehicleTypeEnum
//        {
//            VAN,
//            BUS,
//            JEEP,
//            MINI_BUS,
//            MINI_TRUCK, //PICKUP
//            MID_TRUCK,
//            TRUNK_TRUCK,//GEN CARTAGE
//            MOTORCYCLE,
//            SALOON,
//            TRICYCLE,
//            TRACTOR,
//            OTHER
//        }

//        public enum VehicleUsageEnum
//        {
//            PRIVATE,
//            COMMERCIAL
//        }

//        public enum VehicleCoverEnum
//        {
//            COMPREHENSIVE,
//            THIRD_PARTY_ONLY,
//        }

//        [Required]
//        public string VehicleRegNo { get; set; }
//        [Required]
//        public VehicleTypeEnum? VehicleTypeID { get; set; }
//        [Required]
//        public string VehicleUser { get; set; }
//        [Required]
//        public string EngineNumber { get; set; }
//        [Required]
//        public string ChasisNumber { get; set; }
//        [Required]
//        public VehicleUsageEnum? VehicleUsage { get; set; }
//        [Required]
//        public int NumberOfSeats { get; set; }
//        [Required]
//        public string StateOfIssue { get; set; }//ENUM
//        [Required]
//        public string VehicleMake { get; set; }
//        [Required]
//        public string VehicleModel { get; set; }
//        [Required]
//        public int ManufactureYear { get; set; }
//        [Required]
//        public string VehicleColour { get; set; }

//        public string EngineCapacityHP { get; set; }
//        [Required]
//        public VehicleCoverEnum? CoverType { get; set; }

//        public override void FromPolicyDetail(PolicyDetail pd)
//        {
//            CoverType = Enum.Parse<VehicleCoverEnum>(pd.Field17);
//            VehicleRegNo = pd.Field19;
//            EngineNumber = pd.Field20;
//            ChasisNumber = pd.Field21;
//            VehicleTypeID = Enum.Parse<VehicleTypeEnum>(pd.Field22);
//            VehicleMake = pd.Field23;
//            VehicleModel = pd.Field24;
//            ManufactureYear = int.Parse(pd.Field25);
//            VehicleColour = pd.Field26;

//            StateOfIssue = pd.Field28;
//            EngineCapacityHP = pd.Field29;
//            NumberOfSeats = int.Parse(pd.Field30);
//            VehicleUsage = Enum.Parse<VehicleUsageEnum>(pd.Field31);
//        }

//        public override PolicyDetail ToPolicyDetail()
//        {
//            return new PolicyDetail
//            {
//                 //Field1 = txtFTRate.Text,
//                 //Field2 = txtFTValue.Text,

//                 //Field1 = cdbTracking.SelectedValue,
//                 //Field2 = cdbRescue.SelectedValue,
//                 //Field3 = txtPCSSValue.Text,
//                 //Field4 = txtSRCCRate.Text,
//                 //Field5 = txtBuyBackRate.Text,
//                 //Field6 = txtPldiscRate.Text,
//                 //Field7 = txtSpDiscRate.Text,
//                 //Field8 = txtNCDRate.Text,
//                 //Field9 = txtTrackingCost.Text,
//                 //Field10 = txtRescueCost.Text,
//                 //Field11 = txtTPPDValue.Text,
//                 //Field12 = txtSRCCValue.Text,
//                 //Field13 = txtExcessValue.Text,
//                 //Field14 = txtPlDiscValue.Text,
//                 //Field15 = txtSpDiscValue.Text,
//                 //Field16 = txtNCDValue.Text,
//                 Field17 = CoverType.ToString() /*cdbCoverType.SelectedValue*/,
//                 //Field18 = waxcode1.SelectedValue,
//                 Field19 = VehicleRegNo /*txtVehRegNo.Text*/,
//                 Field20 = EngineNumber /*txtEngineNo.Text*/,
//                 Field21 = ChasisNumber /*txtChassisNo.Text*/,
//                 Field22 = VehicleTypeID.ToString() /*cbVehType.SelectedValue*/,
//                 Field23 = VehicleMake /*txtVehMake.Text*/,
//                 Field24 = VehicleModel /*txtVehBrand.Text*/,
//                 Field25 = ManufactureYear.ToString() /*txtModelYr.Text*/,
//                 Field26 = VehicleColour/*txtVehColor.Text*/,

//                 Field28 = StateOfIssue /*cbState.SelectedValue*/,
//                 Field29 = EngineCapacityHP /*txtCapacity.Text*/,
//                 Field30 = NumberOfSeats.ToString() /*txtSeatNo.Text*/,
//                 Field31 = VehicleUsage.ToString() /*cdbUsage.SelectedValue*/,
//                 //Field35 = txtTPFTRate.Text,
//                 //Field45 = cdbReversalTypes.SelectedItem.Value,
//                 //Field48 = cdbEndorseOption.SelectedItem.Value,
//                 //Field49 = CDbl(txtProRataPrem.Text),
//                 Field50 = "PENDING",
//            };
//        }
//    }
//}
