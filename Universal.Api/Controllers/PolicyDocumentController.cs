using GibsLifesMicroWebApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PolicyDocumentController : ControllerBase
    {
        private readonly PolicyDocumentService _docService;
        private readonly ILogger<PolicyDocumentController> _logger;

        public PolicyDocumentController(
            PolicyDocumentService docService,
            ILogger<PolicyDocumentController> logger)
        {
            _docService = docService;
            _logger = logger;
        }

        /// <summary>
        /// Generates and downloads a filled policy document (.docx) for the given policy number.
        /// The product code stored on the policy record determines which template is used.
        /// </summary>
        /// <param name="policyNo">e.g. CAP/26/0007169/LAG</param>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPolicyDocument(
            [FromQuery] string policyNo,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(policyNo))
                return BadRequest("Policy number is required.");

            try
            {
                var (fileBytes, fileName) =
                    await _docService.GeneratePolicyDocumentAsync(policyNo, ct);

                return File(fileBytes, PolicyDocumentService.DocxContentType, fileName);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Policy not found: {PolicyNo}", policyNo);
                return NotFound(new { message = ex.Message });
            }
            catch (NotSupportedException ex)
            {
                _logger.LogWarning(ex, "No template for policy: {PolicyNo}", policyNo);
                return BadRequest(new { message = ex.Message });
            }
            // FileNotFoundException derives from IOException — it MUST come first,
            // or the IOException handler below swallows it.
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Template file missing for policy: {PolicyNo}", policyNo);
                return StatusCode(500, new { message = "Template file is missing. Contact IT support." });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Template file locked for policy: {PolicyNo}", policyNo);
                return StatusCode(503, new
                {
                    message = "The document template is temporarily locked. Please try again in a moment."
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Client hung up mid-download. Nothing to report.
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error generating document for: {PolicyNo}", policyNo);
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Lists the configured product templates and whether each file is present.
        /// Paths come from the service, so this reports exactly what the generator reads.
        /// </summary>
        [HttpGet("templates")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetAvailableTemplates()
        {
            var templates = _docService.Templates.Select(t => new
            {
                t.ProductCode,
                t.ProductName,
                FullPath = t.TemplatePath,
                TemplateExists = System.IO.File.Exists(t.TemplatePath)
            });

            return Ok(templates);
        }
    }
}