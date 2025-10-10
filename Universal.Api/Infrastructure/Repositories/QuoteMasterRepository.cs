using CsvHelper;
using CsvHelper.Configuration;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Data.Repositories
{

    public partial class Repository
    {

        public async Task<QuotesMaster> CreateQuoteAsync(QuotesMaster quote)
        {
            _db.QuotesMaster.Add(quote);
            await _db.SaveChangesAsync();
            return quote;
        }

        public async Task<QuotesMaster> GetQuoteByNumberAsync(string quoteNumber)
        {
            return await _db.QuotesMaster
                                .AsNoTracking()

                .FirstOrDefaultAsync(q => q.QuoteNo == quoteNumber);
        }

        // Fix for the UpdateQuoteAsync method to handle tracking issues
        public async Task<QuotesMaster> UpdateQuoteAsync(QuotesMaster quote)
        {
            // Check if entity is already tracked
            var trackedEntity = _db.Entry(quote);

            if (trackedEntity.State == EntityState.Detached)
            {
                // If detached, attach and mark as modified
                _db.QuotesMaster.Attach(quote);
                _db.Entry(quote).State = EntityState.Modified;
            }
            else
            {
                // If already tracked, just update the properties
                _db.QuotesMaster.Update(quote);
            }

            await _db.SaveChangesAsync();
            return quote;
        }

        // Alternative UpdateQuoteAsync using Find and property updating
        public async Task<QuotesMaster> UpdateQuoteAsyncSafe(QuotesMaster quote)
        {
            // Find the existing entity (this will track it if not already tracked)
            var existingEntity = await _db.QuotesMaster.FindAsync(quote.QuoteID);

            if (existingEntity == null)
            {
                throw new InvalidOperationException($"QuotesMaster with ID {quote.QuoteID} not found");
            }

            // Update properties of the existing tracked entity
            _db.Entry(existingEntity).CurrentValues.SetValues(quote);

            await _db.SaveChangesAsync();
            return existingEntity;
        }

        public async Task<bool> DeleteQuoteAsync(long quoteId)
        {
            var quote = await _db.QuotesMaster.FindAsync(quoteId);
            if (quote != null)
            {
                _db.QuotesMaster.Remove(quote);
                await _db.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<string> GenerateQuoteNumberAsync()
        {
            var lastQuote = GetNextAutoNumber("OTHERS", "ALL");

           
            return GetNextAutoNumber("OTHERS", "ALL");
        }

        public async Task<QuoteMember> CreateMemberAsync(QuoteMember member)
        {
            _db.QuoteMembers.Add(member);
            await _db.SaveChangesAsync();
            return member;
        }

        public async Task<List<QuoteMember>> CreateMembersAsync(List<QuoteMember> members)
        {
            _db.QuoteMembers.AddRange(members);
            await _db.SaveChangesAsync();
            return members;
        }

        public async Task<List<QuoteMember>> GetMembersByQuotationAsync(string quotationNumber)
        {
            return await _db.QuoteMembers
                .Where(m => m.QuotationNo == quotationNumber)
                .ToListAsync();
        }

        public async Task<QuoteMember> GetMemberByIdAsync(long memberId)
        {
            return await _db.QuoteMembers.FindAsync(memberId);
        }

        public async Task<QuoteMember> UpdateMemberAsync(QuoteMember member)
        {
            _db.QuoteMembers.Update(member);
            await _db.SaveChangesAsync();
            return member;
        }

        public async Task<bool> DeleteMemberAsync(long memberId)
        {
            var member = await _db.QuoteMembers.FindAsync(memberId);
            if (member != null)
            {
                _db.QuoteMembers.Remove(member);
                await _db.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> DeleteMembersByQuotationAsync(string quotationNumber)
        {
            var members = await _db.QuoteMembers
                .Where(m => m.QuotationNo == quotationNumber)
                .ToListAsync();

            foreach (var member in members)
            {
                _db.QuoteMembers.Remove(member);
            }

            await _db.SaveChangesAsync();
            return true;
        }


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


        public async Task<QuotationResponseDto> CreateQuotationAsync(QuotationRequestDto request, string currentUser)
        {
            // Generate quotation number if not provided
            if (string.IsNullOrEmpty(request.QuotationNumber))
            {
                request.QuotationNumber = await GenerateQuoteNumberAsync();
            }

            // Compute quotation
            var computation = await ComputeQuotationAsync(request);

            // Create QuotesMaster entity
            var quoteMaster = MapToQuoteMaster(request, computation, currentUser);
            await CreateQuoteAsync(quoteMaster);

            // Create QuoteMember entities
            var members = await CreateQuoteMembersAsync(request, computation, currentUser);
            await CreateMembersAsync(members);

            return computation;
        }

        public async Task<QuotationResponseDto> GetQuotationAsync(string quotationNumber)
        {
            var quoteMaster = await GetQuoteByNumberAsync(quotationNumber);
            if (quoteMaster == null)
                return null;

            var members = await GetMembersByQuotationAsync(quotationNumber);

            return MapToQuotationResponse(quoteMaster, members);
        }

        public async Task<QuotationResponseDto> UpdateQuotationAsync(string quotationNumber, QuotationRequestDto request, string currentUser)
        {
            var existingQuote = await GetQuoteByNumberAsync(quotationNumber);
            if (existingQuote == null)
                return null;

            try
            {
                // Ensure quotation number consistency
                request.QuotationNumber = quotationNumber;

                // Compute updated quotation
                var computation = await ComputeQuotationAsync(request);

                // Update properties of the EXISTING tracked entity instead of creating a new one
                existingQuote.TDate = request.TransactionDate;
                existingQuote.StartDate = request.EffectiveDate;
                existingQuote.MaturityDate = request.ExpiryDate;
                existingQuote.Covertype = request.SchemeType;
                existingQuote.SurName = request.ClientName;
                existingQuote.SumAssured = computation.TotalSumInsured;
                existingQuote.BasicPremium = computation.TotalBasicPremium;
                existingQuote.TotalPremium = computation.NetPremium;
                existingQuote.Loading = request.LoadingRate;
                existingQuote.A1 = (double?)request.ReductionRate;
                existingQuote.ModifiedBy = currentUser;
                existingQuote.ModifiedOn = DateTime.Now;

                // Save changes to the existing tracked entity
                await _db.SaveChangesAsync();

                // Delete existing members and create new ones
                await DeleteMembersByQuotationAsync(quotationNumber);
                var members = await CreateQuoteMembersAsync(request, computation, currentUser);
                await CreateMembersAsync(members);

                return computation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating quotation {quotationNumber}: {ex.Message}");
                throw;
            }
        }

        // Solution 2: Detach the existing entity before updating (Alternative approach)
        public async Task<QuotationResponseDto> UpdateQuotationAsyncAlternative(string quotationNumber, QuotationRequestDto request, string currentUser)
        {
            var existingQuote = await GetQuoteByNumberAsync(quotationNumber);
            if (existingQuote == null)
                return null;

            try
            {
                // Detach the existing entity from tracking
                _db.Entry(existingQuote).State = EntityState.Detached;

                // Ensure quotation number consistency
                request.QuotationNumber = quotationNumber;

                // Compute updated quotation
                var computation = await ComputeQuotationAsync(request);

                // Create updated entity
                var updatedQuoteMaster = MapToQuoteMaster(request, computation, currentUser);
                updatedQuoteMaster.QuoteID = existingQuote.QuoteID;
                updatedQuoteMaster.ModifiedBy = currentUser;
                updatedQuoteMaster.ModifiedOn = DateTime.Now;

                // Preserve original creation info
                updatedQuoteMaster.SubmittedBy = existingQuote.SubmittedBy;
                updatedQuoteMaster.SubmittedOn = existingQuote.SubmittedOn;

                // Now update the detached entity
                await UpdateQuoteAsync(updatedQuoteMaster);

                // Delete existing members and create new ones
                await DeleteMembersByQuotationAsync(quotationNumber);
                var members = await CreateQuoteMembersAsync(request, computation, currentUser);
                await CreateMembersAsync(members);

                return computation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating quotation {quotationNumber}: {ex.Message}");
                throw;
            }
        }

        // Solution 3: Use AsNoTracking for the initial query
public async Task<QuotationResponseDto> UpdateQuotationAsyncNoTracking(string quotationNumber, QuotationRequestDto request, string currentUser)
        {
            // Get the existing quote WITHOUT tracking it
            var existingQuote = await _db.QuotesMaster
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.QuoteNo == quotationNumber);

            if (existingQuote == null)
                return null;

            try
            {
                // Ensure quotation number consistency
                request.QuotationNumber = quotationNumber;

                // Compute updated quotation
                var computation = await ComputeQuotationAsync(request);

                // Update QuoteMaster
                var updatedQuoteMaster = MapToQuoteMaster(request, computation, currentUser);
                updatedQuoteMaster.QuoteID = existingQuote.QuoteID;
                updatedQuoteMaster.ModifiedBy = currentUser;
                updatedQuoteMaster.ModifiedOn = DateTime.Now;

                // Preserve original creation info
                updatedQuoteMaster.SubmittedBy = existingQuote.SubmittedBy;
                updatedQuoteMaster.SubmittedOn = existingQuote.SubmittedOn;

                await UpdateQuoteAsync(updatedQuoteMaster);

                // Delete existing members and create new ones
                await DeleteMembersByQuotationAsync(quotationNumber);
                var members = await CreateQuoteMembersAsync(request, computation, currentUser);
                await CreateMembersAsync(members);

                return computation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating quotation {quotationNumber}: {ex.Message}");
                throw;
            }
        }

        // Also fix the UpdateMemberAndRecalculateAsync and similar methods
        public async Task<QuotationResponseDto> UpdateMemberAndRecalculateAsync(string quotationNumber, long memberId, MemberDetailsDto memberDetails, string currentUser)
        {
            // Use AsNoTracking to avoid tracking conflicts
            var quoteMaster = await _db.QuotesMaster
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.QuoteNo == quotationNumber);

            if (quoteMaster == null) return null;

            // Get existing member
            var existingMember = await GetMemberByIdAsync(memberId);
            if (existingMember == null || existingMember.QuotationNo != quotationNumber) return null;

            // Get all current members
            var allMembers = await GetMembersByQuotationAsync(quotationNumber);

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
                    memberDtos.Add(ConvertQuoteMemberToDto(member));
                }
            }

            // Create request DTO to use existing calculation logic
            var request = await BuildQuotationRequestFromExistingAsync(quotationNumber, memberDtos);

            // Compute updated quotation
            var computation = await ComputeQuotationAsync(request);

            // Update the existing tracked entity
            var trackedQuoteMaster = await _db.QuotesMaster.FindAsync(quoteMaster.QuoteID);
            if (trackedQuoteMaster != null)
            {
                var updatedData = MapToQuoteMaster(request, computation, currentUser);

                // Update properties of tracked entity
                trackedQuoteMaster.TDate = updatedData.TDate;
                trackedQuoteMaster.StartDate = updatedData.StartDate;
                trackedQuoteMaster.MaturityDate = updatedData.MaturityDate;
                trackedQuoteMaster.Covertype = updatedData.Covertype;
                trackedQuoteMaster.SurName = updatedData.SurName;
                trackedQuoteMaster.SumAssured = updatedData.SumAssured;
                trackedQuoteMaster.BasicPremium = updatedData.BasicPremium;
                trackedQuoteMaster.TotalPremium = updatedData.TotalPremium;
                trackedQuoteMaster.Loading = updatedData.Loading;
                trackedQuoteMaster.A1 = updatedData.A1;
                trackedQuoteMaster.ModifiedBy = currentUser;
                trackedQuoteMaster.ModifiedOn = DateTime.Now;

                await _db.SaveChangesAsync();
            }

            // Delete existing members and create new ones
            await DeleteMembersByQuotationAsync(quotationNumber);
            var newMembers = await CreateQuoteMembersAsync(request, computation, currentUser);
            await CreateMembersAsync(newMembers);

            return computation;
        }

        /// <summary>
        /// Add a new member to existing quotation and recalculate
        /// </summary>
        public async Task<QuotationResponseDto> AddMemberAndRecalculateAsync(string quotationNumber, MemberDetailsDto memberDetails, string currentUser)
        {
            // Get existing quotation
            var quoteMaster = await GetQuoteByNumberAsync(quotationNumber);
            if (quoteMaster == null) return null;

            // Get all current members
            var allMembers = await GetMembersByQuotationAsync(quotationNumber);

            // Convert to DTO list and add new member
            var memberDtos = new List<MemberDetailsDto>();

            foreach (var member in allMembers)
            {
                memberDtos.Add(ConvertQuoteMemberToDto(member));
            }

            // Add the new member
            memberDtos.Add(memberDetails);

            // Create request DTO to use existing calculation logic
            var request = await BuildQuotationRequestFromExistingAsync(quotationNumber, memberDtos);

            // Compute updated quotation
            var computation = await ComputeQuotationAsync(request);

            // Update QuoteMaster
            var updatedQuoteMaster = MapToQuoteMaster(request, computation, currentUser);
            updatedQuoteMaster.QuoteID = quoteMaster.QuoteID;
            updatedQuoteMaster.ModifiedBy = currentUser;
            updatedQuoteMaster.ModifiedOn = DateTime.Now;
            await UpdateQuoteAsync(updatedQuoteMaster);

            // Delete existing members and create new ones
            await DeleteMembersByQuotationAsync(quotationNumber);
            var newMembers = await CreateQuoteMembersAsync(request, computation, currentUser);
            await CreateMembersAsync(newMembers);

            return computation;
        }

        /// <summary>
        /// Remove a member from quotation and recalculate
        /// </summary>
        public async Task<QuotationResponseDto> RemoveMemberAndRecalculateAsync(string quotationNumber, long memberId, string currentUser)
        {
            // Get existing quotation
            var quoteMaster = await GetQuoteByNumberAsync(quotationNumber);
            if (quoteMaster == null) return null;

            // Get existing member to verify it exists
            var existingMember = await GetMemberByIdAsync(memberId);
            if (existingMember == null || existingMember.QuotationNo != quotationNumber) return null;

            // Get all current members except the one to be removed
            var allMembers = await GetMembersByQuotationAsync(quotationNumber);

            // Convert to DTO list excluding the removed member
            var memberDtos = new List<MemberDetailsDto>();

            foreach (var member in allMembers.Where(m => m.MemberID != memberId))
            {
                memberDtos.Add(ConvertQuoteMemberToDto(member));
            }

            // Check if there are any members left
            if (!memberDtos.Any())
            {
                throw new InvalidOperationException("Cannot remove the last member from a quotation");
            }

            // Create request DTO to use existing calculation logic
            var request = await BuildQuotationRequestFromExistingAsync(quotationNumber, memberDtos);

            // Compute updated quotation
            var computation = await ComputeQuotationAsync(request);

            // Update QuoteMaster
            var updatedQuoteMaster = MapToQuoteMaster(request, computation, currentUser);
            updatedQuoteMaster.QuoteID = quoteMaster.QuoteID;
            updatedQuoteMaster.ModifiedBy = currentUser;
            updatedQuoteMaster.ModifiedOn = DateTime.Now;
            await UpdateQuoteAsync(updatedQuoteMaster);

            // Delete existing members and create new ones
            await DeleteMembersByQuotationAsync(quotationNumber);
            var newMembers = await CreateQuoteMembersAsync(request, computation, currentUser);
            await CreateMembersAsync(newMembers);

            return computation;
        }
        /// <summary>
        /// Build QuotationRequestDto from existing quotation data
        /// </summary>
        private async Task<QuotationRequestDto> BuildQuotationRequestFromExistingAsync(string quotationNumber, List<MemberDetailsDto> members)
        {
            var quoteMaster = await GetQuoteByNumberAsync(quotationNumber);

            // Calculate GroupLifeFactor from existing data if possible
            decimal groupLifeFactor = 3.0m; // Default
            if (members.Any() && quoteMaster?.SumAssured > 0)
            {
                var totalEmolument = members.Sum(m => m.TotalEmolument);
                if (totalEmolument > 0)
                {
                    groupLifeFactor = (quoteMaster.SumAssured ?? 0) / totalEmolument;
                }
            }

            return new QuotationRequestDto
            {
                QuotationNumber = quotationNumber,
                TransactionDate = quoteMaster?.TDate ?? DateTime.Now,
                EffectiveDate = quoteMaster?.StartDate ?? DateTime.Now,
                ExpiryDate = quoteMaster?.MaturityDate ?? DateTime.Now.AddYears(1),
                InsuredType = "GROUP",
                SchemeType = quoteMaster?.Covertype ?? "GROUP LIFE ONLY",
                ClientName = quoteMaster?.SurName ?? "",
                GroupLifeFactor = groupLifeFactor,
                GroupFactorAmount = groupLifeFactor,
                // ✅ FIXED: Retrieve the original settings from database
                UseFixedRate = quoteMaster?.A2 == 1,
                FixedRate = quoteMaster?.A2 == 1 ? (quoteMaster?.IntrestRate ?? 0) : 0,
                UseAverageAge = quoteMaster?.XMas == 1,
                AverageAge = (int)(quoteMaster?.XMas == 1 ? (quoteMaster?.ADBSA ?? 0) : 0),
                LoadingRate = (decimal)(quoteMaster?.Loading ?? 0),
                ReductionRate = (decimal)(quoteMaster?.A1 ?? 0),
                CoverDays = 365,
                Members = members
            };
        }


        /// <summary>
        /// Convert QuoteMember entity to MemberDetailsDto
        /// </summary>
        private MemberDetailsDto ConvertQuoteMemberToDto(QuoteMember member)
        {
            return new MemberDetailsDto
            {
                StaffNo = member.MembersNo ,
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
                FuneralExtensionRate = member.A6 ?? 0,
                CriticalIllnessRate = member.A8 ?? 0
            };
        }


        public async Task<bool> DeleteQuotationAsync(string quotationNumber)
        {
            var quoteMaster = await GetQuoteByNumberAsync(quotationNumber);
            if (quoteMaster == null)
                return false;

            await DeleteMembersByQuotationAsync(quotationNumber);
            await DeleteQuoteAsync(quoteMaster.QuoteID);

            return true;
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


        // Fix 2: Enhanced ComputeMemberPremiumAsync with proper null handling
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

        // Fix #9: Extension Premium Calculation
        // Improved CalculateExtensionPremium method

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
            // Use decimal division for precision
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
        private QuotesMaster MapToQuoteMaster(QuotationRequestDto request, QuotationResponseDto computation, string currentUser)
        {
            return new QuotesMaster
            {
                QuoteNo = request.QuotationNumber,
                TDate = request.TransactionDate,
                StartDate = request.EffectiveDate,
                MaturityDate = request.ExpiryDate,
                Covertype = request.SchemeType,
                SurName = request.ClientName,
                SumAssured = computation.TotalSumInsured,
                BasicPremium = computation.TotalBasicPremium,
                
                TotalPremium = computation.NetPremium,
                Loading = request.LoadingRate,
                A1= (double?)request.ReductionRate,
                // Fix for CS0029: Cannot implicitly convert type 'bool' to 'double?'
                // The issue is that the property `A2` expects a `double?` value, but `request.UseFixedRate` is a `bool`.
                // To fix this, we need to convert the `bool` to a `double?` appropriately.

                A2 = request.UseFixedRate ? 1 : 0, // Store UseFixedRate as 1/0
                IntrestRate = request.UseFixedRate ? (decimal?)request.FixedRate : null, // Store FixedRate
                XMas = request.UseAverageAge ? 1 : 0,
                ADBSA = request.UseAverageAge ? (decimal?)request.AverageAge : null, // Store AverageAge
                SUPPSA = request.CoverDays,
                TPDRate = 0, // Individual rates are stored per member
                TTDRAte = 0,
                MedicalRate = 0,
                FuneralRate = 0,
                CIRate = 0,
                isQuote = 1,
                TransSTATUS = "PENDING",
                Status = "ACTIVE",
                SubmittedBy = currentUser,
                SubmittedOn = DateTime.Now,
                ModifiedBy = currentUser,
                ModifiedOn = DateTime.Now,
                Tag = "GL"
            };
        }

        private async Task<List<QuoteMember>> CreateQuoteMembersAsync(QuotationRequestDto request, QuotationResponseDto computation, string currentUser)
        {
            var members = new List<QuoteMember>();
            int serialNo = 1;

            foreach (var memberCalc in computation.MemberCalculations)
            {
                var memberDto = request.Members.FirstOrDefault(m =>
            m.EmployeeNames == memberCalc.EmployeeNames && m.DateOfBirth == memberCalc.DateOfBirth);

                var generatedMemberNo = GetNextAutoNumber("INSURED", "01");


                var member = new QuoteMember
                {
                    QuotationNo = request.QuotationNumber,
                    SerialNo = serialNo++.ToString(),
                    Surname = ExtractSurname(memberCalc.EmployeeNames),
                    OtherNames = ExtractOtherNames(memberCalc.EmployeeNames),
                    Age = memberCalc.ANB,
                    TEmolument = memberCalc.TotalEmolument,
                    SumInsured = memberCalc.SumAssured,
                    PremiumRate = (double)memberCalc.PremiumRate,
                    GrossPremium = memberCalc.ActualPremium,

                    // FIXED: Properly handle nullable decimals with null coalescing
                    PDERate = (double)(memberDto?.PermanentDisabilityRate ?? 0),
                    PDEPremium = memberCalc.PDEPremium,
                    TDRate = (double)(memberDto?.TemporaryDisabilityRate ?? 0),
                    TDPremium = memberCalc.TDEPremium,
                    MERate = (double)(memberDto?.MedicalExpensesRate ?? 0),
                    MEPremium = memberCalc.MEPremium,
                    A6 = memberDto?.FuneralExtensionRate ?? 0, // FuneralRate
                    A7 = memberCalc.FUNPremium, // FuneralPremium
                    A8 = memberDto?.CriticalIllnessRate ?? 0, // CriticalRate
                    A9 = memberCalc.CRLPremium, // CriticalPremium

                    BasicSalary = memberCalc.BasicSalary,
                    Housing = memberCalc.HousingAllowance,
                    Transport = memberCalc.TransportAllowance,
                    OtherAmount = 0,
                    DOBDate = memberCalc.DateOfBirth,
                    HireDate = memberCalc.HireDate,
                    Sex = memberCalc.Sex,
                    MembersNo = memberCalc.StaffNo,
                    Active = 1,
                    Deleted = 0,
                    SubmittedBy = currentUser,
                    SubmittedOn = DateTime.Now,
                    ModifiedBy = currentUser,
                    ModifiedOn = DateTime.Now
                };

                members.Add(member);
            }

            return members;
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


        private QuotationResponseDto MapToQuotationResponse(QuotesMaster quoteMaster, List<QuoteMember> members)
        {
            var response = new QuotationResponseDto
            {
                QuotationNumber = quoteMaster.QuoteNo,
                TotalSumInsured = quoteMaster.SumAssured ?? 0,
                TotalBasicPremium = quoteMaster.BasicPremium ?? 0,
                NetPremium = quoteMaster.TotalPremium ?? 0 // This is already the final net premium
            };

            foreach (var member in members)
            {
                var memberCalc = new MemberCalculationDto
                {
                    StaffNo = member.MembersNo ,
                    // Fix for CS1039 and CS1003: Corrected the string interpolation syntax and method chaining.
                    EmployeeNames = $"{(member.Surname ?? " ")} {(member.OtherNames ?? " ")}".Trim(),
                    Sex = member.Sex ?? " ",
                    DateOfBirth = member.DOBDate ?? DateTime.MinValue, // Safe handling of nullable DateTime
                    ANB = (int)(member.Age ?? 0), // Safe handling of nullable long
                    BasicSalary = member.BasicSalary ?? 0, // Safe handling of nullable decimal
                    HousingAllowance = member.Housing ?? 0,
                    TransportAllowance = member.Transport ?? 0,
                    TotalEmolument = member.TEmolument ?? 0,
                    SumAssured = member.SumInsured ?? 0,
                    PremiumRate = (decimal)(member.PremiumRate ?? 0), // Safe handling of nullable double
                    ActualPremium = member.GrossPremium ?? 0,
                    PDEPremium = member.PDEPremium ?? 0,
                    TDEPremium = member.TDPremium ?? 0,
                    MEPremium = member.MEPremium ?? 0,
                    FUNPremium = member.A7 ?? 0, // Safe handling of nullable
                    CRLPremium = member.A9 ?? 0, // Safe handling of nullable
                    HireDate = member.HireDate ?? DateTime.MinValue
                };

                // Calculate total member premium safely
                memberCalc.TotalMemberPremium = (memberCalc.ActualPremium ?? 0) +
                                               (memberCalc.PDEPremium ?? 0) +
                                               (memberCalc.TDEPremium ?? 0) +
                                               (memberCalc.MEPremium ?? 0) +
                                               (memberCalc.FUNPremium ?? 0) +
                                               (memberCalc.CRLPremium ?? 0);

                response.MemberCalculations.Add(memberCalc);
            }

            // Calculate totals from member data safely
            response.TotalExtensionPremiums = response.MemberCalculations.Sum(m =>
                (m.PDEPremium ?? 0) + (m.TDEPremium ?? 0) + (m.MEPremium ?? 0) + (m.FUNPremium ?? 0) + (m.CRLPremium ?? 0));

            // FIX: Calculate loading and reduction EXACTLY as in ComputeQuotationAsync
            // Total premium BEFORE loading/reduction
            var totalPremiumBeforeAdjustments = (response.TotalBasicPremium ?? 0) + (response.TotalExtensionPremiums ?? 0);

            // Apply loading and reduction to the total premium (not net premium)
            var loadingRate = (decimal)(quoteMaster.Loading ?? 0);
            var reductionRate = (decimal)(quoteMaster.A1 ?? 0);

            response.LoadingAmount = totalPremiumBeforeAdjustments * loadingRate / 100;
            response.ReductionAmount = totalPremiumBeforeAdjustments * reductionRate / 100;


            // Verify the net premium calculation matches what's stored
            var calculatedNetPremium = totalPremiumBeforeAdjustments +
                                       (response.LoadingAmount ?? 0) -
                                       (response.ReductionAmount ?? 0);

            // Use stored value but log if there's a discrepancy
            if (Math.Abs(calculatedNetPremium - (quoteMaster.TotalPremium ?? 0)) > 0.01m)
            {
                Console.WriteLine($"Warning: Net premium mismatch for {quoteMaster.QuoteNo}. " +
                                 $"Calculated: {calculatedNetPremium:C}, Stored: {quoteMaster.TotalPremium:C}");
            }

            // Build extension summary safely
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
                        // Log the error and skip invalid rows
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
                        // Log the error and skip invalid rows
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
    }
}
