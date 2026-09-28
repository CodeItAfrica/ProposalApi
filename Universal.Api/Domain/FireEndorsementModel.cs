using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace GibsLifesMicroWebApi.Models
{
    /// <summary>
    /// Fire endorsement types. The canonical code is what goes into PolicyDetails.BizOption;
    /// RowTagFor() gives the Tag written on the section/item rows the endorsement creates.
    /// </summary>
    public static class EndorsementTypes
    {
        public const string Additional = "ADDITIONAL";
        public const string Reversal = "REVERSAL";
        public const string Return = "RETURN";
        public const string Redo = "REDO";
        public const string Extension = "EXTENSION";
        public const string Reduction = "REDUCTION";

        /// <summary>Status an endorsement puts on original records (BizOption) and on sections/items (Tag).</summary>
        public const string Expired = "EXPIRED";

        /// <summary>Tag on the rows a REDO creates — the screen calls a REDO "NEW".</summary>
        public const string RedoRowTag = "NEW";

        public static List<string> GetAll() => new() { Additional, Reversal, Return, Redo, Extension, Reduction };

        // Screen labels and older spellings accepted on input.
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADDITIONAL"] = Additional,
            ["ADDITION"] = Additional,
            ["REVERSAL"] = Reversal,
            ["DELETE"] = Reversal,
            ["DELETED"] = Reversal,
            ["CANCELLATION"] = Reversal,
            ["RETURN"] = Return,
            ["REDO"] = Redo,
            ["REDONEW"] = Redo,
            ["NEW"] = Redo,
            ["EXTENSION"] = Extension,
            ["REDUCTION"] = Reduction
        };

        /// <summary>Maps input (any case; spaces, underscores and hyphens ignored; aliases allowed) onto a canonical code.</summary>
        public static bool TryNormalize(string value, out string canonical)
        {
            canonical = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            return Aliases.TryGetValue(new string(value.Where(char.IsLetter).ToArray()), out canonical);
        }

        /// <summary>
        /// True when a stored BizOption is one of the canonical endorsement codes. No aliases here:
        /// a stored "New" is new business, not a REDO.
        /// </summary>
        public static bool IsEndorsement(string bizOption) =>
            !string.IsNullOrWhiteSpace(bizOption) &&
            GetAll().Contains(bizOption.Trim(), StringComparer.OrdinalIgnoreCase);

        /// <summary>Tag for the section/item rows an endorsement of this type creates.</summary>
        public static string RowTagFor(string canonical) =>
            string.Equals(canonical?.Trim(), Redo, StringComparison.OrdinalIgnoreCase)
                ? RedoRowTag
                : canonical?.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// One request for every fire endorsement type. What matters per type:
    ///   ADDITIONAL          Sections (+ optional EffectiveDate and rate overrides)
    ///   RETURN / REDO       SelectedSectionTokens (the ticked sections)
    ///   EXTENSION           NewEndDate
    ///   REVERSAL/REDUCTION  nothing beyond the record being endorsed
    /// </summary>
    public class FireEndorsementRequestDto
    {
        /// <summary>Set from the query string by the controller (policy numbers contain slashes).</summary>
        public string PolicyNo { get; set; }

        /// <summary>ADDITIONAL, REVERSAL (DELETE/DELETED), RETURN, REDO (screen: NEW), EXTENSION, REDUCTION.</summary>
        [Required]
        public string EndorsementType { get; set; }

        /// <summary>DetailID of the record (PolicyDetails row) being endorsed.</summary>
        [Required]
        public string SourceDetailID { get; set; }

        /// <summary>RETURN / REDO: SelectionToken values from GET endorsement/fire/sections.</summary>
        public List<string> SelectedSectionTokens { get; set; } = new();

        /// <summary>ADDITIONAL: the new sections, priced like any fire calculation.</summary>
        public List<FireSectionDto> Sections { get; set; } = new();

        /// <summary>ADDITIONAL: date the new sections go on cover (default today). Pro-rated from here to the policy's expiry.</summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>EXTENSION: the new end date — must be after the record's current end date.</summary>
        public DateTime? NewEndDate { get; set; }

        // ADDITIONAL only — leave null to use the endorsed record's values.
        public decimal? ProportionRate { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string Currency { get; set; }
        public decimal? OtherDiscountsRate { get; set; }
        public decimal? OtherLoadingsRate { get; set; }

        /// <summary>
        /// PolicyDetails.Field4 for ADDITIONAL / REDO / EXTENSION / REDUCTION.
        /// REVERSAL and RETURN always use their fixed descriptions.
        /// </summary>
        public string Description { get; set; }

        public string Remarks { get; set; }
        public string CreatedBy { get; set; } = "SYSTEM";
    }

    public class FireEndorsementResponseDto
    {
        public string PolicyNo { get; set; }

        /// <summary>Canonical type — the new record's BizOption.</summary>
        public string EndorsementType { get; set; }

        /// <summary>The new record's EndorsementNo — also the value stamped on original sections.</summary>
        public string EndorsementNo { get; set; }

        public string CertOrDocNo { get; set; }
        public string NewDetailID { get; set; }

        /// <summary>PolicyDetails.Field4.</summary>
        public string Description { get; set; }

        /// <summary>PolicyDetails.Field50 — UNCOMPLETED until the endorsement is completed.</summary>
        public string Status { get; set; }

        public string SourceDetailID { get; set; }
        public string SourceCertOrDocNo { get; set; }

        /// <summary>True for REVERSAL and REDO, which set the endorsed record to EXPIRED.</summary>
        public bool SourceRecordExpired { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int CoverDays { get; set; }

        public int SectionCount { get; set; }
        public int RiskItemCount { get; set; }

        /// <summary>Original sections set to EXPIRED by this endorsement.</summary>
        public List<string> ExpiredSectionTokens { get; set; } = new();

        /// <summary>Original sections stamped with this endorsement's number.</summary>
        public List<string> StampedSectionTokens { get; set; } = new();

        public decimal TotalSumInsured { get; set; }

        /// <summary>Sections after section-level adjustments, before the proposal-level ones.</summary>
        public decimal TotalPremium { get; set; }

        /// <summary>After proposal-level (other discount/loading) adjustments.</summary>
        public decimal NetPremium { get; set; }

        public decimal ProRataPremium { get; set; }
        public decimal PremiumDue { get; set; }

        public List<string> Warnings { get; set; } = new();
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    /// <summary>A section of a record, for the RETURN / REDO tick list.</summary>
    public class EndorsableFireSectionDto
    {
        /// <summary>Send this back in SelectedSectionTokens. The section's TransGUID (declaration number for rows without one).</summary>
        public string SelectionToken { get; set; }

        /// <summary>PolicyTempDetails.ENDTNum.</summary>
        public string SectionDeclarationNo { get; set; }

        public string SectionID { get; set; }
        public string SectionName { get; set; }
        public string Location { get; set; }
        public string Tag { get; set; }

        /// <summary>Already off cover from an earlier endorsement — can't be ticked.</summary>
        public bool IsExpired { get; set; }

        /// <summary>Endorsement number stamped on the section, if any.</summary>
        public string StampedBy { get; set; }

        public decimal SectionSumInsured { get; set; }
        public decimal SectionNetPremium { get; set; }
        public int RiskItemCount { get; set; }
    }

    /// <summary>One endorsement record on a policy.</summary>
    public class FireEndorsementListItemDto
    {
        public string DetailID { get; set; }
        public string EndorsementNo { get; set; }
        public string CertOrDocNo { get; set; }

        /// <summary>BizOption — the type, or EXPIRED once a later endorsement expired it.</summary>
        public string EndorsementType { get; set; }

        /// <summary>Field4.</summary>
        public string Description { get; set; }

        /// <summary>Field50 — UNCOMPLETED / COMPLETED.</summary>
        public string Status { get; set; }

        public DateTime? EntryDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TotalSumInsured { get; set; }
        public decimal PremiumDue { get; set; }
        public string CreatedBy { get; set; }
        public string Remarks { get; set; }
    }
}
