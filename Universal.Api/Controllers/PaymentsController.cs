using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Data;

namespace GibsLifesMicroWebApi.Controllers
{
    [Authorize(Roles = "APP,AGENT")]
    public class PaymentsController : SecureControllerBase
    {
        public PaymentsController(Repository repository, AuthContext authContext) : base(repository, authContext)
        {
        }

        [HttpPost]
        public async Task<ActionResult<string>>
            CreatePayment(PaymentDto payment)
        {
            await Task.Delay(200);
            return Ok(payment);
        }
    }

    public class PaymentDto
    {
        public long DOc_Num { get; set; }
        public string PolicyNo { get; set; }
        public string InsSurname { get; set; }
        public string InsFirstname { get; set; }
        public DateTime TransDate { get; set; }
        public string SubRisk { get; set; }
        public string BrokerName { get; set; }
        public string InsAddress { get; set; }
        public string InsMobilePhone { get; set; }
        public string InsEmail { get; set; }
        public DateTime StartDate { get; set; }
        public decimal AmountPaid { get; set; }
        public string CoverType { get; set; }
    }
}