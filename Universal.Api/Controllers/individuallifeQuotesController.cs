//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;

//namespace GibsLifesMicroWebApi.Controllers
//{
//      using global::GibsLifesMicroWebApi.Contracts.V1;
//    using global::GibsLifesMicroWebApi.Data;
//    using global::GibsLifesMicroWebApi.Data.Repositories;
//    using global::GibsLifesMicroWebApi.Domain;
//    using global::GibsLifesMicroWebApi.Models;
//    using global::GibsLifesMicroWebApi.Services;
//    using Microsoft.AspNetCore.Mvc;
//    using Microsoft.EntityFrameworkCore;
//    using System;
//    using System.Collections.Generic;
//    using System.IO;
//    using System.Linq;
//    using System.Threading.Tasks;

//    namespace GibsLifesMicroWebApi.Controllers
//    {
//        //[Authorize(Roles = "APP")]
//        [Route("api/[controller]")]
//        [ApiController]
//        public class IndividualLifeQuotesController : SecureControllerBase
//        {
//            private readonly DataContext _db;
//            private readonly Settings _settings;
//            private readonly ProposalReportService _reportService;

//            public IndividualLifeQuotesController(
//                Repository repository,
//                DataContext db,
//                AuthContext authContext,
//                Settings settings,
//                ProposalReportService reportService) : base(repository, authContext)
//            {
//                _db = db;
//                _settings = settings;
//                _reportService = reportService;
               
//            }

//            // -------------------------
//            // HELPER METHODS
//            // -------------------------
//            private IActionResult HandlePagedResult<T>(Models.PagedResult<T> result)
//            {
//                return Ok(new
//                {
//                    success = true,
//                    data = result.Items,
//                    page = result.Page,
//                    pageSize = result.PageSize,
//                    totalCount = result.TotalCount,
//                    totalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize)
//                });
//            }

//            private string GetCurrentUser() => User?.Identity?.Name ?? "SYSTEM";

//            // -------------------------
//            // POLICY MASTER
//            // -------------------------
//            [HttpGet("policy/{policyNo}")]
//            public async Task<IActionResult> GetPolicyMaster(string policyNo)
//            {
//                if (string.IsNullOrEmpty(policyNo)) return BadRequest("Policy number is required.");

//                var policy = await _repository.PolicyMasterSelectThisAsync(policyNo);
//                if (policy == null) return NotFound();

//                return Ok(policy);
//            }

//            [HttpPost("createpolicy")]
//            public async Task<IActionResult> CreatePolicyMaster([FromBody] CreateNewPolicyMasterRequest request)
//            {
//                try
//                {
//                    var policy = await _repository.PolicyMasterCreateAsync(request);
//                    await _repository.SaveChangesAsync();
//                    return Ok(policy);
//                }
//                catch (Exception ex)
//                {
//                    return ExceptionResult(ex);
//                }
//            }

//            // -------------------------
//            // PROPOSALS (REGULAR)
//            // -------------------------
//            [HttpPost("proposal/create")]
//            public async Task<IActionResult> CreateProposal([FromBody] ProposalRequest request)
//            {
//                if (request == null) return BadRequest("Request is null");

//                var calculation = await _repository.CalculateAsync(new QuotationRequest
//                {
//                    SurName = request.SurName,
//                    OtherNames = request.OtherNames,
//                    DateOfBirth = request.DateOfBirth,
//                    Duration = request.Duration,
//                    Frequency = request.Frequency,
//                    RegularSaving = request.RegularSaving,
//                    ProductCode = request.ProductCode,
//                    ExtraRate = request.ExtraRate,
//                    LoadingRate = request.LoadingRate
//                });

//                var proposalNo = await _repository.SaveProposalAsync(request, calculation);

//                return Ok(new
//                {
//                    message = "Proposal created successfully",
//                    proposalNo,
//                    calculation
//                });
//            }

//            [HttpPut("proposal/{proposalNo}")]
//            public async Task<IActionResult> EditProposal(string proposalNo, [FromBody] ProposalRequest request)
//            {
//                if (string.IsNullOrEmpty(proposalNo)) return BadRequest("Proposal number is required.");
//                if (request == null) return BadRequest("Request is null");

//                var proposals = await _repository.GetByProposalNoAsync(proposalNo);
//                if (proposals == null || !proposals.Any()) return NotFound();

//                var calculation = await _repository.CalculateAsync(new QuotationRequest
//                {
//                    SurName = request.SurName,
//                    OtherNames = request.OtherNames,
//                    DateOfBirth = request.DateOfBirth,
//                    Duration = request.Duration,
//                    Frequency = request.Frequency,
//                    RegularSaving = request.RegularSaving,
//                    ProductCode = request.ProductCode,
//                    ExtraRate = request.ExtraRate,
//                    LoadingRate = request.LoadingRate
//                });

//                foreach (var proposal in proposals)
//                {
//                    proposal.UpdateFromRequest(request, calculation, GetCurrentUser());
//                }

//                await _repository.UpdateRangeAsync(proposals);

//                return Ok(new { message = "Proposal updated successfully", recalculated = calculation });
//            }

//            [HttpGet("proposals")]
//            public async Task<IActionResult> GetAllProposals(
//                [FromQuery] int page = 1,
//                [FromQuery] int pageSize = 20,
//                [FromQuery] string? proposalNo = null,
//                [FromQuery] string? assuredCode = null,
//                [FromQuery] string? surname = null)
//            {
//                var result = await _repository.GetProposalsPagedAsync(page, pageSize, proposalNo, assuredCode, surname);
//                return HandlePagedResult(result);
//            }

//            [HttpGet("proposal/{proposalNo}/report")]
//            public async Task<IActionResult> DownloadProposalReport(string proposalNo)
//            {
//                if (string.IsNullOrEmpty(proposalNo)) return BadRequest("Proposal number is required");

//                var bytes = await _reportService.GenerateProposalReportAsync(proposalNo, isTraditional: false);
//                return File(bytes,
//                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
//                    $"Proposal_{proposalNo}_{DateTime.Now:yyyyMMdd}.docx");
//            }

//            [HttpPut("approve")]
//            public async Task<IActionResult> ApproveProposal([FromQuery] string proposalNo)
//            {
//                var proposal = await _db.PolicyMaster.FirstOrDefaultAsync(p => p.ProposalNo == proposalNo);
//                if (proposal == null) return NotFound(new { message = $"Proposal {proposalNo} not found." });
//                if (proposal.Status == "APPROVED") return BadRequest(new { message = $"Proposal {proposalNo} already approved." });

//                proposal.Status = "APPROVED";
//                proposal.ModifiedOn = DateTime.Now;
//                proposal.ModifiedBy = GetCurrentUser();

//                await _db.SaveChangesAsync();

//                return Ok(new { message = "Proposal approved successfully", proposalNo, status = proposal.Status });
//            }

//            // -------------------------
//            // TRADITIONAL PROPOSALS
//            // -------------------------
//            [HttpPost("tradproposal/create")]
//            public async Task<IActionResult> CreateTradProposal([FromBody] PolicyTradRequestDto request)
//            {
//                var calculation = await _repository.CalculateTraditionalAsync(request);
//                var proposalNo = await _repository.SaveTradProposalAsync(request, calculation);

//                return Ok(new { message = "Traditional Proposal created successfully", proposalNo, calculation });
//            }

//            [HttpPut("tradproposal/edit/{proposalNo}")]
//            public async Task<IActionResult> EditTradProposal(string proposalNo, [FromBody] PolicyTradRequestDto request)
//            {
//                var result = await _repository.EditTradProposalAsync(proposalNo, request);
//                return Ok(new { message = "Proposal updated successfully", calculation = result });
//            }

//            [HttpGet("tradproposal/{proposalNo}/report")]
//            public async Task<IActionResult> DownloadTradProposalReport(string proposalNo)
//            {
//                if (string.IsNullOrEmpty(proposalNo)) return BadRequest("Proposal number is required");

//                var bytes = await _reportService.GenerateProposalReportAsync(proposalNo, isTraditional: true);
//                return File(bytes,
//                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
//                    $"TradProposal_{proposalNo}_{DateTime.Now:yyyyMMdd}.docx");
//            }

//            // -------------------------
//            // PAGED PROPOSALS BY AGENT / INSURED
//            // -------------------------
//            [HttpGet("agent/{agentCode}/paged")]
//            public async Task<IActionResult> GetProposalsByAgentCodePaged(string agentCode, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
//            {
//                var result = await _repository.GetProposalsByAgentCodePagedAsync(agentCode, page, pageSize);
//                return HandlePagedResult(result);
//            }

//            [HttpGet("traditional/agent/{agentCode}/paged")]
//            public async Task<IActionResult> GetTradProposalsByAgentCodePaged(string agentCode, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
//            {
//                var result = await _repository.GetTradProposalsByAgentCodePagedAsync(agentCode, page, pageSize);
//                return HandlePagedResult(result);
//            }

//            [HttpGet("insured/{insuredId}/paged")]
//            public async Task<IActionResult> GetProposalsByInsuredIdPaged(string insuredId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
//            {
//                var result = await _repository.GetProposalsByInsuredIdPagedAsync(insuredId, page, pageSize);
//                return HandlePagedResult(result);
//            }

//            [HttpGet("traditional/insured/{insuredId}/paged")]
//            public async Task<IActionResult> GetTradProposalsByInsuredIdPaged(string insuredId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
//            {
//                var result = await _repository.GetTradProposalsByInsuredIdPagedAsync(insuredId, page, pageSize);
//                return HandlePagedResult(result);
//            }

//            // -------------------------
//            // ADVANCED SEARCH
//            // -------------------------
//            [HttpGet("search")]
//            public async Task<IActionResult> SearchProposals(
//                [FromQuery] string? agentCode = null,
//                [FromQuery] string? insuredId = null,
//                [FromQuery] string? proposalNo = null,
//                [FromQuery] string? surname = null,
//                [FromQuery] DateTime? fromDate = null,
//                [FromQuery] DateTime? toDate = null,
//                [FromQuery] int page = 1,
//                [FromQuery] int pageSize = 10)
//            {
//                var result = await _repository.SearchProposalsAsync(agentCode, insuredId, proposalNo, surname, fromDate, toDate, page, pageSize);
//                return HandlePagedResult(result);
//            }
//        }

//        // -------------------------
//        // EXTENSION METHOD FOR PROPOSAL UPDATE
//        // -------------------------
//        public static class ProposalExtensions
//        {
//            public static void UpdateFromRequest(this PolicyMaster proposal, ProposalRequest request, dynamic calculation, string modifiedBy)
//            {
//                // Personal Info
//                proposal.SurName = request.SurName;
//                proposal.OtherNames = request.OtherNames;
//                proposal.Address = request.Address;
//                proposal.State = request.State;
//                proposal.Landphone = request.Landphone;
//                proposal.MobilePhone = request.MobilePhone;
//                proposal.Email = request.Email;
//                proposal.NationalID = request.NationalID;
//                proposal.Occupation = request.Occupation;
//                proposal.Sex = request.Gender;
//                proposal.MaritalStatus = request.MaritalStatus;
//                proposal.Country = request.Country;
//                proposal.DateofBirth = request.DateOfBirth;
//                proposal.Duration = request.Duration;
//                proposal.FOP = request.Frequency;

//                // Premiums / Calculation fields
//                proposal.SumAssured = calculation.SumAssured;
//                proposal.Premium1 = calculation.AnnualPremium;
//                proposal.Premium2 = calculation.ExtraPremium;
//                proposal.Premium3 = calculation.Loading;
//                proposal.TotalPremium = calculation.TotalPremium;
//                proposal.BasicPremium = calculation.AnnualPremium;

//                // Additional fields
//                proposal.PlaceOfBirth = request.PlaceOfBirth;
//                proposal.SpouseName = request.SpouseName;
//                proposal.GovtOfOrigin = request.GovtOfOrigin;
//                proposal.ResAddress = request.ResAddress;
//                proposal.LandMark = request.LandMark;
//                proposal.NIN = request.NIN;
//                proposal.BVN = request.BVN;

//                // Employment Info
//                proposal.EmploymentType = request.EmploymentType;
//                proposal.EmployerName = request.EmployerName;
//                proposal.EmployerAddress = request.EmployerAddress;
//                proposal.EmployerNumber = request.EmployerNumber;
//                proposal.EmployerEmail = request.EmployerEmail;
//                proposal.DateEmployed = request.DateEmployed;

//                // Health & Lifestyle
//                proposal.Height = request.Height;
//                proposal.weight = request.weight;
//                proposal.Sport = request.Sport;
//                proposal.Infirmity = request.Infirmity;
//                proposal.stroke = request.stroke;
//                proposal.Treatment = request.Treatment;
//                proposal.WorkAbesent = request.WorkAbesent;
//                proposal.Smoke = request.Smoke;
//                proposal.Aviation = request.Aviation;
//                proposal.Relgion = request.Religion;
//                proposal.Designation = request.Designation;
//                proposal.IssueDate = request.IssueDate;
//                proposal.ExpiryDate = request.ExpiryDate;

//                // Tracking
//                proposal.ModifiedBy = modifiedBy;
//                proposal.ModifiedOn = DateTime.Now;
//            }
//        }
    
    
    
//    }
//}
