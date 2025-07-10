using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Domain;
using Microsoft.AspNetCore.Mvc;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {

        public Task<PolicyMaster> PolicyMasterSelectThisAsync([FromRoute] string policyNo)
        {
            string appId = _authContext.User.AppId;

            if (string.IsNullOrWhiteSpace(policyNo))
                throw new ArgumentNullException(nameof(policyNo), "Policy No cannot be empty");

            return _db.PolicyMaster.Where(x => x.PolicyNo == policyNo)
                              //.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{appId}")
                              .FirstOrDefaultAsync();
        }

        public async Task<PolicyMaster> PolicyMasterCreateAsync(CreateNewPolicyMasterRequest dto)
        {
            string appId = _authContext.User.AppId;

            //check for duplicate
            //var existing = await _db.PolicyMaster.Where(x => x.Email == dto.Email )
            //                                .Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{appId}")
            //                                .FirstOrDefaultAsync();
            //if (existing != null)
            //    throw new ArgumentException($"Duplicate agent found. ID={existing.PartyID} {existing.Email}, {existing.mobilePhone}");

            var policymaster = new PolicyMaster()
            {
                PolicyNo = GetNextAutoNumber("POLICY", BRANCH_ID),
                ProposalNo= dto.         ProposalNo,
                BrCode= dto.             BrCode,
                TDate= dto.              TDate,
                PropDate= dto.           PropDate,
                AssDate= dto.            AssDate,
                CoverCode= dto.          CoverCode,
                Covertype= dto.          Covertype,
                Title= dto.              Title,
                AssuredCode= dto.        AssuredCode,
                SurName= dto.            SurName,
                OtherNames= dto.         OtherNames,
                Address= dto.            Address,
                FullName= dto.           FullName,
                State= dto.              State,
                Landphone= dto.          Landphone,
                MobilePhone= dto.        MobilePhone,
                Email= dto.              Email,
                NationalID= dto.         NationalID,
                Occupation= dto.         Occupation,
                Sex= dto.                Sex,
                MaritalStatus= dto.      MaritalStatus,
                Country= dto.            Country,
                Agentcode= dto.          Agentcode,
                AgentDescription= dto.   AgentDescription,
                StartDate= dto.          StartDate,
                MaturityDate= dto.       MaturityDate,
                Duration= dto.           Duration,
                DateofBirth= dto.        DateofBirth,
                FOP= dto.                FOP,
                Age= dto.                Age,
                MOP= dto.                MOP,
                SumAssured= dto.         SumAssured,
                isProposal= 1,
                Deleted= 0,


            };

            _db.PolicyMaster.Add(policymaster);
            return policymaster;
        }

        public async Task<List<PolicyMaster>> GetByProposalNoAsync(string proposalNo)
        {
            return await _db.PolicyMaster
                .Where(p => p.ProposalNo == proposalNo)
                .OrderByDescending(p => p.SubmittedOn)
                .ToListAsync();
        }

        public async Task UpdateRangeAsync(List<PolicyMaster> proposals)
        {
            _db.PolicyMaster.UpdateRange(proposals);
            await _db.SaveChangesAsync();
        }

        public async Task<List<PolicyMaster>> GetAllProposalsAsync()
        {
            return await _db.PolicyMaster
                .Where(p => p.isProposal == 1)
                .OrderByDescending(p => p.SubmittedOn)
                .ToListAsync();
        }

        public async Task<Models.PagedResult<PolicyMaster>> GetProposalsPagedAsync(int page, int pageSize, string? proposalNo, string? assuredCode, string? surname)
        {
            var query = _db.PolicyMaster
                .Where(p => p.isProposal == 1);

            if (!string.IsNullOrEmpty(proposalNo))
                query = query.Where(p => p.ProposalNo.Contains(proposalNo));

            if (!string.IsNullOrEmpty(assuredCode))
                query = query.Where(p => p.AssuredCode.Contains(assuredCode));

            if (!string.IsNullOrEmpty(surname))
                query = query.Where(p => p.SurName.Contains(surname));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.SubmittedOn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new Models.PagedResult<PolicyMaster>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task SaveProposalAsync(ProposalRequest request, QuotationResult result)
{
    // 1. Create InsuredClient record
    var insured = new InsuredClients
    {
        InsuredID = GetNextAutoNumber("INSURED", "01") ?? "",
        SurName = request.SurName,
        OtherNames = request.OtherNames,
        Title = request.Title,
        Address = request.Address,
        FullName = request.FullName,
        State = request.State,
        Landphone = request.Landphone,
        MobilePhone = request.MobilePhone,
        Email = request.Email,
        NationalID = request.NationalID,
        Occupation = request.Occupation,
        Sex = request.Gender,
        MaritalStatus = request.MaritalStatus,
        Country = request.Country,
        DateofBirth = request.DateOfBirth,
        Age = request.DateOfBirth.HasValue
            ? DateTime.Today.Year - request.DateOfBirth.Value.Year -
                (DateTime.Today < request.DateOfBirth.Value.AddYears(DateTime.Today.Year - request.DateOfBirth.Value.Year) ? 1 : 0)
            : null,
        ClientType = "Individual",
        SubmittedBy = "system",
        SubmittedOn = DateTime.Now,
        ModifiedOn = DateTime.Now,
        Active = 1,
        Deleted = 0
    };

    _db.InsuredClients.Add(insured);
    await _db.SaveChangesAsync();

    // 2. Retrieve generated InsuredID
    var generatedAssuredCode = insured.InsuredID;

    // 3. Create PolicyMaster record
    var policy = new PolicyMaster
    {
        ProposalNo ="PR" + GetNextAutoNumber("OTHERS", "ALL"),
        BrCode = "HEAD OFFICE",
        Title = request.Title,
        AssuredCode = generatedAssuredCode, // Important!
        SurName = request.SurName,
        OtherNames = request.OtherNames,
        FullName = request.FullName,
        Address = request.Address,
        State = request.State,
        Landphone = request.Landphone,
        MobilePhone = request.MobilePhone,
        Email = request.Email,
        NationalID = request.NationalID,
        Occupation = request.Occupation,
        Sex = request.Gender,
        MaritalStatus = request.MaritalStatus,
        Country = request.Country,
        Agentcode = request.AgentCode,
        AgentDescription = request.AgentDescription,
        DateofBirth = request.DateOfBirth,
        StartDate = DateTime.Today,
        MaturityDate = DateTime.Today.AddYears(request.Duration),
        Duration = request.Duration,
        Age = insured.Age,
        CoverCode = request.ProductCode,
        SumAssured = result.SumAssured,
        Premium1 = result.AnnualPremium,
        Premium2 = result.ExtraPremium,
        Premium3 = result.Loading,
        TotalPremium = result.TotalPremium,
        BasicPremium = result.AnnualPremium,
        isProposal = 1,
        SubmittedBy = "system",
        SubmittedOn = DateTime.Now,
        ModifiedOn = DateTime.Now,
        TransGUID = Guid.NewGuid().ToString(),
        ContactName = request.ContactName,
        ContactAddress = request.ContactAddress,
        ContactPhone = request.ContactPhone,
        ContactEmail = request.ContactEmail,
        ContactJob = request.ContactJob,
        MeansID = request.MeansID,
        IDNo = request.IDNo,
        ChronicIllness = request.ChronicIllness
    };

    _db.PolicyMaster.Add(policy);
    await _db.SaveChangesAsync();
}

        public async Task<QuotationResult> CalculateTraditionalAsync(TradProposalRequest request)
        {
            var ps = await GetByCodeAsync(request.ProductCode);

            var age = CalculateAge((DateTime)request.DateOfBirth);
            var intRateEntity = await GetPremiumRateAsync(
                request.ProductCode,
                request.Duration,
                age,
                request.Frequency
            );

            if (intRateEntity == null)
                throw new Exception("Interest rate not found.");
            decimal rate = Convert.ToDecimal(intRateEntity);


            decimal mopFactor = request.Frequency?.ToUpper() switch
            {
                "MONTHLY" => 9m,
                "QUARTERLY" => 26.5m,
                "HALF-YEARLY" => 52.5m,
                _ => 1m
            };

            decimal annPrem = 0;
            decimal sumAssured = 0;
            decimal firstPremium = 0;

            if (request.FirstPremium.HasValue)
            {
                // From First Premium ➡ Sum Assured
                var annPrem1 = request.FirstPremium.Value;
                if (mopFactor != 1)
                    annPrem1 = (annPrem1 * 100m) / mopFactor;

                sumAssured = (decimal)(ps.RateDiv_factor * (annPrem1 / rate));
                annPrem = annPrem1;
                firstPremium = request.FirstPremium.Value;
            }
            else if (request.SumAssured.HasValue)
            {
                // From Sum Assured ➡ Premium
                sumAssured = request.SumAssured.Value;
                annPrem = (decimal)(sumAssured * (rate / ps.RateDiv_factor));

                if (mopFactor != 1)
                    firstPremium = annPrem * mopFactor / 100m;
                else
                    firstPremium = annPrem;
            }
            else
            {
                throw new Exception("You must provide either FirstPremium or SumAssured.");
            }

        //    var quoteNo = !string.IsNullOrEmpty(request.QuoteNo)
        //? request.QuoteNo
        //: GetNextAutoNumber("OTHERS", "ALL");

            var result = new QuotationResult
            {
                //QuoteNo = quoteNo,
                AnnualPremium = annPrem,
                SumAssured = sumAssured,
                LifeCover = sumAssured,
                TotalPremium = firstPremium
            };

            await SaveTradProposalAsync(request, result);

            return result;
        }
        public async Task SaveTradProposalAsync(TradProposalRequest request, QuotationResult result)
        {
            // 1. Create InsuredClient record
            var insured = new InsuredClients
            {
                InsuredID = GetNextAutoNumber("INSURED", "01") ?? "",
                SurName = request.SurName,
                OtherNames = request.OtherNames,
                Title = request.Title,
                Address = request.Address,
                FullName = request.FullName,
                State = request.State,
                Landphone = request.Landphone,
                MobilePhone = request.MobilePhone,
                Email = request.Email,
                Occupation = request.Occupation,
                Sex = request.Gender,
                MaritalStatus = request.MaritalStatus,
                Country = request.Country,
                DateofBirth = request.DateOfBirth,
                Age = request.DateOfBirth.HasValue
                    ? DateTime.Today.Year - request.DateOfBirth.Value.Year -
                        (DateTime.Today < request.DateOfBirth.Value.AddYears(DateTime.Today.Year - request.DateOfBirth.Value.Year) ? 1 : 0)
                    : null,
                ClientType = "Individual",
                SubmittedBy = "system",
                SubmittedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                Active = 1,
                Deleted = 0
            };

            _db.InsuredClients.Add(insured);
            await _db.SaveChangesAsync();

            // 2. Retrieve generated InsuredID
            var generatedAssuredCode = insured.InsuredID;

            // 3. Create PolicyMaster record
            var policy = new PolicyMaster
            {
                ProposalNo = "PR" + GetNextAutoNumber("OTHERS", "ALL"),
                BrCode = "HEAD OFFICE",
                Title = request.Title,
                AssuredCode = generatedAssuredCode, // Important!
                SurName = request.SurName,
                OtherNames = request.OtherNames,
                FullName = request.FullName,
                Address = request.Address,
                State = request.State,
                Landphone = request.Landphone,
                MobilePhone = request.MobilePhone,
                Email = request.Email,
                Occupation = request.Occupation,
                Sex = request.Gender,
                MaritalStatus = request.MaritalStatus,
                Country = request.Country,
                Agentcode = request.AgentCode,
                AgentDescription = request.AgentDescription,
                DateofBirth = request.DateOfBirth,
                StartDate = DateTime.Today,
                MaturityDate = DateTime.Today.AddYears(request.Duration),
                Duration = request.Duration,
                Age = insured.Age,
                CoverCode = request.ProductCode,
                SumAssured = result.SumAssured,
                Premium1 = result.AnnualPremium,
                Premium2 = result.ExtraPremium,
                Premium3 = result.Loading,
                TotalPremium = result.TotalPremium,
                BasicPremium = result.AnnualPremium,
                isProposal = 1,
                SubmittedBy = "system",
                SubmittedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                TransGUID = Guid.NewGuid().ToString(),
                ContactName = request.ContactName,
                ContactAddress = request.ContactAddress,
                ContactPhone = request.ContactPhone,
                ContactEmail = request.ContactEmail,
                ContactJob = request.ContactJob,
                MeansID = request.MeansID,
                IDNo = request.IDNo,
                ChronicIllness = request.ChronicIllness
            };

            _db.PolicyMaster.Add(policy);
            await _db.SaveChangesAsync();
        }
        private static int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            return age;
        }

        public async Task<QuotationResult> EditTradProposalAsync(string proposalNo, TradProposalRequest request)
        {
            if (string.IsNullOrEmpty(proposalNo))
                throw new ArgumentNullException(nameof(proposalNo));

            // 1. Load PolicyMaster
            var policy = await _db.PolicyMaster.FirstOrDefaultAsync(x => x.ProposalNo == proposalNo);
            if (policy == null)
                throw new Exception($"Proposal with ProposalNo '{proposalNo}' not found.");

            // 2. Load InsuredClients
            var insured = await _db.InsuredClients.FirstOrDefaultAsync(x => x.InsuredID == policy.AssuredCode);
            if (insured == null)
                throw new Exception($"Insured client with ID '{policy.AssuredCode}' not found.");

            // 3. Recalculate
            var result = await CalculateTraditionalAsync(request);

            // 4. Update InsuredClient
            insured.SurName = request.SurName;
            insured.OtherNames = request.OtherNames;
            insured.Title = request.Title;
            insured.Address = request.Address;
            insured.FullName = request.FullName;
            insured.State = request.State;
            insured.Landphone = request.Landphone;
            insured.MobilePhone = request.MobilePhone;
            insured.Email = request.Email;
            insured.Occupation = request.Occupation;
            insured.Sex = request.Gender;
            insured.MaritalStatus = request.MaritalStatus;
            insured.Country = request.Country;
            insured.DateofBirth = request.DateOfBirth;
            insured.Age = request.DateOfBirth.HasValue
                ? DateTime.Today.Year - request.DateOfBirth.Value.Year - (
                    DateTime.Today < request.DateOfBirth.Value.AddYears(DateTime.Today.Year - request.DateOfBirth.Value.Year) ? 1 : 0
                )
                : null;
            insured.ModifiedOn = DateTime.Now;

            // 5. Update PolicyMaster
            policy.Title = request.Title;
            policy.SurName = request.SurName;
            policy.OtherNames = request.OtherNames;
            policy.FullName = request.FullName;
            policy.Address = request.Address;
            policy.State = request.State;
            policy.Landphone = request.Landphone;
            policy.MobilePhone = request.MobilePhone;
            policy.Email = request.Email;
            policy.Occupation = request.Occupation;
            policy.Sex = request.Gender;
            policy.MaritalStatus = request.MaritalStatus;
            policy.Country = request.Country;
            policy.Agentcode = request.AgentCode;
            policy.AgentDescription = request.AgentDescription;
            policy.DateofBirth = request.DateOfBirth;
            policy.StartDate = DateTime.Today;
            policy.MaturityDate = DateTime.Today.AddYears(request.Duration);
            policy.Duration = request.Duration;
            policy.Age = insured.Age;
            policy.CoverCode = request.ProductCode;
            policy.SumAssured = result.SumAssured;
            policy.Premium1 = result.AnnualPremium;
            policy.Premium2 = result.ExtraPremium;
            policy.Premium3 = result.Loading;
            policy.TotalPremium = result.TotalPremium;
            policy.BasicPremium = result.AnnualPremium;
            policy.ModifiedOn = DateTime.Now;
            policy.ContactName = request.ContactName;
            policy.ContactAddress = request.ContactAddress;
            policy.ContactPhone = request.ContactPhone;
            policy.ContactEmail = request.ContactEmail;
            policy.ContactJob = request.ContactJob;
            policy.MeansID = request.MeansID;
            policy.IDNo = request.IDNo;
            policy.ChronicIllness = request.ChronicIllness;

            await _db.SaveChangesAsync();

            return result;
        }


    }
}
