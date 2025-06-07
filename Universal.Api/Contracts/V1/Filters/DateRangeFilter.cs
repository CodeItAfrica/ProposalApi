using System;

namespace Gibs.OpenApi.Contracts.V1
{
    public class DateRangeFilter
    {
        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        internal bool CanSearch => DateFrom.HasValue && DateTo.HasValue;
    }
}
