using GibsLifesMicroWebApi.Domain;
using System.Threading.Tasks;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using GibsLifesMicroWebApi.Models;

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
        public async Task<QuotationResult> UpdateAsync(long quoteId, QuotationRequest request)
        {
            var targetRow = await _db.QuotesMaster.FindAsync(quoteId);
            if (targetRow == null)
                throw new Exception("Quote not found");

            var quoteNo = targetRow.QuoteNo;

            // 🔹 Update basic details across all rows with the same QuoteNo
            var relatedQuotes = await _db.QuotesMaster
                .Where(q => q.QuoteNo == quoteNo)
                .ToListAsync();

            foreach (var row in relatedQuotes)
            {
                row.SurName = request.SurName;
                row.OtherNames = request.OtherNames;
                row.DateofBirth = request.DateOfBirth;
                row.ModifiedOn = DateTime.Now;
            }

            // 🔹 Update calculation-specific fields for the target row only
            targetRow.CoverCode = request.ProductCode;
            targetRow.Duration = request.Duration;
            targetRow.ModifiedOn = DateTime.Now;

            var config = await GetByCodeAsync(request.ProductCode);
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

            targetRow.SumAssured = sumAssured;
            targetRow.Premium1 = annualPremium;
            targetRow.Premium2 = extraPremium;
            targetRow.Premium3 = loadingFreq;
            targetRow.BasicPremium = annualPremium;
            targetRow.TotalPremium = totalPremium;

            await _db.SaveChangesAsync();

            return new QuotationResult
            {
                QuoteNo = quoteNo,
                AnnualPremium = annualPremium,
                SumAssured = sumAssured,
                LifeCover = lifeCover,
                ExtraPremium = extraPremium,
                Loading = loadingFreq,
                TotalPremium = totalPremium
            };
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

        public async Task<Domain.PagedResult<QuotesMaster>> GetAllAsync(int page, int pageSize)
        {
            var query = _db.QuotesMaster.OrderByDescending(q => q.SubmittedOn);

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


        public async Task<List<QuotesMaster>> GetByQuoteNoAsync(string quoteNo)
        {
            return await _db.QuotesMaster
                .Where(q => q.QuoteNo == quoteNo/* && q.isQuote == true*/)
                .ToListAsync();
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
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteByQuoteNoAsync(string quoteNo)
        {
            var quotes = await _db.QuotesMaster
                .Where(q => q.QuoteNo == quoteNo)
                .ToListAsync();

            if (quotes.Any())
            {
                _db.QuotesMaster.RemoveRange(quotes);
                await _db.SaveChangesAsync();
            }
        }



        public async Task SaveTradQuoteAsync(TraditionalQuotationRequest request, QuotationResult result)
        {

            var quoteno = GetNextAutoNumber("OTHERS", "ALL");

                var quote = new QuotesMaster
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
                Premium3 = request.RegularSaving,
                    Loading = result.Loading,
                TotalPremium = result.TotalPremium,
                BasicPremium = result.AnnualPremium,
                isQuote = 0,
                SubmittedBy = "system",
                    //A1 = (double?)request.LoadingRate,
                    //A2 = (double?)request.ExtraRate,
                    SubmittedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                TransGUID = Guid.NewGuid().ToString(),
                Status = request.Status
            };

            _db.QuotesMaster.Add(quote);
            await _db.SaveChangesAsync();
        }
        public async Task SaveQuoteAsync(QuotationRequest request, QuotationResult result)
        {

            var quoteno = GetNextAutoNumber("OTHERS", "ALL");

                var quote = new QuotesMaster
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
                Premium3 = request.RegularSaving,
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
                Status = request.Status
            };

            _db.QuotesMaster.Add(quote);
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
    }
}


