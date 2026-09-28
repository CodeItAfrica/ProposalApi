using System;
    
namespace GibsLifesMicroWebApi.Infrastructure.Common
{
    /// <summary>
    /// Replacement for the legacy global <c>settings.*</c> module
    /// (settings.COMPANY_ABREV, settings.CURRENT_ACCT_PERIOD, ...).
    /// Bound from the "GibsSettings" configuration section.
    /// </summary>
    public class GibsSettings
    {
        public const string SectionName = "GibsSettings";

        /// <summary>e.g. "GOXI", "CIP", "SA", "PAP". Some legacy branches switch on this.</summary>
        public string CompanyAbbrev { get; set; } = "GOXI";

        /// <summary>
        /// The current open accounting period. Transactions dated before this are rejected.
        /// Leave null to skip the check.
        /// </summary>
        public DateTime? CurrentAccountPeriod { get; set; }
    }
    /// <summary>
    /// Thrown when a domain/validation rule is violated. Surfaced as HTTP 400.
    /// Replaces the many "Messagebox(...) : Exit Sub" guards in the VB code.
    /// </summary>
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when a referenced record (insured, broker, sub-risk, policy...) is missing.
    /// Surfaced as HTTP 404.
    /// </summary>
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    /// <summary>
    /// Mirrors cls_AutoNumber.AutoNumEnum in the legacy VB code.
    /// </summary>
    public enum AutoNumberType
    {
        CNote,
        Policy,
        DNote,
        EndorsementNo,
        CertNo
    }

    /// <summary>
    /// The querystring "type" that drives proposal vs. policy and endorsement flows
    /// (POLY, PROP, RENEWAL, ADDITIONAL, DELETE, REVERSAL, ...).
    /// </summary>
    public static class TransactionType
    {
        public const string Policy = "POLY";
        public const string Proposal = "PROP";
        public const string Renewal = "RENEWAL";
        public const string Additional = "ADDITIONAL";
    }

    /// <summary>
    /// Business-source values that carry proportion rules in the legacy Save logic.
    /// </summary>
    public static class BusinessSource
    {
        public const string Direct = "DIRECT";
        public const string CoinsuranceLead = "CO-INSURANCE LEAD";
        public const string Accepted = "ACCEPTED";
    }

    /// <summary>
    /// Frequency-of-payment codes used when computing the loan/cover duration.
    /// </summary>
    public static class FrequencyOfPayment
    {
        public const string Weekly = "W";
        public const string Monthly = "M";
    }

    /// <summary>
    /// How premiums are derived on a bulk member upload.
    /// </summary>
    public enum BulkPricingMode
    {
        /// <summary>Premium = Policy.A10 (rate fraction) * amount. Matches cmduploadtodb_Click.</summary>
        PolicyRate,

        /// <summary>Premium = amount * rate/100 adjusted by loading/discount. Matches UploadDataExcel_GOXI.</summary>
        FixedRate
    }
}
