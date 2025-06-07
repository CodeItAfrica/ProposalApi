//using NIID;
//using System;
//using System.Threading.Tasks;
//using GibsLifesMicroWebApi.Data.Repositories;
//using GibsLifesMicroWebApi.Models;

//namespace GibsLifesMicroWebApi
//{
//    public class NiidService
//    {
//        private readonly ServiceSoapClient _client;
//        private readonly Repository _repository;
//        private readonly string _username;
//        private readonly string _password;

//        public NiidService(Repository repository)
//        {
//            _client = new ServiceSoapClient(ServiceSoapClient.EndpointConfiguration.ServiceSoap);
//            _repository = repository;
//            _username = "andrewiyke";
//            _password = "universal1234";
//        }

//        public async Task PublishAndSaveNIID(Policy p)
//        {
//            string classID = p.SubRisk.RiskID; // policy.Product.ClassID;

//            if (p is null)
//                return;

//            if (p.DebitNote is null)
//                return;

//            try
//            {
//                switch (classID)
//                {
//                    case "V":
//                        await RecordAuto(p);
//                        break;

//                    case "M":
//                        await RecordMarine(p);
//                        break;

//                    default:
//                        break;
//                }
//            }
//            catch (Exception ex)
//            {
//                _repository.AuditLogCreateAsync("NIID_EXCEPTION", p.PolicyNo, classID, ex.ToString());
//            }
//            finally
//            {
//                await _repository.SaveChangesAsync();
//            }
//        }

//        private async Task RecordAuto(Policy p)
//        {
//            foreach (var sec in p.PolicyDetails)
//            {
//                var coverType = sec.Field17.ToUpper() switch
//                {
//                    "COMPREHENSIVE" => "C",
//                    "THIRD PARTY ONLY" => "T",
//                    "THIRD PARTY, FIRE & THEFT" => "P",
//                    _ => "P"
//                };

//                var r = new VehicleRecord
//                {
//                    Username = _username,
//                    Password = _password,
//                    NiaNaicomID = "",
//                    PolicyNo = p.PolicyNo,
//                    InsuredName = p.InsFullname,
//                    ContactAddress = p.InsAddress ??= "TBA",
//                    GSMNo = p.InsMobilePhone ??= "080577777777",
//                    Email = p.InsEmail ??= "info@universal.com.ng",
//                    EffectiveCoverDate = p.StartDate.Value,
//                    ExpirationDate = p.EndDate.Value,
//                    TypeOfCover = coverType,
//                    VehicleCategory = sec.Field22,
//                    EngineNo = sec.Field20,
//                    ChasisNo = sec.Field21,
//                    VehicleColor = sec.Field26,
//                    YearofMake = sec.Field25,
//                    VehicleMake = sec.Field23,
//                    RegistrationNo = sec.Field19,
//                    OldRegistrationNo = sec.Field19,
//                    VehicleType = sec.Field22,
//                    EngineCapacity = sec.Field25,
//                    VehicleModel = sec.Field24,
//                    SumAssured = p.SumInsured.Value,
//                    Premium = p.GrossPremium.Value,
//                    CoverNoteNo = sec.CertOrDocNo,
//                    CertificateNo = sec.CertOrDocNo,
//                    GeographicalZone = sec.Field28,
//                };
//                var res = await r.Submit(_client);
//                ProcessResponse(res, sec);
//            }
//        }

//        private async Task RecordMarine(Policy p)
//        {
//            foreach (var sec in p.PolicyDetails)
//            {
//                var coverTypeID = sec.Field39.Trim().Replace("\r\n", "") switch
//                {
//                    "ICC ’A’" => 1,
//                    "ICC ’B’" => 2,
//                    "ICC ’C’" => 3,
//                    "IFFC ’A’" => 4,
//                    "IFFC ’C’" => 5,
//                    "IFMC ’A’" => 6,
//                    "IFMC ’C’" => 7,
//                    "IBOC" => 8,
//                    "ICC(AIR)" => 9,
//                    "INLAND TRANSIT" => 10,
//                    _ => 1
//                };

//                var currencyTypeID = p.ExCurrency switch
//                {
//                    "NAIRA" => 1,
//                    "Dollar" => 2,
//                    "Pound" => 3,
//                    "Euro" => 4,
//                    "Yen" => 5,
//                    _ => 1
//                };

//                var packingTypeID = sec.Field40 == "Containerized" ? 1 : 2;
//                var natureOfCargoID = sec.Field12 == "General Merchandise - 360 days" ? 1 : 2;

//                var r = new MarineRecord
//                {
//                    Username = _username,
//                    Password = _password,
//                    NiaNaicomID = "",
//                    CustomerCategoryId = 2,
//                    PolicyNo = p.PolicyNo,
//                    CertificateNo = sec.CertOrDocNo,
//                    ProformaInvoice = sec.Field41,
//                    ClientName = p.InsFullname,
//                    ClientAddress = p.InsAddress,
//                    ClientMobile = "07037428830",
//                    ClientEmail = "admin@universalinsuranceplc.com",
//                    InceptionDate = (DateTime)p.StartDate,
//                    MarinePolicyTypeId = sec.Field2 == "SINGLE TRANSIT" ? 1 : 2,
//                    CoverTypeId = coverTypeID,
//                    CargoDescription = sec.Field35,
//                    PackingTypeId = packingTypeID,
//                    BankName = sec.Field28,
//                    Premium = (decimal)sec.GrossPremium,
//                    SumInsured = (decimal)sec.SumInsured,
//                    WarAndStrikeRate = 0,
//                    BasicRate = (decimal)sec.PremiumRate,
//                    TotalRate = 0,
//                    Conditions = sec.Field26,
//                    SailingFrom = sec.Field9,
//                    SailingTo = sec.Field10,
//                    VesselName = sec.Field34,
//                    InvoicedValue = (decimal)sec.SumInsured,
//                    CargoCurrencyTypeId = currencyTypeID,
//                    Coinsurance = 0,
//                    TIN = sec.Field8,
//                    NatureOfCargo = natureOfCargoID
//                };
//                var res = await r.Submit(_client);
//                ProcessResponse(res, sec);
//            }
//        }

//        private void ProcessResponse(string result, PolicyDetail sec)
//        {
//            switch (result.ToLower())
//            {
//                case "successful":
//                case "early renewal captured!":
//                case "early renewal already captured!":
//                    //(New cls_PolicyDetails).UpdatePolicyDetailsNIIDStatus(D.DetailID)
//                    sec.Active = 2;
//                    break;

//                default:
//                    break;
//            }
//            sec.Field27 = result;
//            _repository.AuditLogCreateAsync("NIID_ALERT", sec.Policy.PolicyNo, sec.Field19, result);
//        }

//        private record class VehicleRecord()
//        {
//            public string Username { get; init; }
//            public string Password { get; init; }
//            public string NiaNaicomID { get; init; }
//            public string PolicyNo { get; init; }
//            public string InsuredName { get; init; }
//            public string ContactAddress { get; init; }
//            public string GSMNo { get; init; }
//            public string Email { get; init; }
//            public DateTime EffectiveCoverDate { get; init; }
//            public DateTime ExpirationDate { get; init; }
//            public string TypeOfCover { get; init; }
//            public string VehicleCategory { get; init; }
//            public string EngineNo { get; init; }
//            public string ChasisNo { get; init; }
//            public string VehicleColor { get; init; }
//            public string YearofMake { get; init; }
//            public string VehicleMake { get; init; }
//            public string RegistrationNo { get; init; }
//            public string OldRegistrationNo { get; init; }
//            public string VehicleType { get; init; }
//            public string EngineCapacity { get; init; }
//            public string VehicleModel { get; init; }
//            public decimal SumAssured { get; init; }
//            public decimal Premium { get; init; }
//            public string CoverNoteNo { get; init; }
//            public string CertificateNo { get; init; }
//            public string GeographicalZone { get; init; }

//            public Task<string> Submit(ServiceSoapClient client)
//            {
//                return client.Vehicle_Policy_PushAsync(
//                   Username,
//                   Password,
//                   NiaNaicomID,
//                   PolicyNo,
//                   InsuredName,
//                   ContactAddress,
//                   GSMNo,
//                   Email,
//                   EffectiveCoverDate.ToString(),
//                   ExpirationDate.ToString(),
//                   TypeOfCover,
//                   VehicleCategory,
//                   EngineNo,
//                   ChasisNo,
//                   VehicleColor,
//                   YearofMake,
//                   VehicleMake,
//                   RegistrationNo,
//                   OldRegistrationNo,
//                   VehicleType,
//                   EngineCapacity,
//                   VehicleModel,
//                   (double)SumAssured,
//                   (double)Premium,
//                   CoverNoteNo,
//                   CertificateNo,
//                   GeographicalZone);
//            }
//        }

//        private record class MarineRecord()
//        {
//            public string Username { get; init; }
//            public string Password { get; init; }
//            public string NiaNaicomID { get; init; }
//            public int CustomerCategoryId { get; init; }
//            public string PolicyNo { get; init; }
//            public string CertificateNo { get; init; }
//            public string ProformaInvoice { get; init; }
//            public string ClientName { get; init; }
//            public string ClientAddress { get; init; }
//            public string ClientMobile { get; init; }
//            public string ClientEmail { get; init; }
//            public DateTime InceptionDate { get; init; }
//            public int MarinePolicyTypeId { get; init; }
//            public int CoverTypeId { get; init; }
//            public string CargoDescription { get; init; }
//            public int PackingTypeId { get; init; }
//            public string BankName { get; init; }
//            public decimal Premium { get; init; }
//            public decimal SumInsured { get; init; }
//            public decimal WarAndStrikeRate { get; init; }
//            public decimal BasicRate { get; init; }
//            public decimal TotalRate { get; init; }
//            public string Conditions { get; init; }
//            public string SailingFrom { get; init; }
//            public string SailingTo { get; init; }
//            public string VesselName { get; init; }
//            public decimal InvoicedValue { get; init; }
//            public int CargoCurrencyTypeId { get; init; }
//            public decimal Coinsurance { get; init; }
//            public string TIN { get; init; }
//            public int NatureOfCargo { get; init; }

//            public Task<string> Submit(ServiceSoapClient client)
//            {
//                return client.Marine_Policy_PushAsync(
//                        Username,
//                        Password,
//                        NiaNaicomID,
//                        CustomerCategoryId.ToString(),
//                        PolicyNo,
//                        CertificateNo,
//                        ProformaInvoice,
//                        ClientName,
//                        ClientAddress,
//                        ClientMobile,
//                        ClientEmail,
//                        InceptionDate.ToString(),
//                        MarinePolicyTypeId.ToString(),
//                        CoverTypeId.ToString(),
//                        CargoDescription,
//                        PackingTypeId.ToString(),
//                        BankName,
//                        Premium.ToString(),
//                        SumInsured.ToString(),
//                        WarAndStrikeRate.ToString(),
//                        BasicRate.ToString(),
//                        TotalRate.ToString(),
//                        Conditions,
//                        SailingFrom,
//                        SailingTo,
//                        VesselName,
//                        InvoicedValue.ToString(),
//                        CargoCurrencyTypeId.ToString(),
//                        Coinsurance.ToString(),
//                        TIN,
//                        NatureOfCargo.ToString());
//            }
//        }
//    }
//}
