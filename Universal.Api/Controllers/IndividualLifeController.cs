using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    //[Authorize(Roles = "APP")]
    public class IndividualLifeController : SecureControllerBase
    {
        private readonly Settings _settings;
        public IndividualLifeController(Repository repository, AuthContext authContext, Settings settings) : base(repository, authContext)
        {
            _settings = settings;
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
            if (request == null)
                return BadRequest("Request is null");

            var calculation = await _repository.CalculateAsync(new QuotationRequest
            {
                //QuoteNo = request.ProposalNo,
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

            await _repository.SaveProposalAsync(request, calculation);

            return Ok(new { message = "Proposal created successfully", calculation });
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

                // Tracking
                proposal.ModifiedBy = "system";
                proposal.ModifiedOn = DateTime.Now;
            }

            await _repository.UpdateRangeAsync(proposals);

            return Ok(new
            {
                message = "Proposal updated successfully",
                recalculated = calculation
            }); }
              catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpGet("{proposalNo}")]
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
            try { 
            var calculation = await _repository.CalculateTraditionalAsync(request);

            return Ok(new
            {
                message = "Traditional Proposal created successfully.",
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



    }
}
