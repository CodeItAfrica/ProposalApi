using GibsLifesMicroWebApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AutoNumbersController : ControllerBase
    {
        private readonly AutonumberService _autonumberService;

        public AutoNumbersController(AutonumberService autonumberService)
        {
            _autonumberService = autonumberService;
        }

        [HttpGet("next-policy-number")]
        public async Task<IActionResult> GetNextPolicyNumber(string branchId, string subRiskId, string endorsementType = "")
        {
            var nextPolicyNo = await _autonumberService.GeneratePolicyNumberAsync(branchId, subRiskId, endorsementType);
            return Ok(new { PolicyNumber = nextPolicyNo });
        }
    }
}
