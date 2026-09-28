using System.Collections.Generic;

namespace GibsLifesMicroWebApi.Domain
{

    /// <summary>Generic dropdown option: the stored id (Value) and its display label (Text).</summary>
    public class LookupItemDto
    {
        public string Value { get; set; } = default!;
        public string? Text { get; set; }
    }

    /// <summary>
    /// Currency option. The form submits <c>Currency</c> as CurrencyName and <c>Rate</c> as the
    /// exchange rate (the legacy dropdown used the rate as its value and the currency as its text).
    /// </summary>
    public class CurrencyOptionDto
    {
        public string Currency { get; set; } = default!;
        public double Rate { get; set; }
    }

    /// <summary>All dropdown options needed to render the policy form in one call.</summary>
    public class PolicyFormOptionsDto
    {
        public IReadOnlyList<LookupItemDto> AccountExecutives { get; set; } = new List<LookupItemDto>();
        public IReadOnlyList<LookupItemDto> BusinessSources { get; set; } = new List<LookupItemDto>();
        public IReadOnlyList<LookupItemDto> AccountTypes { get; set; } = new List<LookupItemDto>();
        public IReadOnlyList<CurrencyOptionDto> Currencies { get; set; } = new List<CurrencyOptionDto>();
        public IReadOnlyList<LookupItemDto> BusinessChannels { get; set; } = new List<LookupItemDto>();
    }
}
