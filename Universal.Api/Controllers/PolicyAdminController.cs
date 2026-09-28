using Gibs.Domain.Entities;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public partial class PolicyAdminController : ControllerBase
    {


        private readonly ProposalService _proposalService;
        private readonly ILogger<PolicyAdminController> _logger;

        public PolicyAdminController(ProposalService proposalService, ILogger<PolicyAdminController> logger)
        {
            _proposalService = proposalService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new proposal with enhanced data population
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ResponseInfo>> CreateProposal([FromBody] EnhancedCreateProposalRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _proposalService.CreateProposalAsync(request);

            if (result.ErrorCode == 0)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }

        [HttpPut("{proposalNo}/renew")]
        public async Task<ActionResult<ResponseInfo>> RenewProposal(
    string proposalNo,
    [FromBody] UpdateProposalRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _proposalService.RenewProposalAsync(proposalNo, request);

            if (result.ErrorCode == 0)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        //[HttpPost("{proposalNo}/renewal/create")]
        //public async Task<ActionResult<PolicyRenewalResponseDto>> CreatePolicyRenewal(
        //  string proposalNo,
        //  [FromBody] CreatePolicyRenewalRequestDto request)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }

        //    request.OriginalPolicyNo = proposalNo;
        //    var result = await _proposalService.CreatePolicyRenewalAsync(request);

        //    if (result.Success)
        //    {
        //        return Ok(result);
        //    }
        //    else
        //    {
        //        return BadRequest(result);
        //    }
        //}
        /// <summary>
        /// Get all occupations
        /// </summary>
        [HttpGet("occupations")]
        public async Task<ActionResult<List<object>>> GetAllOccupations([FromQuery] string tag = null)
        {
            var occupations = await _proposalService.GetAllOccupationsAsync(tag);
            return Ok(occupations);
        }
        [HttpGet("Buschannels")]
        public async Task<ActionResult<List<object>>> GetAllChannels()
        {
            var channels = await _proposalService.GetAllChannelsAsync();
            return Ok(channels);
        }


        [HttpGet("Subchannel")]
        public async Task<ActionResult<List<object>>> GetAllGroups([FromQuery] string branchId = null)
        {
            var groups = await _proposalService.GetAllGroupsAsync(branchId);
            return Ok(groups);
        }
        [HttpGet("accttype")]
        public IActionResult GetBizShareType()
        {
            var items = new List<object>
            {
                new { Text = "100% Accounting", Value = "100" },
                new { Text = "Our Share Accounting", Value = "0" }
            };

            return Ok(items);
        }


        [HttpGet("biztype")]
        public IActionResult GetPolBizType()
        {
            var items = new List<object>
            {
                new { Text = "DIRECT With OUT COINSURANCE", Value = "DIRECT" },
                new { Text = "DIRECT With COINSURANCE", Value = "CO-INSURANCE LEAD" },
                new { Text = "INWARD COINSURANCE", Value = "ACCEPTED" },
                new { Text = "FACULTATIVE INWARD", Value = "FAC-In" }
            };

            return Ok(items);
        }

        [HttpGet("bizSource")]
        public IActionResult GetPolBizSource()
        {
            var items = new List<object>
            {
                new { Text = "DIRECT", Value = "DIRECT" },
                new { Text = "AGENTS", Value = "AGENTS" },
                new { Text = "BROKERS", Value = "BROKERS" },
                new { Text = "INSURANCE COMPANIES", Value = "INSURANCE COMPANIES" },
                  new { Text = "REINSURANCE COMPANIES", Value = "REINSURANCE COMPANIES" }
            };

            return Ok(items);
        }

        /// <summary>
        /// Get all branches
        /// </summary>
        [HttpGet("branches")]
        public async Task<ActionResult<List<object>>> GetAllBranches(
            [FromQuery] string regionId = null,
            [FromQuery] string stateId = null)
        {
            var branches = await _proposalService.GetAllBranchesAsync(regionId, stateId);
            return Ok(branches);
        }

        /// <summary>
        /// Get all wax codes
        /// </summary>
        [HttpGet("waxcodes")]
        public async Task<ActionResult<List<object>>> GetAllWaxCodes(
            [FromQuery] string subRiskId = null)
        {
            var waxCodes = await _proposalService.GetAllWaxCodesAsync(subRiskId);
            return Ok(waxCodes);
        }
        /// <summary>
        /// Get all proposals with optional filtering and pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<ProposalDto>>> GetAllProposals(
            [FromQuery] string branchId = null,
            [FromQuery] string status = null,
            [FromQuery] string partyId = null,
            [FromQuery] string mktStaffId = null,
            [FromQuery] string riskClass = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string searchTerm = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string sortBy = "TransDate",
            [FromQuery] string sortDirection = "DESC")
        {
            var filter = new ProposalFilterDto
            {
                BranchId = branchId,
                Status = status,
                PartyId = partyId,
                MktStaffId = mktStaffId,
                RiskClass = riskClass,
                StartDate = startDate,
                EndDate = endDate,
                SearchTerm = searchTerm,
                Page = page,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDirection = sortDirection
            };

            var result = await _proposalService.GetAllProposalsAsync(filter);
            return Ok(result);
        }


        /// <summary>
        /// Get proposal by proposal number
        /// </summary>
        [HttpGet("{proposalNo}")]
        public async Task<ActionResult<ProposalDto>> GetProposal([FromQuery] string proposalNo)
        {


            var proposal = await _proposalService.GetProposalAsync(proposalNo);

            if (proposal == null)
            {
                return NotFound($"Proposal {proposalNo} not found");
            }

            return Ok(proposal);
        }

        /// <summary>
        /// Get the currency and exchange rate saved on a policy
        /// </summary>
        [HttpGet("{proposalNo}/exchange-rate")]
        public async Task<ActionResult<PolicyExchangeRateDto>> GetPolicyExchangeRate(string proposalNo)
        {
            var exchangeRate = await _proposalService.GetPolicyExchangeRateAsync(proposalNo);

            if (exchangeRate == null)
            {
                return NotFound($"Proposal {proposalNo} not found");
            }

            return Ok(exchangeRate);
        }

        /// <summary>
        /// Delete a proposal
        /// </summary>
        [HttpDelete("{proposalNo}")]
        public async Task<ActionResult<ResponseInfo>> DeleteProposal([FromQuery] string proposalNo)
        {
            var result = await _proposalService.DeleteProposalAsync(proposalNo);

            if (result.ErrorCode == 0)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Get complete Fire calculation breakdown showing inputs and results
        /// </summary>
        [HttpGet("{proposalNo}/calculation/fire/breakdown")]
        public async Task<ActionResult<FireCalculationBreakdownDto>> GetFireCalculationBreakdown(string proposalNo)
        {
            var breakdown = await _proposalService.GetFireCalculationBreakdownAsync(proposalNo);

            if (breakdown == null)
            {
                return NotFound($"No fire calculation found for proposal {proposalNo}");
            }

            return Ok(breakdown);
        }
        /// <summary>
        /// Get calculation summary by section
        /// </summary>
        [HttpGet("{proposalNo}/calculation/sections/summary")]
        public async Task<ActionResult<List<SectionSummaryDto>>> GetSectionCalculationSummary(string proposalNo)
        {
            var sections = await _proposalService.GetSectionCalculationSummaryAsync(proposalNo);

            if (sections == null || !sections.Any())
            {
                return NotFound($"No section calculations found for proposal {proposalNo}");
            }

            return Ok(sections);
        }


        /// <summary>
        /// Get all fire calculations for a proposal
        /// </summary>
        [HttpGet("proposalNo/calculations/fire")]
        public async Task<ActionResult<List<FireCalculationListItemDto>>> GetAllFireCalculations(
            [FromQuery] string proposalNo)
        {
            var calculations = await _proposalService.GetAllFireCalculationsAsync(proposalNo);

            if (calculations == null || !calculations.Any())
            {
                return NotFound($"No fire calculations found for proposal {proposalNo}");
            }

            return Ok(calculations);
        }

        /// <summary>
        /// Get breakdown for a specific fire calculation by DetailID
        /// </summary>
        [HttpGet("proposalNo/calculations/fire/detailID/breakdown")]
        public async Task<ActionResult<FireCalculationBreakdownDto>> GetFireCalculationBreakdownByDetailID(
            [FromQuery] string proposalNo,
            [FromQuery] string detailID)
        {
            var breakdown = await _proposalService.GetFireCalculationBreakdownByDetailIDAsync(proposalNo, detailID);

            if (breakdown == null)
            {
                return NotFound($"Fire calculation {detailID} not found for proposal {proposalNo}");
            }

            return Ok(breakdown);
        }

        /// <summary>
        /// Create additional fire calculation for existing proposal
        /// This creates a NEW PolicyDetails record
        /// </summary>
        [HttpPost("{proposalNo}/calculations/fire/additional")]
        public async Task<ActionResult<CompleteCalculationResponseDto>> CreateAdditionalFireCalculation(
            string proposalNo,
            [FromBody] CreateAdditionalFireCalculationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate sections
            if (request.Sections == null || !request.Sections.Any())
            {
                return BadRequest("At least one section with risk items is required");
            }

            foreach (var section in request.Sections)
            {
                if (section.RiskItems == null || !section.RiskItems.Any())
                {
                    return BadRequest($"Section {section.SectionID} must have at least one risk item");
                }

                foreach (var item in section.RiskItems)
                {
                    if (string.IsNullOrEmpty(item.SectionID))
                    {
                        item.SectionID = section.SectionID;
                    }
                    else if (item.SectionID != section.SectionID)
                    {
                        return BadRequest($"Risk item {item.ItemNo} has mismatched section ID");
                    }
                }
            }

            request.ProposalNo = proposalNo;
            var result = await _proposalService.CreateAdditionalFireCalculationAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }


        // The old whole-record CreateFireEndorsement/CreateReversalEndorsement flow has been
        // superseded by EndorsementController.cs's unified Additional/Reversal/Return/Redo/
        // Extension/Reduction model (POST endorsement/fire, POST endorsement/motor).

        /// <summary>
        /// Get available endorsement types
        /// </summary>
        [HttpGet("endorsement/types")]
        public ActionResult<List<string>> GetEndorsementTypes()
        {
            return Ok(EndorsementTypes.GetAll());
        }

        /// <summary>
        /// Get all endorsements for a policy
        /// </summary>
        [HttpGet("{proposalNo}/endorsements")]
        public async Task<ActionResult<List<FireCalculationListItemDto>>> GetPolicyEndorsements(
            string proposalNo)
        {
            var calculations = await _proposalService.GetAllFireCalculationsAsync(proposalNo);

            if (calculations == null || !calculations.Any())
            {
                return NotFound($"No calculations found for policy {proposalNo}");
            }

            // Filter for endorsements only (those with BizOption as endorsement types)
            var validEndorsementTypes = EndorsementTypes.GetAll();
            var endorsements = calculations
                .Where(c => validEndorsementTypes.Contains(c.CalculationType))
                .ToList();

            if (!endorsements.Any())
            {
                return NotFound($"No endorsements found for policy {proposalNo}");
            }

            return Ok(endorsements);
        }
        /// <summary>List sections of a calculation that can be renewed.</summary>
        [HttpGet("{proposalNo}/calculations/fire/{detailID}/renewable-sections")]
        public async Task<ActionResult<List<RenewableSectionDto>>> GetRenewableSections(
            string proposalNo, string detailID)
        {
            var sections = await _proposalService.GetRenewableSectionsAsync(proposalNo, detailID);
            if (sections == null || !sections.Any())
                return NotFound($"No renewable sections found for calculation {detailID}");
            return Ok(sections);
        }

        /// <summary>
        /// Renew selected sections of a calculation into a new RENEWAL calculation (UNCOMPLETED).
        /// Empty SelectedSectionTokens renews all sections.
        /// </summary>
        [HttpPost("{proposalNo}/calculations/fire/{detailID}/renew")]
        public async Task<ActionResult<RenewCalculationResponseDto>> RenewCalculationSections(
            string proposalNo, string detailID, [FromBody] RenewCalculationSectionsRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _proposalService.CreateRenewalFromCalculationAsync(proposalNo, detailID, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>List vehicles of a motor calculation that can be renewed.</summary>
        [HttpGet("{proposalNo}/calculations/motor/{detailID}/renewable-vehicles")]
        public async Task<ActionResult<List<RenewableVehicleDto>>> GetRenewableVehicles(
            string proposalNo, string detailID)
        {
            var vehicles = await _proposalService.GetRenewableVehiclesAsync(proposalNo, detailID);
            if (vehicles == null || !vehicles.Any())
                return NotFound($"No renewable vehicles found for calculation {detailID}");
            return Ok(vehicles);
        }

        /// <summary>
        /// Renew selected vehicles of a calculation into a new RENEWAL calculation (UNCOMPLETED).
        /// Empty SelectedVehicleTokens renews all vehicles.
        /// </summary>
        [HttpPost("{proposalNo}/calculations/motor/{detailID}/renew")]
        public async Task<ActionResult<RenewMotorCalculationResponseDto>> RenewMotorVehicles(
            string proposalNo, string detailID, [FromBody] RenewMotorCalculationRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _proposalService.CreateMotorRenewalFromCalculationAsync(proposalNo, detailID, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Update/recalculate a specific fire calculation
        /// This UPDATES an existing PolicyDetails record
        /// </summary>
        [HttpPut("calculations/fire/{detailID}")]
        public async Task<ActionResult<CompleteCalculationResponseDto>> UpdateFireCalculation(
            string detailID,
            [FromBody] UpdateFireCalculationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate sections
            if (request.Sections == null || !request.Sections.Any())
            {
                return BadRequest("At least one section with risk items is required");
            }

            foreach (var section in request.Sections)
            {
                if (section.RiskItems == null || !section.RiskItems.Any())
                {
                    return BadRequest($"Section {section.SectionID} must have at least one risk item");
                }

                foreach (var item in section.RiskItems)
                {
                    if (string.IsNullOrEmpty(item.SectionID))
                    {
                        item.SectionID = section.SectionID;
                    }
                    else if (item.SectionID != section.SectionID)
                    {
                        return BadRequest($"Risk item {item.ItemNo} has mismatched section ID");
                    }
                }
            }

            request.DetailID = detailID;
            var result = await _proposalService.UpdateFireCalculationAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Step 1: Calculate individual risk items for a section
        /// Returns calculated values per item including item-level discounts
        /// </summary>
        [HttpPost("calculate/risk-items")]
        public async Task<ActionResult<RiskItemCalculationResponseDto>> CalculateRiskItems(
            [FromBody] RiskItemCalculationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate risk items
            if (request.RiskItems == null || !request.RiskItems.Any())
            {
                return BadRequest("At least one risk item is required");
            }
            foreach (var item in request.RiskItems)
            {
                if (!ValidateFireAdjustmentList(item.Discounts, out var itemDiscountError))
                    return BadRequest($"Risk item {item.ItemNo}: {itemDiscountError}");
                if (!ValidateFireAdjustmentList(item.Loadings, out var itemLoadingError))
                    return BadRequest($"Risk item {item.ItemNo}: {itemLoadingError}");
            }
            var result = await _proposalService.CalculateRiskItemsAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>Get available discount/loading name suggestions for risk-item adjustment dropdowns.</summary>
        [HttpGet("fire/risk-item-adjustment-options")]
        public ActionResult<FireItemAdjustmentOptionsDto> GetFireRiskItemAdjustmentOptions()
        {
            return Ok(new FireItemAdjustmentOptionsDto());
        }
        /// <summary>
        /// Preview item-level discounts/loadings for a single risk item without saving.
        /// Loadings apply first (cascading), then discounts — same order as sections.
        /// </summary>
        [HttpPost("calculate/risk-item-adjustments")]
        public async Task<ActionResult<FireRiskItemDynamicAdjustmentResponseDto>> ApplyRiskItemAdjustments(
            [FromBody] FireRiskItemDynamicAdjustmentRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!ValidateFireAdjustmentList(request.Discounts, out var discountError))
                return BadRequest(discountError);
            if (!ValidateFireAdjustmentList(request.Loadings, out var loadingError))
                return BadRequest(loadingError);
            if (request.ItemGrossPremium < 0)
                return BadRequest("Item gross premium cannot be negative");

            var result = await _proposalService.ApplyRiskItemDynamicAdjustmentsAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        /// <summary>
        /// Step 2: Calculate section aggregate from calculated risk items
        /// This aggregates ONE section's risk items BEFORE any adjustments
        /// </summary>
        [HttpPost("calculate/section-aggregate")]
        public async Task<ActionResult<SectionAggregateResponseDto>> CalculateSectionAggregate(
            [FromBody] SectionAggregateRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate calculated items
            if (request.CalculatedItems == null || !request.CalculatedItems.Any())
            {
                return BadRequest("Calculated items are required for aggregation");
            }

            var result = await _proposalService.CalculateSectionAggregatesAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Step 3: Apply section-level adjustments (7 rates) to ONE section
        /// This should be called for EACH section after aggregating its risk items
        /// </summary>
        [HttpPost("calculate/section-adjustments")]
        public async Task<ActionResult<SectionAdjustmentResponseDto>> ApplySectionAdjustments(
            [FromBody] SectionAdjustmentRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.AggregatePremium <= 0)
            {
                return BadRequest("Aggregate premium must be greater than zero");
            }

            var result = await _proposalService.ApplySectionAdjustmentsAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        /// <summary>
        /// Step 3.5: Aggregate multiple ADJUSTED sections before proposal-level adjustments
        /// Takes sections that have already had section-level adjustments applied
        /// Returns the totals that proposal-level adjustments will be applied to
        /// </summary>
        [HttpPost("calculate/aggregate-adjusted-sections")]
        public async Task<ActionResult<AggregateAdjustedSectionsResponseDto>> AggregateAdjustedSections(
            [FromBody] AggregateAdjustedSectionsRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.AdjustedSections == null || !request.AdjustedSections.Any())
            {
                return BadRequest("At least one adjusted section is required");
            }

            decimal totalSumInsured = 0;
            decimal totalAdjustedPremium = 0;
            var sectionSummaries = new List<SectionAggregateDetail>();

            foreach (var section in request.AdjustedSections)
            {
                if (section.SectionNetPremium < 0)
                {
                    return BadRequest($"Section {section.SectionID} has invalid net premium");
                }

                sectionSummaries.Add(new SectionAggregateDetail
                {
                    SectionID = section.SectionID,
                    SectionName = section.SectionName,
                    SectionSumInsured = section.SectionSumInsured,
                    SectionAggregatePremium = section.SectionNetPremium,
                    RiskItemCount = section.RiskItemCount
                });

                totalSumInsured += section.SectionSumInsured;
                totalAdjustedPremium += section.SectionNetPremium;
            }

            return Ok(new AggregateAdjustedSectionsResponseDto
            {
                SectionSummaries = sectionSummaries,
                TotalSumInsured = totalSumInsured,
                TotalAggregatePremium = totalAdjustedPremium,
                SectionCount = request.AdjustedSections.Count,
                Success = true,
                Message = $"Successfully aggregated {request.AdjustedSections.Count} adjusted section(s). Ready for proposal-level adjustments."
            });
        }


        /// Step 4: Apply proposal-level adjustments (ONLY 2 rates)
        /// Applied to the aggregate of ALL sections AFTER section-level adjustments
        /// </summary>
        [HttpPost("calculate/proposal-adjustments")]
        public async Task<ActionResult<ProposalLevelAdjustmentResponseDto>> ApplyProposalAdjustments(
            [FromBody] ProposalAdjustmentRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.TotalAggregatePremium <= 0)
            {
                return BadRequest("Total aggregate premium must be greater than zero");
            }

            var result = await _proposalService.ApplyProposalLevelAdjustmentsAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Step 5: Calculate pro-rata premium based on cover period
        /// This is the final step applied to the adjusted premium
        /// UNCHANGED - This step is correct as-is
        /// </summary>
        [HttpPost("calculate/pro-rata")]
        public async Task<ActionResult<ProRataCalculationResponseDto>> CalculateProRata(
            [FromBody] ProRataCalculationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate inputs
            if (request.NetPremiumDue <= 0)
            {
                return BadRequest("Net premium due must be greater than zero");
            }

            if (request.CoverDays <= 0 || request.CoverDays > 366)
            {
                return BadRequest("Cover days must be between 1 and 366");
            }

            var result = await _proposalService.CalculateProRataAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Helper: Preview section calculation WITH section-level adjustments
        /// This shows what ONE section's premium will be after all section-level adjustments
        /// </summary>
        [HttpPost("calculate/section-preview")]
        public async Task<ActionResult<SectionPreviewWithAdjustmentsResponseDto>> PreviewSectionCalculation(
            [FromBody] SectionPreviewWithAdjustmentsRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.Section == null)
            {
                return BadRequest("Section is required");
            }

            if (request.Section.RiskItems == null || !request.Section.RiskItems.Any())
            {
                return BadRequest("Section must have at least one risk item");
            }

            try
            {
                // Step 1: Calculate risk items
                var itemCalcRequest = new RiskItemCalculationRequestDto
                {
                    RiskItems = request.Section.RiskItems,
                    ProportionRate = request.ProportionRate
                };
                var itemCalcResponse = await _proposalService.CalculateRiskItemsAsync(itemCalcRequest);

                if (!itemCalcResponse.Success)
                {
                    return BadRequest(itemCalcResponse.Message);
                }

                // Step 2: Aggregate section
                var aggregateRequest = new SectionAggregateRequestDto
                {
                    CalculatedItems = itemCalcResponse.CalculatedItems
                };
                var aggregateResponse = await _proposalService.CalculateSectionAggregatesAsync(aggregateRequest);

                if (!aggregateResponse.Success)
                {
                    return BadRequest(aggregateResponse.Message);
                }

                // Step 3: Apply section-level adjustments
                var sectionAdjustmentRequest = new SectionAdjustmentRequestDto
                {
                    AggregatePremium = aggregateResponse.AggregatePremium,
                    SpecialDiscountRate = request.Section.SpecialDiscountRate,
                    DeductibleDiscountRate = request.Section.DeductibleDiscountRate,
                    SpreadDiscountRate = request.Section.SpreadDiscountRate,
                    LtaDiscountRate = request.Section.LtaDiscountRate,
                    TheftLoadingRate = request.Section.TheftLoadingRate,
                    SrccLoadingRate = request.Section.SrccLoadingRate,
                    OtherLoading2Rate = request.Section.OtherLoading2Rate
                };
                var sectionAdjustmentResponse = await _proposalService.ApplySectionAdjustmentsAsync(sectionAdjustmentRequest);

                if (!sectionAdjustmentResponse.Success)
                {
                    return BadRequest(sectionAdjustmentResponse.Message);
                }

                return Ok(new SectionPreviewWithAdjustmentsResponseDto
                {
                    SectionID = request.Section.SectionID,
                    SectionName = request.Section.SectionName,
                    CalculatedItems = itemCalcResponse.CalculatedItems,
                    SectionSumInsured = aggregateResponse.AggregateSumInsured,
                    SectionGrossPremium = aggregateResponse.AggregatePremium,
                    SectionNetPremium = sectionAdjustmentResponse.NetPremiumDue,
                    SectionAdjustments = sectionAdjustmentResponse,
                    ProportionRate = request.ProportionRate,
                    ShareSumInsured = aggregateResponse.AggregateSumInsured * (request.ProportionRate / 100),
                    SharePremium = sectionAdjustmentResponse.NetPremiumDue * (request.ProportionRate / 100),
                    Success = true,
                    Message = "Section preview calculated successfully with adjustments"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing section calculation");
                return BadRequest($"Error: {ex.Message}");
            }
        }
        /// <summary>
        /// Helper: Preview complete calculation flow without saving
        /// Shows what the final premium would be with ALL sections and adjustments
        /// </summary>
        [HttpPost("calculate/preview-complete")]
        public async Task<ActionResult<CompleteCalculationPreviewDto>> PreviewCompleteCalculation(
            [FromBody] CompleteCalculationPreviewRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate sections
            if (request.Sections == null || !request.Sections.Any())
            {
                return BadRequest("At least one section is required");
            }

            foreach (var section in request.Sections)
            {
                if (section.RiskItems == null || !section.RiskItems.Any())
                {
                    return BadRequest($"Section {section.SectionID} must have at least one risk item");
                }
            }

            try
            {
                var sectionPreviews = new List<SectionPreviewWithAdjustmentsResponseDto>();
                decimal totalSumInsured = 0;
                decimal totalGrossPremium = 0;

                // Step 1-3: Process each section with section-level adjustments
                foreach (var section in request.Sections)
                {
                    var sectionPreviewRequest = new SectionPreviewWithAdjustmentsRequestDto
                    {
                        Section = section,
                        ProportionRate = request.ProportionRate
                    };

                    // Step 1: Calculate risk items
                    var itemCalcRequest = new RiskItemCalculationRequestDto
                    {
                        RiskItems = section.RiskItems,
                        ProportionRate = request.ProportionRate
                    };
                    var itemCalcResponse = await _proposalService.CalculateRiskItemsAsync(itemCalcRequest);

                    if (!itemCalcResponse.Success)
                    {
                        return BadRequest($"Section {section.SectionID} calculation failed: {itemCalcResponse.Message}");
                    }

                    // Step 2: Aggregate section
                    var aggregateRequest = new SectionAggregateRequestDto
                    {
                        CalculatedItems = itemCalcResponse.CalculatedItems
                    };
                    var aggregateResponse = await _proposalService.CalculateSectionAggregatesAsync(aggregateRequest);

                    if (!aggregateResponse.Success)
                    {
                        return BadRequest($"Section {section.SectionID} aggregation failed: {aggregateResponse.Message}");
                    }

                    // Step 3: Apply section-level adjustments
                    var sectionAdjustmentRequest = new SectionAdjustmentRequestDto
                    {
                        AggregatePremium = aggregateResponse.AggregatePremium,
                        SpecialDiscountRate = section.SpecialDiscountRate,
                        DeductibleDiscountRate = section.DeductibleDiscountRate,
                        SpreadDiscountRate = section.SpreadDiscountRate,
                        LtaDiscountRate = section.LtaDiscountRate,
                        TheftLoadingRate = section.TheftLoadingRate,
                        SrccLoadingRate = section.SrccLoadingRate,
                        OtherLoading2Rate = section.OtherLoading2Rate
                    };
                    var sectionAdjustmentResponse = await _proposalService.ApplySectionAdjustmentsAsync(sectionAdjustmentRequest);

                    if (!sectionAdjustmentResponse.Success)
                    {
                        return BadRequest($"Section {section.SectionID} adjustments failed: {sectionAdjustmentResponse.Message}");
                    }

                    var sectionPreview = new SectionPreviewWithAdjustmentsResponseDto
                    {
                        SectionID = section.SectionID,
                        SectionName = section.SectionName,
                        CalculatedItems = itemCalcResponse.CalculatedItems,
                        SectionSumInsured = aggregateResponse.AggregateSumInsured,
                        SectionGrossPremium = aggregateResponse.AggregatePremium,
                        SectionNetPremium = sectionAdjustmentResponse.NetPremiumDue,
                        SectionAdjustments = sectionAdjustmentResponse,
                        Success = true
                    };

                    sectionPreviews.Add(sectionPreview);
                    totalSumInsured += aggregateResponse.AggregateSumInsured;
                    totalGrossPremium += sectionAdjustmentResponse.NetPremiumDue; // Use adjusted premium
                }

                // Step 4: Apply proposal-level adjustments (ONLY 2 rates)
                var proposalAdjustmentRequest = new ProposalAdjustmentRequestDto
                {
                    TotalAggregatePremium = totalGrossPremium,
                    OtherDiscountsRate = request.OtherDiscountsRate,
                    OtherLoadingsRate = request.OtherLoadingsRate
                };
                var proposalAdjustmentResponse = await _proposalService.ApplyProposalLevelAdjustmentsAsync(proposalAdjustmentRequest);

                if (!proposalAdjustmentResponse.Success)
                {
                    return BadRequest($"Proposal adjustments failed: {proposalAdjustmentResponse.Message}");
                }

                decimal totalNetPremium = proposalAdjustmentResponse.NetPremiumDue;

                // Step 5: Calculate pro-rata
                var proRataRequest = new ProRataCalculationRequestDto
                {
                    NetPremiumDue = totalNetPremium,
                    CoverDays = request.CoverDays
                };
                var proRataResponse = await _proposalService.CalculateProRataAsync(proRataRequest);

                if (!proRataResponse.Success)
                {
                    return BadRequest($"Pro-rata calculation failed: {proRataResponse.Message}");
                }

                // Build preview response
                return Ok(new CompleteCalculationPreviewDto
                {
                    SectionPreviews = sectionPreviews,
                    TotalSumInsured = totalSumInsured,
                    TotalGrossPremium = totalGrossPremium,
                    TotalNetPremium = totalNetPremium,
                    ProRataPremium = proRataResponse.ProRataPremium,
                    FinalPremiumDue = proRataResponse.PremiumDue,
                    ShareSumInsured = totalSumInsured * (request.ProportionRate / 100),
                    SharePremium = proRataResponse.PremiumDue * (request.ProportionRate / 100),
                    ForeignSumInsured = totalSumInsured * request.ExchangeRate,
                    ForeignPremium = proRataResponse.PremiumDue * request.ExchangeRate,
                    ProposalLevelAdjustments = proposalAdjustmentResponse,
                    ProRataDetails = proRataResponse,
                    ProportionRate = request.ProportionRate,
                    ExchangeRate = request.ExchangeRate,
                    Currency = request.Currency,
                    CoverDays = request.CoverDays,
                    Success = true,
                    Message = "Complete calculation preview generated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing complete calculation");
                return BadRequest($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Complete Fire proposal calculation with section-level adjustments and save to database
        /// </summary>
        [HttpPost("{proposalNo}/calculate/fire/complete")]
        public async Task<ActionResult<CompleteCalculationResponseDto>> CompleteFireCalculation(
     string proposalNo,
     [FromBody] CompleteFireCalculationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.Sections == null || !request.Sections.Any())
            {
                return BadRequest("At least one section with risk items is required for calculation");
            }

            foreach (var section in request.Sections)
            {
                if (section.RiskItems == null || !section.RiskItems.Any())
                {
                    return BadRequest($"Section {section.SectionID} must have at least one risk item");
                }

                //foreach (var item in section.RiskItems)
                //{
                //    if (string.IsNullOrEmpty(item.SectionID))
                //    {
                //        item.SectionID = section.SectionID;
                //    }
                //    else if (item.SectionID != section.SectionID)
                //    {
                //        return BadRequest($"Risk item {item.ItemNo} has mismatched section ID");
                //    }
                //}
            }

            request.ProposalNo = proposalNo;
            var result = await _proposalService.CompleteFireCalculationAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>Get available discount/loading name suggestions for section adjustment dropdowns.</summary>
        [HttpGet("fire/section-adjustment-options")]
        public ActionResult<FireAdjustmentOptionsDto> GetFireAdjustmentOptions()
        {
            return Ok(new FireAdjustmentOptionsDto());
        }

        private static bool ValidateFireAdjustmentList(List<FireAdjustmentDto> adjustments, out string error)
        {
            error = null;
            if (adjustments == null) return true;

            foreach (var adjustment in adjustments)
            {
                if (string.IsNullOrEmpty(adjustment.AdjustmentName))
                {
                    error = "Adjustment name is required";
                    return false;
                }
                if (adjustment.Rate < 0)
                {
                    error = $"Adjustment rate cannot be negative. Got: {adjustment.Rate}";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Add one Section (with its own risk-item grid) to the proposal's Fire
        /// declaration. Persists it and recalculates the declaration's totals.
        /// Incremental — works against whatever's currently persisted, NOT a
        /// resubmission of the full section list. Do not mix with the existing
        /// bulk /calculate/fire/complete endpoint in the same editing session
        /// without resubmitting the complete section list there first.
        /// </summary>
        [HttpPost("fire/sections")]
        public async Task<ActionResult<FireDeclarationRecomputeResponseDto>> AddFireSection(
            [FromQuery] string proposalNo, [FromBody] FireSectionIncrementalInputDto section)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (section.RiskItems == null || !section.RiskItems.Any())
                return BadRequest("At least one risk item is required");
            if (!ValidateFireAdjustmentList(section.Discounts, out var discountError))
                return BadRequest(discountError);
            if (!ValidateFireAdjustmentList(section.Loadings, out var loadingError))
                return BadRequest(loadingError);

            var result = await _proposalService.AddFireSectionAsync(proposalNo, section);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Edit an existing Section's fields/items/adjustments. Note: since
        /// the underlying persist method always mints a fresh section
        /// declaration number (it has no way to be told to reuse the old
        /// one), this deletes the old section entirely and re-persists it as
        /// a new section row — check the response Message for the new
        /// section number if you need to reference it again afterward.
        /// </summary>
        [HttpPut("fire/sections")]
        public async Task<ActionResult<FireDeclarationRecomputeResponseDto>> UpdateFireSection(
            [FromQuery] string proposalNo, [FromQuery] string sectionDeclarationNo,
            [FromBody] FireSectionIncrementalInputDto section)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (section.RiskItems == null || !section.RiskItems.Any())
                return BadRequest("At least one risk item is required");
            if (!ValidateFireAdjustmentList(section.Discounts, out var discountError))
                return BadRequest(discountError);
            if (!ValidateFireAdjustmentList(section.Loadings, out var loadingError))
                return BadRequest(loadingError);

            var result = await _proposalService.UpdateFireSectionAsync(proposalNo, sectionDeclarationNo, section);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Remove one Section from the proposal's Fire declaration and
        /// recalculate the declaration's totals.
        /// </summary>
        [HttpDelete("fire/sections")]
        public async Task<ActionResult<FireDeclarationRecomputeResponseDto>> RemoveFireSection(
            [FromQuery] string proposalNo, [FromQuery] string sectionDeclarationNo)
        {
            var result = await _proposalService.RemoveFireSectionAsync(proposalNo, sectionDeclarationNo);
            return result.Success ? Ok(result) : BadRequest(result);
        }



        /// <summary>
        /// Get list of available calculations that can be used for renewal
        /// </summary>
        [HttpGet("{originalPolicyNo}/renewal/available-calculations")]
        public async Task<ActionResult<List<RenewalCalculationOptionDto>>> GetAvailableCalculationsForRenewal(
            string originalPolicyNo)
        {
            var calculations = await _proposalService.GetAllFireCalculationsAsync(originalPolicyNo);

            if (calculations == null || !calculations.Any())
            {
                return NotFound($"No calculations found for policy {originalPolicyNo}");
            }

            var options = calculations
                .Where(c => c.Status != "DELETED")
                .Select(c => new RenewalCalculationOptionDto
                {
                    DetailID = c.DetailID,
                    CertOrDocNo = c.CertOrDocNo,
                    CalculatedOn = c.CalculatedOn,
                    CalculatedBy = c.CalculatedBy,
                    CalculationType = c.CalculationType,
                    TotalSumInsured = c.TotalSumInsured,
                    TotalPremium = c.TotalPremium,
                    SectionCount = c.SectionCount,
                    Status = c.Status,
                })
                .OrderByDescending(c => c.CalculatedOn)
                .ToList();

            return Ok(options);
        }


        /// <summary>
        /// Recalculate existing Fire proposal with section-level adjustments
        /// </summary>
        [HttpPut("{proposalNo}/calculate/fire/complete")]
        public async Task<ActionResult<CompleteCalculationResponseDto>> RecalculateFireCalculation(
            string proposalNo,
            [FromBody] RecalculateFireCalculationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate that sections are provided
            if (request.Sections == null || !request.Sections.Any())
            {
                return BadRequest("At least one section with risk items is required for recalculation");
            }

            // Validate that each section has risk items
            foreach (var section in request.Sections)
            {
                if (section.RiskItems == null || !section.RiskItems.Any())
                {
                    return BadRequest($"Section {section.SectionID} must have at least one risk item");
                }

                // Validate that all risk items in the section have the correct SectionID
                foreach (var item in section.RiskItems)
                {
                    if (string.IsNullOrEmpty(item.SectionID))
                    {
                        item.SectionID = section.SectionID;
                    }
                    else if (item.SectionID != section.SectionID)
                    {
                        return BadRequest($"Risk item {item.ItemNo} has mismatched section ID. Expected: {section.SectionID}, Got: {item.SectionID}");
                    }
                }
            }

            request.ProposalNo = proposalNo;
            var result = await _proposalService.RecalculateFireCalculationAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        /// <summary>
        /// Get current calculation data for modification
        /// </summary>
        [HttpGet("{proposalNo}/calculation/current")]
        public async Task<ActionResult<CurrentCalculationDto>> GetCurrentCalculation(string proposalNo)
        {
            var calculation = await _proposalService.GetCurrentCalculationAsync(proposalNo);

            if (calculation == null)
            {
                return NotFound($"No calculation found for proposal {proposalNo}");
            }

            return Ok(calculation);
        }

        /// <summary>
        /// Calculate and save motor calculation to database
        /// This performs complete calculation and persists to database
        /// </summary>
        [HttpPost("{proposalNo}/calculate/complete")]
        public async Task<ActionResult<MotorCalculationResponseDto>> CalculateAndSave(
            string proposalNo,
            [FromBody] MotorCalculationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.Vehicles == null || request.Vehicles.Count == 0)
            {
                return BadRequest("At least one vehicle is required");
            }

            // Validate each vehicle
            for (int i = 0; i < request.Vehicles.Count; i++)
            {
                var vehicle = request.Vehicles[i];

                if (vehicle.VehicleValue <= 0)
                {
                    return BadRequest($"Vehicle {i + 1}: Vehicle value must be greater than zero");
                }

                if (string.IsNullOrEmpty(vehicle.VehicleType))
                {
                    return BadRequest($"Vehicle {i + 1}: Vehicle type is required");
                }

                if (string.IsNullOrEmpty(vehicle.CoverType))
                {
                    return BadRequest($"Vehicle {i + 1}: Cover type is required");
                }

                // Validate adjustments
                if (vehicle.Discounts != null)
                {
                    foreach (var discount in vehicle.Discounts)
                    {
                        if (string.IsNullOrEmpty(discount.AdjustmentName))
                        {
                            return BadRequest($"Vehicle {i + 1}: Discount name is required");
                        }
                        if (discount.Rate < 0 || discount.Rate > 100)
                        {
                            return BadRequest($"Vehicle {i + 1}: Discount rate must be between 0 and 100");
                        }
                    }
                }

                if (vehicle.Loadings != null)
                {
                    foreach (var loading in vehicle.Loadings)
                    {
                        if (string.IsNullOrEmpty(loading.AdjustmentName))
                        {
                            return BadRequest($"Vehicle {i + 1}: Loading name is required");
                        }
                        if (loading.Rate < 0)
                        {
                            return BadRequest($"Vehicle {i + 1}: Loading rate cannot be negative");
                        }
                    }
                }
            }

            request.ProposalNo = proposalNo;

            // Calculate
            var calculation = await _proposalService.CalculateMotorRiskItemsAsync(request);

            if (!calculation.Success)
            {
                return BadRequest(calculation);
            }

            // Save
            var saveResult = await _proposalService.SaveMotorCalculationAsync(calculation);

            if (saveResult.ErrorCode != 0)
            {
                return BadRequest(saveResult);
            }

            return Ok(calculation);
        }

        /// <summary>
        /// Recalculate existing motor calculation
        /// Updates existing calculation with new values
        /// </summary>
        //[HttpPut("{proposalNo}/calculate/complete")]
        //public async Task<ActionResult<MotorCalculationResponseDto>> Recalculate(
        //    string proposalNo,
        //    [FromBody] MotorCalculationRequestDto request)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }

        //    // Check if proposal exists
        //    var proposal = await _proposalService.GetProposalAsync(proposalNo);
        //    if (proposal == null)
        //    {
        //        return NotFound($"Proposal {proposalNo} not found");
        //    }

        //    if (request.Vehicles == null || request.Vehicles.Count == 0)
        //    {
        //        return BadRequest("At least one vehicle is required");
        //    }

        //    request.ProposalNo = proposalNo;

        //    // Calculate
        //    var calculation = await _proposalService.CalculateMotorRiskItemsAsync(request);

        //    if (!calculation.Success)
        //    {
        //        return BadRequest(calculation);
        //    }

        //    // Save (will update existing records)
        //    var saveResult = await _proposalService.SaveMotorCalculationAsync(calculation);

        //    if (saveResult.ErrorCode != 0)
        //    {
        //        return BadRequest(saveResult);
        //    }

        //    return Ok(calculation);
        //}
        //[HttpPost("save")]
        //public async Task<ActionResult<ResponseInfo>> SaveMotorCalculation(
        //    [FromBody] MotorCalculationResponseDto calculation)
        //{
        //    var result = await _proposalService.SaveMotorCalculationAsync(calculation);

        //    if (result.ErrorCode != 0)
        //    {
        //        return BadRequest(result);
        //    }

        //    return Ok(result);
        //}

        /// <summary>
        /// Calculate and save motor calculation in one step
        /// Includes proposal-level adjustments
        /// </summary>
        [HttpPost("{proposalNo}/calculate/motor/complete")]
        public async Task<ActionResult<MotorCalculationResponseDto>> CalculateAndSaveMotor(
            string proposalNo,
            [FromBody] MotorCalculationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate vehicles
            if (request.Vehicles == null || !request.Vehicles.Any())
            {
                return BadRequest("At least one vehicle is required");
            }

            // Validate each vehicle
            for (int i = 0; i < request.Vehicles.Count; i++)
            {
                var vehicle = request.Vehicles[i];

                if (vehicle.VehicleValue <= 0)
                {
                    return BadRequest($"Vehicle {i + 1}: Vehicle value must be greater than zero");
                }

                if (string.IsNullOrEmpty(vehicle.VehicleType))
                {
                    return BadRequest($"Vehicle {i + 1}: Vehicle type is required");
                }

                if (string.IsNullOrEmpty(vehicle.CoverType))
                {
                    return BadRequest($"Vehicle {i + 1}: Cover type is required");
                }
            }

            request.ProposalNo = proposalNo;

            // Calculate with proposal-level adjustments
            var calculation = await _proposalService.CalculateMotorCompleteAsync(request);

            if (!calculation.Success)
            {
                return BadRequest(calculation);
            }

            // Save
            // Save with deleteExisting = false (this is a new calculation)
            var saveResult = await _proposalService.SaveMotorCalculationAsync(calculation, deleteExisting: false);

            if (saveResult.ErrorCode != 0)
            {
                return BadRequest(saveResult);
            }
            return Ok(calculation);
        }
        /// <summary>
        /// Get available adjustment options for dropdowns
        /// </summary>
        [HttpGet("adjustment-options")]
        public ActionResult<MotorAdjustmentOptionsDto> GetAdjustmentOptions()
        {
            return Ok(new MotorAdjustmentOptionsDto());
        }

        // ============================================
        // STEP-BY-STEP CALCULATION ENDPOINTS
        // ============================================

        /// <summary>
        /// STEP 1: Calculate basic premium for a vehicle
        /// Only requires vehicle type, value, rate, cover type, and usage
        /// </summary>
        [HttpPost("motor/calculate/step1-basic-premium")]
        public async Task<ActionResult<MotorBasicPremiumResponseDto>> CalculateBasicPremium(
            [FromBody] MotorStep1RequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.VehicleValue <= 0)
            {
                return BadRequest("Vehicle value must be greater than zero");
            }

            var result = await _proposalService.CalculateMotorBasicPremiumAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// STEP 2: Apply discounts to basic premium
        /// Only requires basic premium, vehicle value, and discount list
        /// </summary>
        [HttpPost("motor/calculate/step2-apply-discounts")]
        public async Task<ActionResult<MotorDiscountsResponseDto>> ApplyDiscounts(
            [FromBody] MotorStep2RequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.BasicPremium <= 0)
            {
                return BadRequest("Basic premium must be greater than zero");
            }

            // Validate discounts
            if (request.Discounts != null)
            {
                foreach (var discount in request.Discounts)
                {
                    if (string.IsNullOrEmpty(discount.AdjustmentName))
                    {
                        return BadRequest("Discount name is required");
                    }
                    if (discount.Rate < 0 || discount.Rate > 100)
                    {
                        return BadRequest($"Discount rate must be between 0 and 100. Got: {discount.Rate}");
                    }
                }
            }

            var result = await _proposalService.ApplyMotorDiscountsAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// STEP 3: Apply loadings after discounts
        /// Only requires premium after discounts, vehicle value, and loading list
        /// </summary>
        [HttpPost("motor/calculate/step3-apply-loadings")]
        public async Task<ActionResult<MotorLoadingsResponseDto>> ApplyLoadings(
            [FromBody] MotorStep3RequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.PremiumAfterDiscounts < 0)
            {
                return BadRequest("Premium after discounts cannot be negative");
            }

            // Validate loadings
            if (request.Loadings != null)
            {
                foreach (var loading in request.Loadings)
                {
                    if (string.IsNullOrEmpty(loading.AdjustmentName))
                    {
                        return BadRequest("Loading name is required");
                    }
                    if (loading.Rate < 0)
                    {
                        return BadRequest($"Loading rate cannot be negative. Got: {loading.Rate}");
                    }
                }
            }

            var result = await _proposalService.ApplyMotorLoadingsAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// STEP 4: Calculate final premium by adding additional costs
        /// Only requires gross premium, tracking cost, and rescue cost
        /// </summary>
        [HttpPost("motor/calculate/step4-final-premium")]
        public async Task<ActionResult<MotorFinalPremiumResponseDto>> CalculateFinalPremium(
            [FromBody] MotorStep4RequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.GrossPremium < 0)
            {
                return BadRequest("Gross premium cannot be negative");
            }

            var result = await _proposalService.CalculateMotorFinalPremiumAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// STEP 5: Aggregate all vehicles (sum their net premiums and values)
        /// Only requires list of calculated vehicles
        /// </summary>
        [HttpPost("motor/calculate/step5-aggregate-vehicles")]
        public async Task<ActionResult<MotorAggregateVehiclesResponseDto>> AggregateVehicles(
            [FromBody] MotorStep5RequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.Vehicles == null || !request.Vehicles.Any())
            {
                return BadRequest("At least one vehicle is required");
            }

            var result = await _proposalService.AggregateVehiclesAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// STEP 6: Apply proposal-level adjustments (other discount and other loading)
        /// Only requires aggregate premium and the two rates
        /// </summary>
        [HttpPost("motor/calculate/step6-proposal-adjustments")]
        public async Task<ActionResult<MotorProposalAdjustmentsResponseDto>> ApplyProposalAdjustments(
            [FromBody] MotorStep6RequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.AggregatePremium <= 0)
            {
                return BadRequest("Aggregate premium must be greater than zero");
            }

            var result = await _proposalService.ApplyMotorProposalAdjustmentsAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// STEP 7: Calculate pro-rata and apply proportion rate
        /// Only requires net premium, cover days, and proportion rate
        /// </summary>
        [HttpPost("motor/calculate/step7-final-calculation")]
        public async Task<ActionResult<MotorFinalCalculationResponseDto>> CalculateFinal(
            [FromBody] MotorStep7RequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.NetPremium <= 0)
            {
                return BadRequest("Net premium must be greater than zero");
            }

            if (request.CoverDays <= 0 || request.CoverDays > 366)
            {
                return BadRequest("Cover days must be between 1 and 366");
            }

            var result = await _proposalService.CalculateMotorFinalAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }


        ///// <summary>
        ///// Get detailed calculation breakdown showing all inputs and results
        ///// Shows step-by-step calculation for each vehicle and proposal-level adjustments
        ///// </summary>
        //[HttpGet("{proposalNo}/motor/calculation/breakdown")]
        //public async Task<ActionResult<MotorCalculationBreakdownDto>> GetMotorCalculationBreakdown(
        //    string proposalNo)
        //{
        //    var breakdown = await _proposalService.GetMotorCalculationBreakdownAsync(proposalNo);

        //    if (breakdown == null)
        //    {
        //        return NotFound($"No motor calculation found for proposal {proposalNo}");
        //    }

        //    return Ok(breakdown);
        //}


        ///// <summary>
        ///// Recalculate existing motor calculation
        ///// Updates existing calculation with new values including proposal-level adjustments
        ///// </summary>
        //[HttpPut("{proposalNo}/calculate/motor/complete")]
        //public async Task<ActionResult<MotorCalculationResponseDto>> RecalculateMotor(
        //    string proposalNo,
        //    [FromBody] MotorCalculationRequestDto request)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }

        //    // Check if proposal exists
        //    var proposal = await _proposalService.GetProposalAsync(proposalNo);
        //    if (proposal == null)
        //    {
        //        return NotFound($"Proposal {proposalNo} not found");
        //    }

        //    // Validate vehicles
        //    if (request.Vehicles == null || !request.Vehicles.Any())
        //    {
        //        return BadRequest("At least one vehicle is required");
        //    }

        //    request.ProposalNo = proposalNo;

        //    // Recalculate with proposal-level adjustments
        //    var calculation = await _proposalService.CalculateMotorCompleteAsync(request);

        //    if (!calculation.Success)
        //    {
        //        return BadRequest(calculation);
        //    }

        //    // Save with deleteExisting = true (this will delete all existing data first)
        //    var saveResult = await _proposalService.SaveMotorCalculationAsync(calculation, deleteExisting: true);

        //    if (saveResult.ErrorCode != 0)
        //    {
        //        return BadRequest(saveResult);
        //    }


        //    return Ok(calculation);
        //}
        /// <summary>
        /// Create an additional motor calculation (new PolicyDetails record)
        /// </summary>
        [HttpPost("{proposalNo}/calculations/motor/additional")]
        public async Task<ActionResult<MotorCalculationResponseDto>> CreateAdditionalMotorCalculation(
            string proposalNo,
            [FromBody] MotorCalculationRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (request.Vehicles == null || !request.Vehicles.Any())
                return BadRequest("At least one vehicle is required");

            request.ProposalNo = proposalNo;
            var result = await _proposalService.CreateAdditionalMotorCalculationAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Update/recalculate a specific motor calculation by DetailID
        /// </summary>
        [HttpPut("calculations/motor/{detailID}")]
        public async Task<ActionResult<MotorCalculationResponseDto>> UpdateMotorCalculation(
            string detailID,
            [FromBody] UpdateMotorCalculationRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (request.Vehicles == null || !request.Vehicles.Any())
                return BadRequest("At least one vehicle is required");

            request.DetailID = detailID;
            var result = await _proposalService.UpdateMotorCalculationAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// List all motor calculations for a policy
        /// </summary>
        [HttpGet("proposalNo/calculations/motor")]
        public async Task<ActionResult<List<MotorCalculationListItemDto>>> GetAllMotorCalculations([FromQuery] string proposalNo)
        {
            var calcs = await _proposalService.GetAllMotorCalculationsAsync(proposalNo);
            if (calcs == null || !calcs.Any())
                return NotFound($"No motor calculations found for policy {proposalNo}");
            return Ok(calcs);
        }

        /// <summary>
        /// Breakdown for a specific motor calculation by DetailID
        /// </summary>
        [HttpGet("proposalNo/calculations/motor/detailID/breakdown")]
        public async Task<ActionResult<MotorCalculationBreakdownDto>> GetMotorCalculationBreakdownByDetailID(
            [FromQuery] string proposalNo, [FromQuery] string detailID)
        {
            var breakdown = await _proposalService.GetMotorCalculationBreakdownByDetailIDAsync(proposalNo, detailID);
            if (breakdown == null)
                return NotFound($"Motor calculation {detailID} not found for policy {proposalNo}");
            return Ok(breakdown);
        }
        ///// <summary>
        ///// Delete a specific vehicle from motor calculation
        ///// </summary>
        //[HttpDelete("{proposalNo}/motor/vehicles/{certificateNo}")]
        //public async Task<ActionResult<ResponseInfo>> DeleteMotorVehicle(
        //    string proposalNo,
        //    string certificateNo)
        //{
        //    var result = await _proposalService.DeleteMotorVehicleAsync(proposalNo, certificateNo);

        //    if (result.ErrorCode != 0)
        //    {
        //        return BadRequest(result);
        //    }

        //    return Ok(result);
        //}

        ///// <summary>
        ///// Add a single vehicle to existing motor calculation
        ///// </summary>
        //[HttpPost("{proposalNo}/motor/vehicles/add")]
        //public async Task<ActionResult<CalculatedMotorRiskItemDto>> AddMotorVehicle(
        //    string proposalNo,
        //    [FromBody] AddMotorVehicleRequestDto request)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }

        //    var result = await _proposalService.AddMotorVehicleAsync(proposalNo, request);

        //    if (result == null)
        //    {
        //        return BadRequest("Failed to add vehicle");
        //    }

        //    return Ok(result);
        //}

        ///// <summary>
        ///// Update a specific vehicle in motor calculation
        ///// </summary>
        //[HttpPut("{proposalNo}/motor/vehicles/{certificateNo}")]
        //public async Task<ActionResult<CalculatedMotorRiskItemDto>> UpdateMotorVehicle(
        //    string proposalNo,
        //    string certificateNo,
        //    [FromBody] MotorRiskItemInputDto request)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }

        //    var result = await _proposalService.UpdateMotorVehicleAsync(proposalNo, certificateNo, request);

        //    if (result == null)
        //    {
        //        return NotFound($"Vehicle {certificateNo} not found");
        //    }

        //    return Ok(result);
        //}


        /// <summary>
        /// Get available discount and loading types for motor
        /// </summary>
        [HttpGet("motor/adjustment-types")]
        public ActionResult<MotorAdjustmentTypesDto> GetMotorAdjustmentTypes()
        {
            return Ok(new MotorAdjustmentTypesDto
            {
                StandardDiscounts = new List<string>
        {
            "NO CLAIMS DISCOUNT",
            "FLEET DISCOUNT",
            "SPECIAL DISCOUNT",
            "LOYALTY DISCOUNT"
        },
                StandardLoadings = new List<string>
        {
            "SRCC",
            "FLOOD",
            "EXCESS BUY BACK",
            "TPFT",
            "YOUNG DRIVER LOADING",
            "HIGH RISK AREA LOADING"
        },
                CoverTypes = new List<string>
        {
            "COMPREHENSIVE",
            "THIRD_PARTY",
            "THIRD_PARTY_FIRE_THEFT"
        },
                UsageTypes = new List<string>
        {
            "PRIVATE",
            "COMMERCIAL",
            "HIRE_PURCHASE"
        }
            });
        }


        ///// <summary>
        ///// Get calculated vehicles for a proposal
        ///// </summary>
        //[HttpGet("{proposalNo}/motor/vehicles")]
        //public async Task<ActionResult<List<CalculatedMotorRiskItemDto>>> GetMotorVehicles(
        //    string proposalNo)
        //{
        //    var vehicles = await _proposalService.GetMotorVehiclesAsync(proposalNo);

        //    if (vehicles == null || !vehicles.Any())
        //    {
        //        return NotFound($"No vehicles found for proposal {proposalNo}");
        //    }

        //    return Ok(vehicles);
        //}

        /// <summary>
        /// Add one vehicle to the proposal's Motor declaration. Persists it
        /// and recalculates the declaration's totals against Motor's existing
        /// Other Discount/Loading 2-rate box. Incremental — works against
        /// whatever's currently persisted, NOT a resubmission of the full
        /// vehicle list. Do not mix with Motor's existing bulk calculate-and-
        /// save endpoint in the same editing session without resubmitting the
        /// complete vehicle list there first.
        /// </summary>
        [HttpPost("motor/vehicles")]
        public async Task<ActionResult<MotorCalculationResponseDto>> AddMotorVehicle(
            [FromQuery] string proposalNo, [FromBody] MotorRiskItemInputDto vehicle)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (vehicle.VehicleValue <= 0)
                return BadRequest("Vehicle value must be greater than zero");

            var result = await _proposalService.AddMotorVehicleAsync(proposalNo, vehicle);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Edit an existing vehicle's fields/rates in place — keeps its
        /// CertificateNo, recalculates the vehicle, then recalculates the
        /// declaration's totals (same recompute as AddMotorVehicle/RemoveMotorVehicle).
        /// </summary>
        [HttpPut("motor/vehicles")]
        public async Task<ActionResult<MotorCalculationResponseDto>> UpdateMotorVehicle(
            [FromQuery] string proposalNo, [FromQuery] string certificateNo, [FromBody] MotorRiskItemInputDto vehicle)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (vehicle.VehicleValue <= 0)
                return BadRequest("Vehicle value must be greater than zero");

            var result = await _proposalService.UpdateMotorVehicleAsync(proposalNo, certificateNo, vehicle);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Remove one vehicle from the proposal's Motor declaration and
        /// recalculate the declaration's totals — same recompute logic as
        /// AddMotorVehicle above.
        /// </summary>
        [HttpDelete("motor/vehicles")]
        public async Task<ActionResult<MotorCalculationResponseDto>> RemoveMotorVehicle(
            [FromQuery] string proposalNo, [FromQuery] string certificateNo)
        {
            var result = await _proposalService.RemoveMotorVehicleAsync(proposalNo, certificateNo);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
