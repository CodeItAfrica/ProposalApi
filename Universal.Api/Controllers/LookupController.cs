using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/lookups")]
    [ApiController]
    public class LookupController : SecureControllerBase
    {
        public LookupController(Repository repo, AuthContext ctx) : base(repo, ctx) { }

        [HttpGet("branches")]
        public async Task<IActionResult> GetBranches()
            => Ok(await _repository.GetAllBranchesAsync());

        [HttpGet("premium-rate")]
        public async Task<IActionResult> GetRate(string productCode, long duration, long age, string rateCode, string remark)
        {
            var rate = await _repository.GetPremiumRateAsync(productCode, duration, age, rateCode, remark);
            return Ok(rate);
        }
    }
}
