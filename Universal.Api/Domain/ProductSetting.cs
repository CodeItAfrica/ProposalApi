namespace GibsLifesMicroWebApi.Domain
{
    public class ProductSetting
    {
        public string ProductID { get; set; } = string.Empty;

        public long? Min_Age { get; set; }

        public long? Max_Age { get; set; }

        public long? Min_Duration { get; set; }

        public long? Max_Duration { get; set; }

        public decimal? Min_Contribution { get; set; }

        public decimal? Max_Contribution { get; set; }

        public decimal? RateDiv_factor { get; set; }

        public decimal? Min_SA { get; set; }

        public decimal? Max_SA { get; set; }

        public string? Multiplier { get; set; }

        public decimal? A1 { get; set; }

        public decimal? A2 { get; set; }

        public string? Remarks { get; set; }

        public string? Tag { get; set; }
    }
}