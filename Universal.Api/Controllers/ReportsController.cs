using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GibsLifesMicroWebApi.Data.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly Repository _repository;
        private readonly string _templatePath;

        public ReportsController(Repository repository, IWebHostEnvironment environment)
        {
            _repository = repository;
            _templatePath = Path.Combine(environment.ContentRootPath, "Templates", "Reports", "Business Proposal Letter - Group Life .docx");
        }
        [HttpPost("generate-business-proposal")]
        public IActionResult GenerateBusinessProposal([FromBody] BusinessProposalRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CompanyName))
            {
                return BadRequest(new { message = "Company name is required" });
            }

            try
            {
                // Check if template exists
                if (!System.IO.File.Exists(_templatePath))
                {
                    return NotFound(new { message = "Template file not found" });
                }

                // Create a memory stream for the output document
                using var memoryStream = new MemoryStream();

                // Copy template to memory stream
                using (var fileStream = new FileStream(_templatePath, FileMode.Open, FileAccess.Read))
                {
                    fileStream.CopyTo(memoryStream);
                }

                memoryStream.Position = 0;

                // Process the document
                using (var wordDoc = WordprocessingDocument.Open(memoryStream, true))
                {
                    var body = wordDoc.MainDocumentPart.Document.Body;

                    // Get current date in format
                    string currentDate = DateTime.Now.ToString("MMMM dd, yyyy");

                    // Replace placeholders in all text elements
                    foreach (var text in body.Descendants<Text>())
                    {
                        if (text.Text.Contains("(Date)"))
                        {
                            text.Text = text.Text.Replace("(Date)", currentDate);
                        }

                        if (text.Text.Contains("[Company Name]"))
                        {
                            text.Text = text.Text.Replace("[Company Name]", request.CompanyName);
                        }
                    }

                    // Save changes
                    wordDoc.MainDocumentPart.Document.Save();
                }

                // Reset position for reading
                memoryStream.Position = 0;

                // Prepare file name
                string fileName = $"Business_Proposal_{request.CompanyName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.docx";

                // Return the file
                return File(
                    memoryStream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    fileName
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error generating document",
                    error = ex.Message
                });
            }
        }
    }

    // Request model
    public class BusinessProposalRequest
    {
        public string CompanyName { get; set; }
    }

}
