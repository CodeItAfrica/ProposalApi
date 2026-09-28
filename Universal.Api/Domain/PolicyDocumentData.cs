using System;
using System.Collections.Generic;
using System.Linq;

namespace GibsLifesMicroWebApi.Models
{
    /// <summary>
    /// Flat projection of all data needed to fill any policy document template.
    /// Populated from PolicyMaster + Beneficiaries table.
    /// </summary>
    public class PolicyDocumentData
    {
        // ── Core identifiers ──────────────────────────────────────────────
        public string PolicyNo { get; set; } = string.Empty;
        public string ProposalNo { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;

        // ── Assured personal details ──────────────────────────────────────
        public string FullName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Occupation { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;   // "4th JULY 1971"
        public string AgeNextBirthday { get; set; } = string.Empty;   // "55"
        public string Sex { get; set; } = string.Empty;
        public string MaritalStatus { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobilePhone { get; set; } = string.Empty;
        public string NIN { get; set; } = string.Empty;
        public string BVN { get; set; } = string.Empty;

        // ── Beneficiary — sourced from Beneficiaries table ────────────────
        public string BeneficiaryName { get; set; } = string.Empty;
        public string BeneficiaryAddress { get; set; } = string.Empty;
        public string BeneficiaryDOB { get; set; } = string.Empty;   // "12th MARCH 1990"
        public string BeneficiaryAge { get; set; } = string.Empty;
        public string BeneficiarySex { get; set; } = string.Empty;
        public string BeneficiaryRelation { get; set; } = string.Empty;   // e.g. "SPOUSE", "CHILD"
        public string BeneficiaryPercentage { get; set; } = string.Empty;   // e.g. "100%"
        public string BeneficiaryType { get; set; } = string.Empty;   // e.g. "PRIMARY"

        // ── Policy dates ──────────────────────────────────────────────────
        public string PropDate { get; set; } = string.Empty;
        public string CommencementDate { get; set; } = string.Empty;
        public string MaturityDate { get; set; } = string.Empty;
        public string IssuedDate { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;   // "ONE YEAR"

        // ── Financial ─────────────────────────────────────────────────────
        public string InitialDeposit { get; set; } = string.Empty;
        public string MonthlyPremium { get; set; } = string.Empty;
        public string TotalPremium { get; set; } = string.Empty;
        public string SumAssured { get; set; } = string.Empty;
        public string BasicPremium { get; set; } = string.Empty;
        public string AnnualPremium { get; set; } = string.Empty;
        public string SupplementaryBenefits { get; set; } = "N/A";
        public string ModeOfPayment { get; set; } = string.Empty;   // "MONTHLY"

        // ── Agent ─────────────────────────────────────────────────────────
        public string AgentCode { get; set; } = string.Empty;
        public string AgentDescription { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;

        // ── Term-specific extras ──────────────────────────────────────────
        public string TermYears { get; set; } = string.Empty;
        public string DeathBenefit { get; set; } = string.Empty;
    }

    /// <summary>Maps a product code to its .docx template file and a friendly name.</summary>
    public class ProductTemplateMap
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string TemplatePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Full payment audit trail for a single policy: header details from
    /// PolicyMaster plus every DNCN (undDNCN) transaction recorded against it.
    /// </summary>
    public class PolicyAuditTrailDto
    {
        // ---- Policy header ----
        public string PolicyNo { get; set; }
        public string ProposalNo { get; set; }
        public string InsuredName { get; set; }
        public string Address { get; set; }
        public string CoverType { get; set; }      // PolicyMaster.Covertype
        public string CoverCode { get; set; }       // PolicyMaster.CoverCode
        public DateTime? CommencementDate { get; set; } // PolicyMaster.StartDate
        public DateTime? MaturityDate { get; set; }
        public string Frequency { get; set; }        // PolicyMaster.FOP
        public decimal? SumAssured { get; set; }
        public decimal? BasicPremium { get; set; }
        public decimal? TotalPremium { get; set; }
        public string Marketer { get; set; }          // PolicyMaster.AgentDescription
        public string AgentCode { get; set; }
        public string PolicyStatus { get; set; }       // PolicyMaster.Remarks
        public string CurrentTransGuid { get; set; }    // PolicyMaster.TransGUID
        public DateTime ReportDate { get; set; } = DateTime.Now;

        public List<AuditTrailEntryDto> Entries { get; set; } = new();

        public decimal TotalPaid => Entries?.Sum(e => e.AmountPaid) ?? 0m;
        public decimal TotalNet => Entries?.Sum(e => e.NetAmount ?? 0m) ?? 0m;
    }

    /// <summary>
    /// One transaction row, sourced from undDNCN.
    /// </summary>
    public class AuditTrailEntryDto
    {
        public int SN { get; set; }
        public string DNCNNo { get; set; }
        public string ReceiptNo { get; set; }
        public DateTime? TransDate { get; set; }   // undDNCN.TDate
        public string TransType { get; set; }
        public string BusType { get; set; }
        public decimal AmountPaid { get; set; }     // undDNCN.GrossPremium
        public decimal? NetAmount { get; set; }      // undDNCN.NetAmount
        public string Status { get; set; }            // undDNCN.TransSTATUS
        public string SubmittedBy { get; set; }         // undDNCN.SubmitBy
        public string TransGuid { get; set; }            // undDNCN.TransGUID
        public string Remark { get; set; }
        public string Naration { get; set; }              // undDNCN.Naration (full/raw — truncated at display time)
    }
}