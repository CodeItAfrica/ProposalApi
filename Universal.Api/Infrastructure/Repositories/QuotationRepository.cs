using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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

            switch (config.ProductID.ToUpper())
            {
                case "SLA":
                    var multiplierValue = decimal.TryParse(config.Multiplier, out var m) ? m : 1m;
                    sumAssured = Math.Min(annualPremium * multiplierValue, config.Max_SA ?? (annualPremium * multiplierValue));
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
                    sumAssured = request.RegularSaving switch
                    {
                        >= 20000 => 500000,
                        >= 10000 => 250000,
                        >= 500 => 150000,
                        _ => 0
                    };
                    lifeCover = sumAssured;
                    break;

                case "ESP":
                    sumAssured = 250000;
                    lifeCover = sumAssured;
                    break;

                default:
                    // Use SIP calculation if no match
                    sumAssured = annualPremium * request.Duration;
                    lifeCover = sumAssured;
                    break;
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

            await SaveQuoteAsync(request, result);

            return result;
        }


        public async Task<QuotationResult> CalculateTraditionalAsync(TraditionalQuotationRequest request)
        {
            var ps = await GetByCodeAsync(request.ProductCode);
            var intRateEntity = await GetPremiumRateAsync(
                request.ProductCode,
                request.Duration,
                request.Age,
                request.RateCode
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

            var quoteNo = !string.IsNullOrEmpty(request.QuoteNo)
        ? request.QuoteNo
        : GetNextAutoNumber("OTHERS", "ALL");

            var result = new QuotationResult
            {
               QuoteNo = quoteNo,
               AnnualPremium = annPrem,
                SumAssured = sumAssured,
                LifeCover = sumAssured,
                TotalPremium = firstPremium
            };

            await SaveTradQuoteAsync(request, result);

            return result;
        }

        public async Task<QuotationResult> UpdateTraditionalAsync(int memberId, TraditionalQuotationRequest request)
        {
            // Find the member record
            var existingMember = await _db.QuoteMembers.FirstOrDefaultAsync(q => q.MemberID == memberId);
            if (existingMember == null)
                throw new KeyNotFoundException("Traditional quotation member not found.");

            var ps = await GetByCodeAsync(request.ProductCode);
            var intRateEntity = await GetPremiumRateAsync(request.ProductCode, request.Duration, request.Age, request.RateCode);
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
                if (mopFactor != 1)
                    annPrem1 = (annPrem1 * 100m) / mopFactor;

                sumAssured = (decimal)(ps.RateDiv_factor * (annPrem1 / rate));
                annPrem = annPrem1;
                firstPremium = request.FirstPremium.Value;
            }
            else if (request.SumAssured.HasValue)
            {
                sumAssured = request.SumAssured.Value;
                annPrem = (decimal)(sumAssured * (rate / ps.RateDiv_factor));
                firstPremium = mopFactor != 1 ? annPrem * mopFactor / 100m : annPrem;
            }
            else
            {
                throw new Exception("You must provide either FirstPremium or SumAssured.");
            }

            // Update the member record
            existingMember.TEmolument = request.RegularSaving;
            existingMember.SumInsured = sumAssured;
            existingMember.GrossPremium = annPrem;
            existingMember.Age = request.Age;
            existingMember.DOBDate = request.DateOfBirth;
            existingMember.Surname = request.SurName;
            existingMember.OtherNames = request.OtherNames;
            existingMember.ModifiedBy = "API";
            existingMember.ModifiedOn = DateTime.Now;

            // Update master record totals
            var master = await _db.QuotesMaster.FirstOrDefaultAsync(q => q.QuoteNo == existingMember.QuotationNo);
            if (master != null)
            {
                var allMembers = await _db.QuoteMembers
                    .Where(m => m.QuotationNo == existingMember.QuotationNo && (m.Deleted == null || m.Deleted == 0))
                    .ToListAsync();

                master.SumAssured = allMembers.Sum(m => m.SumInsured ?? 0);
                master.Premium1 = allMembers.Sum(m => m.GrossPremium ?? 0);
                master.TotalPremium = allMembers.Sum(m => m.GrossPremium ?? 0);
                master.CardNo = request.Frequency;
                master.Duration = request.Duration;
                master.ModifiedBy = "API";
                master.ModifiedOn = DateTime.Now;
            }

            await _db.SaveChangesAsync();

            return new QuotationResult
            {
                QuoteNo = existingMember.QuotationNo,
                AnnualPremium = annPrem,
                SumAssured = sumAssured,
                LifeCover = sumAssured,
                TotalPremium = firstPremium
            };
        }


        public async Task<QuotationResult> UpdateAsync(int quoteId, QuotationRequest request)
        {
            // Find the member record
            var existingMember = await _db.QuoteMembers.FirstOrDefaultAsync(q => q.MemberID == quoteId);
            if (existingMember == null)
                throw new KeyNotFoundException("Quotation member not found.");

            var config = await GetByCodeAsync(request.ProductCode);
            if (config == null)
                throw new InvalidOperationException($"No ProductSettings found for code '{request.ProductCode}'.");

            var factor = FrequencyFactor.Values[request.Frequency];
            decimal annualPremium = request.RegularSaving.GetValueOrDefault() * factor;

            decimal sumAssured = 0;
            decimal lifeCover = 0;

            switch (config.ProductID.ToUpper())
            {
                case "SLA":
                    var multiplierValue = decimal.TryParse(config.Multiplier, out var m) ? m : 1m;
                    sumAssured = Math.Min(annualPremium * multiplierValue, config.Max_SA ?? (annualPremium * multiplierValue));
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
                    sumAssured = request.RegularSaving switch
                    {
                        >= 20000 => 500000,
                        >= 10000 => 250000,
                        >= 500 => 150000,
                        _ => 0
                    };
                    lifeCover = sumAssured;
                    break;

                case "ESP":
                    sumAssured = 250000;
                    lifeCover = sumAssured;
                    break;

                default:
                    sumAssured = annualPremium * request.Duration;
                    lifeCover = sumAssured;
                    break;
            }

            decimal? loadingFreq = 0;
            decimal? extraPremium = 0;

            if (request.LoadingRate > 0)
                loadingFreq = (request.LoadingRate / 1000m) * sumAssured / factor;

            if (request.ExtraRate > 0)
                extraPremium = (sumAssured * request.ExtraRate) / 1000m;

            var totalPremium = annualPremium + extraPremium + loadingFreq;

            // Update the member record
            existingMember.TEmolument = request.RegularSaving;
            existingMember.SumInsured = sumAssured;
            existingMember.GrossPremium = annualPremium;
            existingMember.A1 = extraPremium;
            existingMember.A2 = loadingFreq;
            existingMember.ModifiedBy = "API";
            existingMember.ModifiedOn = DateTime.Now;

            // Update master record totals
            var master = await _db.QuotesMaster.FirstOrDefaultAsync(q => q.QuoteNo == existingMember.QuotationNo);
            if (master != null)
            {
                var allMembers = await _db.QuoteMembers
                    .Where(m => m.QuotationNo == existingMember.QuotationNo && (m.Deleted == null || m.Deleted == 0))
                    .ToListAsync();

                master.SumAssured = allMembers.Sum(m => m.SumInsured ?? 0);
                master.Premium1 = allMembers.Sum(m => m.GrossPremium ?? 0);
                master.Premium2 = allMembers.Sum(m => m.A1 ?? 0);
                master.Loading = allMembers.Sum(m => m.A2 ?? 0);
                master.TotalPremium = allMembers.Sum(m => (m.GrossPremium ?? 0) + (m.A1 ?? 0) + (m.A2 ?? 0));
                master.ModifiedBy = "API";
                master.ModifiedOn = DateTime.Now;
            }

            await _db.SaveChangesAsync();

            return new QuotationResult
            {
                QuoteNo = existingMember.QuotationNo,
                AnnualPremium = annualPremium,
                SumAssured = sumAssured,
                LifeCover = lifeCover,
                Loading = loadingFreq,
                ExtraPremium = extraPremium,
                TotalPremium = totalPremium
            };
        }




        public async Task<double> GetPremiumRateAsync(string productCode, long duration, long age, string rateCode)
        {
            var rate = await _db.InterestRate
                .Where(r =>
                    r.CoverCode == productCode &&
                    r.Duration == duration &&
                    r.Premtype == rateCode &&
                    r.Age == age
                )
                .FirstOrDefaultAsync();

            if (rate == null)
                throw new InvalidOperationException("Interest Rate not found for the specified criteria.");

            return rate.IntRate;
        }
        //public async Task<QuotationResult> UpdateAsync(long quoteId, QuotationRequest request)
        //{
        //    var targetRow = await _db.QuotesMaster.FindAsync(quoteId);
        //    if (targetRow == null)
        //        throw new Exception("Quote not found");

        //    var quoteNo = targetRow.QuoteNo;

        //    // 🔹 Update basic details across all rows with the same QuoteNo
        //    var relatedQuotes = await _db.QuotesMaster
        //        .Where(q => q.QuoteNo == quoteNo)
        //        .ToListAsync();

        //    foreach (var row in relatedQuotes)
        //    {
        //        row.SurName = request.SurName;
        //        row.OtherNames = request.OtherNames;
        //        row.DateofBirth = request.DateOfBirth;
        //        row.ModifiedOn = DateTime.Now;
        //    }

        //    // 🔹 Update calculation-specific fields for the target row only
        //    targetRow.CoverCode = request.ProductCode;
        //    targetRow.Duration = request.Duration;
        //    targetRow.ModifiedOn = DateTime.Now;

        //    var config = await GetByCodeAsync(request.ProductCode);
        //    var factor = FrequencyFactor.Values[request.Frequency];
        //    decimal annualPremium = request.RegularSaving.GetValueOrDefault() * factor;

        //    decimal sumAssured = 0;
        //    decimal lifeCover = 0;

        //    switch (config.ProductID.ToUpper())
        //    {
        //        case "SLA":
        //            var multiplierValue = decimal.TryParse(config.Multiplier, out var m) ? m : 1m;
        //            sumAssured = Math.Min(annualPremium * multiplierValue, config.Max_SA ?? (annualPremium * multiplierValue));
        //            lifeCover = sumAssured;
        //            break;

        //        case "IPP":
        //        case "SIP":
        //            sumAssured = annualPremium * request.Duration;
        //            lifeCover = sumAssured;
        //            break;

        //        case "FAS":
        //        case "RPAS":
        //        case "SSP":
        //            sumAssured = request.RegularSaving switch
        //            {
        //                >= 20000 => 500000,
        //                >= 10000 => 250000,
        //                >= 500 => 150000,
        //                _ => 0
        //            };
        //            lifeCover = sumAssured;
        //            break;

        //        case "ESP":
        //            sumAssured = 250000;
        //            lifeCover = sumAssured;
        //            break;

        //        default:
        //            // Use SIP calculation if no match
        //            sumAssured = annualPremium * request.Duration;
        //            lifeCover = sumAssured;
        //            break;
        //    }

        //    decimal? loadingFreq = 0;
        //    decimal? extraPremium = 0;

        //    if (request.LoadingRate > 0)
        //    {
        //        var loading = (request.LoadingRate / 1000m) * sumAssured;
        //        loadingFreq = loading / factor;
        //    }

        //    if (request.ExtraRate > 0)
        //    {
        //        extraPremium = (sumAssured * request.ExtraRate) / 1000m;
        //    }
        //    var totalPremium = annualPremium + extraPremium + loadingFreq;

        //    targetRow.SumAssured = sumAssured;
        //    targetRow.Premium1 = annualPremium;
        //    targetRow.Premium2 = extraPremium;
        //    targetRow.Premium3 = loadingFreq;
        //    targetRow.BasicPremium = annualPremium;
        //    targetRow.TotalPremium = totalPremium;

        //    await _db.SaveChangesAsync();

        //    return new QuotationResult
        //    {
        //        QuoteNo = quoteNo,
        //        AnnualPremium = annualPremium,
        //        SumAssured = sumAssured,
        //        LifeCover = lifeCover,
        //        ExtraPremium = extraPremium,
        //        Loading = loadingFreq,
        //        TotalPremium = totalPremium
        //    };
        //}
        /// <summary>
        /// Permanently delete a member (hard delete)
        /// </summary>
        public async Task PermanentlyDeleteMemberAsync(long memberId)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var member = await _db.QuoteMembers.FindAsync(memberId);

                if (member == null)
                    throw new KeyNotFoundException($"Member with ID {memberId} not found");

                var quoteNo = member.QuotationNo;

                _db.QuoteMembers.Remove(member);
                await _db.SaveChangesAsync();

                // Recalculate master totals
                await RecalculateMasterTotals(quoteNo);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

        public async Task<Domain.PagedResult<QuotesMaster>> GetAllAsync(int page, int pageSize, string tag = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.QuotesMaster.AsQueryable();

            // Apply tag filter if provided (supports comma-separated tags)
            if (!string.IsNullOrWhiteSpace(tag))
            {
                // split comma separated list, trim and remove empties
                var tags = tag.Split(',')
                              .Select(t => t.Trim())
                              .Where(t => !string.IsNullOrEmpty(t))
                              .ToArray();

                if (tags.Length == 1)
                {
                    var single = tags[0];
                    query = query.Where(q => q.Tag != null && q.Tag.Contains(single));
                }
                else if (tags.Length > 1)
                {
                    query = query.Where(q => q.Tag != null && tags.Any(t => q.Tag.Contains(t)));
                }
            }

            // ordering and paging
            query = query.OrderByDescending(q => q.SubmittedOn);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new Domain.PagedResult<QuotesMaster>
            {
                TotalCount = total,
                Items = items
            };
        }



        public async Task<QuoteWithMembersDto> GetByQuoteNoAsync(string quoteNo)
        {
            var master = await _db.QuotesMaster
                .FirstOrDefaultAsync(q => q.QuoteNo == quoteNo);

            if (master == null)
                return null;

            var members = await _db.QuoteMembers
                .Where(m => m.QuotationNo == quoteNo && (m.Deleted == null || m.Deleted == 0))
                .OrderBy(m => m.SerialNo)
                .ToListAsync();

            return new QuoteWithMembersDto
            {
                Master = master,
                Members = members
            };
        }

        public async Task<QuoteWithMembersDto> GetByIdWithMembersAsync(long quoteId)
        {
            var master = await _db.QuotesMaster.FirstOrDefaultAsync(q => q.QuoteID == quoteId);

            if (master == null)
                return null;

            var members = await _db.QuoteMembers
                .Where(m => m.QuotationNo == master.QuoteNo && (m.Deleted == null || m.Deleted == 0))
                .OrderBy(m => m.SerialNo)
                .ToListAsync();

            return new QuoteWithMembersDto
            {
                Master = master,
                Members = members
            };
        }

        public async Task<QuotesMaster?> GetByIdAsync(long quoteId)
        {
            return await _db.QuotesMaster.FirstOrDefaultAsync(q => q.QuoteID == quoteId);
        }

        public async Task DeleteByIdAsync(long quoteId)
        {
            var quote = await _db.QuotesMaster.FindAsync(quoteId);
            if (quote != null)
            {
                _db.QuotesMaster.Remove(quote);

                // Also delete associated members
                var members = await _db.QuoteMembers
                    .Where(m => m.QuotationNo == quote.QuoteNo)
                    .ToListAsync();

                _db.QuoteMembers.RemoveRange(members);

                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteByQuoteNoAsync(string quoteNo)
        {
            var quote = await _db.QuotesMaster
                .FirstOrDefaultAsync(q => q.QuoteNo == quoteNo);

            if (quote != null)
            {
                _db.QuotesMaster.Remove(quote);

                var members = await _db.QuoteMembers
                    .Where(m => m.QuotationNo == quoteNo)
                    .ToListAsync();

                _db.QuoteMembers.RemoveRange(members);

                await _db.SaveChangesAsync();
            }
        }




        public async Task SaveTradQuoteAsync(TraditionalQuotationRequest request, QuotationResult result)
        {
            // Check if quote master already exists
            var existingMaster = await _db.QuotesMaster
                .FirstOrDefaultAsync(q => q.QuoteNo == result.QuoteNo);

            if (existingMaster == null)
            {
                // Create new master record
                var quoteMaster = new QuotesMaster
                {
                    QuoteNo = result.QuoteNo,
                    ProposalNo = result.QuoteNo,
                    BrCode = request.BranchID,
                    TDate = request.QuotationDate,
                    SurName = request.SurName,
                    OtherNames = request.OtherNames,
                    DateofBirth = request.DateOfBirth,
                    MobilePhone = request.MobileNumber,
                    Email = request.Email,
                    Occupation = request.AgentCode,
                    Duration = request.Duration,
                    StartDate = request.QuotationDate,
                    MaturityDate = DateTime.Today.AddYears((int)request.Duration),
                    CoverCode = request.ProductCode,
                    Covertype = request.Product,
                    CardNo = request.Frequency,
                    AssDate = DateTime.Now,
                    SumAssured = result.SumAssured,
                    Premium1 = result.AnnualPremium,
                    TotalPremium = result.TotalPremium,
                    BasicPremium = result.AnnualPremium,
                    isQuote = 0,
                    SubmittedBy = "system",
                    SubmittedOn = DateTime.Now,
                    ModifiedOn = DateTime.Now,
                    TransGUID = Guid.NewGuid().ToString(),
                    Status = request.Status,
                    Tag = "A"
                };

                _db.QuotesMaster.Add(quoteMaster);
            }

            // Add member record
            var serialNo = await _db.QuoteMembers
                .Where(m => m.QuotationNo == result.QuoteNo)
                .CountAsync() + 1;

            var quoteMember = new QuoteMember
            {
                QuotationNo = result.QuoteNo,
                SerialNo = serialNo.ToString(),
                Surname = request.SurName,
                OtherNames = request.OtherNames,
                DOBDate = request.DateOfBirth,
                Age = request.Age,
                TEmolument = request.RegularSaving,
                SumInsured = result.SumAssured,
                GrossPremium = result.AnnualPremium,
                Field10 = request.Status,
                SubmittedBy = "system",
                SubmittedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                Active = 1,
                Deleted = 0
            };

            _db.QuoteMembers.Add(quoteMember);
            await _db.SaveChangesAsync();
            // CRITICAL: Recalculate master totals
            await RecalculateMasterTotals(result.QuoteNo);
        }
        public async Task SaveQuoteAsync(QuotationRequest request, QuotationResult result)
        {
            // Check if quote master already exists
            var existingMaster = await _db.QuotesMaster
                .FirstOrDefaultAsync(q => q.QuoteNo == result.QuoteNo);

            if (existingMaster == null)
            {
                // Create new master record
                var quoteMaster = new QuotesMaster
                {
                    QuoteNo = result.QuoteNo,
                    ProposalNo = result.QuoteNo,
                    BrCode = request.BranchID,
                    TDate = request.QuotationDate,
                    Occupation = request.AgentCode,
                    SurName = request.SurName,
                    OtherNames = request.OtherNames,
                    DateofBirth = request.DateOfBirth,
                    MobilePhone = request.MobileNumber,
                    Email = request.Email,
                    Duration = request.Duration,
                    StartDate = request.QuotationDate,
                    MaturityDate = DateTime.Today.AddYears((int)request.Duration),
                    CoverCode = request.ProductCode,
                    Covertype = request.Product,
                    CardNo = request.Frequency,
                    AssDate = DateTime.Now,
                    SumAssured = result.SumAssured,
                    Premium1 = result.AnnualPremium,
                    Premium2 = result.ExtraPremium,
                    Loading = result.Loading,
                    TotalPremium = result.TotalPremium,
                    BasicPremium = result.AnnualPremium,
                    isQuote = 0,
                    SubmittedBy = "system",
                    A1 = (double?)request.LoadingRate,
                    A2 = (double?)request.ExtraRate,
                    SubmittedOn = DateTime.Now,
                    ModifiedOn = DateTime.Now,
                    TransGUID = Guid.NewGuid().ToString(),
                    Status = request.Status,
                    Tag = "B"
                };

                _db.QuotesMaster.Add(quoteMaster);
            }

            // Add member record
            var serialNo = await _db.QuoteMembers
                .Where(m => m.QuotationNo == result.QuoteNo)
                .CountAsync() + 1;

            var quoteMember = new QuoteMember
            {
                QuotationNo = result.QuoteNo,
                SerialNo = serialNo.ToString(),
                Surname = request.SurName,
                OtherNames = request.OtherNames,
                DOBDate = request.DateOfBirth,
                TEmolument = request.RegularSaving,
                SumInsured = result.SumAssured,
                GrossPremium = result.AnnualPremium,
                A1 = result.ExtraPremium,
                A2 = result.Loading,
                Field10 = request.Status,
                SubmittedBy = "system",
                SubmittedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                Active = 1,
                Deleted = 0
            };

            _db.QuoteMembers.Add(quoteMember);
            await _db.SaveChangesAsync();

            // CRITICAL: Recalculate master totals
            await RecalculateMasterTotals(result.QuoteNo);
        }

        private async Task RecalculateMasterTotals(string quoteNo)
        {
            var master = await _db.QuotesMaster.FirstOrDefaultAsync(q => q.QuoteNo == quoteNo);
            if (master == null) return;

            var members = await _db.QuoteMembers
                .Where(m => m.QuotationNo == quoteNo && (m.Deleted == null || m.Deleted == 0))
                .ToListAsync();

            master.SumAssured = members.Sum(m => m.SumInsured ?? 0);
            master.Premium1 = members.Sum(m => m.GrossPremium ?? 0);
            master.Premium2 = members.Sum(m => m.A1 ?? 0);
            master.Loading = members.Sum(m => m.A2 ?? 0);
            master.TotalPremium = members.Sum(m => (m.GrossPremium ?? 0) + (m.A1 ?? 0) + (m.A2 ?? 0));

            await _db.SaveChangesAsync();
        }


        public async Task<IEnumerable<ProductSettings>> GetAllProductAsync()
        {
            return await _db.ProductSettings.ToListAsync();
        }

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

        public async Task<IEnumerable<Branch>> GetAllBranchesAsync()
        {
            return await _db.Branches.ToListAsync();
        }


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

            // Apply loading/reduction on total premium for the member
            decimal total = basePrem + pdePrem + ttdPrem + mePrem + funPrem + crlPrem;

            if (loadingPercent.HasValue && loadingPercent.Value != 0)
                total *= (1m + (loadingPercent.Value / 100m));

            if (reductionPercent.HasValue && reductionPercent.Value != 0)
                total *= (1m - (reductionPercent.Value / 100m));

            // Split back proportionally to retain transparency (optional). For simplicity, adjust base premium only.
            // If you want proportional adjustment, update below.
            decimal adjustment = total - (basePrem + pdePrem + ttdPrem + mePrem + funPrem + crlPrem);
            basePrem += adjustment;

            return (decimal.Round(basePrem, 2), decimal.Round(pdePrem, 2), decimal.Round(ttdPrem, 2), decimal.Round(mePrem, 2), decimal.Round(funPrem, 2), decimal.Round(crlPrem, 2));
        }

        //public async Task<decimal> GetUnderwritingRateAsync(string coverCode, int anb)
        //{
        //    return await _db.UndPremRates
        //        .Where(r => r.CoverCode == coverCode && r.Yr == anb)
        //        .OrderByDescending(r => r.FromDate)
        //        .Select(r => r.Rate)
        //        .FirstOrDefaultAsync();
        //}

        //public async Task<decimal?> GetUnderwritingRateAsync(string productCode, int age, DateTime effectiveDate, CancellationToken ct = default)
        //{
        //    var query = _db.UndPremRates.AsNoTracking()
        //        .Where(r => r.CoverCode == productCode && r.Yr == age);

        //    // filter by effective date if From/To present
        //    query = query.Where(r =>
        //        (!r.FromDate.HasValue || r.FromDate.Value.Date <= effectiveDate.Date) &&
        //        (!r.ToDate.HasValue || r.ToDate.Value.Date >= effectiveDate.Date));

        //    var row = await query
        //        .OrderByDescending(r => r.ModifiedOn ?? r.SubmittedOn ?? r.FromDate)
        //        .FirstOrDefaultAsync(ct);

        //    return row?.Rate;
        //}

        //public async Task SaveQuotationAsync(QuotesMaster quote)
        //{
        //    _db.QuotesMaster.Add(quote);
        //    await _db.SaveChangesAsync();
        //}


        //public async Task<string> CreateQuotationAsync(GroupLifeQuotationRequest request, string submittedBy)
        //{
        //    var quoteNo = $"Q-{DateTime.Now.Ticks}";
        //    var quoteMaster = new QuotesMaster
        //    {
        //        QuoteNo = quoteNo,
        //        TransactionDate = request.TransactionDate,
        //        EffectiveDate = request.EffectiveDate,
        //        ExpiryDate = request.ExpiryDate,
        //        InsuredType = request.InsuredType,
        //        SchemeType = request.SchemeType,
        //        ClientName = request.ClientName,
        //        GroupLifeFactor = request.GroupLifeFactor,
        //        GroupFactorAmount = request.GroupFactorAmount,
        //        LoadingPercent = request.LoadingPercent,
        //        DiscountPercent = request.DiscountPercent,
        //        SubmittedBy = submittedBy,
        //        SubmittedOn = DateTime.UtcNow,
        //        Members = new List<QuoteMember>()
        //    };

        //    decimal totalPremium = 0;

        //    foreach (var m in request.Members)
        //    {
        //        var sumAssured = PremiumCalculator.CalculateSumAssured(m.Emolument, request.GroupLifeFactor);
        //        var rate = request.UseFixedRate ? request.FixedRate ?? 0 : GetUnderwrittenRate(m.ANB); // You may replace this with a DB lookup

        //        var basePremium = PremiumCalculator.CalculatePremium(sumAssured, rate);
        //        var loading = PremiumCalculator.ApplyLoading(basePremium, request.LoadingPercent);
        //        var discount = PremiumCalculator.ApplyDiscount(basePremium, request.DiscountPercent);

        //        var pdePrem = m.HasPDE ? PremiumCalculator.CalculateExtensionPremium(sumAssured, m.PDE_Rate, m.CoverDays) : 0;
        //        var ttdPrem = m.HasTTD ? PremiumCalculator.CalculateExtensionPremium(sumAssured, m.TTD_Rate, m.CoverDays) : 0;
        //        var mePrem = m.HasME ? PremiumCalculator.CalculateExtensionPremium(sumAssured, m.ME_Rate, m.CoverDays) : 0;
        //        var funPrem = m.HasFUN ? PremiumCalculator.CalculateExtensionPremium(sumAssured, m.FUN_Rate, m.CoverDays) : 0;
        //        var crlPrem = m.HasCRL ? PremiumCalculator.CalculateExtensionPremium(sumAssured, m.CRL_Rate, m.CoverDays) : 0;

        //        var total = basePremium + pdePrem + ttdPrem + mePrem + funPrem + crlPrem + loading - discount;
        //        totalPremium += total;

        //        quoteMaster.Members.Add(new QuoteMember
        //        {
        //            Surname = m.Surname,
        //            OtherNames = m.OtherNames,
        //            ANB = m.ANB,
        //            Emolument = m.Emolument,
        //            SumAssured = sumAssured,
        //            PremiumRate = rate,
        //            ActualPremium = basePremium,
        //            LoadingAmount = loading,
        //            DiscountAmount = discount,

        //            PDE_Rate = m.PDE_Rate,
        //            PDE_Premium = pdePrem,

        //            TTD_Rate = m.TTD_Rate,
        //            TTD_Premium = ttdPrem,

        //            ME_Rate = m.ME_Rate,
        //            ME_Premium = mePrem,

        //            FUN_Rate = m.FUN_Rate,
        //            FUN_Premium = funPrem,

        //            CRL_Rate = m.CRL_Rate,
        //            CRL_Premium = crlPrem,

        //            ModifiedBy = submittedBy,
        //            ModifiedOn = DateTime.UtcNow
        //        });
        //    }

        //    quoteMaster.TotalPremium = totalPremium;
        //    quoteMaster.NetPremium = totalPremium;

        //    _context.Quotes.Add(quoteMaster);
        //    await _context.SaveChangesAsync();

        //    return quoteNo;
        //}
//        public async Task<QuotesMaster> CreateQuoteAsync(CreateQuoteRequest request, string approvedBy, CancellationToken ct = default)
//        {
//            var strategy = _db.Database.CreateExecutionStrategy();

//            return await strategy.ExecuteAsync(async () =>
//            {
//                await using var trx = await _db.Database.BeginTransactionAsync(ct);

//                var qm = new QuotesMaster
//                {
//                    QuoteNo = $"Q{DateTime.UtcNow:yyyyMMddHHmmssfff}",
//                    CoverCode = request.CoverCode ?? "GL",
//                    Covertype = request.CoverType ?? "GROUP LIFE ONLY",
//                    AssuredCode = request.ClientCode,
//                    SurName = request.ClientSurname,
//                    OtherNames = request.ClientOtherNames,
//                    TDate = request.TransactionDate,
//                    StartDate = request.EffectiveDate,
//                    MaturityDate = request.ExpiryDate,
//                    CustomerType = request.CustomerType,
//                    Loading = request.LoadingPercent,
//                    SubmittedBy = approvedBy,
//                    isQuote = 1,
//                SubmittedOn = DateTime.UtcNow
//                }; 

//            _db.QuotesMaster.Add(qm);
//            await _db.SaveChangesAsync(ct);

//            int serial = 1;
//            decimal totalPremium = 0m;
//            foreach (var m in request.Members)
//            {
//                int age = m.UseAverageAge && m.AverageAge.HasValue ? m.AverageAge.Value : m.ANB;
//                decimal sumAssured = request.GroupFactor * m.Emolument;

//                decimal premiumRate;
//                if (request.UseFixedRate && request.FixedRate.HasValue)
//                    premiumRate = request.FixedRate.Value;
//                else
//                {
//                    premiumRate = await GetUnderwritingRateAsync(qm.CoverCode ?? "GL", age, request.EffectiveDate, ct) ?? 0m;
//                }

//                var (basePrem, pdePrem, ttdPrem, mePrem, funPrem, crlPrem) =
//                    ComputeMemberPremiums(sumAssured, premiumRate, m.PDERate, m.TDRate, m.MERate, m.FUNRate, m.CRLRate, request.CoverDays, request.LoadingPercent, request.ReductionPercent);

//                var member = new QuoteMember
//                {
//                    QuotationNo = qm.QuoteNo,
//                    SerialNo = serial++. ToString(),
//                    Surname = m.Surname,
//                    OtherNames = m.OtherNames,
//                    Age = age,
//                    TEmolument = m.Emolument,
//                    SumInsured = sumAssured,
//                    PremiumRate = premiumRate,
//                    GrossPremium = basePrem,
//                    PDERate = m.PDERate,
//                    PDEPremium = pdePrem,
//                    PDESumInsured = sumAssured,
//                    TDRate = m.TDRate,
//                    TDPremium = ttdPrem,
//                    TDSuminsured = sumAssured,
//                    MERate = m.MERate,
//                    MEPremium = mePrem,
//                    MESumInsured = sumAssured,
//                    A6 = m.FUNRate,
//                    A7 = funPrem,
//                    A8 = m.CRLRate,
//                    A9 = crlPrem,
//                    Field10 = request.CustomerType,
//                    SubmittedBy = approvedBy,
//                    SubmittedOn = DateTime.UtcNow,
//                    Active = true,
//                    Deleted = false
//                };

//                _db.QuoteMembers.Add(member);
//                totalPremium += member.TotalPremium;
//            }

//            qm.TotalPremium = decimal.Round(totalPremium, 2);
//            qm.ModifiedBy = approvedBy;
//            qm.ModifiedOn = DateTime.UtcNow;

//            await _db.SaveChangesAsync(ct);
//            await trx.CommitAsync(ct);

//                return qm;
//            });
//        }

//        public async Task<QuotesMaster?> GetQuoteAsync(string QuoteNo, CancellationToken ct = default)
//        {
//            return await _db.QuotesMaster.FirstOrDefaultAsync(q => q.QuoteNo == QuoteNo, ct);
//        }

//        public async Task<List<QuoteMember>> GetQuoteMembersAsync(string QuoteNo, CancellationToken ct = default)
//        {
//            return await _db.QuoteMembers
//                .Where(m => m.QuotationNo == QuoteNo && (m.Deleted == null || m.Deleted == false))
//                .OrderBy(m => m.SerialNo)
//                .ToListAsync(ct);
//        }



//public async Task<UploadMembersResult> UploadMembersCsvAsync(string QuoteNo, Stream csvStream, string approvedBy, CancellationToken ct = default)
//    {
//        using var reader = new StreamReader(csvStream);
//        string? header = await reader.ReadLineAsync();
//        var result = new UploadMembersResult();
//        if (header == null) return result;

//        var headers = header.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();

//        int idxSurname = Array.IndexOf(headers, "surname");
//        int idxOther = Array.IndexOf(headers, "othernames");
//        int idxAge = Array.IndexOf(headers, "anb");
//        int idxEmol = Array.IndexOf(headers, "emolument");
//        int idxUseFixed = Array.IndexOf(headers, "usefixedrate");
//        int idxFixed = Array.IndexOf(headers, "fixedrate");
//        int idxUseAvg = Array.IndexOf(headers, "useaverageage");
//        int idxAvg = Array.IndexOf(headers, "averageage");
//        int idxPDE = Array.IndexOf(headers, "pderate");
//        int idxTTD = Array.IndexOf(headers, "tdrate");
//        int idxME = Array.IndexOf(headers, "merate");
//        int idxFUN = Array.IndexOf(headers, "funrate");
//        int idxCRL = Array.IndexOf(headers, "crlrate");
//        int idxDays = Array.IndexOf(headers, "coverdays");

//        var qm = await GetQuoteAsync(QuoteNo, ct);
//        if (qm is null) throw new InvalidOperationException("Quote not found.");

//        int serialStart = (await _db.QuoteMembers.CountAsync(m => m.QuotationNo == QuoteNo, ct)) + 1;

//        string? line;
//        while ((line = await reader.ReadLineAsync()) != null)
//        {
//            result.Total++;
//            if (string.IsNullOrWhiteSpace(line)) { result.Skipped++; continue; }
//            var cols = line.Split(',');

//            try
//            {
//                string? surname = idxSurname >= 0 ? cols[idxSurname] : null;
//                string? other = idxOther >= 0 ? cols[idxOther] : null;
//                int age = idxAge >= 0 ? int.Parse(cols[idxAge]) : 0;
//                decimal emol = idxEmol >= 0 ? decimal.Parse(cols[idxEmol], CultureInfo.InvariantCulture) : 0m;

//                decimal? pde = idxPDE >= 0 && !string.IsNullOrWhiteSpace(cols[idxPDE]) ? decimal.Parse(cols[idxPDE], CultureInfo.InvariantCulture) : null;
//                decimal? ttd = idxTTD >= 0 && !string.IsNullOrWhiteSpace(cols[idxTTD]) ? decimal.Parse(cols[idxTTD], CultureInfo.InvariantCulture) : null;
//                decimal? me  = idxME  >= 0 && !string.IsNullOrWhiteSpace(cols[idxME ]) ? decimal.Parse(cols[idxME ], CultureInfo.InvariantCulture) : null;
//                decimal? fun = idxFUN >= 0 && !string.IsNullOrWhiteSpace(cols[idxFUN]) ? decimal.Parse(cols[idxFUN], CultureInfo.InvariantCulture) : null;
//                decimal? crl = idxCRL >= 0 && !string.IsNullOrWhiteSpace(cols[idxCRL]) ? decimal.Parse(cols[idxCRL], CultureInfo.InvariantCulture) : null;

//                int? coverDays = idxDays >= 0 && !string.IsNullOrWhiteSpace(cols[idxDays]) ? int.Parse(cols[idxDays]) : null;

//                    // Rate selection precedence per CSV row (optional overrides)
//                    bool rowUseFixed = idxUseFixed >= 0 && new[] { "true", "1", "yes", "y" }
//                        .Contains(cols[idxUseFixed].Trim().ToLowerInvariant());

//                    decimal? rowFixed = idxFixed >= 0 && !string.IsNullOrWhiteSpace(cols[idxFixed])
//                        ? decimal.Parse(cols[idxFixed], CultureInfo.InvariantCulture)
//                        : null;

//                    bool rowUseAvg = idxUseAvg >= 0 && new[] { "true", "1", "yes", "y" }
//                        .Contains(cols[idxUseAvg].Trim().ToLowerInvariant());

//                    int? rowAvg = idxAvg >= 0 && !string.IsNullOrWhiteSpace(cols[idxAvg])
//                        ? int.Parse(cols[idxAvg])
//                        : null;

//                    int calcAge = rowUseAvg && rowAvg.HasValue ? rowAvg.Value : age;
//                decimal sumAssured = (qm.SumAssured.HasValue && qm.SumAssured.Value > 0)
//                    ? qm.SumAssured.Value // if stored as overall, but here we recompute per member
//                    : 0m;
//                // We follow request.GroupFactor * Emolument pattern; GroupFactor isn't stored on master; you can add it if desired.
//                // For CSV upload endpoint, we'll assume a GroupFactor of 1 unless overridden elsewhere.
//                decimal groupFactor = 1m;
//                sumAssured = groupFactor * emol;

//                decimal premiumRate;
//                if (rowUseFixed && rowFixed.HasValue)
//                    premiumRate = rowFixed.Value;
//                else
//                    premiumRate = await GetUnderwritingRateAsync(qm.CoverCode ?? "GL", calcAge, qm.StartDate ?? DateTime.Today, ct) ?? 0m;

//                // Use master loading if any
//                var (basePrem, pdePrem, ttdPrem, mePrem, funPrem, crlPrem) =
//                   ComputeMemberPremiums(sumAssured, premiumRate, pde, ttd, me, fun, crl, coverDays, qm.Loading, 0);

//                var member = new QuoteMember
//                {
//                    QuotationNo = QuoteNo,
//                    SerialNo = serialStart++.ToString(),
//                    Surname = surname,
//                    OtherNames = other,
//                    Age = calcAge,
//                    TEmolument = emol,
//                    SumInsured = sumAssured,
//                    PremiumRate = premiumRate,
//                    GrossPremium = basePrem,
//                    PDERate = pde,
//                    PDEPremium = pdePrem,
//                    PDESumInsured = sumAssured,
//                    TDRate = ttd,
//                    TDPremium = ttdPrem,
//                    TDSuminsured = sumAssured,
//                    MERate = me,
//                    MEPremium = mePrem,
//                    MESumInsured = sumAssured,
//                    A6 = fun,
//                    A7 = funPrem,
//                    A8 = crl,
//                    A9 = crlPrem,
//                    Field10 = qm.CustomerType,
//                    SubmittedBy = approvedBy,
//                    SubmittedOn = DateTime.UtcNow,
//                    Active = true,
//                    Deleted = false
//                };

//                _db.QuoteMembers.Add(member);
//                result.Imported++;
//            }
//            catch (Exception ex)
//            {
//                result.Skipped++;
//                result.Messages.Add($"Skipped row {result.Total}: {ex.Message}");
//            }
//        }

//        await _db.SaveChangesAsync(ct);

//        // Update master total
//        var members = await GetQuoteMembersAsync(QuoteNo, ct);
//        var qm2 = await GetQuoteAsync(QuoteNo, ct);
//        if (qm2 != null)
//        {
//            qm2.TotalPremium = members.Sum(m => m.TotalPremium);
//            qm2.ModifiedOn = DateTime.UtcNow;
//            await _db.SaveChangesAsync(ct);
//        }

//        return result;
//    }


//        public async Task<QuotesMaster?> RecomputeAsync(string QuoteNo, CreateQuoteRequest? overrides, string approvedBy, CancellationToken ct = default)
//        {
//            var qm = await GetQuoteAsync(QuoteNo, ct);
//            if (qm is null) return null;

//            var members = await GetQuoteMembersAsync(QuoteNo, ct);
//            decimal total = 0m;

//            foreach (var m in members)
//            {
//                int age = m.Age ?? 0;
//                decimal emol = m.TEmolument ?? 0;
//                decimal groupFactor = overrides?.GroupFactor ?? 1m;
//                decimal sumAssured = groupFactor * emol;

//                decimal premiumRate;
//                if (overrides?.UseFixedRate == true && overrides.FixedRate.HasValue)
//                    premiumRate = overrides.FixedRate.Value;
//                else
//                    premiumRate = await GetUnderwritingRateAsync(qm.CoverCode ?? "GL", age, qm.StartDate ?? DateTime.Today, ct) ?? (m.PremiumRate ?? 0);

//                var (basePrem, pdePrem, ttdPrem, mePrem, funPrem, crlPrem) =
//                    ComputeMemberPremiums(sumAssured, premiumRate, m.PDERate, m.TDRate, m.MERate, m.A6, m.A8, overrides?.CoverDays, overrides?.LoadingPercent ?? qm.Loading, overrides?.ReductionPercent);

//                m.SumInsured = sumAssured;
//                m.PremiumRate = premiumRate;
//                m.GrossPremium = basePrem;
//                m.PDEPremium = pdePrem;
//                m.TDPremium = ttdPrem;
//                m.MEPremium = mePrem;
//                m.A7 = funPrem;
//                m.A9 = crlPrem;
//                m.ModifiedBy = approvedBy;
//                m.ModifiedOn = DateTime.UtcNow;

//                total += m.TotalPremium;
//            }

//            qm.TotalPremium = decimal.Round(total, 2);
//            qm.ModifiedBy = approvedBy;
//            qm.ModifiedOn = DateTime.UtcNow;

//            await _db.SaveChangesAsync(ct);
//            return qm;
//        }



    }
}


