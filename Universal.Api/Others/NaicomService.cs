//using System;
//using System.Linq;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Naicom.ApiV1;
//using GibsLifesMicroWebApi.Data.Repositories;
//using GibsLifesMicroWebApi.Models;

//namespace GibsLifesMicroWebApi
//{
//    public class NaicomService
//    {
//        private readonly Client _client;
//        private readonly Repository _repository;

//        public NaicomService(Repository repository)
//        {
//            var sid = "3d40ebd7-2956-42b6-812d-ea4cb91284c7";
//            var token = "GdfvV/3W2EjX60A9Vim2QoEt6ky5EoTH";

//            _client = CreateClient(sid, token, false);
//            _repository = repository;

//            static Client CreateClient(string sid, string token, bool isTest)
//            {
//                if (isTest)
//                    return Client.CreateTestClient(sid, token, 20000);

//                return Client.CreateLiveClient(sid, token, 20000);
//            }
//        }

//        //public async Task<NaicomDetail> FetchNaicomStatus(string policyNo)
//        //{
//        //    var policy = await GetPolicy(policyNo);

//        //    var ph = policy.Current;

//        //    if (ph is null)
//        //        throw new Exception("Policy History is missing");

//        //    if (ph.DebitNote is null)
//        //        throw new Exception("Debit Note is missing");

//        //    return policy.Current.DebitNote.Naicom;
//        //}

//        //public async Task<NaicomDetail> PublishAndSaveNaicomID(string policyNo)
//        //{
//        //    var policy = await GetPolicy(policyNo);

//        //    // save the result
//        //    return await PublishAndSaveNaicomID(policy);
//        //}

//        public async Task<NaicomDetail> PublishAndSaveNaicomID(Policy p)
//        {
//            NaicomDetail naicom = null;

//            string classID = p.SubRisk.RiskID; // policy.Product.ClassID;

//            if (p is null)
//                return null;

//            if (p.DebitNote is null)
//                return null;

//            try
//            {
//                var postResponse = classID switch
//                {
//                    "V" => await RecordPolicy(p, Auto(p)),
//                    "B" => await RecordPolicy(p, Bond(p)),
//                    "E" => await RecordPolicy(p, Engineer(p)),
//                    "F" => await RecordPolicy(p, Fire(p)),
//                    "G" => await RecordPolicy(p, Casualty(p)),
//                    "M" => await RecordPolicy(p, Marine(p)),
//                    "O" => await RecordPolicy(p, Oil(p)),

//                    "H" => await RecordPolicy(p, Marine(p)),  // aviation   -> marine
//                    "PP" => await RecordPolicy(p, Fire(p)),    // package    -> fire
//                    "MH" => await RecordPolicy(p, Marine(p)),  // marinehull -> marine
//                    _ => throw new Exception($"Unknown ClassID [{classID}]"),
//                };

//                naicom = ToNaicomDetail(postResponse);

//                static NaicomDetail ToNaicomDetail(PostResponse response)
//                {
//                    if (response.IsSucceed)
//                        return NaicomDetail.Success(response.PolicyUniqueID);

//                    return NaicomDetail.FailedPermanent(response.ErrorMessages, response.RequestPayload);
//                }
//            }
//            catch (NotImplementedException)
//            {
//                naicom = NaicomDetail.FailedPermanent($"NaicomNotImplemented {classID}", string.Empty);
//            }
//            catch (System.Net.Http.HttpRequestException ex)
//            {
//                naicom = NaicomDetail.FailedTemporary($"HttpRequestException {ex.Message}");
//            }
//            catch (Exception ex)
//            {
//                //var payload = (res == null) ? string.Empty : res.RequestPayload;
//                naicom = NaicomDetail.FailedPermanent($"NaicomUnknownError: {ex.Message}", ex.ToString());
//            }
//            finally
//            {
//                p.DebitNote.Z_NAICOM_UID = naicom.UniqueID;
//                p.DebitNote.Z_NAICOM_STATUS = naicom.Status.ToString();
//                p.DebitNote.Z_NAICOM_SENT_ON = naicom.SubmitDate;
//                p.DebitNote.Z_NAICOM_ERROR = naicom.ErrorMessage;
//                p.DebitNote.Z_NAICOM_JSON = naicom.JsonPayload;

//                await _repository.SaveChangesAsync();
//            }
//            return naicom;
//        }

//        //private async Task<Policy> GetPolicy(string policyNo)
//        //{
//        //    var policy = await _uow.Policies.Query
//        //                           .Where(p => p.PolicyNo == policyNo)
//        //                           .Include(p => p.Histories)
//        //                           .ThenInclude(h => h.Sections)
//        //                           .Include(p => p.Histories)
//        //                           .ThenInclude(h => h.DebitNote)
//        //                           //.Include(p => p.DebitNotes)
//        //                           .Include(p => p.Customer)
//        //                           .Include(p => p.Product)
//        //                           .SingleOrDefaultAsync();
//        //    if (policy is null)
//        //        throw new ArgumentOutOfRangeException(nameof(policyNo), "Policy was not found");

//        //    return policy;
//        //}

//        private Task<PostResponse> RecordPolicy<T>(Policy p, T details) where T : Detail, new()
//        {
//            var px = new Policy<T>(p.SubRisk.InsuranceTypeID,
//                                   p.StartDate.Value,
//                                   p.EndDate.Value,
//                                   $"{p.PolicyNo}/{p.DebitNote.DNCNNo}",
//                                   p.SubRiskName,
//                                   details);

//            px.Details.Premium = p.GrossPremium.Value;
//            px.Details.InsuredValue = p.SumInsured.Value;
//            px.Details.CommissionFee = p.DebitNote.Commission.Value;
//            px.Details.ExtraFee = 0;

//            px.Details.Endorsements = null;
//            px.Details.PremiumNote = null;
//            px.Details.Conditions = null;
//            px.Details.Exceptions = null;
//            px.Details.Preamble = null;
//            px.Details.Terms = null;

//            return _client.PolicyRecord(px);
//        }

//        private static Auto Auto(Policy p)
//        {
//            var coverType = p.PolicyDetails.First().Field2; // ("CoverType");
//            //var c = ph.Policy.Customer;
//            var dx = new Auto
//            {
//                CoverageType = ToEnum2(coverType),
//                OrgType = OrganizationTypeEnum.COMMERCIAL,
//                OrgName = p.InsSurname,
//                OrgID = p.InsuredID,

//                OwnerType = AutoOwnerTypeEnum.PERSON,
//                OwnerLicense = "9809282AF",
//                PersonNameLast = p.InsSurname,
//                PersonNameFirst = p.InsFirstname,
//                PersonNameMiddle = p.InsOthernames,
//                AddressLine = p.InsAddress,
//                Phone = p.InsMobilePhone,
//                Email = p.InsEmail,
//                CityLGA = "n/a",
//                State = p.InsStateID,
//                PostCode = 11755,
//            };

//            if (p.InsFirstname == "")
//                dx.PersonNameFirst = "NIL";

//            if (p.InsAddress == "")
//                dx.AddressLine = "TBA";

//            if (p.InsMobilePhone == "")
//                dx.Phone = "080577777777";

//            if (p.InsEmail == "")
//                dx.Email = "info@universal.com.ng";

//            foreach (var sec in p.PolicyDetails)
//            {
//                var insured = new AutoInsured()
//                {
//                    //EngineNumber,ChasisNumber,VehicleUsage
//                    //StateOfIssue,CoverType

//                    VehicleID = sec.Field21,
//                    PlateNo = sec.Field19,
//                    RegNo = sec.Field19,
//                    RegDate = p.StartDate.Value,
//                    RegExpDate = p.EndDate.Value,
//                    RegMileage = 15667,
//                    AutoType = AutoTypeEnum.CAR,
//                    AutoMake = sec.Field23,
//                    AutoModel = sec.Field24,
//                    AutoColor = "Blue",
//                    AutoYear = 1999,
//                    EngineCap = sec.Field29,
//                    SeatCap = sec.Field30,
//                    AutoNote = "n/a"
//                };
//                dx.Insured.Add(insured);
//            }
//            return dx;

//            static AutoCoverageTypeEnum ToEnum2(string coverType)
//            {
//                switch (coverType)
//                {
//                    case "COMPREHENSIVE":
//                        return AutoCoverageTypeEnum.COMPREHENSIVE;

//                    case "THIRD_PARTY_ONLY":
//                    case "THIRD PARTY ONLY":
//                    case "third party":
//                    case "Thirdparty":
//                        return AutoCoverageTypeEnum.THIRD_PARTY;

//                    case "THIRD_PARTY_FIRE_THEFT":
//                    case "THIRD PARTY, FIRE & THEFT":
//                    case "third party fire and theft":
//                        return AutoCoverageTypeEnum.INJURY_PROTECTION;

//                    default:
//                        return AutoCoverageTypeEnum.OTHER;
//                }
//            }
//        }

//        private static Bond Bond(Policy p)
//        {
//            throw new NotImplementedException();
//        }

//        private static Casualty Casualty(Policy p)
//        {
//            var dx = new Casualty
//            {
//                PersonalName = p.InsSurname,
//                PersonalAddress = p.InsAddress,     //TBA
//                PersonalSpecialisation = "PhD",
//                PersonalDateBirth = p.StartDate.Value,
//                PersonalSex = Naicom.ApiV1.GenderEnum.MALE,

//                ContactPhone = p.InsLandPhone,     //08011111111
//                ContactEmail = p.InsEmail,         //info@universal.com.ng

//                //PremisesName = PD.Field9,        //TBA
//                //PremisesLocation = c.Remarks,    //Lagos
//                //PremisesOccupation = PD.Field11, //TBA
//                //PremisesBusinessType = "Commercial",
//                //PremisesBusinessHour = "8am-9pm",

//                //TransitSchedule = PD.Field12, //TBA
//                //TransitRoute = PD.Field13, //TBA
//                //SafeStrongRoomDetail = PD.Field14, //TBA
//                //SafeGuardInfo = PD.Field15, //TBA
//                //GoodPropertyInfo = PD.Field16, //TBA
//                //GoodTransitMethod = PD.Field17, //TBA
//                //GoodTransitVehicleInfo = PD.Field18, //TBA
//                //WorkDescription = PD.Field19, //TBA
//                //WorkLocation = PD.Field20, //TBA
//                //WorkEmployeeInfo = PD.Field21, //TBA
//                //CompanyInfo = PD.Field22, //TBA
//                //CompanyDirectorInfo = PD.Field23, //TBA
//                //ProfessionalActivityInfo = PD.Field24, //TBA
//                //ProfessionalEmployeeInfo = PD.Field25, //TBA
//                //AccidentCoverType = PD.Field26, //TBA
//                //AccidentBeneficiaryInfo = PD.Field27, //TBA
//                //AccidentInsuredBenefit = PD.Field28, //TBA
//                //AccidentInsuredPersons = PD.InsuredName, //TBA
//                //AccountInfo = PD.Field29, //TBA
//                //EmployeeInfo = PD.Field30, //TBA
//                //PublicRiskInfo = PD.Field31, //TBA
//                //WorkCondition = PD.Field32, //TBA
//                //EstimatedWages = PD.Field33, //TBA
//                //EstimatedEarnings = PD.Field34, //TBA
//                //RiskManagementDetail = PD.Field36 //TBA
//            };
//            return dx;
//        }

//        private static Engineer Engineer(Policy p)
//        {
//            throw new NotImplementedException();
//        }

//        private static Fire Fire(Policy p)
//        {
//            //var c = ph.Policy.Customer;
//            var dx = new Fire
//            {
//                //customer
//                CustomerEmail = p.InsEmail,
//                CustomerName = p.InsFullname,
//                CustomerPhone = p.InsMobilePhone,
//                //building
//                CustomerBuildingDoorNo = "202",
//                CustomerBuildingName = p.InsFullname,
//                CustomerBuildingAddressLine = p.InsAddress,
//                CustomerBuildingAddressCityLGA = "n/a",
//                CustomerBuildingAddressState = p.InsStateID,
//                CustomerBuildingAddressPostCode = 11755,
//                //property
//                PropertyBusiness = "n/a",       //PD.Field4;
//                PropertyConstruction = "n/a",   //PD.Field5;
//                PropertyConstructionValue = (decimal)p.SumInsured, //PD.SumInsured;
//                PropertyContent = "n/a",        //PD.Field6;
//                PropertyContentValue = (decimal)p.SumInsured, //PD.SumInsured;
//                //fire
//                FireCoverageDetail = "n/a",     //PD.Field6;
//                FireProtectionDetail = "n/a",   //PD.Field7;
//                FireHistory = "n/a",            //PD.Field6;
//                //buglary
//                BurglaryCoverageDetail = "n/a", //PD.Field10;
//                BurglaryAntiTheftDetail = "n/a", //PD.Field11;
//                BurglaryHistory = "n/a",        //PD.Field12;
//                //house
//                HouseBuildingType = HouseBuildingTypeEnum.PRIVATE_DEVELLING,
//                HouseCoverageDetail = "n/a",    //PD.Field14;
//                HouseSecurityDetail = "n/a",    //PD.Field15;
//                HouseHistory = "n/a"            //PD.Field16;
//            };
//            return dx;
//        }

//        private static Marine Marine(Policy p)
//        {
//            var dx = new Marine
//            {
//                CoverageType = "general",
//                OwnerName = p.InsFullname,
//                AddressLine = p.InsAddress,
//                Phone = p.InsMobilePhone,
//                Email = p.InsEmail,
//                CityLGA = "n/a",
//                State = p.InsStateID,
//                PostCode = 11755,
//            };

//            if (p.InsAddress == "")
//                dx.AddressLine = "TBA";

//            if (p.InsMobilePhone == "")
//                dx.Phone = "080577777777";

//            if (p.InsEmail == "")
//                dx.Email = "info@universal.com.ng";

//            foreach (var sec in p.PolicyDetails)
//            {
//                var insured = new MarineInsured()
//                {
//                    //VessleLength = 0,
//                    //VessleBreadth = 0,
//                    //VessleGrossTonage = 0,
//                    //VessleCapacityCargo = 0,
//                    //VessleMaximumSpeed = 0,
//                    VessleCapacityPassenger = 24,
//                    VesslePurchaseYear = 2005,
//                    VessleBuildYear = 2012,
//                    VessleNote = "TBA",
//                    VessleType = sec.Field2, //PD.Field2,
//                    VessleName = sec.Field1,       //PD.Location
//                    //VessleRegNo = sec.ENDTNum,   //PD.ENDTNum
//                    VessleInsuredValue = sec.SumInsured.Value,            //PD.RiskSum
//                    VesslePurchaseValue = sec.SumInsured.Value,           //PD.RiskSum
//                    //VessleMakeModel = sec.SectionID,//PD.SectionID 
//                    VessleUsage = sec.Field13,//PD.Field13
//                };
//                dx.Insured.Add(insured);
//            }
//            return dx;
//        }

//        private static Oil Oil(Policy p)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
