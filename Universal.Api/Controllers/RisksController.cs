using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class RisksController : ControllerBase
    {
        private readonly RiskService _service;

        public RisksController(RiskService service)
        {
            _service = service;
        }

        [HttpGet("List")]
        [ProducesResponseType(typeof(PagedResultDto<RiskListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResultDto<RiskListDto>>> List(
            [FromQuery] RiskQueryDto query,
            CancellationToken ct)
        {
            var result = await _service.ListAsync(query, ct);
            return Ok(result);
        }
    }
}