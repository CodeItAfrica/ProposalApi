using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Services.GroupLife;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GibsLifesMicroWebApi.Tests.GroupLife
{
    /// <summary>
    /// The calculator is pure, so all of this runs with no database.
    ///
    /// Several tests below assert behaviour that is arguably WRONG — the net
    /// premium divergence, the unreachable balance branch. They are here so the
    /// quirks are pinned: if someone "fixes" one without deciding to, a test
    /// fails and says so. Each is marked LEGACY QUIRK.
    /// </summary>
    public class GroupLifeCalculatorTests
    {
        // =================================================================
        // ApplyRates
        // =================================================================

        [Fact]
        public void ApplyRates_computes_commission_wht_and_orc_off_the_right_bases()
        {
            var r = GroupLifeCalculator.ApplyRates(
                grossPremium: 2_500_000m, commissionRate: 15m, whtRate: 10m, orcRate: 2.5m);

            Assert.Equal(375_000.00m, r.CommissionAmount);   // 15% of gross
            Assert.Equal(37_500.00m, r.WhtAmount);           // 10% of COMMISSION, not gross
            Assert.Equal(62_500.00m, r.OrcAmount);           // 2.5% of gross
        }

        [Fact]
        public void ApplyRates_screen_rule_ignores_orc_in_the_net()
        {
            var r = GroupLifeCalculator.ApplyRates(2_500_000m, 15m, 10m, 2.5m, deductOrcFromNet: false);

            // 2,500,000 - (375,000 + 37,500). ORC is calculated but not deducted.
            Assert.Equal(2_087_500.00m, r.NetPremium);
            Assert.False(r.OrcDeductedFromNet);
        }

        [Fact]
        public void ApplyRates_posting_rule_deducts_orc_from_the_net()
        {
            var r = GroupLifeCalculator.ApplyRates(2_500_000m, 15m, 10m, 2.5m, deductOrcFromNet: true);

            Assert.Equal(2_025_000.00m, r.NetPremium);
            Assert.True(r.OrcDeductedFromNet);
        }

        /// <summary>
        /// LEGACY QUIRK. cmdApply_Click and btnOk_Click disagree, so on a
        /// zero-commission note the user is shown one net premium and a different
        /// one is stored. The gap is exactly the ORC amount.
        /// </summary>
        [Fact]
        public void ApplyRates_screen_and_posting_diverge_by_the_orc_amount()
        {
            var shown = GroupLifeCalculator.ApplyRates(2_500_000m, 0m, 10m, 2.5m, deductOrcFromNet: false);
            var saved = GroupLifeCalculator.ApplyRates(2_500_000m, 0m, 10m, 2.5m, deductOrcFromNet: true);

            Assert.Equal(2_500_000.00m, shown.NetPremium);
            Assert.Equal(2_437_500.00m, saved.NetPremium);
            Assert.Equal(shown.OrcAmount, shown.NetPremium - saved.NetPremium);
        }

        [Theory]
        [InlineData(150, 100)]   // clamped down
        [InlineData(-5, 0)]      // clamped up
        [InlineData(15, 15)]     // untouched
        public void ApplyRates_clamps_rates_to_zero_through_one_hundred(
            decimal input, decimal expected)
        {
            var r = GroupLifeCalculator.ApplyRates(1_000_000m, input, 0m, 0m);
            Assert.Equal(expected, r.CommissionRate);
        }

        [Fact]
        public void ApplyRates_rounds_half_away_from_zero_to_two_places()
        {
            // 0.125% of 1,000.10 = 1.2501...  -> 1.25
            var r = GroupLifeCalculator.ApplyRates(1_000.10m, 0.125m, 0m, 0m);
            Assert.Equal(1.25m, r.CommissionAmount);

            // 12.5% of 100.20 = 12.525 -> 12.53 (half away from zero, not banker's)
            var r2 = GroupLifeCalculator.ApplyRates(100.20m, 12.5m, 0m, 0m);
            Assert.Equal(12.53m, r2.CommissionAmount);
        }

        [Fact]
        public void ApplyRates_skips_levies_when_the_rate_is_zero()
        {
            var r = GroupLifeCalculator.ApplyRates(1_000_000m, 0m, 0m, 0m);

            Assert.Equal(0m, r.NcdfValue);
            Assert.Equal(0m, r.NcdfNetValue);      // NOT gross - 0; the legacy left it untouched
            Assert.Equal(0m, r.NaicomLevyNetValue);
        }

        [Fact]
        public void ApplyRates_computes_levies_off_gross_when_rates_are_present()
        {
            var r = GroupLifeCalculator.ApplyRates(
                1_000_000m, 0m, 0m, 0m, ncdfRate: 1m, naicomLevyRate: 0.5m);

            Assert.Equal(10_000.00m, r.NcdfValue);
            Assert.Equal(990_000.00m, r.NcdfNetValue);
            Assert.Equal(5_000.00m, r.NaicomLevyValue);
            Assert.Equal(995_000.00m, r.NaicomLevyNetValue);
        }

        // =================================================================
        // Distribute
        // =================================================================

        private static CoinsuranceLineDto Line(
            string id, decimal pct, decimal comm = 10m, decimal orc = 2.5m, decimal wht = 10m,
            bool lead = false) =>
            new CoinsuranceLineDto
            {
                ParticipantId = id,
                ParticipantName = id,
                SharePercent = pct,
                CommissionRate = comm,
                OrcRate = orc,
                WhtRate = wht,
                IsLead = lead
            };

        [Fact]
        public void Distribute_computes_each_line_off_the_total_risk_figures()
        {
            var r = GroupLifeCalculator.Distribute(
                totalRiskSumInsured: 100_000_000m,
                totalRiskPremium: 5_000_000m,
                ourSharePercent: 60m,
                lines: new[] { Line("ALLIANZ", 25m), Line("AIICO", 15m) });

            var allianz = r.Lines.Single(l => l.ParticipantId == "ALLIANZ");
            Assert.Equal(25_000_000.00m, allianz.ShareSumInsured);
            Assert.Equal(1_250_000.00m, allianz.SharePremium);
            Assert.Equal(125_000.00m, allianz.CommissionAmount);   // 10% of share premium
            Assert.Equal(31_250.00m, allianz.OrcAmount);           // 2.5% of share premium
            Assert.Equal(12_500.00m, allianz.WhtAmount);           // 10% of commission
            Assert.Equal(1_081_250.00m, allianz.NetPremium);       // share - (comm + orc + wht)

            Assert.Equal(40m, r.TotalCededPercent);
            Assert.Equal(100m, r.ShareProportion);
            Assert.True(r.IsBalanced);
        }

        [Fact]
        public void Distribute_ignores_rows_with_no_participant_selected()
        {
            var r = GroupLifeCalculator.Distribute(1_000_000m, 100_000m, 50m,
                new[] { Line("A", 50m), Line(null, 10m), Line("   ", 10m) });

            Assert.Single(r.Lines);
            Assert.Equal(50m, r.TotalCededPercent);
        }

        [Fact]
        public void Distribute_numbers_lines_sequentially_from_one()
        {
            var r = GroupLifeCalculator.Distribute(1_000_000m, 100_000m, 40m,
                new[] { Line("A", 30m), Line("B", 30m) });

            Assert.Equal(new[] { 1, 2 }, r.Lines.Select(l => l.Sn));
        }

        /// <summary>
        /// The VB compared each row only against the previous one, so A, B, A passed.
        /// This is the case that regressed silently in production.
        /// </summary>
        [Fact]
        public void Distribute_detects_a_duplicate_that_is_not_adjacent()
        {
            var r = GroupLifeCalculator.Distribute(1_000_000m, 100_000m, 10m,
                new[] { Line("ALLIANZ", 30m), Line("AIICO", 30m), Line("ALLIANZ", 30m) });

            Assert.Contains(r.Errors, e => e.Contains("ALLIANZ") && e.Contains("more than once"));
            Assert.False(r.IsBalanced);
        }

        [Fact]
        public void Distribute_duplicate_check_is_case_insensitive()
        {
            var r = GroupLifeCalculator.Distribute(1_000_000m, 100_000m, 40m,
                new[] { Line("allianz", 30m), Line("ALLIANZ", 30m) });

            Assert.Contains(r.Errors, e => e.Contains("more than once"));
        }

        [Fact]
        public void Distribute_rejects_more_than_one_lead()
        {
            var r = GroupLifeCalculator.Distribute(1_000_000m, 100_000m, 40m,
                new[] { Line("A", 30m, lead: true), Line("B", 30m, lead: true) });

            Assert.Contains(r.Errors, e => e.Contains("one participant"));
        }

        [Fact]
        public void Distribute_flags_a_split_that_does_not_reach_one_hundred()
        {
            var r = GroupLifeCalculator.Distribute(1_000_000m, 100_000m, 60m,
                new[] { Line("A", 25m) });

            Assert.Equal(85m, r.ShareProportion);
            Assert.False(r.IsBalanced);
            Assert.Contains(r.Errors, e => e.Contains("did not agree to 100%"));
        }

        /// <summary>
        /// LEGACY QUIRK. When our share is >= 100 the legacy computes
        /// ourShare - ceded, which can only reach 100 when nothing is ceded. A
        /// fully retained risk with participant rows can never be saved.
        /// </summary>
        [Fact]
        public void Distribute_fully_retained_with_participants_can_never_balance()
        {
            var r = GroupLifeCalculator.Distribute(100_000_000m, 5_000_000m, 100m,
                new[] { Line("A", 10m) });

            Assert.Equal(90m, r.ShareProportion);   // 100 - 10, not 110
            Assert.False(r.IsBalanced);
        }

        [Fact]
        public void Distribute_fully_retained_with_no_participants_balances()
        {
            var r = GroupLifeCalculator.Distribute(100_000_000m, 5_000_000m, 100m,
                new CoinsuranceLineDto[0]);

            Assert.Equal(100m, r.ShareProportion);
            Assert.True(r.IsBalanced);
            Assert.Empty(r.Errors);
        }

        // =================================================================
        // Premium reconciliation (not in the legacy at all)
        // =================================================================

        [Fact]
        public void Distribute_warns_when_the_head_premium_is_not_our_share_of_the_total()
        {
            // Percentages balance perfectly, but the head note carries 2,000,000
            // where 60% of 5,000,000 is 3,000,000.
            var r = GroupLifeCalculator.Distribute(100_000_000m, 5_000_000m, 60m,
                new[] { Line("A", 25m), Line("B", 15m) },
                ourGrossPremium: 2_000_000m);

            Assert.True(r.IsBalanced);              // percentages still agree
            Assert.NotEmpty(r.Warnings);            // but the money does not
            Assert.Contains(r.Warnings, w => w.Contains("3000000") || w.Contains("3,000,000"));
        }

        [Fact]
        public void Distribute_is_silent_when_the_money_reconciles()
        {
            var r = GroupLifeCalculator.Distribute(100_000_000m, 5_000_000m, 60m,
                new[] { Line("A", 25m), Line("B", 15m) },
                ourGrossPremium: 3_000_000m);

            Assert.True(r.IsBalanced);
            Assert.Empty(r.Warnings);
        }

        [Fact]
        public void Distribute_does_not_reconcile_money_when_no_premium_is_supplied()
        {
            var r = GroupLifeCalculator.Distribute(100_000_000m, 5_000_000m, 60m,
                new[] { Line("A", 25m), Line("B", 15m) });

            Assert.Empty(r.Warnings);
        }

        // =================================================================
        // Rounding behaviour worth pinning
        // =================================================================

        [Fact]
        public void Distribute_fractional_shares_still_sum_to_the_ceded_total()
        {
            var lines = Enumerable.Range(0, 3)
                .Select(i => Line($"P{i}", 33.33m, comm: 0m, orc: 0m, wht: 0m))
                .ToList();

            var r = GroupLifeCalculator.Distribute(1_000_000m, 1_000_000m, 0.01m, lines);

            Assert.Equal(99.99m, r.TotalCededPercent);
            Assert.Equal(100m, r.ShareProportion);
            Assert.Equal(999_900.00m, r.TotalCededPremium);
            Assert.Equal(999_900.00m, r.Lines.Sum(l => l.SharePremium));
        }

        [Fact]
        public void R2_rounds_half_away_from_zero()
        {
            Assert.Equal(0.13m, GroupLifeCalculator.R2(0.125m));
            Assert.Equal(-0.13m, GroupLifeCalculator.R2(-0.125m));
            Assert.Equal(2.35m, GroupLifeCalculator.R2(2.345m));
        }
    }
}