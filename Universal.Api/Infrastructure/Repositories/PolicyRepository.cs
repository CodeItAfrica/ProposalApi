using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Domain;
using Microsoft.Extensions.Logging;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {

        public async Task<List<policydto>> GetIndividualPolicies(string agentId)
        {
            return await _db.PolicyMaster
                .Where(p => p.AgentCode == agentId) // Filter policies by authenticated agent
                .Select(p => new policydto
                {
                    PolicyNo = p.PolicyNo,
                    ProposalNo = p.ProposalNo,
                    FullName = p.FullName,
                    CoverType = p.Covertype,
                    AgentCode = p.AgentCode,
                    AgentDescription = p.AgentDescription,
                    StartDate = p.StartDate,
                    MaturityDate = p.MaturityDate,
                    SumAssured = p.SumAssured,
                    BasicPremium = p.BasicPremium,
                    Status = p.TransSTATUS, // Mapping TransSTATUS as Status
                    MobilePhone = p.MobilePhone,
                    Email = p.Email
                })
                .ToListAsync();
        }

        public async Task<List<Policy>> SearchPoliciesAsync(string? policyNumber, string? holderName, string? status)
        {
            var query = _db.Policies.AsQueryable();

            if (!string.IsNullOrEmpty(policyNumber))
                query = query.Where(p => p.PolicyNo == policyNumber);

            if (!string.IsNullOrEmpty(holderName))
                query = query.Where(p => p.InsSurname.Contains(holderName));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.TransSTATUS == status);

            return await query.Select(p => new Policy
            {
                PolicyNo = p.PolicyNo,
                CoPolicyNo = p.CoPolicyNo,
                BranchID = p.BranchID,
                Branch = p.Branch,
                BizSource = p.BizSource,
                SubRiskID = p.SubRiskID,
                SubRisk = p.SubRisk,
                InsSurname = p.InsSurname,
                InsFirstname = p.InsFirstname,
                InsOthernames = p.InsOthernames,
                InsAddress = p.InsAddress,
                InsStateID = p.InsStateID,
                InsMobilePhone = p.InsMobilePhone,
                InsLandPhone = p.InsLandPhone,
                InsEmail = p.InsEmail,
                InsFaxNo = p.InsFaxNo,
                InsOccupation = p.InsOccupation,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                ExCurrency = p.ExCurrency,
                SumInsured = p.SumInsured,
                GrossPremium = p.GrossPremium,
                TransSTATUS = p.TransSTATUS,
                SubmittedBy = p.SubmittedBy,
                SubmittedOn = p.SubmittedOn,
                ModifiedBy = p.ModifiedBy,
                ModifiedOn = p.ModifiedOn
            }).ToListAsync();
        }
        public DateTime NormalizeDate(DateTime date)
        {
            return date < new DateTime(1753, 1, 1) ? new DateTime(1753, 1, 1) : date;
        }

        public async Task<PolicyMaster> CreatePolicyMasterAsync(PolicyModel policiesDto)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var agentId = (_authContext.User as AgentUser)?.AgentId;
            var insured = await CustomerMasterGetOrAddAsync(policiesDto);
            if (insured is null)
                throw new ArgumentOutOfRangeException($"This CustomerId or Insured does not exist");

            var subRisk = await ProductSelectThisAsync(policiesDto.ProductID);
            if (subRisk is null)
                throw new ArgumentOutOfRangeException($"This ProductId [{policiesDto.ProductID}] does not exist");

            var agents = await AgentSelectThisAsync(agentId);
            if (agents is null)
                throw new ArgumentOutOfRangeException($"This AgentId [{agentId}] does not exist");

            //var branch = await BranchSelectThisAsync(_authContext.User.AppId);
            //if (branch is null)
            //    throw new ArgumentOutOfRangeException($"No BranchId for [{_authContext.User.AppId}]");


            var policyNo = GetNextAutoNumber("POLICY", "01", policiesDto.ProductID);
            //using var transaction = await _db.Database.BeginTransactionAsync();


            // Insert into Policies table
            var policy = new PolicyMaster
            {
                PolicyNo = policyNo,
                ProposalNo = "PR" + policyNo,
                BrCode = "HO",
                TDate = NormalizeDate(policiesDto.TransDate),
                CoverCode = subRisk.SubRiskID,
                Covertype = subRisk.SubRiskName,
                AssuredCode = insured.InsuredID,
                SurName = insured.Surname,
                OtherNames = insured.OtherNames,
                Address = insured.Address,
                FullName = insured.FullName,
                Landphone = insured.LandPhone,
                MobilePhone = insured.MobilePhone,
                Email = insured.Email,
                Occupation = insured.Occupation,
                AgentCode = agents.AgentID,
                AgentDescription = agents.Agent,
                StartDate = NormalizeDate(policiesDto.StartDate),
                MaturityDate = NormalizeDate(policiesDto.MaturityDate),
                FOP = policiesDto.FrequencyOfPayment,
                MOP = "Transfer",
                TransSTATUS = "PENDING",
                BasicPremium = policiesDto.BasicPremium,
                SumAssured = policiesDto.SumAssured,
                isProposal = 1,
                Deleted = 0, // Set default to 0
                SubmittedBy = agents.Agent,
                SubmittedOn = DateTime.Now,
                ModifiedBy = agents.Agent,
                ModifiedOn = DateTime.Now,

            };

            await _db.PolicyMaster.AddAsync(policy);


           

            // Insert into GrpMembers table
            var grpMember = new GrpMember
            {
                PolicyNo = policyNo,
                QuotationNo = "",
                EndorsementNo = "",
                BizOption = "",
                SerialNo = "",
                MembersNo = "",
                Surname = insured.Surname,
                OtherNames = insured.FullName + " " + insured.OtherNames,
                Address = insured.Address,
                DOBDate = null,
                HireDate = null,
                DisbursedDate = null,
                Sex = "",
                Age = 0,
                BasicSalary = 0,
                Housing = 0,
                Transport = 0,
                OtherAmount = 0,
                TEmolument = 0,
                SumInsured = policiesDto.SumAssured,
                PremiumRate = 0,
                GrossPremium = policiesDto.BasicPremium,
                PDERate = 0,
                PDEPremium = 0,
                TDRate = 0,
                TDPremium = 0,
                MERate = 0,
                MEPremium = 0,
                FURate = 0,
                FUPremium = 0,
                CRRate = 0,
                CRPremium = 0,
                RentEXP = 0,
                Deleted = 0,
                Active = 1,
                SubmittedBy = agents.Agent,
                SubmittedOn = DateTime.Now,
                ModifiedBy = agents.Agent,
                ModifiedOn = DateTime.Now,
                Photo = null
            };

            await _db.GrpMembers.AddAsync(grpMember);


            //await transaction.CommitAsync();
            return policy;


        }

        public async Task<Policy> CreatePolicyAsync(Policiesdto policiesDto)
        {

            var agentId = (_authContext.User as AgentUser)?.AgentId;

            var insured = await CustomerGetOrAddAsync(policiesDto);
            if (insured is null)
                throw new ArgumentOutOfRangeException($"This CustomerId or Insured does not exist");

            var subRisk = await ProductSelectThisAsync(policiesDto.ProductID);
            if (subRisk is null)
                throw new ArgumentOutOfRangeException($"This ProductId [{policiesDto.ProductID}] does not exist");

            var agents = await AgentSelectThisAsync(agentId);
            if (agents is null)
                throw new ArgumentOutOfRangeException($"This AgentId [{agentId}] does not exist");

            //var branch = await BranchSelectThisAsync(_authContext.User.AppId);
            //if (branch is null)
            //    throw new ArgumentOutOfRangeException($"No BranchId for [{_authContext.User.AppId}]");


            var policyNo = GetNextAutoNumber("POLICY", "01", policiesDto.ProductID);
            //using var transaction = await _db.Database.BeginTransactionAsync();


            // Insert into Policies table
            var policy = new Policy
                {
                PolicyNo = policyNo,
                CoPolicyNo = policyNo,
                TransDate = DateTime.Now,
                StartDate = policiesDto.StartDate,
                EndDate = policiesDto.EndDate,
                SubRiskID = subRisk.SubRiskID,
                SubRiskName = subRisk.SubRiskName,
                PartyID = agents.AgentID,
                Party = agents.Agent,
                BranchID = "100",
                Branch = "GOXI",

                InsStateID = null,
                InsuredID = insured.InsuredID,
                InsSurname = insured.Surname,
                InsFirstname = insured.FullName,
                InsOthernames = insured.OtherNames,
                InsAddress = insured.Address,
                InsMobilePhone = insured.MobilePhone,
                InsLandPhone = insured.LandPhone,
                InsEmail = insured.Email,
                InsOccupation = insured.Occupation,
                InsFaxNo = "0" /*insured.ApiId*/,

  

                ExRate = 1,
                ExCurrency = "NAIRA",
                PremiumRate = 0,
                ProportionRate = 100,
                SumInsured = policiesDto.SumInsured,
                GrossPremium = policiesDto.GrossPremium,
                SumInsuredFrgn = 0,
                GrossPremiumFrgn = 0,
                //ProRataDays = (int)(newpolicydto.EndDate - newpolicydto.StartDate).TotalDays + 1,
                ProRataPremium = 0,
                IsProposal = 0,
                BizSource = "DIRECT",
                TransSTATUS = "PENDING",
                Remarks = "RETAIL",

                SubmittedBy = agents.Agent,
                SubmittedOn = DateTime.Now,
          
                Active = 1,
                Deleted = 0,


            };

                await _db.Policies.AddAsync(policy);


            // Insert into PolicyDetails table
            var policyDetail = new PolicyDetail
            {
                //PolicyNo = policyNo,
                CoPolicyNo = policyNo,
                EntryDate = DateTime.Now,
                EndorsementNo = "", // Set to empty since it's not in policiesDto
                BizOption = "", // Set to empty
                DNCNNo = "",
                CertOrDocNo = "",
                InsuredName = insured.FullName + " " + insured.OtherNames,
                StartDate = policiesDto.StartDate,
                EndDate = policiesDto.EndDate,

                ExRate = 1,
                ExCurrency = "NAIRA",
                PremiumRate = 0,
                ProportionRate = 0,
                SumInsured = policiesDto.SumInsured,
                GrossPremium = policiesDto.GrossPremium,
                SumInsuredFrgn = 0,
                GrossPremiumFrgn = 0,
                ProRataDays = 365,
                ProRataPremium = 0,
                NetAmount = 0, // Default value
                Deleted = 0,
                Active = 1,
                SubmittedBy = agents.Agent,
                SubmittedOn = DateTime.Now,
                ModifiedBy = agents.Agent,
                ModifiedOn = DateTime.Now,
                TotalRiskValue = 0 // Default value
            };

            await _db.PolicyDetails.AddAsync(policyDetail);


            // Insert into GrpMembers table
            var grpMember = new GrpMember
            {
                PolicyNo = policyNo,
                QuotationNo = "",
                EndorsementNo = "",
                BizOption = "",
                SerialNo = "",
                MembersNo = "",
                Surname = insured.Surname,
                OtherNames = insured.FullName + " " + insured.OtherNames,
                Address = insured.Address,
                DOBDate = null,
                HireDate = null,
                DisbursedDate = null,
                Sex = "",
                Age = 0,
                BasicSalary = 0,
                Housing = 0,
                Transport = 0,
                OtherAmount = 0,
                TEmolument = 0,
                SumInsured = policiesDto.SumInsured,
                PremiumRate = 0,
                GrossPremium = policiesDto.GrossPremium,
                PDERate = 0,
                PDEPremium = 0,
                TDRate = 0,
                TDPremium = 0,
                MERate = 0,
                MEPremium = 0,
                FURate = 0,
                FUPremium = 0,
                CRRate = 0,
                CRPremium = 0,
                RentEXP = 0,
                Deleted = 0,
                Active = 1,
                SubmittedBy = agents.Agent,
                SubmittedOn = DateTime.Now,
                ModifiedBy = agents.Agent,
                ModifiedOn = DateTime.Now,
                Photo = null
            };

            await _db.GrpMembers.AddAsync(grpMember);


            //await transaction.CommitAsync();
            return policy;
            
         
        }
        public async Task<int> GetTotalPaymentsCountAsync()
        {
            return await _db.DNCNNotes.CountAsync(x => x.NoteType == "RCP");
        }

        public async Task<int> GetIndividualTotalActivePoliciesAsync()
        {
            return await _db.Policies.CountAsync();
        }

        public async Task<int> GetGroupTotalActivePoliciesAsync()
        {
            return await _db.PolicyMaster.CountAsync();
        }

        //private async Task SavePolicyDetails(string policyNo, Policiesdto policiesDto)
        //{
        //    // Insert into PolicyDetails table
        //    var policyDetail = new PolicyDetail
        //    {
        //        PolicyNo = policyNo,
        //        CoPolicyNo = policiesDto.PolicyNo,
        //        EntryDate = DateTime.Now,
        //        EndorsementNo = "", // Set to empty since it's not in policiesDto
        //        BizOption = "", // Set to empty
        //        DNCNNo = "",
        //        CertOrDocNo = "",
        //        InsuredName = policiesDto.InsSurname + " " + policiesDto.InsFirstname,
        //        StartDate = policiesDto.StartDate,
        //        EndDate = policiesDto.EndDate,

        //        ExRate = 1,
        //        ExCurrency = policiesDto.ExCurrency,
        //        PremiumRate = 0,
        //        ProportionRate = 0,
        //        SumInsured = policiesDto.SumInsured,
        //        GrossPremium = policiesDto.GrossPremium,
        //        SumInsuredFrgn = 0,
        //        GrossPremiumFrgn = 0,
        //        ProRataDays = 365,
        //        ProRataPremium = 0,
        //        NetAmount = 0, // Default value
        //        Deleted = 0,
        //        Active = 1,
        //        SubmittedBy = policiesDto.SubmittedBy,
        //        SubmittedOn = policiesDto.SubmittedOn,
        //        ModifiedBy = policiesDto.ModifiedBy,
        //        ModifiedOn = policiesDto.ModifiedOn,
        //        TotalRiskValue = 0 // Default value
        //    };

        //    await _db.PolicyDetails.AddAsync(policyDetail);
        //}

        public async Task<List<GetPoliciesdto>> GetGroupPoliciesAsync(string agentId)
        {
            return await _db.Policies
                 .Where(p => p.PartyID == agentId)
                  .Select(p => new GetPoliciesdto
                  {
                      PolicyNo = p.PolicyNo,
                      CoPolicyNo = p.CoPolicyNo,
                      BranchID = p.BranchID,
                      Branch = p.Branch,
                      BizSource = p.BizSource,
                      SubRiskID = p.SubRiskID,
                      SubRisk = p.SubRiskName,
                      InsSurname = p.InsSurname,
                      InsFirstname = p.InsFirstname,
                      InsOthernames = p.InsOthernames,
                      InsAddress = p.InsAddress,
                      InsStateID = p.InsStateID,
                      InsMobilePhone = p.InsMobilePhone,
                      InsLandPhone = p.InsLandPhone,
                      InsEmail = p.InsEmail,
                      InsFaxNo = p.InsFaxNo,
                      InsOccupation = p.InsOccupation,
                      StartDate = p.StartDate,
                      EndDate = p.EndDate,
                      ExCurrency = p.ExCurrency,
                      SumInsured = p.SumInsured,
                      GrossPremium = p.GrossPremium,
                      TransSTATUS = p.TransSTATUS,
                      SubmittedBy = p.SubmittedBy,
                      SubmittedOn = p.SubmittedOn,
                      ModifiedBy = p.ModifiedBy,
                      ModifiedOn = p.ModifiedOn,
                  })
                .ToListAsync();
        }

        public async Task<Policy> PolicySelectThisAsync(string policyNo)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                throw new ArgumentNullException(nameof(policyNo));

            var p = await _db.Policies.FirstOrDefaultAsync(x => x.PolicyNo == policyNo);

            if (p != null)
                p.DebitNote = await _db.DNCNNotes.FirstOrDefaultAsync(z => z.PolicyNo == policyNo &&
                                                                           z.NoteType == "DN");
            return p;
        }

        public Task<List<Policy>> PolicySelectAsync(FilterPaging filter)
        {
            if (filter == null)
                filter = new FilterPaging();

            var query = _db.Policies.Where(x => x.Deleted == 0);

            if (_authContext.User is AppUser u)
                query = query.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{u.AppId}");

            //else if (_authContext.User is AgentUser a)
            //    query = query.Where(x => x.PartyID == a.PartyId);

            else if (_authContext.User is CustomerUser c)
                query = query.Where(x => x.InsuredID == c.InsuredId);

            if (filter.CanSearchDate)
                query = query.Where(x => (x.TransDate >= filter.DateFrom) &&
                                         (x.TransDate <= filter.DateTo));

            return query.OrderByDescending(x => x.TransDate)
                        //.Skip(filter.SkipCount)
                        .Take(filter.PageSize)
                        .ToListAsync();
        }

        //public Task<List<PolicyDetail>> PolicyDetailSelectAsync(string policyNo)
        //{
        //    if (string.IsNullOrWhiteSpace(policyNo))
        //        throw new ArgumentNullException(nameof(policyNo));

        //    var query = _db.PolicyDetails.Where(x => x.PolicyNo == policyNo);

        //    return query.OrderByDescending(x => x.StartDate)
        //                //.Skip(filter.SkipCount)
        //                .ToListAsync();
        //}

        //public async Task<Policy> PolicyCreateAsync(Policiesdto newpolicydto)

        //{
        //    if (newpolicydto is null)
        //        throw new ArgumentNullException(nameof(newpolicydto));

            

        //    // check for insured, party, product
        //    var insured = await CustomerGetOrAddAsync(newpolicydto);
        //    if (insured is null)
        //        throw new ArgumentOutOfRangeException($"This CustomerId or Insured does not exist");

        //    var subRisk = await ProductSelectThisAsync(newpolicydto.ProductID);
        //    if (subRisk is null)
        //        throw new ArgumentOutOfRangeException($"This ProductId [{newpolicydto.ProductID}] does not exist");

        //    var agents = await AgentSelectThisAsync(newpolicydto.AgentID);
        //    if (agents is null)
        //        throw new ArgumentOutOfRangeException($"This AgentId [{newpolicydto.AgentID}] does not exist");

        //    var branch = await BranchSelectThisAsync(_authContext.User.AppId);
        //    if (branch is null)
        //        throw new ArgumentOutOfRangeException($"No BranchId for [{_authContext.User.AppId}]");

        //    // create the policy
        //    var policy = CreateNewPolicy(newpolicydto, insured, branch, agents, subRisk);
        //    _db.Policies.Add(policy);

        //    policy.PolicyDetails = new List<PolicyDetail>();

        //    // create the policy details
        //    //foreach (var detailDto in newpolicydto.PolicySections)
        //    //{
        //    //    var policyDetail = CreateNewPolicyDetail(detailDto, policy);
        //    //    policy.PolicyDetails.Add(policyDetail);
        //    //    //_db.PolicyDetails.Add(policyDetail);
        //    //}
        //    await SavePolicyDetail(policy.PolicyNo, newpolicydto);

        //    //// create a debit note 
        //    //var debitNote = CreateNewDebitNote(policy);
        //    //_db.DNCNNotes.Add(debitNote);

        //    //policy.DebitNote = debitNote; //add the DN to the policy object
        //    //policy.SubRisk = subRisk;     //add the subRisk to the policy object

        //    //var hasPaid = await PaymentValidate(newpolicydto.PaymentReferenceID, newpolicydto.PaymentProviderID);

        //    //if (hasPaid)
        //    //{
        //    //    // and also a reciept
        //    //    var receipt = CreateNewReceipt(policy, debitNote.refDNCNNo);
        //    //    _db.DNCNNotes.Add(receipt);
        //    //}

        //    //return the policy number
        //    return policy;
        //}

        //public void SaveNaicomStatus(Policy policy, NaicomDetail naicom)
        //{
        //    policy.Z_NAICOM_UID = naicom.UniqueID;

        //    policy.DebitNote.Z_NAICOM_UID = naicom.UniqueID;
        //    policy.DebitNote.Z_NAICOM_STATUS = naicom.Status.ToString();
        //    policy.DebitNote.Z_NAICOM_SENT_ON = naicom.SubmitDate;
        //    policy.DebitNote.Z_NAICOM_ERROR = naicom.ErrorMessage;
        //    policy.DebitNote.Z_NAICOM_JSON = naicom.JsonPayload;
        //}

        //private Policy CreateNewPolicy(Policiesdto newpolicydto, InsuredClient insured, Branch branch, Agents agents, SubRisk subRisk)
  
        //{
        //    if (newpolicydto.StartDate >= newpolicydto.EndDate)
        //        throw new ArgumentOutOfRangeException(nameof(newpolicydto.StartDate),
        //            $"{nameof(newpolicydto.StartDate)} cannot be later than {nameof(newpolicydto.EndDate)}");

        //    var policyNo = GetNextAutoNumber("POLICY", branch.BranchID, newpolicydto.ProductID);

          

        //    return new Policy()
        //    {
        //        //save the PolicyNo from caller to MktStaffID //14-feb-23
        //        MktStaffID = newpolicydto.PolicyNo,

        //        PolicyNo = policyNo,
        //        CoPolicyNo = null,
        //        TransDate = DateTime.Now,
        //        StartDate = newpolicydto.StartDate,
        //        EndDate = newpolicydto.EndDate,
        //        SubRiskID = subRisk.SubRiskID,
        //        SubRiskName = subRisk.SubRiskName,
        //        PartyID = agents.AgentID,
        //        Party = agents.Agent,
        //        BranchID = branch.BranchID,
        //        Branch = branch.Description,

        //        InsStateID = null,
        //        InsuredID = insured.InsuredID,
        //        InsSurname = insured.Surname,
        //        InsFirstname = insured.FirstName,
        //        InsOthernames = insured.OtherNames,
        //        InsAddress = insured.Address,
        //        InsMobilePhone = insured.MobilePhone,
        //        InsLandPhone = insured.LandPhone,
        //        InsEmail = insured.Email,
        //        InsOccupation = insured.Occupation,
        //        InsFaxNo = "0" /*insured.ApiId*/,

        //        InsuredClient = insured, //hmmm

        //        ExRate = 1,
        //        ExCurrency = "NAIRA",
        //        PremiumRate = 0,
        //        ProportionRate = 100,
        //        SumInsured = newpolicydto.SumInsured,
        //        GrossPremium = newpolicydto.GrossPremium,
        //        SumInsuredFrgn = 0,
        //        GrossPremiumFrgn = 0,
        //        //ProRataDays = (int)(newpolicydto.EndDate - newpolicydto.StartDate).TotalDays + 1,
        //        ProRataPremium = 0,
        //        IsProposal = 0,
        //        BizSource = "DIRECT",
        //        TransSTATUS = "PENDING",
        //        Remarks = "RETAIL",

        //        SubmittedBy = $"{SUBMITTED_BY}/{_authContext.User.AppId}",
        //        SubmittedOn = DateTime.Now,
        //        Active = 1,
        //        Deleted = 0,
        //    };
        //}
        //private async Task SavePolicyDetail(string policyNo, Policiesdto newpolicydto)
        //{
        //    // Insert into PolicyDetails table
        //    var policyDetail = new PolicyDetail
        //    {
        //        PolicyNo = policyNo,
        //        CoPolicyNo = newpolicydto.PolicyNo,
        //        EntryDate = DateTime.Now,
        //        EndorsementNo = "", // Set to empty since it's not in newpolicydto
        //        BizOption = "", // Set to empty
        //        DNCNNo = "",
        //        CertOrDocNo = "",
        //        InsuredName = newpolicydto.InsSurname + " " + newpolicydto.InsFirstname,
        //        StartDate = newpolicydto.StartDate,
        //        EndDate = newpolicydto.EndDate,

        //        ExRate = 1,
        //        ExCurrency = newpolicydto.ExCurrency,
        //        PremiumRate = 0,
        //        ProportionRate = 0,
        //        SumInsured = newpolicydto.SumInsured,
        //        GrossPremium = newpolicydto.GrossPremium,
        //        SumInsuredFrgn = 0,
        //        GrossPremiumFrgn = 0,
        //        ProRataDays = 365,
        //        ProRataPremium = 0,
        //        NetAmount = 0, // Default value
        //        Deleted = 0,
        //        Active = 1,
        //        SubmittedBy = newpolicydto.SubmittedBy,
        //        SubmittedOn = newpolicydto.SubmittedOn,
        //        ModifiedBy = newpolicydto.ModifiedBy,
        //        ModifiedOn = newpolicydto.ModifiedOn,
        //        TotalRiskValue = 0 // Default value
        //    };

        //    await _db.PolicyDetails.AddAsync(policyDetail);
        }

        //private PolicyDetail CreateNewPolicyDetail<T>(T detailDto, Policy policy)
        //    where T : RiskDetail
        //{
        //    string endorseNo = GetNextAutoNumber("INVOICE", BRANCH_ID);
        //    var pd = detailDto.ToPolicyDetail();

        //    //pd.PolicyNo = policy.PolicyNo;
        //    pd.Policy = policy;
        //    pd.ExRate = policy.ExRate;

        //    pd.EndorsementNo = endorseNo;
        //    pd.CertOrDocNo = detailDto.CertificateNo;
        //    pd.EntryDate = policy.TransDate;
        //    pd.BizOption = "NEW";
        //    pd.InsuredName = policy.InsFullname;
        //    pd.StartDate = policy.StartDate;
        //    pd.EndDate = policy.EndDate;
        //    pd.ExRate = policy.ExRate;
        //    pd.ExCurrency = policy.ExCurrency;
        //    pd.PremiumRate = policy.PremiumRate;
        //    pd.ProportionRate = policy.ProportionRate;
        //    pd.ProRataDays = policy.ProRataDays;

        //    pd.SumInsured = detailDto.SectionSumInsured;
        //    pd.TotalRiskValue = detailDto.SectionSumInsured;
        //    pd.GrossPremium = detailDto.SectionPremium;
        //    pd.SumInsuredFrgn = 0;
        //    pd.GrossPremiumFrgn = 0;
        //    //pd.ProRataPremium = CDbl(PolicyAmount);
        //    //pd.NetAmount = CDbl(PolicyAmount);
        //    //pd.Field49 = CDbl(PolicyAmount);


        //    pd.SubmittedBy = policy.SubmittedBy;
        //    pd.SubmittedOn = policy.SubmittedOn;
        //    pd.Deleted = policy.Deleted;
        //    pd.Active = policy.Active;

        //    return pd;
        //}

        //private DNCNNote CreateNewDebitNote(Policy policy)
        //{
        //    decimal partyRate = 0;
        //    string dncnNo = GetNextAutoNumber("DNOTE", BRANCH_ID, policy.SubRiskID);
        //    decimal? commission = (policy.GrossPremium * partyRate) / 100;

        //    return new DNCNNote()
        //    {
        //        NoteType = "DN",

        //        DNCNNo = dncnNo,
        //        refDNCNNo = dncnNo,
        //        PolicyNo = policy.PolicyNo,
        //        CoPolicyNo = policy.CoPolicyNo,
        //        BranchID = policy.BranchID,
        //        BizSource = policy.BizSource,
        //        BizOption = "NEW",
        //        BillingDate = policy.TransDate,
        //        SubRiskID = policy.SubRiskID,
        //        SubRisk = policy.SubRiskName,
        //        PartyID = policy.PartyID,
        //        Party = policy.Party,
        //        InsuredID = policy.InsuredID,
        //        InsuredName = policy.InsFullname,
        //        StartDate = policy.StartDate,
        //        EndDate = policy.EndDate,

        //        Narration = $"Being policy premium  for Policy No. {policy.PolicyNo}",
        //        ExRate = policy.ExRate,
        //        ExCurrency = policy.ExCurrency,
        //        Remarks = "NORMAL",
        //        PaymentType = "NORMAL",

        //        SumInsured = policy.SumInsured,
        //        GrossPremium = policy.GrossPremium,

        //        PartyRate = partyRate,
        //        Commission = commission,
        //        PropRate = policy.ProportionRate,
        //        ProRataDays = policy.ProRataDays,
        //        ProRataPremium = policy.ProRataPremium,
        //        NetAmount = policy.GrossPremium - commission,
        //        SumInsuredFrgn = policy.SumInsuredFrgn,
        //        GrossPremiumFrgn = policy.GrossPremiumFrgn,
        //        TotalRiskValue = policy.SumInsured,
        //        TotalPremium = policy.GrossPremium,
        //        HasTreaty = 0,
        //        Approval = 0,
        //        RetProp = 0,
        //        RetValue = 0,
        //        RetPremium = 0,
        //        DBDate = policy.TransDate, 

        //        SubmittedBy = policy.SubmittedBy,
        //        SubmittedOn = policy.SubmittedOn,
        //        Deleted = policy.Deleted,
        //        Active = policy.Active,

        //        //Z_NAICOM_UID = null,
        //        //Z_NAICOM_STATUS = "QUEUED",  // CIP -> PENDING, SENT, IGNORED, ARCHIVED
        //        // Z_NAICOM_SENT_ON
        //        // Z_NAICOM_ERROR
        //        // Z_NAICOM_JSON
        //    };
        //}

        //private DNCNNote CreateNewReceipt(Policy policy, string refDnCnNo)
        //{
        //    decimal partyRate = 0;
        //    string dncnNo = Guid.NewGuid().ToString().Split('-')[0];
        //    string receiptNo = GetNextAutoNumber("RECEIPT", BRANCH_ID, policy.SubRiskID);
        //    decimal? commission = (policy.GrossPremium * partyRate) / 100;

        //    return new DNCNNote()
        //    {
        //        NoteType = "RCP",

        //        DNCNNo = dncnNo,
        //        refDNCNNo = refDnCnNo,
        //        ReceiptNo = receiptNo,
        //        PolicyNo = policy.PolicyNo,
        //        CoPolicyNo = policy.CoPolicyNo,
        //        BranchID = policy.BranchID,
        //        BizSource = policy.BizSource,
        //        BizOption = "NEW",
        //        BillingDate = policy.TransDate,
        //        SubRiskID = policy.SubRiskID,
        //        SubRisk = policy.SubRiskName,
        //        PartyID = policy.PartyID,
        //        Party = policy.Party,
        //        InsuredID = policy.InsuredID,
        //        InsuredName = policy.InsFullname,
        //        StartDate = policy.StartDate,
        //        EndDate = policy.EndDate,

        //        Narration = $"Being reciept for Debit Note No. {refDnCnNo}",
        //        ExRate = policy.ExRate,
        //        ExCurrency = policy.ExCurrency,
        //        Remarks = "NORMAL",
        //        PaymentType = "NORMAL",

        //        SumInsured = policy.SumInsured,
        //        GrossPremium = policy.GrossPremium,

        //        PartyRate = partyRate,
        //        Commission = commission,
        //        PropRate = policy.ProportionRate,
        //        ProRataDays = policy.ProRataDays,
        //        ProRataPremium = policy.ProRataPremium,
        //        NetAmount = policy.GrossPremium - commission,
        //        SumInsuredFrgn = policy.SumInsuredFrgn,
        //        GrossPremiumFrgn = policy.GrossPremiumFrgn,
        //        TotalRiskValue = policy.SumInsured,
        //        TotalPremium = policy.GrossPremium,
        //        Approval = 0,
        //        HasTreaty = 0,
        //        RetProp = 0,
        //        RetValue = 0,
        //        RetPremium = 0,
        //        DBDate = policy.TransDate,

        //        SubmittedBy = policy.SubmittedBy,
        //        SubmittedOn = policy.SubmittedOn,
        //        Active = policy.Active,
        //        Deleted = policy.Deleted,
        //    };
        //}

        //private async static Task<bool> PaymentValidate( string merchantId ,string transactionId)
        //{
        //    await Task.Delay(50);

        //    if (string.IsNullOrEmpty( merchantId))
        //        return false;

        //    if (string.IsNullOrEmpty(transactionId))
        //        return false;

        //    if (merchantId.ToUpper() == "PAYSTACK")
        //    {
        //        if (transactionId == "DEMO")
        //            return true;
        //        else
        //            return false;
        //    }

        //    return false;
        //}
    }
