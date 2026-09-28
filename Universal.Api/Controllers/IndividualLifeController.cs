using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    //[Authorize(Roles = "APP")]
    public class IndividualLifeController : SecureControllerBase
    {
        private readonly DataContext _db;
        private readonly Settings _settings;
        private readonly ProposalReportService _reportService;

        // Update constructor
        public IndividualLifeController(
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
        /// Fetch a single Policy.
        /// </summary>
        /// <param name="policyNo">No of the Policy to get.</param>
        /// <returns>The Policy with the No entered.</returns>
        [HttpGet("policy/{id}")]
        public async Task<ActionResult> GetPolicyMaster(string policyNo)
        {
            try
            {
                var policymaster = await _repository.PolicyMasterSelectThisAsync(policyNo);

                if (policyNo is null)
                    return NotFound();

                return Ok(policymaster);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateProposal([FromBody] ProposalRequest request)
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

            // Save the proposal - this method now returns the proposal number
            var proposalNo = await _repository.SaveProposalAsync(request, calculation);

            return Ok(new
            {
                message = "Proposal created successfully",
                proposalNo = proposalNo,  // This will now have the value
                calculation
            });
        }
             catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        [HttpGet("proposals")]
        public async Task<IActionResult> GetAllProposals(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? proposalNo = null,
            [FromQuery] string? assuredCode = null,
            [FromQuery] string? surname = null)
        {
            var result = await _repository.GetProposalsPagedAsync(page, pageSize, proposalNo, assuredCode, surname);
            return Ok(result);
        }


        [HttpPut("proposal/{proposalNo}")]
        public async Task<IActionResult> EditProposal(string proposalNo, [FromBody] ProposalRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(proposalNo))
                    return BadRequest("Proposal number is required.");

                if (request == null)
                    return BadRequest("Request is null");

                // Get all proposal records with this ProposalNo
                var proposals = await _repository.GetByProposalNoAsync(proposalNo);
                if (proposals == null || !proposals.Any())
                    return NotFound();

                // Recalculate
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

                foreach (var proposal in proposals)
                {
                    // Basic Details
                    proposal.SurName = request.SurName;
                    proposal.OtherNames = request.OtherNames;
                    proposal.Address = request.Address;
                    proposal.State = request.State;
                    proposal.Landphone = request.Landphone;
                    proposal.MobilePhone = request.MobilePhone;
                    proposal.Email = request.Email;
                    proposal.NationalID = request.NationalID;
                    proposal.Occupation = request.Occupation;
                    proposal.Sex = request.Gender;
                    proposal.MaritalStatus = request.MaritalStatus;
                    proposal.Country = request.Country;
                    proposal.DateofBirth = request.DateOfBirth;
                    proposal.Duration = request.Duration;
                    proposal.FOP = request.Frequency;

                    // Recalculated Premiums
                    proposal.SumAssured = calculation.SumAssured;
                    proposal.Premium1 = calculation.AnnualPremium;
                    proposal.Premium2 = calculation.ExtraPremium;
                    proposal.Premium3 = calculation.Loading;
                    proposal.TotalPremium = calculation.TotalPremium;
                    proposal.BasicPremium = calculation.AnnualPremium;

                    // New Fields - Personal Information
                    proposal.PlaceOfBirth = request.PlaceOfBirth;
                    proposal.SpouseName = request.SpouseName;
                    proposal.GovtOfOrigin = request.GovtOfOrigin;
                    proposal.ResAddress = request.ResAddress;
                    proposal.LandMark = request.LandMark;
                    proposal.NIN = request.NIN;
                    proposal.BVN = request.BVN;

                    // New Fields - Employment Information
                    proposal.EmploymentType = request.EmploymentType;
                    proposal.EmployerName = request.EmployerName;
                    proposal.EmployerAddress = request.EmployerAddress;
                    proposal.EmployerNumber = request.EmployerNumber;
                    proposal.EmployerEmail = request.EmployerEmail;
                    proposal.DateEmployed = request.DateEmployed;

                    // New Fields - Health & Lifestyle Information
                    proposal.Height = request.Height;
                    proposal.weight = request.weight;
                    proposal.Sport = request.Sport;
                    proposal.Infirmity = request.Infirmity;
                    proposal.stroke = request.stroke;
                    proposal.Treatment = request.Treatment;
                    proposal.WorkAbesent = request.WorkAbesent;
                    proposal.Smoke = request.Smoke;
                    proposal.Aviation = request.Aviation;
                    proposal.Relgion = request.Religion;
                    proposal.Designation = request.Designation;
                    proposal.IssueDate = request.IssueDate;
                    proposal.ExpiryDate = request.ExpiryDate;

                    // Tracking
                    proposal.ModifiedBy = "system";
                    proposal.ModifiedOn = DateTime.Now;
                }

                await _repository.UpdateRangeAsync(proposals);

                return Ok(new
                {
                    message = "Proposal updated successfully",
                    recalculated = calculation
                });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        [HttpGet("*{proposalNo}")]
        public async Task<IActionResult> GetProposalByNo(string proposalNo)
        {
            if (string.IsNullOrEmpty(proposalNo))
                return BadRequest("Proposal number is required.");

            var proposals = await _repository.GetByProposalNoAsync(proposalNo);

            if (proposals == null || !proposals.Any())
                return NotFound();

            return Ok(proposals);
        }
        [HttpPost("tradproposal/create")]
        public async Task<IActionResult> CreateTradProposal([FromBody] TradProposalRequest request)
        {
            try
            {
                var calculation = await _repository.CalculateTraditionalAsync(request);

                // Save the proposal - this method now returns the proposal number
                var proposalNo = await _repository.SaveTradProposalAsync(request, calculation);

                return Ok(new
                {
                    message = "Traditional Proposal created successfully.",
                    proposalNo = proposalNo,  // This will now have the value
                    calculation
                });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpPut("tradproposal/edit/{proposalNo}")]
        public async Task<IActionResult> EditTradProposal(string proposalNo, [FromBody] TradProposalRequest request)
        {
            var result = await _repository.EditTradProposalAsync(proposalNo, request);
            return Ok(new
            {
                message = "Proposal updated successfully.",
                calculation = result
            });
        }



        [HttpPost]
        public async Task<ActionResult> CreatePolicyMaster(CreateNewPolicyMasterRequest request)
        {
            try
            {
                var policymaster = await _repository.PolicyMasterCreateAsync(request);
                await _repository.SaveChangesAsync();

                var uri = new Uri($"{Request.Path}/{policymaster.PolicyID}", UriKind.Relative);
                return Ok(policymaster);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Download proposal report as Word document
        /// </summary>
        [HttpGet("proposal/{proposalNo}/report")]
        public async Task<IActionResult> DownloadProposalReport(string proposalNo)
        {
            try
            {
                if (string.IsNullOrEmpty(proposalNo))
                    return BadRequest("Proposal number is required");

                var reportBytes = await _reportService.GenerateProposalReportAsync(proposalNo, isTraditional: false);

                return File(
                    reportBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"Proposal_{proposalNo}_{DateTime.Now:yyyyMMdd}.docx"
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
        /// Download traditional proposal report as Word document
        /// </summary>
        [HttpGet("tradproposal/{proposalNo}/report")]
        public async Task<IActionResult> DownloadTradProposalReport(string proposalNo)
        {
            try
            {
                if (string.IsNullOrEmpty(proposalNo))
                    return BadRequest("Proposal number is required");

                var reportBytes = await _reportService.GenerateProposalReportAsync(proposalNo, isTraditional: true);

                return File(
                    reportBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"TradProposal_{proposalNo}_{DateTime.Now:yyyyMMdd}.docx"
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
        // REGULAR PROPOSALS (Tag 'B') - BY AGENT CODE
        // ============================================

        /// <summary>
        /// Get all regular proposals by agent code
        /// </summary>
        /// <param name="agentCode">Agent code</param>
        /// <returns>List of regular proposals</returns>
        [HttpGet("agent/{agentCode}")]
        public async Task<IActionResult> GetProposalsByAgentCode(string agentCode)
        {
            try
            {
                var proposals = await _repository.GetProposalsByAgentCodeAsync(agentCode);
                return Ok(new
                {
                    success = true,
                    data = proposals,
                    count = proposals.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get regular proposals by agent code with pagination
        /// </summary>
        /// <param name="agentCode">Agent code</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        [HttpGet("agent/{agentCode}/paged")]
        public async Task<IActionResult> GetProposalsByAgentCodePaged(
            string agentCode,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.GetProposalsByAgentCodePagedAsync(agentCode, page, pageSize);
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
        // TRADITIONAL PROPOSALS (Tag 'A') - BY AGENT CODE
        // ============================================

        /// <summary>
        /// Get all traditional proposals by agent code
        /// </summary>
        /// <param name="agentCode">Agent code</param>
        [HttpGet("traditional/agent/{agentCode}")]
        public async Task<IActionResult> GetTradProposalsByAgentCode(string agentCode)
        {
            try
            {
                var proposals = await _repository.GetTradProposalsByAgentCodeAsync(agentCode);
                return Ok(new
                {
                    success = true,
                    data = proposals,
                    count = proposals.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get traditional proposals by agent code with pagination
        /// </summary>
        [HttpGet("traditional/agent/{agentCode}/paged")]
        public async Task<IActionResult> GetTradProposalsByAgentCodePaged(
            string agentCode,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.GetTradProposalsByAgentCodePagedAsync(agentCode, page, pageSize);
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
        // REGULAR PROPOSALS (Tag 'B') - BY INSURED ID
        // ============================================

        /// <summary>
        /// Get all regular proposals by insured ID
        /// </summary>
        /// <param name="insuredId">Insured client ID</param>
        [HttpGet("insured/{insuredId}")]
        public async Task<IActionResult> GetProposalsByInsuredId(string insuredId)
        {
            try
            {
                var proposals = await _repository.GetProposalsByInsuredIdAsync(insuredId);
                return Ok(new
                {
                    success = true,
                    data = proposals,
                    count = proposals.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get regular proposals by insured ID with pagination
        /// </summary>
        [HttpGet("insured/{insuredId}/paged")]
        public async Task<IActionResult> GetProposalsByInsuredIdPaged(
            string insuredId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.GetProposalsByInsuredIdPagedAsync(insuredId, page, pageSize);
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
        // TRADITIONAL PROPOSALS (Tag 'A') - BY INSURED ID
        // ============================================

        /// <summary>
        /// Get all traditional proposals by insured ID
        /// </summary>
        [HttpGet("traditional/insured/{insuredId}")]
        public async Task<IActionResult> GetTradProposalsByInsuredId(string insuredId)
        {
            try
            {
                var proposals = await _repository.GetTradProposalsByInsuredIdAsync(insuredId);
                return Ok(new
                {
                    success = true,
                    data = proposals,
                    count = proposals.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get traditional proposals by insured ID with pagination
        /// </summary>
        [HttpGet("traditional/insured/{insuredId}/paged")]
        public async Task<IActionResult> GetTradProposalsByInsuredIdPaged(
            string insuredId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.GetTradProposalsByInsuredIdPagedAsync(insuredId, page, pageSize);
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
        // ADVANCED SEARCH FOR PROPOSALS
        // ============================================

        /// <summary>
        /// Search proposals with multiple filters
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchProposals(
            [FromQuery] string? agentCode = null,
            [FromQuery] string? insuredId = null,
            [FromQuery] string? proposalNo = null,
            [FromQuery] string? surname = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _repository.SearchProposalsAsync(
                    agentCode, insuredId, proposalNo, surname, fromDate, toDate, page, pageSize);

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

        [HttpPut("approve")]
        public async Task<IActionResult> ApproveProposal([FromQuery]string proposalNo)
        {
            try
            {
                // Find the proposal by ProposalNo
                var proposal = await _db.PolicyMaster
                    .FirstOrDefaultAsync(p => p.ProposalNo == proposalNo);

                if (proposal == null)
                {
                    return NotFound(new { message = $"Proposal {proposalNo} not found." });
                }

                // Check if already approved
                if (proposal.Status == "APPROVED")
                {
                    return BadRequest(new { message = $"Proposal {proposalNo} is already approved." });
                }

                // Update status to APPROVED
                proposal.Status = "APPROVED";
                proposal.ModifiedOn = DateTime.Now;
                // proposal.ModifiedBy = "CurrentUser"; // Set this based on your authentication

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Proposal approved successfully.",
                    proposalNo = proposal.ProposalNo,
                    status = proposal.Status,
                    modifiedOn = proposal.ModifiedOn
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while approving the proposal.", error = ex.Message });
            }
        }
    }
}
