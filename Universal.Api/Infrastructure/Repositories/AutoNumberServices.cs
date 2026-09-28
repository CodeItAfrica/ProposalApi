using Dapper;

using Gibs.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Gibs.Application.Services
{
    /// <summary>
    /// Business logic for AutoNumbers.
    /// Full port of cls_AutoNumber.vb including:
    ///   - getNextAutoNumber()  → GenerateAsync()
    ///   - getNextSerialNo()    → handled atomically in repository
    ///   - getSerialNoFormat()  → handled atomically in repository
    ///   - Insert / Update / Delete / SelectThis / SelectAll
    ///
    /// Format tokens supported:
    ///   $SR$    Sub-risk ID
    ///   $RC$    Risk ID (looked up from SubRiskID)
    ///   $YR$    2-digit current year
    ///   $MO$    2-digit month (from OptionalDate or today)
    ///   $BR$    Branch ID
    ///   $BR2$   Branch secondary code (looked up from Branches table)
    ///   $P$     Endorsement type letter (A/C/R/E/N)
    ///   $0...0$ Zero-padded serial number (length = number of zeros between markers)
    /// </summary>
    public class AutoNumberServices
    {
        private readonly string _connectionString;

        private readonly AutoNumberRepository _repo;
        private readonly ILogger<AutoNumberServices> _logger;
        private readonly AutoNumberSettings _settings;

        public AutoNumberServices(IConfiguration config,
            AutoNumberRepository repo,
            ILogger<AutoNumberServices> logger,
            AutoNumberSettings settings)
        {
            _connectionString = config["AppSettings:SqldbConnString"];

            _repo = repo;
            _logger = logger;
            _settings = settings;
        }

        private IDbConnection CreateConnection() =>
            new SqlConnection(_connectionString);

        // ─────────────────────────────────────────────────────────────────────
        // GENERATE — mirrors getNextAutoNumber()
        // ─────────────────────────────────────────────────────────────────────

        public async Task<ApiResult<GenerateAutoNumberResponse>> GenerateAsync(
            GenerateAutoNumberRequest req)
        {
            // If CustomNo is not "[AUTO]" → return it uppercased, no DB hit
            if (!req.CustomNo.ToUpper().Contains("AUTO"))
            {
                return ApiResult<GenerateAutoNumberResponse>.Ok(new GenerateAutoNumberResponse
                {
                    GeneratedNumber = req.CustomNo.ToUpper(),
                    NumType = req.NumType.ToString(),
                    BranchID = req.BranchID
                });
            }

            // Resolve optional date — default to today
            DateTime optDate = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(req.OptionalDate)
                && DateTime.TryParse(req.OptionalDate, out var parsedDate))
            {
                optDate = parsedDate;
            }

            // Fetch format string and atomically increment the serial counter
            var numTypeStr = req.NumType.ToString();
            var riskID = req.SubRiskID ?? "";

            var (format, nextValue) = await _repo.GetFormatAndIncrementAsync(
                numTypeStr, req.BranchID, riskID);

            if (string.IsNullOrWhiteSpace(format) || nextValue < 0)
            {
                return ApiResult<GenerateAutoNumberResponse>.Fail(
                    $"No AutoNumber format configured for NumType='{numTypeStr}', " +
                    $"BranchID='{req.BranchID}'. Please set up the AutoNumbers table.");
            }

            // ── Apply serial number token ($0...0$) ───────────────────────────
            var result = ApplySerialToken(format, nextValue);
            if (result is null)
            {
                return ApiResult<GenerateAutoNumberResponse>.Fail(
                    "Format string is missing the serial number token (e.g. $00000$).");
            }

            // ── Apply remaining tokens ────────────────────────────────────────
            result = result
                .Replace("$SR$", req.SubRiskID ?? "")
                .Replace("$YR$", DateTime.Now.Year.ToString()[^2..])
                .Replace("$MO$", optDate.Month.ToString().PadLeft(2, '0'))
                .Replace("$BR$", req.BranchID);

            if (result.Contains("$RC$"))
            {
                var riskCode = !string.IsNullOrWhiteSpace(req.SubRiskID)
                    ? await _repo.GetRiskIDBySubRiskAsync(req.SubRiskID)
                    : null;

                result = result.Replace("$RC$", riskCode ?? "");
            }

            if (result.Contains("$BR2$"))
            {
                var branchCode2 = await _repo.GetBranchID2Async(req.BranchID);
                result = result.Replace("$BR2$", branchCode2 ?? "");
            }

            if (result.Contains("$P$"))
            {
                result = result.Replace("$P$", ResolveEndorseTypeLetter(req.EndorseType));
            }

            return ApiResult<GenerateAutoNumberResponse>.Ok(new GenerateAutoNumberResponse
            {
                GeneratedNumber = result,
                NumType = numTypeStr,
                BranchID = req.BranchID
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET NEXT AUTO NUMBER — used internally by Claims Notification save
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches the next formatted auto-number for the given type, branch, and optional risk.
        ///
        /// Lookup priority (mirrors VB fallback behaviour):
        ///   1. Exact match on NumType + BranchID + RiskID
        ///   2. Fallback to NumType + BranchID where RiskID IS NULL or empty
        ///
        /// This means NOTIFY formats only need one row per branch — RiskID is optional.
        /// </summary>
        public async Task<string?> GetNextAutoNumberAsync(
      string numType, string branchId, string? riskId = null)
        {
            var genResult = await GenerateAsync(new GenerateAutoNumberRequest
            {
                CustomNo = "[AUTO]",
                NumType = Enum.Parse<AutoNumType>(numType, ignoreCase: true),
                BranchID = branchId,
                SubRiskID = riskId
            });

            if (!genResult.Success)
            {
                _logger.LogWarning(
                    "GetNextAutoNumberAsync — GenerateAsync failed for NumType={NumType}, BranchID={BranchID}, RiskID={RiskID}: {Error}",
            numType, branchId, riskId ?? "(null)", genResult.Message); // ← fix property name here
                return null;
            }

            return genResult.Data.GeneratedNumber;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CRUD — mirrors Insert / Update / Delete / SelectThis / SelectAll
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<AutoNumberResponse>> GetAllAsync()
        {
            var records = await _repo.GetAllAsync();
            return records.Select(MapToResponse).ToList();
        }

        public async Task<AutoNumberResponse?> GetByIdAsync(long numId)
        {
            var rec = await _repo.GetByIdAsync(numId);
            return rec is null ? null : MapToResponse(rec);
        }

        public async Task<AutoNumberResponse?> GetAsync(string numType, string branchId)
        {
            var rec = await _repo.GetAsync(numType, branchId);
            return rec is null ? null : MapToResponse(rec);
        }

        public async Task<ApiResult<AutoNumberResponse>> InsertAsync(
            AutoNumberRequest request)
        {
            var error = ValidateRequest(request);
            if (error is not null)
                return ApiResult<AutoNumberResponse>.Fail(error);

            var entity = MapToEntity(request);
            var result = await _repo.InsertAsync(entity);

            if (!result.Success)
                return ApiResult<AutoNumberResponse>.Fail(result.ErrorMessage!);

            entity.NumID = (long)result.GeneratedId;
            return ApiResult<AutoNumberResponse>.Ok(
                MapToResponse(entity), "AutoNumber record created.");
        }

        public async Task<ApiResult<AutoNumberResponse>> UpdateAsync(
            long numId, AutoNumberRequest request)
        {
            var existing = await _repo.GetByIdAsync(numId);
            if (existing is null)
                return ApiResult<AutoNumberResponse>.Fail($"AutoNumber {numId} not found.");

            var error = ValidateRequest(request);
            if (error is not null)
                return ApiResult<AutoNumberResponse>.Fail(error);

            existing.NumType = request.NumType;
            existing.BranchID = request.BranchID;
            //existing.CompanyID = request.CompanyID;
            existing.RiskID = request.RiskID;
            existing.NextValue = request.NextValue;
            existing.Format = request.Format;
            existing.Sample = request.Sample;

            var result = await _repo.UpdateAsync(existing);
            if (!result.Success)
                return ApiResult<AutoNumberResponse>.Fail(result.ErrorMessage!);

            return ApiResult<AutoNumberResponse>.Ok(
                MapToResponse(existing), "AutoNumber record updated.");
        }

        public async Task<ApiResult> DeleteAsync(long numId)
        {
            var existing = await _repo.GetByIdAsync(numId);
            if (existing is null)
                return ApiResult.Fail($"AutoNumber {numId} not found.");

            var result = await _repo.DeleteAsync(numId);
            return result.Success
                ? ApiResult.Ok("AutoNumber record deleted.")
                : ApiResult.Fail(result.ErrorMessage!);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the $0...0$ token in the format string, determines the required
        /// padding length, formats the serial number, and replaces the token.
        /// Returns null if no serial token is found.
        /// </summary>
        private static string? ApplySerialToken(string format, long nextValue)
        {
            var startIdx = format.IndexOf("$0", StringComparison.Ordinal);
            if (startIdx < 0) return null;

            var endIdx = format.IndexOf("0$", startIdx, StringComparison.Ordinal);
            if (endIdx < 0) return null;

            // Length = positions between the two markers (minimum 3, matching VB logic)
            var len = endIdx - startIdx;
            if (len < 3) len = 3;

            var zeros = new string('0', len);
            var token = $"${zeros}$";
            var padded = nextValue.ToString(zeros);  // zero-pad to len digits

            return format.Replace(token, padded);
        }

        /// <summary>
        /// Maps EndorseType enum to its single-letter code — mirrors the VB Select Case block.
        /// </summary>
        private static string ResolveEndorseTypeLetter(EndorseType et) => et switch
        {
            EndorseType.DELETED or
            EndorseType.DELETE or
            EndorseType.REVERSAL => "C",
            EndorseType.RETURN => "C",
            EndorseType.RENEWAL or
            EndorseType.RENEW => "R",
            EndorseType.ADDITIONAL => "E",
            EndorseType.NIL or
            EndorseType.REDO => "N",
            _ => "A"   // default (matches VB Case Else → "A")
        };

        private static string? ValidateRequest(AutoNumberRequest req)
        {
            if (!req.Format.Contains("$0"))
                return "Format string must contain a serial number token such as $00000$.";

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // MAPPERS
        // ─────────────────────────────────────────────────────────────────────

        private static AutoNumberResponse MapToResponse(AutoNumber e) => new()
        {
            NumID = e.NumID,
            NumType = e.NumType,
            BranchID = e.BranchID,
            //CompanyID = e.CompanyID,
            RiskID = e.RiskID,
            NextValue = (long)e.NextValue,
            Format = e.Format,
            Sample = e.Sample,
            LastUpdated = e.LastUpdated
        };

        private static AutoNumber MapToEntity(AutoNumberRequest r) => new()
        {
            NumType = r.NumType,
            BranchID = r.BranchID,
            //CompanyID = r.CompanyID,
            RiskID = r.RiskID,
            NextValue = r.NextValue,
            Format = r.Format,
            Sample = r.Sample
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SETTINGS
    // ─────────────────────────────────────────────────────────────────────────

    public class AutoNumberSettings
    {
        /// <summary>e.g. "staco" — affects CLAIM type serial lookup behaviour.</summary>
        public string CompanyAbbrev { get; set; } = string.Empty;
    }
}