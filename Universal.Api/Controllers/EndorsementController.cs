using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    // -----------------------------------------------------------------------------------------------
    // Endorsement endpoints for fire and motor.
    //
    //   GET  endorsement/fire/types
    //   GET  endorsement/fire/sections?policyNo=..&detailId=..   sections to tick for RETURN / REDO
    //   GET  endorsement/fire?policyNo=..
    //   POST endorsement/fire?policyNo=..
    //
    //   GET  endorsement/motor/types
    //   GET  endorsement/motor/vehicles?policyNo=..&detailId=..  vehicles to tick for RETURN / REDO
    //   GET  endorsement/motor?policyNo=..
    //   POST endorsement/motor?policyNo=..
    //
    // The class-level Route supplies the "endorsement" prefix, so every action template below is
    // relative and short.
    //
    // Policy numbers contain forward slashes, so they always travel as a query parameter — never a
    // route segment.
    // -----------------------------------------------------------------------------------------------
    [Route("endorsement")]
    [ApiController]
    public class EndorsementController : ControllerBase
    {
        private readonly ProposalService _proposalService;
        private readonly ILogger<EndorsementController> _logger;

        public EndorsementController(ProposalService proposalService, ILogger<EndorsementController> logger)
        {
            _proposalService = proposalService;
            _logger = logger;
        }

        #region Fire

        /// <summary>The fire endorsement types this API accepts, with the aliases the screens use.</summary>
        [HttpGet("fire/types")]
        public IActionResult GetFireEndorsementTypes()
        {
            return Ok(new
            {
                success = true,
                unit = "section",
                data = new[]
                {
                    new { code = EndorsementTypes.Additional, label = "Additional", description = "Adds new sections, pro-rated to the policy's expiry. The original record and sections are untouched.", needs = "Sections", aliases = new[] { "ADDITION" } },
                    new { code = EndorsementTypes.Reversal, label = "Reversal", description = "Cancels the record: it becomes EXPIRED and every section on cover is copied back at its original figures.", needs = "nothing", aliases = new[] { "DELETE", "DELETED", "CANCELLATION" } },
                    new { code = EndorsementTypes.Return, label = "Return", description = "Takes the ticked sections off cover and copies them at their original figures. The record stays as it is.", needs = "SelectedSectionTokens", aliases = Array.Empty<string>() },
                    new { code = EndorsementTypes.Redo, label = "New", description = "Expires the record and the ticked sections, then copies those sections unchanged so they can be re-entered. Nothing is repriced.", needs = "SelectedSectionTokens", aliases = new[] { "NEW", "REDONEW" } },
                    new { code = EndorsementTypes.Extension, label = "Extension", description = "Extends cover to a new end date. Every section on cover is copied at full premium.", needs = "NewEndDate", aliases = Array.Empty<string>() },
                    new { code = EndorsementTypes.Reduction, label = "Reduction", description = "Copies every section on cover at its full figures, for a reduction to be applied.", needs = "nothing", aliases = Array.Empty<string>() }
                }
            });
        }

        /// <summary>
        /// Sections of one record, for the fire RETURN / REDO tick list. Send each SelectionToken back in
        /// SelectedSectionTokens. Sections already off cover come back with isExpired = true and can't be ticked.
        /// </summary>
        [HttpGet("fire/sections")]
        public async Task<IActionResult> GetEndorsableFireSections(
            [FromQuery] string policyNo, [FromQuery] string detailId)
        {
            if (string.IsNullOrWhiteSpace(policyNo) || string.IsNullOrWhiteSpace(detailId))
                return BadRequest(new { success = false, message = "policyNo and detailId are required" });

            try
            {
                var sections = await _proposalService.GetEndorsableFireSectionsAsync(policyNo, detailId);
                if (sections == null)
                    return NotFound(new { success = false, message = $"Record {detailId} not found on policy {policyNo}" });

                return Ok(new
                {
                    success = true,
                    policyNo,
                    detailId,
                    count = sections.Count,
                    endorsableCount = sections.Count(s => !s.IsExpired),
                    data = sections
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading endorsable fire sections for {PolicyNo} / {DetailID}", policyNo, detailId);
                return StatusCode(500, new { success = false, message = "Error loading sections", error = ex.Message });
            }
        }

        /// <summary>Fire endorsement records on a policy, newest first.</summary>
        [HttpGet("fire")]
        public async Task<IActionResult> GetFireEndorsements([FromQuery] string policyNo)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                return BadRequest(new { success = false, message = "policyNo is required" });

            try
            {
                var endorsements = await _proposalService.GetFireEndorsementsAsync(policyNo);
                return Ok(new { success = true, policyNo, count = endorsements.Count, data = endorsements });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fire endorsements for {PolicyNo}", policyNo);
                return StatusCode(500, new { success = false, message = "Error loading endorsements", error = ex.Message });
            }
        }

        /// <summary>
        /// Creates a fire endorsement of any type. The new record is saved UNCOMPLETED; complete it with
        /// PUT fire/calculation the way any pending calculation is completed.
        /// </summary>
        [HttpPost("fire")]
        public async Task<IActionResult> CreateFireEndorsement(
            [FromQuery] string policyNo, [FromBody] FireEndorsementRequestDto request)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "Request body is required" });

            // Query string wins: the body copy is there for clients that prefer to send it.
            if (!string.IsNullOrWhiteSpace(policyNo)) request.PolicyNo = policyNo;
            if (string.IsNullOrWhiteSpace(request.PolicyNo))
                return BadRequest(new { success = false, message = "policyNo is required (query string or body)" });

            // Adjustment lists are only priced for ADDITIONAL; the other types copy stored figures.
            if (EndorsementTypes.TryNormalize(request.EndorsementType, out var type) && type == EndorsementTypes.Additional)
            {
                var adjustmentErrors = new List<string>();
                for (var i = 0; i < (request.Sections?.Count ?? 0); i++)
                {
                    var section = request.Sections[i];
                    ValidateFireAdjustments(section?.Discounts, $"Sections[{i}].Discounts", adjustmentErrors);
                    ValidateFireAdjustments(section?.Loadings, $"Sections[{i}].Loadings", adjustmentErrors);

                    for (var j = 0; j < (section?.RiskItems?.Count ?? 0); j++)
                    {
                        var item = section.RiskItems[j];
                        ValidateFireAdjustments(item?.Discounts, $"Sections[{i}].RiskItems[{j}].Discounts", adjustmentErrors);
                        ValidateFireAdjustments(item?.Loadings, $"Sections[{i}].RiskItems[{j}].Loadings", adjustmentErrors);
                    }
                }

                if (adjustmentErrors.Count > 0)
                    return BadRequest(new { success = false, message = "Invalid adjustments", errors = adjustmentErrors });
            }

            try
            {
                var result = await _proposalService.CreateFireEndorsementAsync(request);
                if (!result.Success)
                    return BadRequest(result);

                _logger.LogInformation("Created {Type} fire endorsement {EndorsementNo} on {PolicyNo} (record {DetailID})",
                    result.EndorsementType, result.EndorsementNo, result.PolicyNo, result.NewDetailID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fire endorsement on {PolicyNo}", request.PolicyNo);
                return StatusCode(500, new { success = false, message = "Error creating endorsement", error = ex.Message });
            }
        }

        #endregion

        #region Motor

        /// <summary>The motor endorsement types this API accepts, with the aliases the screens use.</summary>
        [HttpGet("motor/types")]
        public IActionResult GetMotorEndorsementTypes()
        {
            return Ok(new
            {
                success = true,
                unit = "vehicle",
                data = new[]
                {
                    new { code = EndorsementTypes.Additional, label = "Additional", description = "Adds new vehicles, pro-rated to the policy's expiry. The original record and vehicles are untouched.", needs = "Vehicles", aliases = new[] { "ADDITION" } },
                    new { code = EndorsementTypes.Reversal, label = "Reversal", description = "Cancels the record: it becomes EXPIRED and every vehicle on cover is copied back at its original figures.", needs = "nothing", aliases = new[] { "DELETE", "DELETED", "CANCELLATION" } },
                    new { code = EndorsementTypes.Return, label = "Return", description = "Takes the ticked vehicles off cover and copies them at their original figures. The record stays as it is.", needs = "SelectedVehicleTokens", aliases = Array.Empty<string>() },
                    new { code = EndorsementTypes.Redo, label = "New", description = "Expires the record and the ticked vehicles, then copies those vehicles unchanged so they can be re-entered. Nothing is repriced.", needs = "SelectedVehicleTokens", aliases = new[] { "NEW", "REDONEW" } },
                    new { code = EndorsementTypes.Extension, label = "Extension", description = "Extends cover to a new end date. Every vehicle on cover is copied at full premium.", needs = "NewEndDate", aliases = Array.Empty<string>() },
                    new { code = EndorsementTypes.Reduction, label = "Reduction", description = "Copies every vehicle on cover at its full figures, for a reduction to be applied.", needs = "nothing", aliases = Array.Empty<string>() }
                }
            });
        }

        /// <summary>
        /// Vehicles of one record, for the motor RETURN / REDO tick list. Send each selectionToken back in
        /// SelectedVehicleTokens. Vehicles already off cover come back with isExpired = true and can't be ticked.
        /// </summary>
        [HttpGet("motor/vehicles")]
        public async Task<IActionResult> GetEndorsableMotorVehicles(
            [FromQuery] string policyNo, [FromQuery] string detailId)
        {
            if (string.IsNullOrWhiteSpace(policyNo) || string.IsNullOrWhiteSpace(detailId))
                return BadRequest(new { success = false, message = "policyNo and detailId are required" });

            try
            {
                var vehicles = await _proposalService.GetEndorsableMotorVehiclesAsync(policyNo, detailId);
                if (vehicles == null)
                    return NotFound(new { success = false, message = $"Record {detailId} not found on policy {policyNo}" });

                return Ok(new
                {
                    success = true,
                    policyNo,
                    detailId,
                    count = vehicles.Count,
                    endorsableCount = vehicles.Count(v => !v.IsExpired),
                    data = vehicles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading endorsable motor vehicles for {PolicyNo} / {DetailID}", policyNo, detailId);
                return StatusCode(500, new { success = false, message = "Error loading vehicles", error = ex.Message });
            }
        }

        /// <summary>Motor endorsement records on a policy, newest first.</summary>
        [HttpGet("motor")]
        public async Task<IActionResult> GetMotorEndorsements([FromQuery] string policyNo)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                return BadRequest(new { success = false, message = "policyNo is required" });

            try
            {
                var endorsements = await _proposalService.GetMotorEndorsementsAsync(policyNo);
                return Ok(new { success = true, policyNo, count = endorsements.Count, data = endorsements });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading motor endorsements for {PolicyNo}", policyNo);
                return StatusCode(500, new { success = false, message = "Error loading endorsements", error = ex.Message });
            }
        }

        /// <summary>
        /// Creates a motor endorsement of any type. The new record is saved UNCOMPLETED.
        /// </summary>
        [HttpPost("motor")]
        public async Task<IActionResult> CreateMotorEndorsement(
            [FromQuery] string policyNo, [FromBody] MotorEndorsementRequestDto request)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "Request body is required" });

            if (!string.IsNullOrWhiteSpace(policyNo)) request.PolicyNo = policyNo;
            if (string.IsNullOrWhiteSpace(request.PolicyNo))
                return BadRequest(new { success = false, message = "policyNo is required (query string or body)" });

            if (EndorsementTypes.TryNormalize(request.EndorsementType, out var type) && type == EndorsementTypes.Additional)
            {
                var adjustmentErrors = new List<string>();
                for (var i = 0; i < (request.Vehicles?.Count ?? 0); i++)
                {
                    var vehicle = request.Vehicles[i];
                    ValidateMotorAdjustments(vehicle?.Discounts, $"Vehicles[{i}].Discounts", adjustmentErrors);
                    ValidateMotorAdjustments(vehicle?.Loadings, $"Vehicles[{i}].Loadings", adjustmentErrors);
                }

                if (adjustmentErrors.Count > 0)
                    return BadRequest(new { success = false, message = "Invalid adjustments", errors = adjustmentErrors });
            }

            try
            {
                var result = await _proposalService.CreateMotorEndorsementAsync(request);
                if (!result.Success)
                    return BadRequest(result);

                _logger.LogInformation("Created {Type} motor endorsement {EndorsementNo} on {PolicyNo} (record {DetailID}, {Vehicles} vehicle(s))",
                    result.EndorsementType, result.EndorsementNo, result.PolicyNo, result.NewDetailID, result.VehicleCount);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating motor endorsement on {PolicyNo}", request.PolicyNo);
                return StatusCode(500, new { success = false, message = "Error creating endorsement", error = ex.Message });
            }
        }

        #endregion

        #region Adjustment validation

        // Both validators collect every problem with the path that caused it, so one bad entry in a
        // multi-section or multi-vehicle post is findable. Separate methods because the two modules use
        // different adjustment DTOs.

        private static void ValidateFireAdjustments(
            List<FireAdjustmentDto> adjustments, string path, List<string> errors)
        {
            if (adjustments == null) return;

            for (var i = 0; i < adjustments.Count; i++)
            {
                var adjustment = adjustments[i];
                if (adjustment == null)
                {
                    errors.Add($"{path}[{i}]: entry is empty");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(adjustment.AdjustmentName))
                    errors.Add($"{path}[{i}]: AdjustmentName is required");
                if (adjustment.Rate < 0)
                    errors.Add($"{path}[{i}] ({adjustment.AdjustmentName}): Rate cannot be negative. Got: {adjustment.Rate}");
            }
        }

        private static void ValidateMotorAdjustments(
            List<MotorAdjustmentDto> adjustments, string path, List<string> errors)
        {
            if (adjustments == null) return;

            for (var i = 0; i < adjustments.Count; i++)
            {
                var adjustment = adjustments[i];
                if (adjustment == null)
                {
                    errors.Add($"{path}[{i}]: entry is empty");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(adjustment.AdjustmentName))
                    errors.Add($"{path}[{i}]: AdjustmentName is required");
                if (adjustment.Rate < 0)
                    errors.Add($"{path}[{i}] ({adjustment.AdjustmentName}): Rate cannot be negative. Got: {adjustment.Rate}");
            }
        }

        #endregion
    }
}
