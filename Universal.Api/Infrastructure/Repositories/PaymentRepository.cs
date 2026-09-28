using DocumentFormat.OpenXml.InkML;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {
        public async Task<List<GetAgentPaymentDto>> GetAgentPayment(string partyCode)
        {

            var normalizedCode = partyCode.Trim();

            var payments = await _db.undDNCN
                .Where(p => p.PartyCode != null &&
                            EF.Functions.Like(p.PartyCode.Trim(), normalizedCode))
                .OrderByDescending(p => p.SubmitOn)
                .Select(p => new GetAgentPaymentDto
                {
                    DNCNID = p.DNCNID,
                    DNCNNo = p.DNCNNo,
                    PartyCode = p.PartyCode,
                    PartyName = p.PartyName,
                    GrossPremium = p.GrossPremium,
                    Comission = p.Comission,
                    NetAmount = p.NetAmount,
                    AmtPaid = p.AmtPaid,
                    SubmitOn = p.SubmitOn
                })
                .ToListAsync();

            return payments;
        }


        public async Task<List<GetAgentPaymentDto>> GetByInsuredCode(string insuredCode)
        {
            // Step 1: Find the policy record for this insured code, to get its linked email
            var email = await _db.PolicyMaster
                .Where(p => p.AssuredCode == insuredCode)
                .Select(p => p.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(email))
            {
                return new List<GetAgentPaymentDto>();
            }

            // Step 2: Get all policy numbers linked to that email (covers multiple policies per person)
            var policyNumbers = await _db.PolicyMaster
                .Where(p => p.Email == email)
                .Select(p => p.PolicyNo)
                .ToListAsync();

            if (!policyNumbers.Any())
            {
                return new List<GetAgentPaymentDto>();
            }

            // Step 3: Pull all undDNCN records tied to any of those policy numbers
            return await _db.undDNCN
                .Where(p => policyNumbers.Contains(p.PolicyNo))
                .OrderByDescending(p => p.SubmitOn)
                .Select(p => new GetAgentPaymentDto
                {
                    DNCNID = p.DNCNID,
                    DNCNNo = p.DNCNNo,
                    PartyCode = p.PartyCode,
                    PartyName = p.PartyName,
                    InsuredCode = p.InsuredCode,
                    InsuredName = p.InsuredName,
                    GrossPremium = p.GrossPremium,
                    Comission = p.Comission,
                    NetAmount = p.NetAmount,
                    AmtPaid = p.AmtPaid,
                    SubmitOn = p.SubmitOn
                })
                .ToListAsync();
        }
    }
}
