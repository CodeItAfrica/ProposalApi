using GibsLifesMicroWebApi.Infrastructure.Common;
using System;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    

    public readonly record struct MemberPremium(
        decimal TotalSumInsured,
        decimal TotalPremium,
        decimal ShareSumInsured,
        decimal SharePremium);

    /// <summary>
    /// All premium arithmetic lifted verbatim from member.ascx.vb so the numbers match the
    /// legacy WebForms output. No I/O here — trivially unit-testable.
    /// </summary>
    public class PremiumCalculator
    {
        public decimal TotalEmolument(decimal basic, decimal transport, decimal housing, decimal other)
            => basic + transport + housing + other;

        public MemberPremium ComputeMember(decimal totalEmolument, decimal fixedPremiumRate, decimal ourSharePercent)
        {
            // cmdCalc_Click: both branches of the legacy If/Else are identical.
            var totalSum = totalEmolument;
            var totalPremium = totalSum * fixedPremiumRate / 100m;
            var shareSum = totalSum * ourSharePercent / 100m;
            var sharePremium = totalPremium * ourSharePercent / 100m;
            return new MemberPremium(totalSum, totalPremium, shareSum, sharePremium);
        }

        public decimal CoverPremium(decimal coverValue, decimal coverRate)
            => coverValue * coverRate / 100m;

        public decimal GroupLifePremium(decimal sumAssured, decimal premiumRate, decimal loadingRate, decimal discountRate)
        {
            var basePremium = sumAssured * premiumRate / 100m;

            if (loadingRate == 0 && discountRate == 0)
                return basePremium;

            if (loadingRate > 0)
                return basePremium * (loadingRate + 100m) / 100m;

            if (discountRate > 0)
                return basePremium / (discountRate + 100m) * 100m;

            return basePremium;
        }

        public int AgeNextBirthday(DateTime dateOfBirth, DateTime startDate)
        {
            // VB: if month(DOB) <= month(Start) -> yearsDiff + 1 else yearsDiff
            var yearsDiff = startDate.Year - dateOfBirth.Year;
            return dateOfBirth.Month <= startDate.Month ? yearsDiff + 1 : yearsDiff;
        }

        public int Duration(string freqOfPayment, DateTime startDate, DateTime endDate)
        {
            return (freqOfPayment ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                FrequencyOfPayment.Weekly => WeeksBetween(startDate, endDate),
                FrequencyOfPayment.Monthly => MonthsBetween(startDate, endDate),
                _ => 0
            };
        }

        private static int WeeksBetween(DateTime a, DateTime b)
            => (int)Math.Floor((b.Date - a.Date).TotalDays / 7d);

        private static int MonthsBetween(DateTime a, DateTime b)
            => ((b.Year - a.Year) * 12) + (b.Month - a.Month);
    }
}