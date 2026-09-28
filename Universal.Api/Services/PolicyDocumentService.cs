using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services
{
    /// <summary>
    /// Resolves a policy number → product template → fills placeholders → returns .docx bytes.
    ///
    /// TEMPLATE CONVENTION
    /// -------------------
    /// Every placeholder in the Word template is written as  {{PLACEHOLDER_NAME}}
    /// e.g. {{POLICY_NO}}, {{FULL_NAME}}, {{MATURITY_DATE}}
    ///
    /// The service does a simple text-run search-and-replace across all paragraphs,
    /// table cells, headers and footers so the template author never needs to write code.
    /// </summary>
    public class PolicyDocumentService
    {
        public const string DocxContentType =
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        private readonly DataContext _db;
        private readonly ILogger<PolicyDocumentService> _logger;
        private readonly Dictionary<string, ProductTemplateMap> _templateMap;

        // Templates live at: {ContentRootPath}/Templates/PolicyDocuments/<FileName>
        // Only the filename goes in appsettings.json — never a full path — so the
        // same config works on dev, staging, and production without changes.
        public PolicyDocumentService(
            DataContext db,
            IWebHostEnvironment environment,
            IConfiguration config,
            ILogger<PolicyDocumentService> logger)
        {
            _db = db;
            _logger = logger;

            var templateFolder = Path.Combine(
                environment.ContentRootPath, "Templates", "PolicyDocuments");

            _templateMap = config
                .GetSection("PolicyTemplates")
                .Get<List<ProductTemplateMap>>()
                ?.Select(t =>
                {
                    // TemplatePath in appsettings is just the filename,
                    // e.g. "CapitalAppreciation_Template.docx"
                    t.TemplatePath = Path.Combine(templateFolder, t.TemplatePath);
                    return t;
                })
                .ToDictionary(t => t.ProductCode.ToUpperInvariant(), t => t)
                ?? new Dictionary<string, ProductTemplateMap>();
        }

        /// <summary>
        /// The configured templates with their paths already resolved against the
        /// content root. Exposed so the controller reports the same paths the
        /// generator actually reads — no second Path.Combine to drift out of sync.
        /// </summary>
        public IReadOnlyCollection<ProductTemplateMap> Templates => _templateMap.Values;

        // ─────────────────────────────────────────────────────────────────────
        // Public entry point
        // ─────────────────────────────────────────────────────────────────────

        public async Task<(byte[] FileBytes, string FileName)> GeneratePolicyDocumentAsync(
            string policyNo,
            CancellationToken ct = default)
        {
            // 1. Load policy from DB
            var policy = await _db.PolicyMaster
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PolicyNo == policyNo && p.Deleted == 0, ct)
                ?? throw new KeyNotFoundException($"Policy '{policyNo}' not found.");

            // 2. Resolve product code → template file
            var productCode = (policy.CoverCode ?? string.Empty).ToUpperInvariant();
            if (!_templateMap.TryGetValue(productCode, out var templateInfo))
                throw new NotSupportedException(
                    $"No document template is configured for product code '{productCode}'.");

            if (!File.Exists(templateInfo.TemplatePath))
                throw new FileNotFoundException(
                    $"Template file not found: {templateInfo.TemplatePath}");

            // 3. Load first active beneficiary for this policy (nullable — templates handle absence)
            var beneficiary = await _db.Beneficiaries
                .AsNoTracking()
                .Where(b => b.PolicyNo == policyNo && (b.Deleted == null || b.Deleted == 0))
                .OrderBy(b => b.TransID)
                .FirstOrDefaultAsync(ct);

            // 4. Build placeholder dictionary from policy + beneficiary data
            var data = BuildDocumentData(policy, templateInfo, beneficiary);
            var placeholders = BuildPlaceholders(data);

            // 5. Copy template to memory stream, fill it, return bytes
            var fileBytes = await FillTemplateAsync(templateInfo.TemplatePath, placeholders, ct);
            var fileName = $"{policy.PolicyNo}_{productCode}_Policy.docx"
                             .Replace("/", "-").Replace("\\", "-");

            return (fileBytes, fileName);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Map PolicyMaster + Beneficiary → PolicyDocumentData
        // ─────────────────────────────────────────────────────────────────────

        private PolicyDocumentData BuildDocumentData(PolicyMaster p, ProductTemplateMap tpl, Beneficiary? ben)
        {
            return new PolicyDocumentData
            {
                PolicyNo = p.PolicyNo ?? string.Empty,
                ProposalNo = p.ProposalNo ?? string.Empty,
                ProductCode = tpl.ProductCode,
                ProductName = tpl.ProductName,

                FullName = p.FullName ?? $"{p.Title} {p.SurName} {p.OtherNames}".Trim(),
                Address = p.Address ?? string.Empty,
                Occupation = p.Occupation ?? string.Empty,
                DateOfBirth = FormatDateOrdinal(p.DateofBirth),
                AgeNextBirthday = p.Age.HasValue ? ((int)p.Age.Value + 1).ToString() : string.Empty,
                Sex = p.Sex ?? string.Empty,
                MaritalStatus = p.MaritalStatus ?? string.Empty,
                Email = p.Email ?? string.Empty,
                MobilePhone = p.MobilePhone ?? string.Empty,
                NIN = p.NIN ?? string.Empty,
                BVN = p.BVN ?? string.Empty,

                // ── Beneficiary — sourced from Beneficiaries table ────────
                BeneficiaryName = ben != null
                                            ? $"{ben.Surname} {ben.OtherNames}".Trim()
                                            : string.Empty,
                BeneficiaryAddress = ben?.Address ?? string.Empty,
                BeneficiaryDOB = FormatDateOrdinal(ben?.DOB),
                BeneficiaryAge = ben?.Age ?? string.Empty,
                BeneficiarySex = ben?.Sex ?? string.Empty,
                BeneficiaryRelation = ben?.Relation ?? string.Empty,
                BeneficiaryPercentage = ben?.Percentage.HasValue == true
                                            ? $"{ben.Percentage:0.##}%"
                                            : string.Empty,
                BeneficiaryType = ben?.Type ?? string.Empty,

                // ── Policy dates ──────────────────────────────────────────
                PropDate = FormatDateOrdinal(p.PropDate),
                CommencementDate = FormatDateOrdinal(p.StartDate),
                MaturityDate = FormatDateOrdinal(p.MaturityDate),
                IssuedDate = FormatDateOrdinal(p.IssueDate ?? p.TDate),
                Duration = FormatDurationWords(p.Duration),

                // ── Financial ─────────────────────────────────────────────
                InitialDeposit = FormatNaira(p.Premium1),
                MonthlyPremium = FormatNaira(p.BasicPremium),
                TotalPremium = FormatNaira(p.TotalPremium),
                SumAssured = FormatNaira(p.SumAssured),
                BasicPremium = FormatNaira(p.BasicPremium),
                AnnualPremium = FormatNaira(p.BasicPremium * 12),
                ModeOfPayment = ExpandMop(p.MOP),
                SupplementaryBenefits = p.SUPPSA.HasValue && p.SUPPSA > 0
                                            ? FormatNaira(p.SUPPSA)
                                            : "N/A",

                // ── Agent ─────────────────────────────────────────────────
                AgentCode = p.Agentcode ?? string.Empty,
                AgentDescription = p.AgentDescription ?? string.Empty,
                BranchCode = p.BrCode ?? string.Empty,

                // ── Term-specific extras ──────────────────────────────────
                TermYears = p.Duration.HasValue ? ((int)p.Duration.Value).ToString() : string.Empty,
                DeathBenefit = FormatNaira(p.ADBSA ?? p.SumAssured),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Build final {{KEY}} → value dictionary
        // ─────────────────────────────────────────────────────────────────────

        private static Dictionary<string, string> BuildPlaceholders(PolicyDocumentData d)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Policy
                { "{{POLICY_NO}}",               d.PolicyNo             },
                { "{{PROPOSAL_NO}}",             d.ProposalNo           },
                { "{{PRODUCT_CODE}}",            d.ProductCode          },
                { "{{PRODUCT_NAME}}",            d.ProductName          },

                // Assured personal
                { "{{FULL_NAME}}",               d.FullName             },
                { "{{ADDRESS}}",                 d.Address              },
                { "{{OCCUPATION}}",              d.Occupation           },
                { "{{DATE_OF_BIRTH}}",           d.DateOfBirth          },
                { "{{AGE_NEXT_BIRTHDAY}}",       d.AgeNextBirthday      },
                { "{{SEX}}",                     d.Sex                  },
                { "{{MARITAL_STATUS}}",          d.MaritalStatus        },
                { "{{EMAIL}}",                   d.Email                },
                { "{{MOBILE_PHONE}}",            d.MobilePhone          },
                { "{{NIN}}",                     d.NIN                  },
                { "{{BVN}}",                     d.BVN                  },

                // Beneficiary
                { "{{BENEFICIARY_NAME}}",        d.BeneficiaryName      },
                { "{{BENEFICIARY_ADDRESS}}",     d.BeneficiaryAddress   },
                { "{{BENEFICIARY_DOB}}",         d.BeneficiaryDOB       },
                { "{{BENEFICIARY_AGE}}",         d.BeneficiaryAge       },
                { "{{BENEFICIARY_SEX}}",         d.BeneficiarySex       },
                { "{{BENEFICIARY_RELATION}}",    d.BeneficiaryRelation  },
                { "{{BENEFICIARY_PERCENTAGE}}",  d.BeneficiaryPercentage},
                { "{{BENEFICIARY_TYPE}}",        d.BeneficiaryType      },

                // Dates
                { "{{PROP_DATE}}",               d.PropDate             },
                { "{{DATE}}",                    DateTime.Now.ToString("MMMM dd, yyyy") },

                { "{{COMMENCEMENT_DATE}}",       d.CommencementDate     },
                { "{{MATURITY_DATE}}",           d.MaturityDate         },
                { "{{ISSUED_DATE}}",             d.IssuedDate           },
                { "{{DURATION}}",                d.Duration             },

                // Financial
                { "{{INITIAL_DEPOSIT}}",         d.InitialDeposit       },
                { "{{MONTHLY_PREMIUM}}",         d.MonthlyPremium       },
                { "{{TOTAL_PREMIUM}}",           d.TotalPremium         },
                { "{{SUM_ASSURED}}",             d.SumAssured           },
                { "{{BASIC_PREMIUM}}",           d.BasicPremium         },
                { "{{ANNUAL_PREMIUM}}",          d.AnnualPremium        },
                { "{{SUPPLEMENTARY_BENEFITS}}",  d.SupplementaryBenefits},
                { "{{MODE_OF_PAYMENT}}",         d.ModeOfPayment        },

                // Agent
                { "{{AGENT_CODE}}",              d.AgentCode            },
                { "{{AGENT_NAME}}",              d.AgentDescription     },
                { "{{BRANCH_CODE}}",             d.BranchCode           },

                // Term-specific
                { "{{TERM_YEARS}}",              d.TermYears            },
                { "{{DEATH_BENEFIT}}",           d.DeathBenefit         },
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Template reading — cached, and tolerant of Word/Dropbox/AV file locks
        // ─────────────────────────────────────────────────────────────────────

        // Keyed on path; the last-write timestamp is stored alongside so editing a
        // template picks up on the next request instead of needing an app restart.
        private static readonly ConcurrentDictionary<string, (DateTime Stamp, byte[] Bytes)> TemplateCache =
            new(StringComparer.OrdinalIgnoreCase);

        private static async Task<byte[]> ReadTemplateBytesAsync(string path, CancellationToken ct)
        {
            // Metadata read — succeeds even while another process holds the file.
            DateTime stamp;
            try { stamp = File.GetLastWriteTimeUtc(path); }
            catch (IOException) { stamp = DateTime.MinValue; }

            if (TemplateCache.TryGetValue(path, out var cached) && cached.Stamp == stamp)
                return cached.Bytes;

            const int maxAttempts = 4;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    // FileShare.ReadWrite | Delete => we don't care who else holds a
                    // handle. File.OpenRead uses FileShare.Read, which is what threw
                    // "being used by another process" when Word or Dropbox had it open.
                    using var fs = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan);

                    using var buffer = new MemoryStream();
                    await fs.CopyToAsync(buffer, ct);

                    var bytes = buffer.ToArray();
                    TemplateCache[path] = (stamp, bytes);
                    return bytes;
                }
                catch (IOException ex) when (ex is not FileNotFoundException && attempt < maxAttempts)
                {
                    // Transient lock from Dropbox sync, antivirus, or Word — back off.
                    await Task.Delay(150 * attempt, ct);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Open template copy in memory, replace all placeholders
        // ─────────────────────────────────────────────────────────────────────

        private static async Task<byte[]> FillTemplateAsync(
            string templatePath,
            Dictionary<string, string> placeholders,
            CancellationToken ct = default)
        {
            var templateBytes = await ReadTemplateBytesAsync(templatePath, ct);

            // Expandable stream. Do NOT use `new MemoryStream(templateBytes)` — that
            // is fixed-size, and OpenXml throws the moment an edit makes it grow.
            using var ms = new MemoryStream();
            ms.Write(templateBytes, 0, templateBytes.Length);
            ms.Position = 0;

            using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
            {
                var mainPart = doc.MainDocumentPart
                    ?? throw new InvalidOperationException("Template has no main document part.");

                ReplacePlaceholdersInPart(mainPart.Document.Body!, placeholders);

                foreach (var hdr in mainPart.HeaderParts)
                    ReplacePlaceholdersInPart(hdr.Header, placeholders);

                foreach (var ftr in mainPart.FooterParts)
                    ReplacePlaceholdersInPart(ftr.Footer, placeholders);

                mainPart.Document.Save();
            }

            return ms.ToArray();
        }

        private static void ReplacePlaceholdersInPart(
            OpenXmlElement root,
            Dictionary<string, string> placeholders)
        {
            foreach (var para in root.Descendants<Paragraph>())
                ReplacePlaceholdersInParagraph(para, placeholders);
        }

        private static void ReplacePlaceholdersInParagraph(
            Paragraph para,
            Dictionary<string, string> placeholders)
        {
            var runs = para.Descendants<Run>().ToList();
            if (runs.Count == 0) return;

            var combined = string.Concat(runs.Select(r =>
                string.Concat(r.Descendants<Text>().Select(t => t.Text))));

            bool anyMatch = placeholders.Keys.Any(k => combined.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (!anyMatch) return;

            foreach (var (key, val) in placeholders)
                combined = combined.Replace(key, val, StringComparison.OrdinalIgnoreCase);

            // NOTE: this collapses the whole paragraph into run 0, so a paragraph
            // that mixes formatting (bold label + plain value, a hyperlink, a
            // differently sized word) comes out uniformly styled as run 0. Fine
            // while each placeholder sits alone in its own run; when that stops
            // being true, rewrite only the runs spanning the placeholder and keep
            // each run's RunProperties.
            var firstText = runs[0].Descendants<Text>().FirstOrDefault();
            if (firstText != null)
            {
                firstText.Text = combined;
                firstText.Space = SpaceProcessingModeValues.Preserve;
            }
            else
            {
                runs[0].AppendChild(new Text(combined)
                {
                    Space = SpaceProcessingModeValues.Preserve
                });
            }

            foreach (var run in runs.Skip(1))
                foreach (var t in run.Descendants<Text>().ToList())
                    t.Text = string.Empty;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Formatting helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string FormatNaira(decimal? amount)
            => amount.HasValue ? $"=N= {amount.Value:N2}" : string.Empty;

        private static string FormatDateOrdinal(DateTime? dt)
        {
            if (!dt.HasValue) return string.Empty;
            var d = dt.Value;
            var ord = d.Day switch { 1 or 21 or 31 => "st", 2 or 22 => "nd", 3 or 23 => "rd", _ => "th" };
            return $"{d.Day}{ord} {d.ToString("MMMM", CultureInfo.InvariantCulture).ToUpperInvariant()} {d.Year}";
        }

        private static string FormatDurationWords(double? months) => months switch
        {
            12 => "ONE YEAR",
            24 => "TWO YEARS",
            36 => "THREE YEARS",
            48 => "FOUR YEARS",
            60 => "FIVE YEARS",
            _ => months.HasValue ? $"{months.Value} MONTHS" : string.Empty
        };

        private static string ExpandMop(string? mop) => (mop ?? string.Empty).ToUpperInvariant() switch
        {
            "M" => "MONTHLY",
            "Q" => "QUARTERLY",
            "H" => "HALF-YEARLY",
            "A" => "ANNUALLY",
            "SP" => "SINGLE PREMIUM",
            _ => mop ?? string.Empty
        };
    }
}