using DocumentFormat.OpenXml.Bibliography;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Models.Customer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {
        // ============================================================
        //  SHARED HELPERS (used by the policy flow)
        // ============================================================

        public Task<PolicyMaster> PolicyMasterSelectThisAsync([FromRoute] string policyNo)
        {
            string appId = _authContext.User.AppId;

            if (string.IsNullOrWhiteSpace(policyNo))
                throw new ArgumentNullException(nameof(policyNo), "Policy No cannot be empty");

            return _db.PolicyMaster.Where(x => x.PolicyNo == policyNo)
                              //.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{appId}")
                              .FirstOrDefaultAsync();
        }


        public async Task<PagedResultDto<PolicyIndListItemDto>> PolicyIndListAsync(PolicyIndQueryDto query, CancellationToken ct = default)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > 500 ? 50 : query.PageSize;

            var q = _db.PolicyMaster.AsNoTracking()
                .Where(p => p.Deleted == null || p.Deleted == 0);

            if (!string.IsNullOrWhiteSpace(query.CoverCode))
                q = q.Where(p => p.CoverCode == query.CoverCode);

            if (!string.IsNullOrWhiteSpace(query.BrCode))
                q = q.Where(p => p.BrCode == query.BrCode);

            if (!string.IsNullOrWhiteSpace(query.AssuredCode))
                q = q.Where(p => p.AssuredCode == query.AssuredCode);

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(p => p.TransSTATUS == query.Status);

            if (query.IsProposal.HasValue)
            {
                // isProposal is 1 for proposals, 0 for policies (legacy convention).
                byte flag = (byte)(query.IsProposal.Value ? 0 : 1);
                q = q.Where(p => p.isProposal == flag);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                q = q.Where(p => (p.PolicyNo != null && p.PolicyNo.Contains(term))
                                 || (p.FullName != null && p.FullName.Contains(term)));
            }

            if (query.FromDate.HasValue)
                q = q.Where(p => p.StartDate >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                q = q.Where(p => p.StartDate <= query.ToDate.Value);

            var total = await q.CountAsync(ct);

            var items = await q
                .OrderByDescending(p => p.TDate)
                .ThenByDescending(p => p.PolicyNo)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PolicyIndListItemDto
                {
                    PolicyNo = p.PolicyNo!,
                    IsProposal = p.isProposal == 0,
                    CoverCode = p.CoverCode,
                    Covertype = p.Covertype,
                    BrCode = p.BrCode,
                    AssuredCode = p.AssuredCode,
                    InsuredName = p.FullName,
                    TDate = p.TDate,
                    StartDate = p.StartDate,
                    MaturityDate = p.MaturityDate,
                    SumAssured = p.SumAssured,
                    BasicPremium = p.BasicPremium,
                    Status = p.Remarks
                })
                .ToListAsync(ct);

            return new PagedResultDto<PolicyIndListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
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
                ProposalNo = dto.ProposalNo,
                BrCode = dto.BrCode,
                TDate = dto.TDate,
                PropDate = dto.PropDate,
                AssDate = dto.AssDate,
                CoverCode = dto.CoverCode,
                Covertype = dto.Covertype,
                Title = dto.Title,
                AssuredCode = dto.AssuredCode,
                SurName = dto.SurName,
                OtherNames = dto.OtherNames,
                Address = dto.Address,
                FullName = dto.FullName,
                State = dto.State,
                Landphone = dto.Landphone,
                MobilePhone = dto.MobilePhone,
                Email = dto.Email,
                NationalID = dto.NationalID,
                Occupation = dto.Occupation,
                Sex = dto.Sex,
                MaritalStatus = dto.MaritalStatus,
                Country = dto.Country,
                Agentcode = dto.Agentcode,
                AgentDescription = dto.AgentDescription,
                StartDate = dto.StartDate,
                MaturityDate = dto.MaturityDate,
                Duration = dto.Duration,
                DateofBirth = dto.DateofBirth,
                FOP = dto.FOP,
                Age = dto.Age,
                MOP = dto.MOP,
                SumAssured = dto.SumAssured,
                isProposal = 1,
                Deleted = 0,


            };

            _db.PolicyMaster.Add(policymaster);
            return policymaster;
        }

        public async Task UpdateRangeAsync(List<PolicyMaster> policies)
        {
            _db.PolicyMaster.UpdateRange(policies);
            await _db.SaveChangesAsync();
        }

        public async Task<InsuredClients> GetInsuredClientAsync(string insuredId)
        {
            return await _db.InsuredClients
                .FirstOrDefaultAsync(i => i.InsuredID == insuredId);
        }
        public async Task<double> GetPremiumRateAsync(string productCode, long duration, long age, string rateCode, string remark)
        {
            var rate = await _db.InterestRate
                .Where(r =>
                    r.CoverCode == productCode &&
                    r.Duration == duration &&
                    r.Premtype == rateCode &&
                    r.Age == age &&
                    r.Remark == remark
                )
                .FirstOrDefaultAsync();

            if (rate == null)
                throw new InvalidOperationException("Interest Rate not found for the specified criteria.");

            return rate.IntRate;
        }
        // Traditional premium engine — shared by the traditional policy create/edit.
        public async Task<QuotationResult> CalculateTraditionalAsync(TradProposalRequest request)
        {
            var ps = await GetByCodeAsync(request.ProductCode);

            var age = CalculateAge((DateTime)request.DateOfBirth);
            var intRateEntity = await GetPremiumRateAsync(
                request.ProductCode,
                request.Duration,
                age,
                request.Frequency,
                request.Remark
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
                var annPrem1 = request.FirstPremium.Value;
                //if (mopFactor != 1)
                //    annPrem1 = (annPrem1 * 100m) / mopFactor;

                sumAssured = (decimal)(1000 * (annPrem1 / rate));
                annPrem = annPrem1;
                firstPremium = request.FirstPremium.Value;
            }
            else if (request.SumAssured.HasValue)
            {
                // From Sum Assured -> Premium
                sumAssured = request.SumAssured.Value;
                annPrem = (decimal)((sumAssured * rate) / 1000);
                firstPremium = annPrem;
            }
            else
            {
                throw new Exception("You must provide either FirstPremium or SumAssured.");
            }

            var result = new QuotationResult
            {
                AnnualPremium = annPrem,
                SumAssured = sumAssured,
                LifeCover = sumAssured,
                TotalPremium = firstPremium
            };


            return result;
        }

        private static int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            return age;
        }

        // ============================================================
        //  POLICY (in-force) METHODS
        //
        //    - Number:      GetNextAutoNumber("POLICY", BRANCH_ID)
        //    - isProposal:  0
        //    - Status:      "IN FORCE"
        //    - Remarks:     "IN FORCE"
        //    - Read filter: Remarks == "IN FORCE"
        // ============================================================

        /// <summary>
        /// Get a single in-force policy by policy number.
        /// </summary>
        public async Task<PolicyMaster> GetPolicyByNumberAsync(string policyNo)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                throw new ArgumentNullException(nameof(policyNo));

            return await _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.PolicyNo == policyNo)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get all regular in-force policies for a specific agent (untyped).
        /// </summary>
        public async Task<List<PolicyMaster>> GetPoliciesByAgentCodeAsync(string agentCode)
        {
            if (string.IsNullOrWhiteSpace(agentCode))
                throw new ArgumentNullException(nameof(agentCode));

            return await _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.Agentcode == agentCode)
                .OrderByDescending(p => p.SubmittedOn)
                .ToListAsync();
        }
        public async Task<PolicyMaster> GetProposalForReportAsync(string proposalNo)
        {
            var proposal = await _db.PolicyMaster
                .FirstOrDefaultAsync(p => p.ProposalNo == proposalNo && p.isProposal == 1);

            if (proposal == null)
                throw new Exception($"Proposal {proposalNo} not found");

            return proposal;
        }
        /// <summary>
        /// Get all in-force policies for a specific insured client (untyped).
        /// </summary>
        public async Task<List<PolicyMaster>> GetPoliciesByInsuredIdAsync(string insuredId)
        {
            if (string.IsNullOrWhiteSpace(insuredId))
                throw new ArgumentNullException(nameof(insuredId));

            return await _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.AssuredCode == insuredId)
                .OrderByDescending(p => p.SubmittedOn)
                .ToListAsync();
        }

        /// <summary>
        /// Search in-force policies with multiple filters.
        /// </summary>
        public async Task<Models.PagedResult<PolicyMaster>> SearchPoliciesAsync(
            string agentCode = null,
            string insuredId = null,
            string policyNo = null,
            string surname = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.PolicyMaster.Where(p => p.Remarks == "IN FORCE");

            if (!string.IsNullOrWhiteSpace(agentCode))
                query = query.Where(p => p.Agentcode == agentCode);

            if (!string.IsNullOrWhiteSpace(insuredId))
                query = query.Where(p => p.AssuredCode == insuredId);

            if (!string.IsNullOrWhiteSpace(policyNo))
                query = query.Where(p => p.PolicyNo.Contains(policyNo));

            if (!string.IsNullOrWhiteSpace(surname))
                query = query.Where(p => p.SurName.Contains(surname));

            if (fromDate.HasValue)
                query = query.Where(p => p.SubmittedOn >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.SubmittedOn <= toDate.Value);

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

        // ---------- CREATE: REGULAR POLICY (Tag 'B') ----------
        public async Task<string> SavePolicyAsync(ProposalRequest request, QuotationResult result)
        {
            string generatedAssuredCode;
            string submittedBy = _authContext.User switch
            {
                AgentUser agent => agent.AgentId,
                CustomerUser customer => customer.InsuredId,
                AppUser app => app.AppId,
                _ => "system"
            };

            if (string.IsNullOrWhiteSpace(request.AssuredCode))
            {
                var existingInsured = !string.IsNullOrWhiteSpace(request.Email)
                    ? await _db.InsuredClients.FirstOrDefaultAsync(x => x.Email == request.Email)
                    : null;

                if (existingInsured != null)
                {
                    generatedAssuredCode = existingInsured.InsuredID;
                }
                else
                {
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
                        SubmittedBy = submittedBy,
                        SubmittedOn = DateTime.Now,
                        ModifiedOn = DateTime.Now,
                        Active = 1,
                        Deleted = 0
                    };

                    _db.InsuredClients.Add(insured);
                    await _db.SaveChangesAsync();

                    generatedAssuredCode = insured.InsuredID;
                }
            }
            else
            {
                generatedAssuredCode = request.AssuredCode;
            }

            // POLICY number generation
            var policyNo = GetNextAutoNumber("POLICY", BRANCH_ID, request.ProductCode);
            var rs = await ProductSelectThisAsync(request.ProductCode);

            var policy = new PolicyMaster
            {
                ProposalNo = policyNo,   // no originating proposal for a direct issue; mirror PolicyNo
                PolicyNo = policyNo,
                BrCode = "HEAD OFFICE",
                Title = request.Title,
                AssuredCode = generatedAssuredCode,
                SurName = request.SurName,
                OtherNames = request.OtherNames,
                FullName = request.FullName,
                Address = request.Address,
                TDate = DateTime.Now,
                AssDate = DateTime.Now,
                PropDate = DateTime.Now,
                FOP = request.Frequency,
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
                Age = request.DateOfBirth.HasValue
                    ? DateTime.Today.Year - request.DateOfBirth.Value.Year -
                        (DateTime.Today < request.DateOfBirth.Value.AddYears(DateTime.Today.Year - request.DateOfBirth.Value.Year) ? 1 : 0)
                    : null,
                CoverCode = request.ProductCode,
                Covertype = rs.SubRiskName,
                SumAssured = result.SumAssured,
                Premium1 = request.RegularSaving,
                Premium2 = result.ExtraPremium,
                Premium3 = result.Loading,
                Loading = request.LoadingRate,
                XMas = request.ExtraRate,
                TotalPremium = result.TotalPremium,
                BasicPremium = request.RegularSaving,
                isProposal = 0,
                Deleted = 0,
                Status = "IN FORCE",
                Tag = "B",
                SubmittedBy = submittedBy,
                SubmittedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                MOP = request.Mop,
                TransGUID = Guid.NewGuid().ToString(),
                PaymentType = request.PaymentType,
                PaymentAmount = request.PaymentAmount,
                Dateofpayment = request.Dateofpayment,
                ReceiptNo = request.ReceiptNo,
                BankName = request.BankName,
                AccountNo = request.AccountNo,
                MeansID = request.MeansID,
                IDNo = request.IDNo,
                ChronicIllness = request.ChronicIllness,
                Remarks = "IN FORCE",
                PlaceOfBirth = request.PlaceOfBirth,
                SpouseName = request.SpouseName,
                GovtOfOrigin = request.GovtOfOrigin,
                ResAddress = request.ResAddress,
                LandMark = request.LandMark,
                NIN = request.NIN,
                BVN = request.BVN,
                EmploymentType = request.EmploymentType,
                EmployerName = request.EmployerName,
                EmployerAddress = request.EmployerAddress,
                EmployerNumber = request.EmployerNumber,
                EmployerEmail = request.EmployerEmail,
                DateEmployed = request.DateEmployed,
                Height = request.Height,
                weight = request.weight,
                Sport = request.Sport,
                Infirmity = request.Infirmity,
                stroke = request.stroke,
                Treatment = request.Treatment,
                WorkAbesent = request.WorkAbesent,
                Smoke = request.Smoke,
                Aviation = request.Aviation,
                Relgion = request.Religion,
                Designation = request.Designation,
                IssueDate = request.IssueDate,
                ExpiryDate = request.ExpiryDate
            };

            _db.PolicyMaster.Add(policy);
            await _db.SaveChangesAsync();

            return policyNo;
        }

        // ---------- CREATE: TRADITIONAL POLICY (Tag 'A') ----------
        public async Task<string> SaveTradPolicyAsync(TradProposalRequest request, QuotationResult result)
        {
            string generatedAssuredCode;
            string submittedBy = _authContext.User switch
            {
                AgentUser agent => agent.AgentId,
                CustomerUser customer => customer.InsuredId,
                AppUser app => app.AppId,
                _ => "system"
            };

            if (string.IsNullOrWhiteSpace(request.AssuredCode))
            {
                var existingInsured = !string.IsNullOrWhiteSpace(request.Email)
                    ? await _db.InsuredClients.FirstOrDefaultAsync(x => x.Email == request.Email)
                    : null;

                if (existingInsured != null)
                {
                    generatedAssuredCode = existingInsured.InsuredID;
                }
                else
                {
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
                        SubmittedBy = submittedBy,
                        SubmittedOn = DateTime.Now,
                        ModifiedOn = DateTime.Now,
                        Active = 1,
                        Deleted = 0
                    };

                    _db.InsuredClients.Add(insured);
                    await _db.SaveChangesAsync();

                    generatedAssuredCode = insured.InsuredID;
                }
            }
            else
            {
                generatedAssuredCode = request.AssuredCode;
            }

            var policyNo = GetNextAutoNumber("POLICY", BRANCH_ID, request.ProductCode);
            var policy = new PolicyMaster
            {
                ProposalNo = policyNo,
                PolicyNo = policyNo,
                BrCode = "HEAD OFFICE",
                Title = request.Title,
                AssuredCode = generatedAssuredCode,
                SurName = request.SurName,
                OtherNames = request.OtherNames,
                FullName = request.FullName,
                Address = request.Address,
                State = request.State,
                TDate = DateTime.Now,
                AssDate = DateTime.Now,
                PropDate = DateTime.Now,
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
                CustomerType = request.RiskID,
                MaturityDate = DateTime.Today.AddYears(request.Duration),
                Duration = request.Duration,
                Age = request.DateOfBirth.HasValue
                    ? DateTime.Today.Year - request.DateOfBirth.Value.Year -
                        (DateTime.Today < request.DateOfBirth.Value.AddYears(DateTime.Today.Year - request.DateOfBirth.Value.Year) ? 1 : 0)
                    : null,
                CoverCode = request.ProductCode,
                FOP = request.Frequency,
                MOP = request.Mop,
                SumAssured = result.SumAssured,
                Premium1 = result.AnnualPremium,
                Premium2 = result.ExtraPremium,
                Premium3 = result.Loading,
                TotalPremium = result.TotalPremium,
                BasicPremium = result.AnnualPremium,
                isProposal = 0,
                Deleted = 0,
                Tag = "A",
                Remarks = "IN FORCE",
                CardNo = request.Remark,
                Status = "IN FORCE",
                SubmittedBy = submittedBy,
                SubmittedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                TransGUID = Guid.NewGuid().ToString(),
                PaymentType = request.PaymentType,
                PaymentAmount = request.PaymentAmount,
                Dateofpayment = request.Dateofpayment,
                ReceiptNo = request.ReceiptNo,
                BankName = request.BankName,
                AccountNo = request.AccountNo,
                MeansID = request.MeansID,
                IDNo = request.IDNo,
                ChronicIllness = request.ChronicIllness,
                PlaceOfBirth = request.PlaceOfBirth,
                SpouseName = request.SpouseName,
                GovtOfOrigin = request.GovtOfOrigin,
                ResAddress = request.ResAddress,
                LandMark = request.LandMark,
                NIN = request.NIN,
                BVN = request.BVN,
                EmploymentType = request.EmploymentType,
                EmployerName = request.EmployerName,
                EmployerAddress = request.EmployerAddress,
                EmployerNumber = request.EmployerNumber,
                EmployerEmail = request.EmployerEmail,
                DateEmployed = request.DateEmployed,
                Height = request.Height,
                weight = request.weight,
                Sport = request.Sport,
                Infirmity = request.Infirmity,
                stroke = request.stroke,
                Treatment = request.Treatment,
                WorkAbesent = request.WorkAbesent,
                Smoke = request.Smoke,
                Aviation = request.Aviation,
                Relgion = request.Religion,
                Designation = request.Designation,
                IssueDate = request.IssueDate,
                ExpiryDate = request.ExpiryDate
            };

            _db.PolicyMaster.Add(policy);
            await _db.SaveChangesAsync();

            return policyNo;
        }

        // ---------- EDIT: REGULAR POLICY (Tag 'B') ----------
        public async Task<QuotationResult> EditPolicyAsync(string policyNo, ProposalRequest request)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                throw new ArgumentNullException(nameof(policyNo));

            var policy = await _db.PolicyMaster
                .FirstOrDefaultAsync(x => x.PolicyNo == policyNo && x.isProposal == 0);
            if (policy == null)
                throw new KeyNotFoundException($"Policy '{policyNo}' not found.");

            var calculation = await CalculateAsync(new QuotationRequest
            {
                SurName = request.SurName,
                OtherNames = request.OtherNames,
                DateOfBirth = request.DateOfBirth,
                Duration = request.Duration,
                Frequency = request.Frequency,
                RegularSaving = request.RegularSaving,
                ProductCode = request.ProductCode,
                ExtraRate = request.ExtraRate,
                LoadingRate = request.LoadingRate
            });

            var rs = await ProductSelectThisAsync(request.ProductCode);

            int? age = request.DateOfBirth.HasValue
                ? DateTime.Today.Year - request.DateOfBirth.Value.Year -
                  (DateTime.Today < request.DateOfBirth.Value.AddYears(DateTime.Today.Year - request.DateOfBirth.Value.Year) ? 1 : 0)
                : (int?)null;

            string modifiedBy = _authContext.User switch
            {
                AgentUser agent => agent.AgentId,
                CustomerUser customer => customer.InsuredId,
                AppUser app => app.AppId,
                _ => "system"
            };

            var insured = await _db.InsuredClients
                .FirstOrDefaultAsync(x => x.InsuredID == policy.AssuredCode);
            if (insured != null)
            {
                insured.SurName = request.SurName;
                insured.OtherNames = request.OtherNames;
                insured.Title = request.Title;
                insured.Address = request.Address;
                insured.FullName = request.FullName;
                insured.State = request.State;
                insured.Landphone = request.Landphone;
                insured.MobilePhone = request.MobilePhone;
                insured.Email = request.Email;
                insured.NationalID = request.NationalID;
                insured.Occupation = request.Occupation;
                insured.Sex = request.Gender;
                insured.MaritalStatus = request.MaritalStatus;
                insured.Country = request.Country;
                insured.DateofBirth = request.DateOfBirth;
                insured.Age = age;
                insured.ModifiedOn = DateTime.Now;
            }

            policy.Title = request.Title;
            policy.SurName = request.SurName;
            policy.OtherNames = request.OtherNames;
            policy.FullName = request.FullName;
            policy.Address = request.Address;
            policy.State = request.State;
            policy.Landphone = request.Landphone;
            policy.MobilePhone = request.MobilePhone;
            policy.Email = request.Email;
            policy.NationalID = request.NationalID;
            policy.Occupation = request.Occupation;
            policy.Sex = request.Gender;
            policy.MaritalStatus = request.MaritalStatus;
            policy.Country = request.Country;
            policy.Agentcode = request.AgentCode;
            policy.AgentDescription = request.AgentDescription;
            policy.DateofBirth = request.DateOfBirth;
            policy.Age = age;
            policy.Duration = request.Duration;
            policy.MaturityDate = (policy.StartDate ?? DateTime.Today).AddYears(request.Duration);
            policy.FOP = request.Frequency;
            policy.MOP = request.Mop;
            policy.CoverCode = request.ProductCode;
            policy.Covertype = rs?.SubRiskName;

            policy.SumAssured = calculation.SumAssured;
            policy.Premium1 = request.RegularSaving;
            policy.Premium2 = calculation.ExtraPremium;
            policy.Premium3 = calculation.Loading;
            policy.Loading = request.LoadingRate;
            policy.XMas = request.ExtraRate;
            policy.TotalPremium = calculation.TotalPremium;
            policy.BasicPremium = request.RegularSaving;

            policy.PaymentType = request.PaymentType;
            policy.PaymentAmount = request.PaymentAmount;
            policy.Dateofpayment = request.Dateofpayment;
            policy.ReceiptNo = request.ReceiptNo;
            policy.BankName = request.BankName;
            policy.AccountNo = request.AccountNo;
            policy.MeansID = request.MeansID;
            policy.IDNo = request.IDNo;
            policy.ChronicIllness = request.ChronicIllness;

            policy.PlaceOfBirth = request.PlaceOfBirth;
            policy.SpouseName = request.SpouseName;
            policy.GovtOfOrigin = request.GovtOfOrigin;
            policy.ResAddress = request.ResAddress;
            policy.LandMark = request.LandMark;
            policy.NIN = request.NIN;
            policy.BVN = request.BVN;
            policy.EmploymentType = request.EmploymentType;
            policy.EmployerName = request.EmployerName;
            policy.EmployerAddress = request.EmployerAddress;
            policy.EmployerNumber = request.EmployerNumber;
            policy.EmployerEmail = request.EmployerEmail;
            policy.DateEmployed = request.DateEmployed;
            policy.Height = request.Height;
            policy.weight = request.weight;
            policy.Sport = request.Sport;
            policy.Infirmity = request.Infirmity;
            policy.stroke = request.stroke;
            policy.Treatment = request.Treatment;
            policy.WorkAbesent = request.WorkAbesent;
            policy.Smoke = request.Smoke;
            policy.Aviation = request.Aviation;
            policy.Relgion = request.Religion;
            policy.Designation = request.Designation;
            policy.IssueDate = request.IssueDate;
            policy.ExpiryDate = request.ExpiryDate;

            policy.ModifiedBy = modifiedBy;
            policy.ModifiedOn = DateTime.Now;

            await _db.SaveChangesAsync();
            return calculation;
        }

        // ---------- EDIT: TRADITIONAL POLICY (Tag 'A') ----------
        public async Task<QuotationResult> EditTradPolicyAsync(string policyNo, TradProposalRequest request)
        {
            if (string.IsNullOrEmpty(policyNo))
                throw new ArgumentNullException(nameof(policyNo));

            var policy = await _db.PolicyMaster
                .FirstOrDefaultAsync(x => x.PolicyNo == policyNo && x.isProposal == 0);
            if (policy == null)
                throw new Exception($"Policy with PolicyNo '{policyNo}' not found.");

            var insured = await _db.InsuredClients.FirstOrDefaultAsync(x => x.InsuredID == policy.AssuredCode);
            if (insured == null)
                throw new Exception($"Insured client with ID '{policy.AssuredCode}' not found.");

            var result = await CalculateTraditionalAsync(request);

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
            policy.PaymentType = request.PaymentType;
            policy.PaymentAmount = request.PaymentAmount;
            policy.Dateofpayment = request.Dateofpayment;
            policy.ReceiptNo = request.ReceiptNo;
            policy.BankName = request.BankName;
            policy.AccountNo = request.AccountNo;
            policy.MeansID = request.MeansID;
            policy.IDNo = request.IDNo;
            policy.ChronicIllness = request.ChronicIllness;
            policy.CardNo = request.Remark;
            policy.PlaceOfBirth = request.PlaceOfBirth;
            policy.SpouseName = request.SpouseName;
            policy.GovtOfOrigin = request.GovtOfOrigin;
            policy.ResAddress = request.ResAddress;
            policy.LandMark = request.LandMark;
            policy.NIN = request.NIN;
            policy.BVN = request.BVN;
            policy.EmploymentType = request.EmploymentType;
            policy.EmployerName = request.EmployerName;
            policy.EmployerAddress = request.EmployerAddress;
            policy.EmployerNumber = request.EmployerNumber;
            policy.EmployerEmail = request.EmployerEmail;
            policy.DateEmployed = request.DateEmployed;
            policy.Height = request.Height;
            policy.weight = request.weight;
            policy.Sport = request.Sport;
            policy.Infirmity = request.Infirmity;
            policy.stroke = request.stroke;
            policy.Treatment = request.Treatment;
            policy.WorkAbesent = request.WorkAbesent;
            policy.Smoke = request.Smoke;
            policy.Aviation = request.Aviation;
            policy.Relgion = request.Religion;
            policy.Designation = request.Designation;
            policy.IssueDate = request.IssueDate;
            policy.ExpiryDate = request.ExpiryDate;
            policy.FOP = request.Frequency;
            policy.MOP = request.Mop;

            await _db.SaveChangesAsync();

            return result;
        }

        // ---------- REPORT LOOKUP ----------
        public async Task<PolicyMaster> GetPolicyForReportAsync(string policyNo)
        {
            var policy = await _db.PolicyMaster
                .FirstOrDefaultAsync(p => p.PolicyNo == policyNo && p.isProposal == 0);

            if (policy == null)
                throw new Exception($"Policy {policyNo} not found");

            return policy;
        }

        // ---------- LIST: ALL POLICIES (paged) ----------
        public async Task<Models.PagedResult<PolicyMaster>> GetAllPoliciesPagedAsync(
            int page, int pageSize, string? policyNo, string? assuredCode, string? surname)
        {
            var query = _db.PolicyMaster.Where(p => p.Remarks == "IN FORCE");

            if (!string.IsNullOrEmpty(policyNo))
                query = query.Where(p => p.PolicyNo.Contains(policyNo));

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

        // ---------- BY AGENT (regular, paged) ----------
        public async Task<Models.PagedResult<PolicyMaster>> GetPoliciesByAgentCodePagedAsync(
            string agentCode, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(agentCode))
                throw new ArgumentNullException(nameof(agentCode));

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.Agentcode == agentCode);

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

        // ---------- BY AGENT (traditional, Tag 'A') ----------
        public async Task<List<PolicyMaster>> GetTradPoliciesByAgentCodeAsync(string agentCode)
        {
            if (string.IsNullOrWhiteSpace(agentCode))
                throw new ArgumentNullException(nameof(agentCode));

            return await _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.Agentcode == agentCode && p.Tag == "A")
                .OrderByDescending(p => p.SubmittedOn)
                .ToListAsync();
        }

        public async Task<Models.PagedResult<PolicyMaster>> GetTradPoliciesByAgentCodePagedAsync(
            string agentCode, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(agentCode))
                throw new ArgumentNullException(nameof(agentCode));

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.Agentcode == agentCode && p.Tag == "A");

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

        // ---------- BY INSURED (regular, paged) ----------
        public async Task<Models.PagedResult<PolicyMaster>> GetPoliciesByInsuredIdPagedAsync(
            string insuredId, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(insuredId))
                throw new ArgumentNullException(nameof(insuredId));

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.AssuredCode == insuredId);

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

        // ---------- BY INSURED (traditional, Tag 'A') ----------
        public async Task<List<PolicyMaster>> GetTradPoliciesByInsuredIdAsync(string insuredId)
        {
            if (string.IsNullOrWhiteSpace(insuredId))
                throw new ArgumentNullException(nameof(insuredId));

            return await _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.AssuredCode == insuredId && p.Tag == "A")
                .OrderByDescending(p => p.SubmittedOn)
                .ToListAsync();
        }

        public async Task<Models.PagedResult<PolicyMaster>> GetTradPoliciesByInsuredIdPagedAsync(
            string insuredId, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(insuredId))
                throw new ArgumentNullException(nameof(insuredId));

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.PolicyMaster
                .Where(p => p.Remarks == "IN FORCE" && p.AssuredCode == insuredId && p.Tag == "A");

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
    }
}