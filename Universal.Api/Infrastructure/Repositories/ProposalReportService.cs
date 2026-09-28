using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Models.Customer;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services
{
    public class ProposalReportService
    {
        private readonly Repository _repository;
        private readonly string _templatePath;

        public ProposalReportService(Repository repository, IWebHostEnvironment environment)
        {
            _repository = repository;
            _templatePath = Path.Combine(environment.ContentRootPath, "Templates", "Reports");
        }

        public async Task<byte[]> GenerateProposalReportAsync(string proposalNo, bool isTraditional = false)
        {
            // Get data from database
            var proposal = await _repository.GetProposalForReportAsync(proposalNo);
            var insured = await _repository.GetInsuredClientAsync(proposal.AssuredCode);

            // Get template file
            string templateFileName = isTraditional ? "TradProposalTemplate.docx" : "ProposalTemplate.docx";
            string templateFilePath = Path.Combine(_templatePath, templateFileName);

            if (!File.Exists(templateFilePath))
                throw new FileNotFoundException($"Template file not found: {templateFilePath}");

            // Create placeholders dictionary
            var placeholders = BuildPlaceholdersDictionary(proposal, insured);

            // Generate document from template
            return GenerateDocumentFromTemplate(templateFilePath, placeholders);
        }

        private Dictionary<string, string> BuildPlaceholdersDictionary(PolicyMaster proposal, InsuredClients insured)
        {
            return new Dictionary<string, string>
            {
                // Proposal Information
                {"{{ProposalNo}}", proposal.ProposalNo ?? "N/A"},
                
                // Personal Information
                {"{{Title}}", proposal.Title ?? "N/A"},
                {"{{SurName}}", proposal.SurName ?? "N/A"},
                {"{{OtherNames}}", proposal.OtherNames ?? "N/A"},
                {"{{FullName}}", proposal.FullName ?? "N/A"},
                {"{{DateOfBirth}}", proposal.DateofBirth?.ToString("dd/MM/yyyy") ?? "N/A"},
                {"{{Age}}", proposal.Age?.ToString() ?? "N/A"},
                {"{{Gender}}", proposal.Sex ?? "N/A"},
                {"{{MaritalStatus}}", proposal.MaritalStatus ?? "N/A"},
                {"{{NationalID}}", proposal.NationalID ?? "N/A"},
                {"{{Occupation}}", proposal.Occupation ?? "N/A"},
                
                // Contact Information
                {"{{Address}}", proposal.Address ?? "N/A"},
                {"{{State}}", proposal.State ?? "N/A"},
                {"{{Country}}", proposal.Country ?? "N/A"},
                {"{{MobilePhone}}", proposal.MobilePhone ?? "N/A"},
                {"{{LandPhone}}", proposal.Landphone ?? "N/A"},
                {"{{Email}}", proposal.Email ?? "N/A"},
                
                // Policy Details
                {"{{ProductCode}}", proposal.CoverCode ?? "N/A"},
                {"{{AssuredCode}}", proposal.AssuredCode ?? "N/A"},
                {"{{StartDate}}", proposal.StartDate?.ToString("dd/MM/yyyy") ?? "N/A"},
                {"{{MaturityDate}}", proposal.MaturityDate?.ToString("dd/MM/yyyy") ?? "N/A"},
                {"{{Duration}}", proposal.Duration?.ToString() ?? "N/A"},
                {"{{Frequency}}", proposal.FOP ?? "N/A"},
                {"{{RiskType}}", proposal.CustomerType ?? "N/A"},
                
                // Financial Details
                {"{{SumAssured}}", FormatCurrency(proposal.SumAssured)},
                {"{{BasicPremium}}", FormatCurrency(proposal.BasicPremium)},
                {"{{AnnualPremium}}", FormatCurrency(proposal.Premium1)},
                {"{{ExtraPremium}}", FormatCurrency(proposal.Premium2)},
                {"{{Loading}}", FormatCurrency(proposal.Premium3)},
                {"{{TotalPremium}}", FormatCurrency(proposal.TotalPremium)},
                
                // Agent Information
                {"{{AgentCode}}", proposal.Agentcode ?? "N/A"},
                {"{{AgentName}}", proposal.AgentDescription ?? "N/A"},
                
                // Contact Person
                {"{{ContactName}}", proposal.ContactName ?? "N/A"},
                {"{{ContactAddress}}", proposal.ContactAddress ?? "N/A"},
                {"{{ContactPhone}}", proposal.ContactPhone ?? "N/A"},
                {"{{ContactEmail}}", proposal.ContactEmail ?? "N/A"},
                {"{{ContactJob}}", proposal.ContactJob ?? "N/A"},
                
                // Identification & Medical
                {"{{MeansID}}", proposal.MeansID ?? "N/A"},
                {"{{IDNo}}", proposal.IDNo ?? "N/A"},
                {"{{ChronicIllness}}", proposal.ChronicIllness ?? "None"},
                
                // Footer
                {"{{GeneratedDate}}", DateTime.Now.ToString("dd/MM/yyyy HH:mm")},
                {"{{SubmittedDate}}", proposal.SubmittedOn?.ToString("dd/MM/yyyy HH:mm") ?? "N/A"}
            };
        }

        private byte[] GenerateDocumentFromTemplate(string templatePath, Dictionary<string, string> placeholders)
        {
            byte[] byteArray = File.ReadAllBytes(templatePath);

            using (MemoryStream stream = new MemoryStream())
            {
                stream.Write(byteArray, 0, byteArray.Length);

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, true))
                {
                    // Replace placeholders in main document body
                    ReplacePlaceholdersInBody(wordDoc.MainDocumentPart.Document.Body, placeholders);

                    // Replace placeholders in headers
                    foreach (var headerPart in wordDoc.MainDocumentPart.HeaderParts)
                    {
                        ReplacePlaceholdersInBody(headerPart.Header, placeholders);
                    }

                    // Replace placeholders in footers
                    foreach (var footerPart in wordDoc.MainDocumentPart.FooterParts)
                    {
                        ReplacePlaceholdersInBody(footerPart.Footer, placeholders);
                    }

                    wordDoc.MainDocumentPart.Document.Save();
                }

                return stream.ToArray();
            }
        }

        private void ReplacePlaceholdersInBody(OpenXmlElement element, Dictionary<string, string> placeholders)
        {
            // First, merge text runs in each paragraph to handle split placeholders
            foreach (var paragraph in element.Descendants<Paragraph>())
            {
                // Get all text content in the paragraph
                string fullText = string.Join("", paragraph.Descendants<Text>().Select(t => t.Text));

                // Check if paragraph contains any placeholder
                bool containsPlaceholder = placeholders.Keys.Any(key => fullText.Contains(key));

                if (containsPlaceholder)
                {
                    // Replace placeholders in the full text
                    foreach (var placeholder in placeholders)
                    {
                        fullText = fullText.Replace(placeholder.Key, placeholder.Value);
                    }

                    // Clear all existing text runs
                    var runs = paragraph.Descendants<Run>().ToList();
                    foreach (var run in runs)
                    {
                        var texts = run.Descendants<Text>().ToList();
                        foreach (var text in texts)
                        {
                            text.Remove();
                        }
                    }

                    // Add the replaced text to the first run (or create new one)
                    var firstRun = paragraph.Descendants<Run>().FirstOrDefault();
                    if (firstRun == null)
                    {
                        firstRun = new Run();
                        paragraph.AppendChild(firstRun);
                    }

                    firstRun.AppendChild(new Text(fullText));
                }
            }
        }

        private string FormatCurrency(decimal? amount)
        {
            return amount.HasValue ? $"₦{amount.Value:N2}" : "N/A";
        }
    }
}