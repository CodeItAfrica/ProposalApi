using System;
using System.Collections.Generic;
using System.Linq;

namespace GibsLifesMicroWebApi.Contracts.V1
{
    public class ProductResult
    {
        public ProductResult(Models.SubRisk product)
        {
            RiskID = product.RiskID;
            ProductID = product.SubRiskID;
            ProductName = product.SubRiskName;
            Description = product.Description;
            ProductType= product.A6;


            //MidRiskID = product.MidClassID;
            //MidRisk = product.MidClass.MidClassName;
        }

        public string RiskID { get; set; }

        public string ProductID { get; set; }

        public string ProductName { get; set; }

        public string ProductType { get; set; }
        public string Description { get; set; }
    }


    public class ProductSettingDto
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

    public class ProductSettingQueryDto 
    {
        public string? Search { get; set; }
    }




    public class RiskListDto
    {
        public string RiskID { get; set; } = string.Empty;
        public string? RiskName { get; set; }
        public string? Description { get; set; }
    }



    public class RiskQueryDto
    {
        private const int MaxPageSize = 100;

        public string? Search { get; set; }

        private int _pageNumber = 1;
        private int _pageSize = 20;

        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = value < 1 ? 1 : value;
        }

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize :
                               value < 1 ? 20 : value;
        }
    }
}
