using GibsLifesMicroWebApi.Dtos;

namespace GibsLifesMicroWebApi.Services.GroupLife
{
    /// <summary>
    /// Ported from businessPropChange2() in postings_coins_edit.aspx.
    ///
    /// IMPORTANT: this script never actually ran. The page wires
    /// <c>onkeyup="businessPropChange();"</c> onto four inputs, but only
    /// <c>businessPropChange2()</c> is defined — so every keystroke threw a
    /// ReferenceError and the fields stayed as typed. Whatever is in production
    /// was keyed in by hand, and will not necessarily agree with this arithmetic.
    /// Reconcile before trusting either.
    ///
    /// Two deliberate differences from GroupLifeCalculator (the debit note page):
    ///   ORC is taken off the NET premium here, off GROSS there.
    ///   Net excludes WHT here, includes it there.
    /// Both preserved; both are flagged rather than harmonised.
    /// </summary>
    public static class GroupLifeCoinsuranceCalculator
    {
        public static decimal R2(decimal v) => GroupLifeCalculator.R2(v);

        public static CoinsurancePremiumDto Compute(CalculateCoinsRequest r)
        {
            var busProp = Clamp(r.BusinessPropPercent, 0m, 100m);
            var commRate = Clamp(r.CommissionRate, 0m, 100m);
            var ourProp = Clamp(r.OurPropPercent, 0m, 100m);

            var shareSumInsured = R2(r.TotalSumInsured * busProp / 100m);
            var sharePremium = R2(r.TotalPremium * busProp / 100m);

            var ourSumInsured = R2(r.TotalSumInsured * ourProp / 100m);
            var ourPremium = R2(r.TotalPremium * ourProp / 100m);

            var commission = R2(sharePremium * commRate / 100m);

            // The legacy computed WHT from the commission value BEFORE recomputing
            // the commission, so it always used the previous keystroke's figure.
            // Fixed here: WHT is taken off the commission just calculated.
            var wht = R2(commission * r.WhtRate / 100m);

            var net = r.DeductWhtFromNet
                ? R2(sharePremium - commission - wht)
                : R2(sharePremium - commission);

            // ORC off the NET premium. The debit note screen takes it off GROSS.
            var orc = R2(net * r.OrcRate / 100m);

            return new CoinsurancePremiumDto
            {
                TotalSumInsured = R2(r.TotalSumInsured),
                TotalPremium = R2(r.TotalPremium),
                BusinessPropPercent = busProp,
                ShareSumInsured = shareSumInsured,
                SharePremium = sharePremium,
                OurPropPercent = ourProp,
                OurSumInsured = ourSumInsured,
                OurPremium = ourPremium,
                CommissionRate = commRate,
                CommissionAmount = commission,
                WhtRate = r.WhtRate,
                WhtAmount = wht,
                NetPremium = net,
                OrcRate = r.OrcRate,
                OrcAmount = orc,
                WhtDeductedFromNet = r.DeductWhtFromNet
            };
        }

        private static decimal Clamp(decimal v, decimal min, decimal max) =>
            v < min ? min : (v > max ? max : v);
    }
}