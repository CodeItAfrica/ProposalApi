using System;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Models
{
    public class AutoNumber
    {
        [Key]
        public long NumID { get; set; }

        public string NumType { get; set; }

        public string BranchID { get; set; }


        //public string CompanyID { get; set; }

        public string RiskID { get; set; }

        public long? NextValue { get; set; }

        public DateTime? LastUpdated { get; set; }

        public string Format { get; set; }

        public string Sample { get; set; }
    }



    public enum AutoNumType
    {
        VOUCHER,
        INVOICE,
        PROPOSAL,
        POLICY,
        DNOTE,
        CNOTE,
        CLAIM,
        ENDTNO,
        AGENTS,
        CERTNO,
        INSURED,
        BROKER,
        NOTIFY,
        INSCO,
        REINCO,
        OTHERS,
        JOURNAL,
        RECEIPT,
        RECEIPT_UNALLOCATED,
        CESSION_NO,
        ASSET_CODE,
        DECLARATION_NO,
        CLAIM_VOUCHER,
        COMMISSION_VOUCHER,
        JOURNAL_VOUCHER,
        AGENCY_COMMISSION_VOUCHER
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ENDORSEMENT TYPE — used in $P$ token resolution
    // ──────────────────────────────────────────────────────────────────────────

    public enum EndorseType
    {
        None,
        DELETED,
        DELETE,
        REVERSAL,
        RETURN,
        RENEWAL,
        RENEW,
        ADDITIONAL,
        NIL,
        REDO
    }

    // ──────────────────────────────────────────────────────────────────────────
    // REQUEST DTOs
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to generate the next formatted auto number.
    /// Mirrors getNextAutoNumber() parameters from cls_AutoNumber.
    /// </summary>
    public class GenerateAutoNumberRequest
    {
        /// <summary>
        /// Pass "[AUTO]" to trigger auto-generation, or a literal value to use as-is (uppercased).
        /// </summary>
        [Required]
        public string CustomNo { get; set; } = "[AUTO]";

        [Required]
        public AutoNumType NumType { get; set; }

        [Required]
        public string BranchID { get; set; } = string.Empty;

        /// <summary>Optional sub-risk ID. Used for $SR$ and $RC$ token replacement.</summary>
        public string? SubRiskID { get; set; }

        /// <summary>Optional endorsement type. Drives the $P$ token (A/C/R/E/N).</summary>
        public EndorseType EndorseType { get; set; } = EndorseType.None;

        /// <summary>
        /// Optional date used for $MO$ token. Defaults to today if not supplied.
        /// Format: yyyy-MM-dd
        /// </summary>
        public string? OptionalDate { get; set; }
    }

    /// <summary>
    /// Request to create or update an AutoNumber record.
    /// </summary>
    public class AutoNumberRequest
    {
        [Required]
        public string NumType { get; set; } = string.Empty;

        [Required]
        public string BranchID { get; set; } = string.Empty;

        public string? CompanyID { get; set; }
        public string? RiskID { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "NextValue must be at least 1.")]
        public long NextValue { get; set; } = 1;

        /// <summary>
        /// Format string using tokens: $SR$, $RC$, $YR$, $MO$, $0000$, $BR$, $BR2$, $P$
        /// Example: "$BR$-CLM-$YR$-$00000$"
        /// </summary>
        [Required]
        public string Format { get; set; } = string.Empty;

        public string? Sample { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RESPONSE DTOs
    // ──────────────────────────────────────────────────────────────────────────

    public class AutoNumberResponse
    {
        public long NumID { get; set; }
        public string? NumType { get; set; }
        public string? BranchID { get; set; }
        public string? CompanyID { get; set; }
        public string? RiskID { get; set; }
        public long NextValue { get; set; }
        public string? Format { get; set; }
        public string? Sample { get; set; }
        public System.DateTime? LastUpdated { get; set; }
    }

    public class GenerateAutoNumberResponse
    {
        /// <summary>The generated formatted number, e.g. "LAG-CLM-25-00042"</summary>
        public string GeneratedNumber { get; set; } = string.Empty;
        public string NumType { get; set; } = string.Empty;
        public string BranchID { get; set; } = string.Empty;
    }
    
    public class ApiResult<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public string? ErrorCode { get; set; }

        public static ApiResult<T> Ok(T data, string? message = null) =>
            new() { Success = true, Data = data, Message = message };

        public static ApiResult<T> Fail(string message, string? code = null) =>
            new() { Success = false, Message = message, ErrorCode = code };
    }

    public class ApiResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }

        public static ApiResult Ok(string? message = null) =>
            new() { Success = true, Message = message };

        public static ApiResult Fail(string message, string? code = null) =>
            new() { Success = false, Message = message, ErrorCode = code };
    }


}
