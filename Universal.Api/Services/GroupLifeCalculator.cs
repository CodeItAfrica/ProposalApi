using GibsLifesMicroWebApi.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GibsLifesMicroWebApi.Services.GroupLife
{
    /// <summary>
    /// Pure arithmetic ported from postings_dnotes_edit.aspx.vb.
    /// No I/O, no state — everything here is unit-testable in isolation.
    ///
    /// Sources:
    ///   cmdApply_Click     -> ApplyRates
    ///   cmdCalcfire_Click  -> Distribute
    ///   btnOk_Click        -> ApplyRates(deductOrcFromNet: true) when commission is 0
    /// </summary>
    public static class GroupLifeCalculator
    {
        /// <summary>
        /// The legacy page ran every figure through FormatNaira(..., 2), i.e. it
        /// persisted money already rounded to 2dp. Kept identical so totals tie
        /// out against notes raised by the VB screen.
        /// </summary>
        public static decimal R2(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Commission / WHT / ORC / net premium.
        ///
        /// LEGACY INCONSISTENCY (preserved, do not "fix" silently):
        ///   cmdApply_Click  -> net = gross - (commission + WHT)          ORC ignored
        ///   btnOk_Click     -> net = gross - commission - WHT - ORC      ORC deducted,
        ///                      but only on the branch where commission = 0.
        /// The caller decides via <paramref name="deductOrcFromNet"/>.
        /// </summary>
        public static PremiumBreakdownDto ApplyRates(
            decimal grossPremium,
            decimal commissionRate,
            decimal whtRate,
            decimal orcRate,
            decimal ncdfRate = 0m,
            decimal naicomLevyRate = 0m,
            bool deductOrcFromNet = false)
        {
            // Legacy JS clamped the commission rate to 0..100; the server now owns it.
            commissionRate = Clamp(commissionRate, 0m, 100m);
            whtRate = Clamp(whtRate, 0m, 100m);
            orcRate = Clamp(orcRate, 0m, 100m);

            var commission = R2(grossPremium * commissionRate / 100m);
            var wht = R2(commission * whtRate / 100m);
            var orc = R2(grossPremium * orcRate / 100m);

            var net = deductOrcFromNet
                ? R2(grossPremium - commission - wht - orc)
                : R2(grossPremium - (commission + wht));

            var result = new PremiumBreakdownDto
            {
                GrossPremium = R2(grossPremium),
                CommissionRate = commissionRate,
                CommissionAmount = commission,
                WhtRate = whtRate,
                WhtAmount = wht,
                OrcRate = orcRate,
                OrcAmount = orc,
                NetPremium = net,
                OrcDeductedFromNet = deductOrcFromNet
            };

            // Levies only calculated when a rate was supplied (legacy `If rate > 0`).
            if (ncdfRate > 0m)
            {
                result.NcdfRate = ncdfRate;
                result.NcdfValue = R2(grossPremium * ncdfRate / 100m);
                result.NcdfNetValue = R2(grossPremium - result.NcdfValue);
            }

            if (naicomLevyRate > 0m)
            {
                result.NaicomLevyRate = naicomLevyRate;
                result.NaicomLevyValue = R2(grossPremium * naicomLevyRate / 100m);
                result.NaicomLevyNetValue = R2(grossPremium - result.NaicomLevyValue);
            }

            return result;
        }

        /// <summary>
        /// Spreads the risk across outward participants and validates the split.
        ///
        /// Per line (legacy cmdCalcfire_Click):
        ///   shareSumInsured = totalRiskSumInsured * share%   / 100
        ///   sharePremium    = totalRiskPremium    * share%   / 100
        ///   commission      = sharePremium        * commRate / 100
        ///   orc             = sharePremium        * orcRate  / 100
        ///   wht             = commission          * whtRate  / 100
        ///   net             = sharePremium - (commission + orc + wht)
        /// </summary>
        public static DistributionResultDto Distribute(
            decimal totalRiskSumInsured,
            decimal totalRiskPremium,
            decimal ourSharePercent,
            IEnumerable<CoinsuranceLineDto> lines,
            decimal? ourGrossPremium = null)
        {
            var result = new DistributionResultDto();
            var input = (lines ?? Enumerable.Empty<CoinsuranceLineDto>())
                .Where(l => !string.IsNullOrWhiteSpace(l.ParticipantId))
                .ToList();

            // The VB duplicate check only compared each row against the previous
            // one, so a repeat two rows down slipped through. Checked properly here.
            var duplicates = input
                .GroupBy(l => l.ParticipantId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var dup in duplicates)
                result.Errors.Add($"Participant '{dup}' is selected more than once.");

            var sn = 1;
            foreach (var line in input)
            {
                var shareSumInsured = R2(totalRiskSumInsured * line.SharePercent / 100m);
                var sharePremium = R2(totalRiskPremium * line.SharePercent / 100m);
                var commission = R2(sharePremium * line.CommissionRate / 100m);
                var orc = R2(sharePremium * line.OrcRate / 100m);
                var wht = R2(commission * line.WhtRate / 100m);
                var net = R2(sharePremium - (commission + orc + wht));

                result.Lines.Add(new CoinsuranceLineDto
                {
                    Sn = sn++,
                    ParticipantId = line.ParticipantId,
                    ParticipantName = line.ParticipantName,
                    SharePercent = line.SharePercent,
                    ShareSumInsured = shareSumInsured,
                    SharePremium = sharePremium,
                    CommissionRate = line.CommissionRate,
                    CommissionAmount = commission,
                    OrcRate = line.OrcRate,
                    OrcAmount = orc,
                    WhtRate = line.WhtRate,
                    WhtAmount = wht,
                    NetPremium = net,
                    IsLead = line.IsLead
                });
            }

            result.TotalCededPercent = result.Lines.Sum(l => l.SharePercent);
            result.TotalCededSumInsured = R2(result.Lines.Sum(l => l.ShareSumInsured));
            result.TotalCededPremium = R2(result.Lines.Sum(l => l.SharePremium));

            // Legacy balance rule, faithfully ported:
            //   ourShare <  100 -> shareprop = ourShare + sum(ceded)
            //   ourShare >= 100 -> shareprop = ourShare - sum(ceded)
            // The second branch can only reach 100 when nothing is ceded, which is
            // why the VB screen refused to save a 100%-retained risk that also had
            // participant rows. Behaviour kept; see MIGRATION-NOTES.md.
            result.ShareProportion = ourSharePercent < 100m
                ? ourSharePercent + result.TotalCededPercent
                : ourSharePercent - result.TotalCededPercent;

            result.IsBalanced = result.ShareProportion == 100m && !result.Errors.Any();

            if (result.ShareProportion != 100m)
                result.Errors.Add(
                    $"The business proportion distribution did not agree to 100% (currently {result.ShareProportion}%).");

            if (result.Lines.Count(l => l.IsLead) > 1)
                result.Errors.Add("Only one participant can be flagged as lead.");

            // Percentages balancing does not mean the money does. The legacy screen
            // validated the split on percentages alone, so a head note whose gross
            // premium was not our share of the total risk premium sailed through and
            // the participations silently failed to add back to the whole risk.
            // Non-blocking: it flags real data without changing what can be saved.
            if (ourGrossPremium.HasValue)
            {
                var expectedOurs = R2(totalRiskPremium * ourSharePercent / 100m);
                var actualOurs = R2(ourGrossPremium.Value);

                if (expectedOurs != actualOurs)
                    result.Warnings.Add(
                        $"Head note gross premium is {actualOurs}, but {ourSharePercent}% of the " +
                        $"total risk premium is {expectedOurs} (difference {R2(actualOurs - expectedOurs)}).");

                var reconciled = R2(actualOurs + result.TotalCededPremium);
                if (reconciled != R2(totalRiskPremium))
                    result.Warnings.Add(
                        $"Retained plus ceded premium is {reconciled}, which does not agree with the " +
                        $"total risk premium of {R2(totalRiskPremium)}.");
            }

            return result;
        }

        private static decimal Clamp(decimal value, decimal min, decimal max) =>
            value < min ? min : (value > max ? max : value);
    }
}