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
                AgentCode= dto.          Agentcode,
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
    }
}
