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
}
