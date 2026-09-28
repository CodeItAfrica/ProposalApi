using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    // NOTE: A class-level route prefix is required because the existing
    // IndividualLifeController already owns bare routes like "create", "search",
    // etc. Without this prefix the two controllers would produce ambiguous-route
    // exceptions at startup. Adjust the prefix to match your routing convention.
    //[Authorize(Roles = "APP")]
    public class lifepolicyAdminController : SecureControllerBase
    {
        private readonly DataContext _db;
        private readonly Settings _settings;
        private readonly ProposalReportService _reportService;

        public lifepolicyAdminController(
            Repository repository,
            DataContext db,
            AuthContext authContext,
            Settings settings,
            ProposalReportService reportService) : base(repository, authContext)
        {
            _db = db;
            _settings = settings;
            _reportService = reportService;
        }


        /// <summary>
        /// List / search Individual-life policies (paged). All filters are optional; pass
        /// <c>subRiskId</c> (e.g. "GLA") to restrict to a specific group-life product.
        /// </summary>
        [HttpGet("IndpolicySearch")]
  
        [ProducesResponseType(typeof(PagedResultDto<PolicyIndListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResultDto<PolicyIndListItemDto>>> IndPolicySearch(
        [FromQuery] PolicyIndQueryDto query,
          CancellationToken ct)
        {
            var result = await _repository.PolicyIndListAsync(query, ct);
            return Ok(result);
        }

        /// <summary>
        /// Fetch a single in-force Policy by policy number.
        /// </summary>
        /// <param name="policyNo">No of the Policy to get.</param>
        /// <returns>The Policy with the No entered.</returns>
        [HttpGet("policy/{policyNo}")]
        public async Task<ActionResult> GetPolicyMaster(string policyNo)
        {
            try
            {
                if (string.IsNullOrEmpty(policyNo))
                    return BadRequest("Policy number is required.");

                var policymaster = await _repository.GetPolicyByNumberAsync(policyNo);

                if (policymaster is null)
                    return NotFound();

                return Ok(policymaster);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // ============================================
        // REGULAR POLICY (Tag 'B')
        // ============================================

        [HttpPost("create")]
        public async Task<IActionResult> CreatePolicy([FromBody] ProposalRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request is null");

                var calculation = await _repository.CalculateAsync(new QuotationRequest
                {
                    SurName = request.SurName,
                    OtherNames = request.OtherNames,
                    DateOfBirth = request.DateOfBirth,
                    Duration = request.Duration,
                    Frequency = request.Frequency,
                    RegularSaving = request.RegularSaving,
                    ProductCode = request.ProductCode,
                    ExtraRate = request.ExtraRate,
                    LoadingRate = request.LoadingRate
                });

                // Saves an in-force policy and returns the generated policy number.
                var policyNo = await _repository.SavePolicyAsync(request, calculation);

                return Ok(new
                {
                    message = "Policy created successfully",
                    policyNo = policyNo,
                    calculation
                });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpGet("policies")]
        public async Task<IActionResult> GetAllPolicies(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? policyNo = null,
            [FromQuery] string? assuredCode = null,
            [FromQuery] string? surname = null)
        {
            var result = await _repository.GetAllPoliciesPagedAsync(page, pageSize, policyNo, assuredCode, surname);
            return Ok(result);
        }

        [HttpPost("edit/policyNo")]
        public async Task<IActionResult> EditPolicy([FromQuery]string policyNo, [FromBody] ProposalRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(policyNo))
                    return BadRequest("Policy number is required.");
                if (request == null)
                    return BadRequest("Request is null");

                var calculation = await _repository.EditPolicyAsync(policyNo, request);
                return Ok(new { message = "Policy updated successfully", recalculated = calculation });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpGet("by-number/{policyNo}")]
        public async Task<IActionResult> GetPolicyByNo(string policyNo)
        {
            if (string.IsNullOrEmpty(policyNo))
                return BadRequest("Policy number is required.");

            var policy = await _repository.GetPolicyByNumberAsync(policyNo);

            if (policy == null)
                return NotFound();

            return Ok(policy);
        }

        // ============================================
        // TRADITIONAL POLICY (Tag 'A')
        // ============================================

        [HttpPost("tradpolicy/create")]
        public async Task<IActionResult> CreateTradPolicy([FromBody] TradProposalRequest request)
        {
            try
            {
                var calculation = await _repository.CalculateTraditionalAsync(request);

                var policyNo = await _repository.SaveTradPolicyAsync(request, calculation);

                return Ok(new
                {
                    message = "Traditional Policy created successfully.",
                    policyNo = policyNo,
                    calculation
                });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpPost("tradpolicy/edit/policyNo")]
        public async Task<IActionResult> EditTradPolicy([FromQuery]string policyNo, [FromBody] TradProposalRequest request)
        {
            var result = await _repository.EditTradPolicyAsync(policyNo, request);
            return Ok(new
            {
                message = "Policy updated successfully.",
                calculation = result
            });
        }

        //[HttpPost]
        //public async Task<ActionResult> CreatePolicyMaster(CreateNewPolicyMasterRequest request)
        //{
        //    try
        //    {
        //        var policymaster = await _repository.PolicyMasterCreateAsync(request);
        //        await _repository.SaveChangesAsync();

        //        var uri = new Uri($"{Request.Path}/{policymaster.PolicyID}", UriKind.Relative);
        //        return Ok(policymaster);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        /// <summary>
        /// Download policy report as Word document
        /// </summary>
        [HttpGet("policy/{policyNo}/report")]
        public async Task<IActionResult> DownloadPolicyReport(string policyNo)
        {
            try
            {
                if (string.IsNullOrEmpty(policyNo))
                    return BadRequest("Policy number is required");

                // NOTE: ProposalReportService.GenerateProposalReportAsync currently resolves
                // records via GetProposalForReportAsync, which filters isProposal == 1 and will
                // NOT find in-force policies. Point the service at the new
                // GetPolicyForReportAsync (added in the repo) for the policy path.
                var reportBytes = await _reportService.GenerateProposalReportAsync(policyNo, isTraditional: false);

                return File(
                    reportBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"Policy_{policyNo}_{DateTime.Now:yyyyMMdd}.docx"
                );
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Download traditional policy report as Word document
        /// </summary>
        [HttpGet("tradpolicy/{policyNo}/report")]
        public async Task<IActionResult> DownloadTradPolicyReport(string policyNo)
        {
            try
            {
                if (string.IsNullOrEmpty(policyNo))
                    return BadRequest("Policy number is required");

                var reportBytes = await _reportService.GenerateProposalReportAsync(policyNo, isTraditional: true);

                return File(
                    reportBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"TradPolicy_{policyNo}_{DateTime.Now:yyyyMMdd}.docx"
                );
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // ============================================
        // REGULAR POLICIES (Tag 'B') - BY AGENT CODE
        // ============================================

        /// <summary>
        /// Get all regular in-force policies by agent code
        /// </summary>
        [HttpGet("agent/{agentCode}")]
        public async Task<IActionResult> GetPoliciesByAgentCode(string agentCode)
        {
            try
            {
                // Existing untyped helper: filters Remarks == "IN FORCE" && Agentcode.
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

        [HttpGet("agent/{agentCode}/paged")]
        public async Task<IActionResult> GetPoliciesByAgentCodePaged(
            string agentCode,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.GetPoliciesByAgentCodePagedAsync(agentCode, page, pageSize);
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

        // ============================================
        // TRADITIONAL POLICIES (Tag 'A') - BY AGENT CODE
        // ============================================

        [HttpGet("traditional/agent/{agentCode}")]
        public async Task<IActionResult> GetTradPoliciesByAgentCode(string agentCode)
        {
            try
            {
                var policies = await _repository.GetTradPoliciesByAgentCodeAsync(agentCode);
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

        [HttpGet("traditional/agent/{agentCode}/paged")]
        public async Task<IActionResult> GetTradPoliciesByAgentCodePaged(
            string agentCode,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.GetTradPoliciesByAgentCodePagedAsync(agentCode, page, pageSize);
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

        // ============================================
        // REGULAR POLICIES - BY INSURED ID
        // ============================================

        [HttpGet("insured/{insuredId}")]
        public async Task<IActionResult> GetPoliciesByInsuredId(string insuredId)
        {
            try
            {
                // Existing untyped helper: filters Remarks == "IN FORCE" && AssuredCode.
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

        [HttpGet("insured/{insuredId}/paged")]
        public async Task<IActionResult> GetPoliciesByInsuredIdPaged(
            string insuredId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.GetPoliciesByInsuredIdPagedAsync(insuredId, page, pageSize);
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

        // ============================================
        // TRADITIONAL POLICIES (Tag 'A') - BY INSURED ID
        // ============================================

        [HttpGet("traditional/insured/{insuredId}")]
        public async Task<IActionResult> GetTradPoliciesByInsuredId(string insuredId)
        {
            try
            {
                var policies = await _repository.GetTradPoliciesByInsuredIdAsync(insuredId);
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

        [HttpGet("traditional/insured/{insuredId}/paged")]
        public async Task<IActionResult> GetTradPoliciesByInsuredIdPaged(
            string insuredId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.GetTradPoliciesByInsuredIdPagedAsync(insuredId, page, pageSize);
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

        // ============================================
        // ADVANCED SEARCH FOR POLICIES
        // ============================================

        [HttpGet("search")]
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
                // Reuses the existing SearchPoliciesAsync (filters Remarks == "IN FORCE").
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
        /// Update the status of a policy (e.g. IN FORCE -> LAPSED / CANCELLED).
        /// This replaces the proposal "approve" workflow, which has no policy equivalent.
        /// </summary>
        [HttpPut("status")]
        public async Task<IActionResult> UpdatePolicyStatus(
            [FromQuery] string policyNo,
            [FromQuery] string status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(policyNo))
                    return BadRequest(new { message = "Policy number is required." });
                if (string.IsNullOrWhiteSpace(status))
                    return BadRequest(new { message = "Status is required." });

                var policy = await _db.PolicyMaster
                    .FirstOrDefaultAsync(p => p.PolicyNo == policyNo && p.isProposal == 0);

                if (policy == null)
                    return NotFound(new { message = $"Policy {policyNo} not found." });

                if (string.Equals(policy.Status, status, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = $"Policy {policyNo} is already '{status}'." });

                policy.Status = status;
                policy.ModifiedOn = DateTime.Now;
                // policy.ModifiedBy = "CurrentUser"; // Set this based on your authentication

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Policy status updated successfully.",
                    policyNo = policy.PolicyNo,
                    status = policy.Status,
                    modifiedOn = policy.ModifiedOn
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating the policy.", error = ex.Message });
            }
        }
    }
}