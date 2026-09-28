using CsvHelper;
using CsvHelper.Configuration;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Models.Customer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {

        public async Task<QuotationResult> CalculateAsync(QuotationRequest request)
        {
            var config = await GetByCodeAsync(request.ProductCode);
            if (config == null)
                throw new InvalidOperationException($"No ProductSettings found for code '{request.ProductCode}'.");

            var factor = FrequencyFactor.Values[request.Frequency];
            decimal annualPremium = request.RegularSaving.GetValueOrDefault() * factor;
            decimal sumAssured = 0;
            decimal lifeCover = 0;

            // Check if frequency is Single
            bool isSingleFrequency = request.Frequency?.Equals("Single", StringComparison.OrdinalIgnoreCase) ?? false;

            if (isSingleFrequency)
            {
                // For Single frequency, sum assured equals regular savings
                sumAssured = request.RegularSaving.GetValueOrDefault();
                lifeCover = sumAssured;
            }
            else
            {
                // Original calculation logic for other frequencies
                switch (config.ProductID.ToUpper())
                {
                    case "SLA":
                        sumAssured = annualPremium * request.Duration;
                        lifeCover = sumAssured;
                        break;
                    case "IPP":
                    case "SIP":
                        sumAssured = annualPremium * request.Duration;
                        lifeCover = sumAssured;
                        break;
                    case "FAS":
                    case "RPAS":
                    case "SSP":
                        sumAssured = annualPremium * request.Duration;
                        lifeCover = sumAssured;
                        break;
                    case "ESP":
                        sumAssured = annualPremium * request.Duration;
                        lifeCover = sumAssured;
                        break;
                    default:
                        // Use SIP calculation if no match
                        sumAssured = annualPremium * request.Duration;
                        lifeCover = sumAssured;
                        break;
                }
            }

            decimal? loadingFreq = 0;
            decimal? extraPremium = 0;
            if (request.LoadingRate > 0)
            {
                var loading = (request.LoadingRate / 1000m) * sumAssured;
                loadingFreq = loading / factor;
            }
            if (request.ExtraRate > 0)
            {
                extraPremium = (sumAssured * request.ExtraRate) / 1000m;
            }
            var totalPremium = annualPremium + extraPremium + loadingFreq;
            var quoteNo = !string.IsNullOrEmpty(request.QuoteNo)
                ? request.QuoteNo
                : GetNextAutoNumber("OTHERS", "ALL");

            var result = new QuotationResult
            {
                QuoteNo = quoteNo,
                AnnualPremium = annualPremium,
                SumAssured = sumAssured,
                LifeCover = lifeCover,
                Loading = loadingFreq,
                ExtraPremium = extraPremium,
                TotalPremium = totalPremium
            };

            return result;
        }
        public async Task<ProductSettings?> GetByCodeAsync(string productID)
        {
            return await _db.ProductSettings.FirstOrDefaultAsync(p => p.ProductID == productID);
        }


        public async Task<List<QuotesMaster>> GetQuotationByNoAsync(string quoteNo, DateTime? fromDate, DateTime? toDate)
        {
            var query = _db.QuotesMaster
                .Where(q => /*q.isQuote == 1 &&*/ q.QuoteNo == quoteNo);

            if (fromDate.HasValue)
                query = query.Where(q => q.StartDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(q => q.MaturityDate <= toDate.Value);

            return await query
                .OrderByDescending(q => q.SubmittedOn)
                .ToListAsync();
        }



        // ============================================================================
        // CREATE / COMPUTE QUOTATION
        // ============================================================================

        // Saves to Policy / PolicyDetails / GrpMembers tables
        public async Task<QuotationResponseDto> CreateQuotationAsync(QuotationRequestDto request, string currentUser)
        {
            // Generate quotation number if not provided
            if (string.IsNullOrEmpty(request.QuotationNumber))
            {
                request.QuotationNumber = await GenerateQuoteNumberAsync();
            }

            // Compute quotation
            var computation = await ComputeQuotationAsync(request);

            // Create Policy entity (replaces QuotesMaster)
            var policy = await MapToPolicy(request, computation, currentUser);
            await CreatePolicyAsync(policy);

            // Create PolicyDetails entity
            var policyDetail = MapToPolicyDetails(request, computation, currentUser);
            await CreatePolicyDetailsAsync(policyDetail);

            // Create GrpMembers entities (replaces QuoteMembers)
            var members = await CreateGrpMembersAsync(request, computation, currentUser);
            await CreateMembersInGrpAsync(members);

            return computation;
        }

        public async Task<QuotationResponseDto> ComputeQuotationAsync(QuotationRequestDto request)
        {
            Console.WriteLine($"Computing quotation for: {request.QuotationNumber}");
            Console.WriteLine($"Request details: SchemeType='{request.SchemeType}', GroupLifeFactor={request.GroupLifeFactor}");
            Console.WriteLine($"Members count: {request.Members?.Count ?? 0}");

            var response = new QuotationResponseDto
            {
                QuotationNumber = request.QuotationNumber,
                MemberCalculations = new List<MemberCalculationDto>()
            };

            if (request.Members == null || !request.Members.Any())
            {
                Console.WriteLine("No members provided for calculation");
                return response;
            }

            // Calculate each member
            foreach (var member in request.Members)
            {
                Console.WriteLine($"Processing member: {member.EmployeeNames}");
                var memberCalc = await ComputeMemberPremiumAsync(member, request);
                response.MemberCalculations.Add(memberCalc);
            }

            // Calculate totals with null safety
            response.TotalSumInsured = response.MemberCalculations.Sum(m => m.SumAssured ?? 0);
            response.TotalBasicPremium = response.MemberCalculations.Sum(m => m.ActualPremium ?? 0);
            response.TotalExtensionPremiums = response.MemberCalculations.Sum(m =>
                (m.PDEPremium ?? 0) + (m.TDEPremium ?? 0) + (m.MEPremium ?? 0) +
                (m.FUNPremium ?? 0) + (m.CRLPremium ?? 0));

            Console.WriteLine($"Calculated totals:");
            Console.WriteLine($"  Total Sum Insured: {response.TotalSumInsured:C}");
            Console.WriteLine($"  Total Basic Premium: {response.TotalBasicPremium:C}");
            Console.WriteLine($"  Total Extension Premiums: {response.TotalExtensionPremiums:C}");

            // Calculate total premium (basic + extensions) BEFORE loading/reduction
            var totalPremium = (response.TotalBasicPremium ?? 0) + (response.TotalExtensionPremiums ?? 0);

            // Apply loading and reduction to the TOTAL PREMIUM of the quotation
            response.LoadingAmount = totalPremium * request.LoadingRate / 100;
            response.ReductionAmount = totalPremium * request.ReductionRate / 100;

            // Net premium = Total Premium + Loading - Reduction
            response.NetPremium = totalPremium + (response.LoadingAmount ?? 0) - (response.ReductionAmount ?? 0);

            Console.WriteLine($"Final calculations:");
            Console.WriteLine($"  Loading ({request.LoadingRate}%): {response.LoadingAmount:C}");
            Console.WriteLine($"  Reduction ({request.ReductionRate}%): {response.ReductionAmount:C}");
            Console.WriteLine($"  Net Premium: {response.NetPremium:C}");

            // Calculate extension summary
            response.ExtensionSummary = new ExtensionSummaryDto
            {
                TotalPDEPremium = response.MemberCalculations.Sum(m => m.PDEPremium ?? 0),
                TotalTDEPremium = response.MemberCalculations.Sum(m => m.TDEPremium ?? 0),
                TotalMEPremium = response.MemberCalculations.Sum(m => m.MEPremium ?? 0),
                TotalFUNPremium = response.MemberCalculations.Sum(m => m.FUNPremium ?? 0),
                TotalCRLPremium = response.MemberCalculations.Sum(m => m.CRLPremium ?? 0)
            };

            return response;
        }

        // ============================================================================
        // RATE LOOKUPS
        // ============================================================================

        public async Task<decimal> GetRateAsync(string productCode, int age)
        {
            var rate = await _db.UndPremRates
                .Where(r => r.CoverCode == productCode && r.Term2 == age)
                .OrderByDescending(r => r.FromDate)
                .FirstOrDefaultAsync();

            return rate?.Rate ?? 0;
        }

        public async Task<UndPremRate> GetRateDetailsAsync(string productCode, int age)
        {
            return await _db.UndPremRates
                .Where(r => r.CoverCode == productCode && r.Term2 == age)
                .OrderByDescending(r => r.FromDate)
                .FirstOrDefaultAsync();
        }

        // ============================================================================
        // PER-MEMBER PREMIUM COMPUTATION
        // ============================================================================

        private async Task<MemberCalculationDto> ComputeMemberPremiumAsync(MemberDetailsDto member, QuotationRequestDto request)
        {
            var memberCalc = new MemberCalculationDto
            {
                StaffNo = !string.IsNullOrEmpty(member.StaffNo) ? member.StaffNo : GetNextAutoNumber("INSURED", "01"),
                EmployeeNames = member.EmployeeNames ?? "",
                Sex = member.Sex ?? "",
                DateOfBirth = member.DateOfBirth,
                ANB = member.ANB,
                BasicSalary = member.BasicSalary,
                HousingAllowance = member.HousingAllowance,
                TransportAllowance = member.TransportAllowance,
                TotalEmolument = member.TotalEmolument,
                HireDate = member.HireDate
            };

            // Calculate Sum Assured
            memberCalc.SumAssured = request.GroupLifeFactor * member.TotalEmolument;

            // Determine Premium Rate with enhanced debugging
            Console.WriteLine($"Determining premium rate for member: {member.EmployeeNames}");
            Console.WriteLine($"  UseFixedRate: {request.UseFixedRate}, FixedRate: {request.FixedRate}");
            Console.WriteLine($"  UseAverageAge: {request.UseAverageAge}, AverageAge: {request.AverageAge}");
            Console.WriteLine($"  SchemeType: '{request.SchemeType}', Member ANB: {member.ANB}");

            if (request.UseFixedRate && request.FixedRate > 0)
            {
                memberCalc.PremiumRate = request.FixedRate;
                Console.WriteLine($"  Using fixed rate: {memberCalc.PremiumRate}");
            }
            else if (request.UseAverageAge && request.AverageAge > 0)
            {
                memberCalc.PremiumRate = await GetRateAsync(request.SchemeType, request.AverageAge);
                Console.WriteLine($"  Using average age rate: {memberCalc.PremiumRate}");
            }
            else
            {
                memberCalc.PremiumRate = await GetRateAsync(request.SchemeType, member.ANB);
                Console.WriteLine($"  Using member age rate: {memberCalc.PremiumRate}");
            }

            // Calculate Actual Premium (basic premium per member)
            memberCalc.ActualPremium = (memberCalc.SumAssured ?? 0) * (memberCalc.PremiumRate ?? 0) / 1000;
            Console.WriteLine($"  Calculated Actual Premium: {memberCalc.ActualPremium}");

            // Ensure coverDays has a valid value
            int effectiveCoverDays = request.CoverDays > 0 ? request.CoverDays : 365;

            // Calculate Extension Premiums per member with proper null handling
            memberCalc.PDEPremium = member.PermanentDisabilityRate > 0
                ? CalculateExtensionPremium(memberCalc.SumAssured ?? 0, member.PermanentDisabilityRate, effectiveCoverDays)
                : 0m;

            memberCalc.TDEPremium = member.TemporaryDisabilityRate > 0
                ? CalculateExtensionPremium(memberCalc.SumAssured ?? 0, member.TemporaryDisabilityRate, effectiveCoverDays)
                : 0m;

            memberCalc.MEPremium = member.MedicalExpensesRate > 0
                ? CalculateExtensionPremium(memberCalc.SumAssured ?? 0, member.MedicalExpensesRate, effectiveCoverDays)
                : 0m;

            memberCalc.FUNPremium = member.FuneralExtensionRate > 0
                ? CalculateExtensionPremium(memberCalc.SumAssured ?? 0, member.FuneralExtensionRate, effectiveCoverDays)
                : 0m;

            memberCalc.CRLPremium = member.CriticalIllnessRate > 0
                ? CalculateExtensionPremium(memberCalc.SumAssured ?? 0, member.CriticalIllnessRate, effectiveCoverDays)
                : 0m;

            // Calculate total member premium (ensure no nulls)
            memberCalc.TotalMemberPremium = (memberCalc.ActualPremium ?? 0) +
                                           (memberCalc.PDEPremium ?? 0) +
                                           (memberCalc.TDEPremium ?? 0) +
                                           (memberCalc.MEPremium ?? 0) +
                                           (memberCalc.FUNPremium ?? 0) +
                                           (memberCalc.CRLPremium ?? 0);

            Console.WriteLine($"Extension Premiums for {member.EmployeeNames}:");
            Console.WriteLine($"  PDE: {memberCalc.PDEPremium:C} (Rate: {member.PermanentDisabilityRate}%)");
            Console.WriteLine($"  TDE: {memberCalc.TDEPremium:C} (Rate: {member.TemporaryDisabilityRate}%)");
            Console.WriteLine($"  ME: {memberCalc.MEPremium:C} (Rate: {member.MedicalExpensesRate}%)");
            Console.WriteLine($"  FUN: {memberCalc.FUNPremium:C} (Rate: {member.FuneralExtensionRate}%)");
            Console.WriteLine($"  CRL: {memberCalc.CRLPremium:C} (Rate: {member.CriticalIllnessRate}%)");
            Console.WriteLine($"  Total Member Premium: {memberCalc.TotalMemberPremium:C}");

            return memberCalc;
        }

        private decimal CalculateExtensionPremium(decimal sumAssured, decimal rate, int coverDays)
        {
            // Validate inputs
            if (sumAssured < 0)
            {
                throw new ArgumentException("Sum assured cannot be negative", nameof(sumAssured));
            }

            if (rate < 0)
            {
                throw new ArgumentException("Rate cannot be negative", nameof(rate));
            }

            // Handle invalid cover days
            if (coverDays <= 0)
            {
                Console.WriteLine($"Warning: Invalid coverDays value {coverDays}. Defaulting to 365.");
                coverDays = 365;
            }

            // Calculate annual premium (rate is per 1000 of sum assured)
            var annualPremium = (sumAssured * rate) / 1000m;

            // For full year coverage, return annual premium directly
            const int DAYS_PER_YEAR = 365;
            if (coverDays == DAYS_PER_YEAR)
            {
                return annualPremium;
            }

            // For partial year coverage, prorate the premium
            var prorateFactor = (decimal)coverDays / DAYS_PER_YEAR;
            var proratedPremium = annualPremium * prorateFactor;

            // Log for audit trail
            Console.WriteLine($"Extension Premium Calculation: " +
                             $"SumAssured={sumAssured:C}, Rate={rate}%, " +
                             $"CoverDays={coverDays}, " +
                             $"AnnualPremium={annualPremium:C}, " +
                             $"ProratedPremium={proratedPremium:C}");

            return proratedPremium;
        }

        public async Task<string> GenerateQuoteNumberAsync()
        {
            var lastQuote = GetNextAutoNumber("OTHERS", "ALL");
            return GetNextAutoNumber("OTHERS", "ALL");
        }

        // ============================================================================
        // POLICY / POLICYDETAIL / GRPMEMBER PERSISTENCE
        // ============================================================================

        public async Task<Policy> CreatePolicyAsync(Policy policy)
        {
            _db.Policies.Add(policy);
            await _db.SaveChangesAsync();
            return policy;
        }

        public async Task<PolicyDetail> CreatePolicyDetailsAsync(PolicyDetail policyDetail)
        {
            _db.PolicyDetails.Add(policyDetail);
            await _db.SaveChangesAsync();
            return policyDetail;
        }

        public async Task<List<GrpMember>> CreateMembersInGrpAsync(List<GrpMember> members)
        {
            _db.GrpMembers.AddRange(members);
            await _db.SaveChangesAsync();
            return members;
        }

        public async Task<Policy> GetGLPolicyByNumberAsync(string policyNumber)
        {
            return await _db.Policies
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PolicyNo == policyNumber);
        }

        public async Task<bool> DeleteMemberAsync(long memberId)
        {
            var member = await _db.GrpMembers.FindAsync(memberId);
            if (member != null)
            {
                _db.GrpMembers.Remove(member);
                await _db.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<PolicyDetail> GetPolicyDetailsByNumberAsync(string policyNumber)
        {
            return await _db.PolicyDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(pd => pd.PolicyNo == policyNumber);
        }

        public async Task<GrpMember> GetMemberByIdAsync(long memberId)
        {
            return await _db.GrpMembers.FindAsync(memberId);
        }

        public async Task<List<GrpMember>> GetGrpMembersByPolicyAsync(string policyNumber)
        {
            return await _db.GrpMembers
                .Where(m => m.PolicyNo == policyNumber)
                .ToListAsync();
        }

        // ============================================================================
        // MAPPING - request/computation -> Policy tables
        // ============================================================================

        private async Task<Policy> MapToPolicy(QuotationRequestDto request, QuotationResponseDto computation, string currentUser)
        {
            string generatedInsuredCode;

            if (string.IsNullOrWhiteSpace(request.AssuredCode))
            {
                var insured = new InsuredClients
                {
                    InsuredID = GetNextAutoNumber("INSURED", "01") ?? "",
                    SurName = request.ClientName,
                    FullName = request.ClientName,
                    ClientType = request.InsuredType,
                    SubmittedBy = currentUser,
                    SubmittedOn = DateTime.Now,
                    ModifiedOn = DateTime.Now,
                    Active = 1,
                    Deleted = 0
                };

                _db.InsuredClients.Add(insured);
                await _db.SaveChangesAsync();
                generatedInsuredCode = insured.InsuredID;
            }
            else
            {
                generatedInsuredCode = request.AssuredCode;
            }

            return new Policy
            {
                TransDate = request.TransactionDate,
                PolicyNo = request.QuotationNumber,
                CoPolicyNo = request.QuotationNumber,
                StartDate = request.EffectiveDate,
                EndDate = request.ExpiryDate,

                // Insured Information
                InsuredID = generatedInsuredCode,
                InsSurname = request.ClientName,
                InsFirstname = "",
                InsOthernames = "",

                // Risk Information
                SubRiskID = "GL", // Group Life
                SubRiskName = request.SchemeType,

                // Financial Information
                SumInsured = computation.TotalSumInsured,
                GrossPremium = computation.TotalBasicPremium,
                ProRataPremium = computation.NetPremium,

                // Extension premiums stored in A fields
                A1 = computation.ExtensionSummary?.TotalPDEPremium ?? 0,
                A2 = computation.ExtensionSummary?.TotalTDEPremium ?? 0,
                A3 = computation.ExtensionSummary?.TotalMEPremium ?? 0,
                A4 = computation.ExtensionSummary?.TotalFUNPremium ?? 0,
                A5 = computation.ExtensionSummary?.TotalCRLPremium ?? 0,
                A6 = (decimal)request.LoadingRate,
                A7 = request.ReductionRate,
                A8 = request.GroupLifeFactor,
                A9 = request.GroupFactorAmount,
                A10 = request.UseFixedRate ? request.FixedRate : 0,

                // Settings stored in Field columns
                Field1 = request.UseFixedRate ? "Y" : "N",
                Field2 = request.UseAverageAge ? "Y" : "N",
                Field3 = request.UseAverageAge ? request.AverageAge.ToString() : "",
                Field4 = request.CoverDays.ToString(),
                Field5 = request.AgentCode,

                // Status fields
                isProposal = 0, // This is a quotation/proposal
                TransSTATUS = "PENDING",
                Active = 1,
                Deleted = 0,

                // Audit fields
                SubmittedBy = currentUser,
                SubmittedOn = DateTime.Now,
                ModifiedBy = currentUser,
                ModifiedOn = DateTime.Now
            };
        }
        // ============================================
        // PRODUCT SETTINGS
        // ============================================

       

        public async Task<IEnumerable<ProductSettings>> GetAllProductAsync()
        {
            return await _db.ProductSettings.ToListAsync();
        }

        // Overload by int key (distinct from GetByIdAsync(long) used for QuotesMaster)
        public async Task<ProductSettings?> GetByIdAsync(int id)
        {
            return await _db.ProductSettings.FindAsync(id);
        }

        public async Task AddAsync(ProductSettings product)
        {
            _db.ProductSettings.Add(product);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(ProductSettings product)
        {
            _db.ProductSettings.Update(product);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(string id)
        {
            var item = await _db.ProductSettings.FindAsync(id);
            if (item != null)
            {
                _db.ProductSettings.Remove(item);
                await _db.SaveChangesAsync();
            }
        }

        // ============================================
        // BRANCHES
        // ============================================

        public async Task<IEnumerable<Branch>> GetAllBranchesAsync()
        {
            return await _db.Branches.ToListAsync();
        }
        private PolicyDetail MapToPolicyDetails(QuotationRequestDto request, QuotationResponseDto computation, string currentUser)
        {
            return new PolicyDetail
            {
                PolicyNo = request.QuotationNumber,
                CoPolicyNo = request.QuotationNumber,
                EntryDate = DateTime.Now,
                InsuredName = request.ClientName,
                StartDate = request.EffectiveDate,
                EndDate = request.ExpiryDate,

                // Financial Information
                SumInsured = computation.TotalSumInsured,
                GrossPremium = computation.TotalBasicPremium,
                ProRataPremium = computation.NetPremium,
                NetAmount = computation.NetPremium,

                // Extension premiums in A fields
                A1 = (decimal)(computation.ExtensionSummary?.TotalPDEPremium ?? 0),
                A2 = (decimal)(computation.ExtensionSummary?.TotalTDEPremium ?? 0),
                A3 = (decimal)(computation.ExtensionSummary?.TotalMEPremium ?? 0),
                A4 = (decimal)(computation.ExtensionSummary?.TotalFUNPremium ?? 0),
                A5 = (decimal)(computation.ExtensionSummary?.TotalCRLPremium ?? 0),
                A6 = (decimal)request.LoadingRate,
                A7 = request.ReductionRate,
                A8 = request.GroupLifeFactor,

                // Settings in Field columns
                Field1 = request.UseFixedRate ? "Y" : "N",
                Field2 = request.UseAverageAge ? "Y" : "N",
                Field3 = request.UseAverageAge ? request.AverageAge.ToString() : "",

                TotalRiskValue = computation.TotalSumInsured,

                // Status
                Active = 1,
                Deleted = 0,

                // Audit
                SubmittedBy = currentUser,
                SubmittedOn = DateTime.Now,
                ModifiedBy = currentUser,
                ModifiedOn = DateTime.Now
            };
        }

        private string ExtractSurname(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.FirstOrDefault() ?? "";
        }

        private string ExtractOtherNames(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";
        }

        private async Task<List<GrpMember>> CreateGrpMembersAsync(
            QuotationRequestDto request,
            QuotationResponseDto computation,
            string currentUser)
        {
            var members = new List<GrpMember>();
            int serialNo = 1;

            foreach (var memberCalc in computation.MemberCalculations)
            {
                var memberDto = request.Members.FirstOrDefault(m =>
                    m.EmployeeNames == memberCalc.EmployeeNames &&
                    m.DateOfBirth == memberCalc.DateOfBirth);

                var member = new GrpMember
                {
                    PolicyNo = request.QuotationNumber,
                    QuotationNo = request.QuotationNumber,
                    SerialNo = serialNo++.ToString(),
                    MembersNo = memberCalc.StaffNo,

                    // Name information
                    Surname = ExtractSurname(memberCalc.EmployeeNames),
                    OtherNames = ExtractOtherNames(memberCalc.EmployeeNames),

                    // Personal information
                    DOBDate = memberCalc.DateOfBirth,
                    HireDate = memberCalc.HireDate,
                    Sex = memberCalc.Sex,
                    Age = (long)memberCalc.ANB,

                    // Salary information
                    BasicSalary = memberCalc.BasicSalary,
                    Housing = memberCalc.HousingAllowance,
                    Transport = memberCalc.TransportAllowance,
                    OtherAmount = 0,
                    TEmolument = memberCalc.TotalEmolument,

                    // Coverage information
                    SumInsured = memberCalc.SumAssured,
                    PremiumRate = (float)(memberCalc.PremiumRate ?? 0),
                    GrossPremium = memberCalc.ActualPremium,

                    // Extension rates and premiums
                    PDERate = (float)(memberDto?.PermanentDisabilityRate ?? 0),
                    PDEPremium = memberCalc.PDEPremium,
                    TDRate = (float)(memberDto?.TemporaryDisabilityRate ?? 0),
                    TDPremium = memberCalc.TDEPremium,
                    MERate = (float)(memberDto?.MedicalExpensesRate ?? 0),
                    MEPremium = memberCalc.MEPremium,
                    FURate = (float)(memberDto?.FuneralExtensionRate ?? 0),
                    A7 = memberCalc.FUNPremium, // Funeral Premium
                    CRRate = (float)(memberDto?.CriticalIllnessRate ?? 0),
                    A9 = memberCalc.CRLPremium, // Critical Illness Premium

                    // Status
                    Active = 1,
                    Deleted = 0,

                    // Audit
                    SubmittedBy = currentUser,
                    SubmittedOn = DateTime.Now,
                    ModifiedBy = currentUser,
                    ModifiedOn = DateTime.Now
                };

                members.Add(member);
            }

            return members;
        }

        // ============================================================================
        // UPDATE / DELETE - Policy tables
        // ============================================================================

        public async Task<Policy> UpdatePolicyAsync(Policy policy)
        {
            var trackedEntity = _db.Entry(policy);

            if (trackedEntity.State == EntityState.Detached)
            {
                _db.Policies.Attach(policy);
                _db.Entry(policy).State = EntityState.Modified;
            }
            else
            {
                _db.Policies.Update(policy);
            }

            await _db.SaveChangesAsync();
            return policy;
        }

        public async Task<PolicyDetail> UpdatePolicyDetailsAsync(PolicyDetail policyDetail)
        {
            var trackedEntity = _db.Entry(policyDetail);

            if (trackedEntity.State == EntityState.Detached)
            {
                _db.PolicyDetails.Attach(policyDetail);
                _db.Entry(policyDetail).State = EntityState.Modified;
            }
            else
            {
                _db.PolicyDetails.Update(policyDetail);
            }

            await _db.SaveChangesAsync();
            return policyDetail;
        }

        public async Task<bool> DeletePolicyAsync(string policyNumber)
        {
            var policy = await _db.Policies.FindAsync(policyNumber);
            if (policy != null)
            {
                _db.Policies.Remove(policy);
                await _db.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> DeletePolicyDetailsAsync(string policyNumber)
        {
            var policyDetail = await _db.PolicyDetails
                .FirstOrDefaultAsync(pd => pd.PolicyNo == policyNumber);

            if (policyDetail != null)
            {
                _db.PolicyDetails.Remove(policyDetail);
                await _db.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> DeleteGrpMembersByPolicyAsync(string policyNumber)
        {
            var members = await _db.GrpMembers
                .Where(m => m.PolicyNo == policyNumber)
                .ToListAsync();

            if (members.Any())
            {
                _db.GrpMembers.RemoveRange(members);
                await _db.SaveChangesAsync();
                return true;
            }
            return false;
        }

        // ============================================================================
        // UPDATE QUOTATION
        // ============================================================================

        public async Task<QuotationResponseDto> UpdateQuotationAsync(
            string quotationNumber,
            QuotationRequestDto request,
            string currentUser)
        {
            var existingPolicy = await GetGLPolicyByNumberAsync(quotationNumber);
            if (existingPolicy == null)
                return null;

            try
            {
                request.QuotationNumber = quotationNumber;

                // Compute updated quotation
                var computation = await ComputeQuotationAsync(request);

                // Update Policy table
                existingPolicy.TransDate = request.TransactionDate;
                existingPolicy.StartDate = request.EffectiveDate;
                existingPolicy.EndDate = request.ExpiryDate;
                existingPolicy.SubRiskName = request.SchemeType;
                existingPolicy.InsSurname = request.ClientName;
                existingPolicy.SumInsured = computation.TotalSumInsured;
                existingPolicy.GrossPremium = computation.TotalBasicPremium;
                existingPolicy.ProRataPremium = computation.NetPremium;

                // Update extension premiums
                existingPolicy.A1 = computation.ExtensionSummary?.TotalPDEPremium ?? 0;
                existingPolicy.A2 = computation.ExtensionSummary?.TotalTDEPremium ?? 0;
                existingPolicy.A3 = computation.ExtensionSummary?.TotalMEPremium ?? 0;
                existingPolicy.A4 = computation.ExtensionSummary?.TotalFUNPremium ?? 0;
                existingPolicy.A5 = computation.ExtensionSummary?.TotalCRLPremium ?? 0;
                existingPolicy.A6 = (decimal)request.LoadingRate;
                existingPolicy.A7 = request.ReductionRate;
                existingPolicy.A8 = request.GroupLifeFactor;
                existingPolicy.A9 = request.GroupFactorAmount;
                existingPolicy.A10 = request.UseFixedRate ? request.FixedRate : 0;

                // Update settings
                existingPolicy.Field1 = request.UseFixedRate ? "Y" : "N";
                existingPolicy.Field2 = request.UseAverageAge ? "Y" : "N";
                existingPolicy.Field3 = request.UseAverageAge ? request.AverageAge.ToString() : "";
                existingPolicy.Field4 = request.CoverDays.ToString();
                existingPolicy.Field5 = request.AgentCode;

                existingPolicy.ModifiedBy = currentUser;
                existingPolicy.ModifiedOn = DateTime.Now;

                await _db.SaveChangesAsync();

                // Update PolicyDetails
                var existingPolicyDetail = await GetPolicyDetailsByNumberAsync(quotationNumber);
                if (existingPolicyDetail != null)
                {
                    existingPolicyDetail.InsuredName = request.ClientName;
                    existingPolicyDetail.StartDate = request.EffectiveDate;
                    existingPolicyDetail.EndDate = request.ExpiryDate;
                    existingPolicyDetail.SumInsured = computation.TotalSumInsured;
                    existingPolicyDetail.GrossPremium = computation.TotalBasicPremium;
                    existingPolicyDetail.ProRataPremium = computation.NetPremium;
                    existingPolicyDetail.NetAmount = computation.NetPremium;

                    existingPolicyDetail.A1 = (decimal)(computation.ExtensionSummary?.TotalPDEPremium ?? 0);
                    existingPolicyDetail.A2 = (decimal)(computation.ExtensionSummary?.TotalTDEPremium ?? 0);
                    existingPolicyDetail.A3 = (decimal)(computation.ExtensionSummary?.TotalMEPremium ?? 0);
                    existingPolicyDetail.A4 = (decimal)(computation.ExtensionSummary?.TotalFUNPremium ?? 0);
                    existingPolicyDetail.A5 = (decimal)(computation.ExtensionSummary?.TotalCRLPremium ?? 0);
                    existingPolicyDetail.A6 = (decimal)request.LoadingRate;
                    existingPolicyDetail.A7 = request.ReductionRate;
                    existingPolicyDetail.A8 = request.GroupLifeFactor;

                    existingPolicyDetail.Field1 = request.UseFixedRate ? "Y" : "N";
                    existingPolicyDetail.Field2 = request.UseAverageAge ? "Y" : "N";
                    existingPolicyDetail.Field3 = request.UseAverageAge ? request.AverageAge.ToString() : "";

                    existingPolicyDetail.TotalRiskValue = computation.TotalSumInsured;
                    existingPolicyDetail.ModifiedBy = currentUser;
                    existingPolicyDetail.ModifiedOn = DateTime.Now;

                    await _db.SaveChangesAsync();
                }

                // Delete existing members and create new ones
                await DeleteGrpMembersByPolicyAsync(quotationNumber);
                var members = await CreateGrpMembersAsync(request, computation, currentUser);
                await CreateMembersInGrpAsync(members);

                return computation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating quotation {quotationNumber}: {ex.Message}");
                throw;
            }
        }

        // ============================================================================
        // UPDATE MEMBER AND RECALCULATE
        // ============================================================================

        public async Task<QuotationResponseDto> UpdateMemberAndRecalculateAsync(
            string quotationNumber,
            long memberId,
            MemberDetailsDto memberDetails,
            string currentUser)
        {
            var policy = await _db.Policies
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PolicyNo == quotationNumber && p.isProposal == 1);

            if (policy == null) return null;

            var existingMember = await _db.GrpMembers.FindAsync(memberId);
            if (existingMember == null || existingMember.PolicyNo != quotationNumber) return null;

            // Get all current members
            var allMembers = await GetGrpMembersByPolicyAsync(quotationNumber);

            // Convert to DTO list and replace the updated member
            var memberDtos = new List<MemberDetailsDto>();

            foreach (var member in allMembers)
            {
                if (member.MemberID == memberId)
                {
                    memberDtos.Add(memberDetails);
                }
                else
                {
                    memberDtos.Add(ConvertGrpMemberToDto(member));
                }
            }

            // Create request DTO to use existing calculation logic
            var request = await BuildQuotationRequestFromExistingPolicyAsync(quotationNumber, memberDtos);

            // Compute updated quotation
            var computation = await ComputeQuotationAsync(request);

            // Update the tracked policy entity
            var trackedPolicy = await _db.Policies
                .FirstOrDefaultAsync(p => p.PolicyNo == quotationNumber && p.isProposal == 1);

            if (trackedPolicy != null)
            {
                var updatedData = await MapToPolicy(request, computation, currentUser);

                // Update properties
                trackedPolicy.TransDate = updatedData.TransDate;
                trackedPolicy.StartDate = updatedData.StartDate;
                trackedPolicy.EndDate = updatedData.EndDate;
                trackedPolicy.SubRiskName = updatedData.SubRiskName;
                trackedPolicy.InsSurname = updatedData.InsSurname;
                trackedPolicy.SumInsured = updatedData.SumInsured;
                trackedPolicy.GrossPremium = updatedData.GrossPremium;
                trackedPolicy.ProRataPremium = updatedData.ProRataPremium;
                trackedPolicy.A1 = updatedData.A1;
                trackedPolicy.A2 = updatedData.A2;
                trackedPolicy.A3 = updatedData.A3;
                trackedPolicy.A4 = updatedData.A4;
                trackedPolicy.A5 = updatedData.A5;
                trackedPolicy.A6 = updatedData.A6;
                trackedPolicy.A7 = updatedData.A7;
                trackedPolicy.A8 = updatedData.A8;
                trackedPolicy.A9 = updatedData.A9;
                trackedPolicy.A10 = updatedData.A10;
                trackedPolicy.Field1 = updatedData.Field1;
                trackedPolicy.Field2 = updatedData.Field2;
                trackedPolicy.Field3 = updatedData.Field3;
                trackedPolicy.Field4 = updatedData.Field4;
                trackedPolicy.Field5 = updatedData.Field5;
                trackedPolicy.ModifiedBy = currentUser;
                trackedPolicy.ModifiedOn = DateTime.Now;

                await _db.SaveChangesAsync();
            }

            // Update PolicyDetails
            var policyDetail = await _db.PolicyDetails
                .FirstOrDefaultAsync(pd => pd.PolicyNo == quotationNumber);

            if (policyDetail != null)
            {
                var updatedDetails = MapToPolicyDetails(request, computation, currentUser);

                policyDetail.SumInsured = updatedDetails.SumInsured;
                policyDetail.GrossPremium = updatedDetails.GrossPremium;
                policyDetail.ProRataPremium = updatedDetails.ProRataPremium;
                policyDetail.NetAmount = updatedDetails.NetAmount;
                policyDetail.A1 = updatedDetails.A1;
                policyDetail.A2 = updatedDetails.A2;
                policyDetail.A3 = updatedDetails.A3;
                policyDetail.A4 = updatedDetails.A4;
                policyDetail.A5 = updatedDetails.A5;
                policyDetail.A6 = updatedDetails.A6;
                policyDetail.A7 = updatedDetails.A7;
                policyDetail.A8 = updatedDetails.A8;
                policyDetail.TotalRiskValue = updatedDetails.TotalRiskValue;
                policyDetail.ModifiedBy = currentUser;
                policyDetail.ModifiedOn = DateTime.Now;

                await _db.SaveChangesAsync();
            }

            // Delete existing members and create new ones
            await DeleteGrpMembersByPolicyAsync(quotationNumber);
            var newMembers = await CreateGrpMembersAsync(request, computation, currentUser);
            await CreateMembersInGrpAsync(newMembers);

            return computation;
        }

        // ============================================================================
        // ADD MEMBER AND RECALCULATE
        // ============================================================================

        public async Task<QuotationResponseDto> AddMemberAndRecalculateAsync(
            string quotationNumber,
            MemberDetailsDto memberDetails,
            string currentUser)
        {
            var policy = await GetGLPolicyByNumberAsync(quotationNumber);
            if (policy == null) return null;

            // Get all current members
            var allMembers = await GetGrpMembersByPolicyAsync(quotationNumber);

            // Convert to DTO list and add new member
            var memberDtos = allMembers.Select(m => ConvertGrpMemberToDto(m)).ToList();
            memberDtos.Add(memberDetails);

            // Create request DTO
            var request = await BuildQuotationRequestFromExistingPolicyAsync(quotationNumber, memberDtos);

            // Compute updated quotation
            var computation = await ComputeQuotationAsync(request);

            // Update Policy
            var updatedPolicy = await MapToPolicy(request, computation, currentUser);
            updatedPolicy.PolicyNo = policy.PolicyNo;
            updatedPolicy.ModifiedBy = currentUser;
            updatedPolicy.ModifiedOn = DateTime.Now;
            updatedPolicy.SubmittedBy = policy.SubmittedBy;
            updatedPolicy.SubmittedOn = policy.SubmittedOn;
            await UpdatePolicyAsync(updatedPolicy);

            // Update PolicyDetails
            var policyDetail = await GetPolicyDetailsByNumberAsync(quotationNumber);
            if (policyDetail != null)
            {
                var updatedDetails = MapToPolicyDetails(request, computation, currentUser);
                updatedDetails.PolicyNo = policyDetail.PolicyNo;
                updatedDetails.SubmittedBy = policyDetail.SubmittedBy;
                updatedDetails.SubmittedOn = policyDetail.SubmittedOn;
                await UpdatePolicyDetailsAsync(updatedDetails);
            }

            // Delete existing members and create new ones
            await DeleteGrpMembersByPolicyAsync(quotationNumber);
            var newMembers = await CreateGrpMembersAsync(request, computation, currentUser);
            await CreateMembersInGrpAsync(newMembers);

            return computation;
        }

        // ============================================================================
        // REMOVE MEMBER AND RECALCULATE
        // ============================================================================

        public async Task<QuotationResponseDto> RemoveMemberAndRecalculateAsync(
            string quotationNumber,
            long memberId,
            string currentUser)
        {
            var policy = await GetGLPolicyByNumberAsync(quotationNumber);
            if (policy == null) return null;

            var existingMember = await _db.GrpMembers.FindAsync(memberId);
            if (existingMember == null || existingMember.PolicyNo != quotationNumber) return null;

            // Get all current members except the one to be removed
            var allMembers = await GetGrpMembersByPolicyAsync(quotationNumber);
            var memberDtos = allMembers
                .Where(m => m.MemberID != memberId)
                .Select(m => ConvertGrpMemberToDto(m))
                .ToList();

            if (!memberDtos.Any())
            {
                throw new InvalidOperationException("Cannot remove the last member from a quotation");
            }

            // Create request DTO
            var request = await BuildQuotationRequestFromExistingPolicyAsync(quotationNumber, memberDtos);

            // Compute updated quotation
            var computation = await ComputeQuotationAsync(request);

            // Update Policy
            var updatedPolicy = await MapToPolicy(request, computation, currentUser);
            updatedPolicy.PolicyNo = policy.PolicyNo;
            updatedPolicy.ModifiedBy = currentUser;
            updatedPolicy.ModifiedOn = DateTime.Now;
            updatedPolicy.SubmittedBy = policy.SubmittedBy;
            updatedPolicy.SubmittedOn = policy.SubmittedOn;
            await UpdatePolicyAsync(updatedPolicy);

            // Update PolicyDetails
            var policyDetail = await GetPolicyDetailsByNumberAsync(quotationNumber);
            if (policyDetail != null)
            {
                var updatedDetails = MapToPolicyDetails(request, computation, currentUser);
                updatedDetails.PolicyNo = policyDetail.PolicyNo;
                updatedDetails.SubmittedBy = policyDetail.SubmittedBy;
                updatedDetails.SubmittedOn = policyDetail.SubmittedOn;
                await UpdatePolicyDetailsAsync(updatedDetails);
            }

            // Delete existing members and create new ones
            await DeleteGrpMembersByPolicyAsync(quotationNumber);
            var newMembers = await CreateGrpMembersAsync(request, computation, currentUser);
            await CreateMembersInGrpAsync(newMembers);

            return computation;
        }

        // ============================================================================
        // DELETE QUOTATION
        // ============================================================================

        public async Task<bool> DeleteQuotationAsync(string quotationNumber)
        {
            var policy = await GetGLPolicyByNumberAsync(quotationNumber);
            if (policy == null)
                return false;

            await DeleteGrpMembersByPolicyAsync(quotationNumber);
            await DeletePolicyDetailsAsync(quotationNumber);
            await DeletePolicyAsync(quotationNumber);

            return true;
        }

        // ============================================================================
        // HELPERS - rebuild request / convert member
        // ============================================================================

        private async Task<QuotationRequestDto> BuildQuotationRequestFromExistingPolicyAsync(
            string quotationNumber,
            List<MemberDetailsDto> members)
        {
            var policy = await GetGLPolicyByNumberAsync(quotationNumber);

            // Calculate GroupLifeFactor from existing data
            decimal groupLifeFactor = 3.0m;
            if (members.Any() && policy?.SumInsured > 0)
            {
                var totalEmolument = members.Sum(m => m.TotalEmolument);
                if (totalEmolument > 0)
                {
                    groupLifeFactor = (policy.SumInsured ?? 0) / totalEmolument;
                }
            }

            return new QuotationRequestDto
            {
                QuotationNumber = quotationNumber,
                TransactionDate = policy?.TransDate ?? DateTime.Now,
                EffectiveDate = policy?.StartDate ?? DateTime.Now,
                ExpiryDate = policy?.EndDate ?? DateTime.Now.AddYears(1),
                InsuredType = "GROUP",
                SchemeType = policy?.SubRiskName ?? "GROUP LIFE ONLY",
                ClientName = policy?.InsSurname ?? "",
                AssuredCode = policy?.InsuredID ?? "",
                GroupLifeFactor = groupLifeFactor,
                GroupFactorAmount = groupLifeFactor,
                UseFixedRate = policy?.Field1 == "Y",
                FixedRate = policy?.Field1 == "Y" ? (policy?.A10 ?? 0) : 0,
                UseAverageAge = policy?.Field2 == "Y",
                AverageAge = policy?.Field2 == "Y" ? int.Parse(policy?.Field3 ?? "0") : 0,
                LoadingRate = (decimal)(policy?.A6 ?? 0),
                ReductionRate = (decimal)(policy?.A7 ?? 0),
                CoverDays = int.Parse(policy?.Field4 ?? "365"),
                AgentCode = policy?.Field5 ?? "",
                Members = members
            };
        }

        private MemberDetailsDto ConvertGrpMemberToDto(GrpMember member)
        {
            return new MemberDetailsDto
            {
                StaffNo = member.MembersNo,
                EmployeeNames = $"{member.Surname?.Trim()} {member.OtherNames?.Trim()}".Trim(),
                Sex = member.Sex ?? "",
                DateOfBirth = member.DOBDate ?? DateTime.MinValue,
                BasicSalary = member.BasicSalary ?? 0,
                HousingAllowance = member.Housing ?? 0,
                TransportAllowance = member.Transport ?? 0,
                HireDate = member.HireDate ?? DateTime.MinValue,
                PermanentDisabilityRate = (decimal)(member.PDERate ?? 0),
                TemporaryDisabilityRate = (decimal)(member.TDRate ?? 0),
                MedicalExpensesRate = (decimal)(member.MERate ?? 0),
                FuneralExtensionRate = (decimal)(member.FURate ?? 0),
                CriticalIllnessRate = (decimal)(member.CRRate ?? 0)
            };
        }

        // ============================================================================
        // GET COMPREHENSIVE QUOTATION
        // ============================================================================

        public async Task<ComprehensiveQuotationResponseDto> GetComprehensiveQuotationAsync(
            string quotationNumber)
        {
            var policy = await GetGLPolicyByNumberAsync(quotationNumber);
            if (policy == null)
                return null;

            var members = await GetGrpMembersByPolicyAsync(quotationNumber);

            return MapToComprehensiveQuotationResponse(policy, members);
        }

        private ComprehensiveQuotationResponseDto MapToComprehensiveQuotationResponse(
            Policy policy,
            List<GrpMember> members)
        {
            var response = new ComprehensiveQuotationResponseDto
            {
                // Basic Information
                QuotationNumber = policy.PolicyNo,
                TransactionDate = policy.TransDate ?? DateTime.Now,
                EffectiveDate = policy.StartDate ?? DateTime.Now,
                ExpiryDate = policy.EndDate ?? DateTime.Now,

                // Client Information
                InsuredType = "GROUP",
                SchemeType = policy.SubRiskName ?? "GROUP LIFE ONLY",
                ClientName = policy.InsSurname ?? "",

                // Group Life Settings
                GroupLifeFactor = policy.A8 ?? 3.0m,
                GroupFactorAmount = policy.A9 ?? 0,

                // Rate Settings
                UseFixedRate = policy.Field1 == "Y",
                FixedRate = policy.Field1 == "Y" ? policy.A10 : null,
                UseAverageAge = policy.Field2 == "Y",
                AverageAge = policy.Field2 == "Y" ? int.Parse(policy.Field3 ?? "0") : (int?)null,

                // Adjustments
                LoadingRate = policy.A6 ?? 0,
                ReductionRate = policy.A7 ?? 0,
                CoverDays = int.Parse(policy.Field4 ?? "365"),

                // Financial Summary
                TotalSumInsured = policy.SumInsured ?? 0,
                TotalBasicPremium = policy.GrossPremium ?? 0,
                NetPremium = policy.ProRataPremium ?? 0,

                // Audit Information
                Status = policy.TransSTATUS ?? "PENDING",
                SubmittedBy = policy.SubmittedBy,
                SubmittedOn = policy.SubmittedOn,
                ModifiedBy = policy.ModifiedBy,
                ModifiedOn = policy.ModifiedOn
            };

            // Map all member calculations
            foreach (var member in members)
            {
                var memberCalc = new MemberCalculationDto
                {
                    StaffNo = member.MembersNo,
                    EmployeeNames = $"{(member.Surname ?? "")} {(member.OtherNames ?? "")}".Trim(),
                    Sex = member.Sex ?? "",
                    DateOfBirth = member.DOBDate ?? DateTime.MinValue,
                    ANB = (int?)member.Age,
                    BasicSalary = member.BasicSalary ?? 0,
                    HousingAllowance = member.Housing ?? 0,
                    TransportAllowance = member.Transport ?? 0,
                    TotalEmolument = member.TEmolument ?? 0,
                    SumAssured = member.SumInsured ?? 0,
                    PremiumRate = (decimal)(member.PremiumRate ?? 0),
                    ActualPremium = member.GrossPremium ?? 0,
                    PDEPremium = member.PDEPremium ?? 0,
                    TDEPremium = member.TDPremium ?? 0,
                    MEPremium = member.MEPremium ?? 0,
                    FUNPremium = member.A7 ?? 0,
                    CRLPremium = member.A9 ?? 0,
                    HireDate = member.HireDate ?? DateTime.MinValue
                };

                memberCalc.TotalMemberPremium = (memberCalc.ActualPremium ?? 0) +
                                               (memberCalc.PDEPremium ?? 0) +
                                               (memberCalc.TDEPremium ?? 0) +
                                               (memberCalc.MEPremium ?? 0) +
                                               (memberCalc.FUNPremium ?? 0) +
                                               (memberCalc.CRLPremium ?? 0);

                response.MemberCalculations.Add(memberCalc);
            }

            // Calculate totals
            response.TotalExtensionPremiums = response.MemberCalculations.Sum(m =>
                (m.PDEPremium ?? 0) + (m.TDEPremium ?? 0) + (m.MEPremium ?? 0) +
                (m.FUNPremium ?? 0) + (m.CRLPremium ?? 0));

            var totalPremiumBeforeAdjustments = (response.TotalBasicPremium ?? 0) +
                                               (response.TotalExtensionPremiums ?? 0);

            response.LoadingAmount = totalPremiumBeforeAdjustments * response.LoadingRate / 100;
            response.ReductionAmount = totalPremiumBeforeAdjustments * response.ReductionRate / 100;

            // Build extension summary
            response.ExtensionSummary = new ExtensionSummaryDto
            {
                TotalPDEPremium = response.MemberCalculations.Sum(m => m.PDEPremium ?? 0),
                TotalTDEPremium = response.MemberCalculations.Sum(m => m.TDEPremium ?? 0),
                TotalMEPremium = response.MemberCalculations.Sum(m => m.MEPremium ?? 0),
                TotalFUNPremium = response.MemberCalculations.Sum(m => m.FUNPremium ?? 0),
                TotalCRLPremium = response.MemberCalculations.Sum(m => m.CRLPremium ?? 0)
            };

            return response;
        }

        // ============================================================================
        // MEMBER IMPORT (EPPlus / CsvHelper) - alternative GL import helpers
        // ============================================================================

        public async Task<List<MemberDetailsDto>> ImportMembersFromCsvAsync(IFormFile file)
        {
            var members = new List<MemberDetailsDto>();

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null,
                    TrimOptions = TrimOptions.Trim
                });

                // Read header to understand the structure
                await csv.ReadAsync();
                csv.ReadHeader();

                while (await csv.ReadAsync())
                {
                    try
                    {
                        var member = new MemberDetailsDto
                        {
                            StaffNo = csv.GetField("STAFF NO")?.Trim() ?? csv.GetField(1)?.Trim() ?? "",
                            EmployeeNames = csv.GetField("EMPLOYEENAMES")?.Trim() ?? csv.GetField(2)?.Trim() ?? "",
                            Sex = csv.GetField("SEX")?.Trim() ?? csv.GetField(3)?.Trim() ?? "",
                            BasicSalary = ParseDecimal(csv.GetField("BASIC SALARY") ?? csv.GetField(6)),
                            HousingAllowance = ParseDecimal(csv.GetField("HOUSING ALLOWANCE") ?? csv.GetField(7)),
                            TransportAllowance = ParseDecimal(csv.GetField("TRANSPORT ALLOWANCE") ?? csv.GetField(8)),
                            PermanentDisabilityRate = ParseDecimal(csv.GetField("PERM. DISABILITY RATE (%)") ?? csv.GetField(9)),
                            TemporaryDisabilityRate = ParseDecimal(csv.GetField("TEMPORARY DISABILITY RATE (%)") ?? csv.GetField(10)),
                            MedicalExpensesRate = ParseDecimal(csv.GetField("MEDICAL EXPENSES RATE (%)") ?? csv.GetField(12)),
                            FuneralExtensionRate = ParseDecimal(csv.GetField("FUNERAL EXTENTION RATE (%)") ?? csv.GetField(13)),
                            CriticalIllnessRate = ParseDecimal(csv.GetField("CRITICAL ILLNESS RATE (%)") ?? csv.GetField(14)),
                            HireDate = ParseDateTime(csv.GetField("HIRE DATE") ?? csv.GetField(16))
                        };

                        // Try to get Date of Birth from multiple possible columns
                        var dobString = csv.GetField("DATE OF BIRTH") ?? csv.GetField("DOB") ?? csv.GetField(4) ?? csv.GetField(15);
                        member.DateOfBirth = ParseDateTime(dobString);

                        // Validate required fields
                        if (!string.IsNullOrEmpty(member.EmployeeNames) &&
                            member.DateOfBirth != DateTime.MinValue &&
                            member.BasicSalary > 0)
                        {
                            members.Add(member);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing row: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading CSV file: {ex.Message}");
            }

            return members;
        }

        public async Task<List<MemberDetailsDto>> ImportMembersFromExcelAsync(IFormFile file)
        {
            var members = new List<MemberDetailsDto>();

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);

                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets[0]; // Get first worksheet

                if (worksheet.Dimension == null)
                {
                    throw new Exception("The Excel file appears to be empty.");
                }

                var rowCount = worksheet.Dimension.Rows;
                var colCount = worksheet.Dimension.Columns;

                // Read header row to understand column positions
                var headers = new Dictionary<string, int>();
                for (int col = 1; col <= colCount; col++)
                {
                    var header = worksheet.Cells[1, col].Value?.ToString()?.Trim().ToUpper();
                    if (!string.IsNullOrEmpty(header))
                    {
                        headers[header] = col;
                    }
                }

                // Process data rows (starting from row 2)
                for (int row = 2; row <= rowCount; row++)
                {
                    try
                    {
                        var member = new MemberDetailsDto
                        {
                            StaffNo = GetCellValue(worksheet, row, GetColumnIndex(headers, "STAFF NO", 2)),
                            EmployeeNames = GetCellValue(worksheet, row, GetColumnIndex(headers, "EMPLOYEENAMES", 3)),
                            Sex = GetCellValue(worksheet, row, GetColumnIndex(headers, "SEX", 4)),
                            BasicSalary = ParseDecimal(GetCellValue(worksheet, row, GetColumnIndex(headers, "BASIC SALARY", 7))),
                            HousingAllowance = ParseDecimal(GetCellValue(worksheet, row, GetColumnIndex(headers, "HOUSING ALLOWANCE", 8))),
                            TransportAllowance = ParseDecimal(GetCellValue(worksheet, row, GetColumnIndex(headers, "TRANSPORT ALLOWANCE", 9))),
                            PermanentDisabilityRate = ParseDecimal(GetCellValue(worksheet, row, GetColumnIndex(headers, "PERM. DISABILITY RATE (%)", 10))),
                            TemporaryDisabilityRate = ParseDecimal(GetCellValue(worksheet, row, GetColumnIndex(headers, "TEMPORARY DISABILITY RATE (%)", 11))),
                            MedicalExpensesRate = ParseDecimal(GetCellValue(worksheet, row, GetColumnIndex(headers, "MEDICAL EXPENSES RATE (%)", 13))),
                            FuneralExtensionRate = ParseDecimal(GetCellValue(worksheet, row, GetColumnIndex(headers, "FUNERAL EXTENTION RATE (%)", 14))),
                            CriticalIllnessRate = ParseDecimal(GetCellValue(worksheet, row, GetColumnIndex(headers, "CRITICAL ILLNESS RATE (%)", 15))),
                            HireDate = ParseDateTime(GetCellValue(worksheet, row, GetColumnIndex(headers, "HIRE DATE", 17)))
                        };

                        // Try to get Date of Birth from multiple possible columns
                        var dobValue = GetCellValue(worksheet, row, GetColumnIndex(headers, "DOB", 5)) ??
                                      GetCellValue(worksheet, row, GetColumnIndex(headers, "DATE OF BIRTH", 16));
                        member.DateOfBirth = ParseDateTime(dobValue);

                        // Validate required fields
                        if (!string.IsNullOrEmpty(member.EmployeeNames) &&
                            member.DateOfBirth != DateTime.MinValue &&
                            member.BasicSalary > 0)
                        {
                            members.Add(member);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing row {row}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading Excel file: {ex.Message}");
            }

            return members;
        }

        private int GetColumnIndex(Dictionary<string, int> headers, string headerName, int defaultIndex)
        {
            return headers.ContainsKey(headerName.ToUpper()) ? headers[headerName.ToUpper()] : defaultIndex;
        }

        private string GetCellValue(ExcelWorksheet worksheet, int row, int col)
        {
            try
            {
                return worksheet.Cells[row, col].Value?.ToString()?.Trim() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;

            // Remove percentage sign if present
            value = value.Replace("%", "").Trim();

            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) ? result : 0;
        }

        private DateTime ParseDateTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DateTime.MinValue;

            // Try multiple date formats
            var formats = new[]
            {
                "yyyy-MM-dd",
                "dd/MM/yyyy",
                "MM/dd/yyyy",
                "dd-MM-yyyy",
                "yyyy/MM/dd",
                "dd/MM/yy",
                "MM/dd/yy"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                {
                    return result;
                }
            }

            // Try general parsing as fallback
            return DateTime.TryParse(value, out DateTime generalResult) ? generalResult : DateTime.MinValue;
        }

        // ============================================================================
        // GROUP LIFE LISTING (Policies, Tag 'GL')
        // ============================================================================

        public async Task<Domain.PagedResult<Policy>> GetAllGLAsync(int page, int pageSize, string tag = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.Policies.AsQueryable();

            // Apply tag filter if provided (supports comma-separated tags)
            if (!string.IsNullOrWhiteSpace(tag))
            {
                var tags = tag.Split(',')
                              .Select(t => t.Trim())
                              .Where(t => !string.IsNullOrEmpty(t))
                              .ToArray();

                if (tags.Length == 1)
                {
                    var single = tags[0];
                    query = query.Where(q => q.SubRiskID != null && q.SubRiskID.Contains(single));
                }
                else if (tags.Length > 1)
                {
                    query = query.Where(q => q.SubRiskID != null && tags.Any(t => q.SubRiskID.Contains(t)));
                }
            }

            query = query.OrderByDescending(q => q.SubmittedOn);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new Domain.PagedResult<Policy>
            {
                TotalCount = total,
                Items = items
            };
        }

        // ============================================================================
        // GROUP LIFE QUOTATIONS BY AGENT CODE (Tag 'GL')
        // ============================================================================

        public async Task<List<Policy>> GetGroupLifeQuotationsByAgentCodeAsync(string agentCode)
        {
            if (string.IsNullOrWhiteSpace(agentCode))
                throw new ArgumentNullException(nameof(agentCode));

            return await _db.Policies
                .Where(q => q.Field5 == agentCode && q.SubRiskID == "GL")
                .OrderByDescending(q => q.SubmittedOn)
                .ToListAsync();
        }

        public async Task<Domain.PagedResult<Policy>> GetGroupLifeQuotationsByAgentCodePagedAsync(
            string agentCode,
            int page,
            int pageSize)
        {
            if (string.IsNullOrWhiteSpace(agentCode))
                throw new ArgumentNullException(nameof(agentCode));

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.Policies
                .Where(q => q.Field5 == agentCode && q.SubRiskID == "GL");

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(q => q.SubmittedOn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new Domain.PagedResult<Policy>
            {
                TotalCount = totalCount,
                Items = items
            };
        }

        // ============================================================================
        // GROUP LIFE QUOTATIONS BY INSURED ID (Tag 'GL')
        // ============================================================================

        public async Task<List<Policy>> GetGroupLifeQuotationsByInsuredIdAsync(string insuredId)
        {
            if (string.IsNullOrWhiteSpace(insuredId))
                throw new ArgumentNullException(nameof(insuredId));

            return await _db.Policies
                .Where(q => q.InsuredID == insuredId && q.SubRiskID == "GL")
                .OrderByDescending(q => q.SubmittedOn)
                .ToListAsync();
        }

        public async Task<Domain.PagedResult<Policy>> GetGroupLifeQuotationsByInsuredIdPagedAsync(
            string insuredId,
            int page,
            int pageSize)
        {
            if (string.IsNullOrWhiteSpace(insuredId))
                throw new ArgumentNullException(nameof(insuredId));

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.Policies
                .Where(q => q.InsuredID == insuredId && q.SubRiskID == "GL");

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(q => q.SubmittedOn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new Domain.PagedResult<Policy>
            {
                TotalCount = totalCount,
                Items = items
            };
        }

        // ============================================================================
        // GROUP LIFE PREMIUM HELPERS (from the other partial)
        // NOTE: these are not wired into the ComputeQuotationAsync flow above,
        // which uses GetRateAsync + CalculateExtensionPremium instead.
        // ============================================================================

        public static class PremiumCalculator
        {
            public static decimal CalculateSumAssured(decimal groupFactor, decimal emolument)
                => groupFactor * emolument;

            public static decimal CalculatePremium(decimal sumAssured, decimal rate)
                => (sumAssured * rate) / 1000;

            public static decimal ApplyLoading(decimal premium, decimal? loadingPercent) =>
                loadingPercent.HasValue ? premium * (loadingPercent.Value / 100m) : 0;

            public static decimal ApplyDiscount(decimal premium, decimal? discountPercent) =>
                discountPercent.HasValue ? premium * (discountPercent.Value / 100m) : 0;

            public static decimal CalculateExtensionPremium(decimal sumAssured, decimal? rate, int? coverDays = 365) =>
                rate.HasValue ? ((sumAssured * rate.Value) / 1000m) * (coverDays.Value / 365m) : 0;
        }

        public (decimal basePremium, decimal pdePrem, decimal ttdPrem, decimal mePrem, decimal funPrem, decimal crlPrem)
        ComputeMemberPremiums(decimal sumAssured, decimal premiumRate, decimal? pdeRate, decimal? ttdRate, decimal? meRate, decimal? funRate, decimal? crlRate, int? coverDays, decimal? loadingPercent, decimal? reductionPercent)
        {
            decimal prorata = coverDays.HasValue && coverDays.Value > 0 && coverDays.Value < 365
                ? (decimal)coverDays.Value / 365m
                : 1m;

            decimal basePrem = (sumAssured * premiumRate) / 1000m;
            basePrem *= prorata;

            decimal pdePrem = pdeRate.HasValue ? ((sumAssured * pdeRate.Value) / 1000m) * prorata : 0m;
            decimal ttdPrem = ttdRate.HasValue ? ((sumAssured * ttdRate.Value) / 1000m) * prorata : 0m;
            decimal mePrem = meRate.HasValue ? ((sumAssured * meRate.Value) / 1000m) * prorata : 0m;
            decimal funPrem = funRate.HasValue ? ((sumAssured * funRate.Value) / 1000m) * prorata : 0m;
            decimal crlPrem = crlRate.HasValue ? ((sumAssured * crlRate.Value) / 1000m) * prorata : 0m;

            decimal total = basePrem + pdePrem + ttdPrem + mePrem + funPrem + crlPrem;

            if (loadingPercent.HasValue && loadingPercent.Value != 0)
                total *= (1m + (loadingPercent.Value / 100m));

            if (reductionPercent.HasValue && reductionPercent.Value != 0)
                total *= (1m - (reductionPercent.Value / 100m));

            decimal adjustment = total - (basePrem + pdePrem + ttdPrem + mePrem + funPrem + crlPrem);
            basePrem += adjustment;

            return (decimal.Round(basePrem, 2), decimal.Round(pdePrem, 2), decimal.Round(ttdPrem, 2), decimal.Round(mePrem, 2), decimal.Round(funPrem, 2), decimal.Round(crlPrem, 2));
        }
    }
}