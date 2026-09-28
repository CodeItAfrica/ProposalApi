using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GetpoliciesController : SecureControllerBase
    {
        private readonly Settings _settings;
        private readonly ProposalReportService _reportService;

        // Update constructor
        public GetpoliciesController(
            Repository repository,
            AuthContext authContext,
            Settings settings,
            ProposalReportService reportService) : base(repository, authContext)
        {
            _settings = settings;
            _reportService = reportService;
        }

        /// <summary>
        /// Get all  policies by agent code
        /// </summary>
        /// <param name="agentCode">Agent code</param>
        /// <returns>List of regular policies</returns>
        [HttpGet("policies/agent")]
        public async Task<IActionResult> GetPoliciesByAgentCode([FromQuery]string agentCode)
        {
            try
            {
                var policies = await _repository.GetPoliciesByAgentCodeAsync(agentCode);
                return Ok(new
                {
                    success = true,
                    data = policies,
                    count = policies.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }


        /// <summary>
        /// Get all  policies by insured ID
        /// </summary>
        /// <param name="insuredId">Insured client ID</param>
        [HttpGet("policies/insured")]
        public async Task<IActionResult> GetPoliciesByInsuredId([FromQuery] string insuredId)
        {
            try
            {
                var policies = await _repository.GetPoliciesByInsuredIdAsync(insuredId);
                return Ok(new
                {
                    success = true,
                    data = policies,
                    count = policies.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Search policies with multiple filters
        /// </summary>
        [HttpGet("policies/search")]
        public async Task<IActionResult> SearchPolicies(
            [FromQuery] string? agentCode = null,
            [FromQuery] string? insuredId = null,
            [FromQuery] string? policyNo = null,
            [FromQuery] string? surname = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.SearchPoliciesAsync(
                    agentCode, insuredId, policyNo, surname, fromDate, toDate, page, pageSize);

                return Ok(new
                {
                    success = true,
                    data = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    totalCount = result.TotalCount,
                    totalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific policy by policy number
        /// </summary>
        [HttpGet("policies/number")]
        public async Task<IActionResult> GetPolicyByNumber([FromQuery] string policyNo)
        {
            try
            {
                var policy = await _repository.GetPolicyByNumberAsync(policyNo);

                if (policy == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Policy with number {policyNo} not found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = policy
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
