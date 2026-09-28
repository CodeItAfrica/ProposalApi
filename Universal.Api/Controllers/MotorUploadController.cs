using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    // ============================================================
    // MOTOR — BULK VEHICLE UPLOAD ENDPOINTS (Excel / CSV)
    //
    //   GET  motor/vehicles/upload/template   download the column contract
    //   POST motor/vehicles/upload/preview    parse + validate, NOTHING saved
    //   POST motor/vehicles/upload            parse + calculate + save
    //
    // AN UPLOAD ADDS VEHICLES TO THE DECLARATION. mode defaults to Append, so
    // uploading is equivalent to calling POST /motor/vehicles once per row —
    // nothing already on the proposal is deleted, and the user can then review
    // and correct each imported vehicle through the existing PUT/DELETE
    // motor/vehicles?certificateNo=… endpoints. Those certificate numbers come
    // back in MotorVehicleUploadResultDto.ImportedCertificateNos.
    //
    // mode=Replace is the opt-in destructive alternative: it clears every
    // existing MOTOR vehicle row for the proposal and rewrites from the file
    // (== POST /{proposalNo}/calculate/motor/complete with deleteExisting).
    // Only the Replace path reads the declaration-level parameters (other
    // discount/loading rates, proportion, FX, cover days); Append takes those
    // from the stored PolicyDetails header.
    //
    // PARTIAL IMPORT IS ALWAYS ON. Valid rows import; rows with errors are
    // skipped and returned in Upload.SkippedRows with row numbers and column
    // names, for the user to fix and re-upload (or key in by hand). One bad row
    // never blocks the other ninety-nine. The strict all-or-nothing check lives
    // in /preview, which the UI should call first.
    //
    // SWAGGER: the upload actions take a single bare IFormFile and take every
    // other value from the query string. Do NOT add loose [FromForm] scalar
    // parameters alongside it — Swashbuckle throws while generating the
    // OpenAPI document when an action mixes the two, which surfaces as
    // "Failed to load API definition" with a 500 on openapi.json and no hint
    // as to which endpoint caused it. If more form fields are ever needed,
    // move the file and all of them into one [FromForm] model class.
    // ============================================================
    public partial class PolicyAdminController
    {
        private const long MaxMotorUploadBytes = 10 * 1024 * 1024; // 10 MB
        private const int MaxMotorUploadRows = 5000;

        private static readonly HashSet<string> AllowedMotorUploadExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".csv", ".txt", ".xlsx", ".xlsm", ".xls" };

        /// <summary>
        /// Download the vehicle upload template — the authoritative list of
        /// column headers, with one worked example row. CSV; Excel opens and
        /// re-saves it as .xlsx happily, and both upload.
        /// </summary>
        [HttpGet("motor/vehicles/upload/template")]
        public ActionResult DownloadMotorVehicleUploadTemplate()
        {
            var csv = MotorVehicleFileParser.BuildTemplateCsv();
            // UTF-8 BOM so Excel renders ₦ and accented characters correctly.
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv);
            return File(bytes, "text/csv", "motor-vehicle-upload-template.csv");
        }

        /// <summary>
        /// Parse and validate an uploaded vehicle file WITHOUT saving anything.
        /// Returns every row with either its mapped vehicle or its errors, plus
        /// the columns that were and weren't recognized. No proposal number
        /// needed — this checks the file, not the policy. Call it first so the
        /// user can fix the spreadsheet before anything is written.
        /// </summary>
        [HttpPost("motor/vehicles/upload/preview")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxMotorUploadBytes)]
        public async Task<ActionResult<MotorVehicleUploadPreviewDto>> PreviewMotorVehicleUpload(
            IFormFile file)
        {
            var fileError = ValidateMotorUploadFile(file);
            if (fileError != null) return BadRequest(fileError);

            var preview = await ParseMotorUploadAsync(file);

            // A file with SOME bad rows is still a useful 200 — the caller needs
            // the row-level detail either way. Only a file that yielded nothing
            // usable is a 400.
            return preview.ValidRowCount > 0 ? Ok(preview) : BadRequest(preview);
        }

        /// <summary>
        /// Upload vehicles from an Excel/CSV file and persist them.
        ///
        /// mode=Append (DEFAULT): every valid row is ADDED to the proposal's
        /// existing Motor declaration — nothing existing is deleted. Declaration
        /// terms (cover days, proportion, FX) and the stored Other Discount/
        /// Loading rates are read from the PolicyDetails header and re-applied
        /// against the new combined total, so the query parameters below are
        /// IGNORED in this mode. Each imported vehicle gets its own CERTNO,
        /// returned in ImportedCertificateNos, and can then be edited or removed
        /// individually via PUT/DELETE motor/vehicles.
        ///
        /// mode=Replace: destructive full replace — clears every existing MOTOR
        /// vehicle row for the proposal and rewrites from the file. The
        /// declaration-level query parameters apply only in this mode.
        ///
        /// Rows with errors are always SKIPPED, not fatal — they come back in
        /// Upload.SkippedRows with the row number and column to fix. The upload
        /// only fails outright when no row at all could be read.
        /// </summary>
        [HttpPost("motor/vehicles/upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxMotorUploadBytes)]
        public async Task<ActionResult<MotorVehicleUploadResultDto>> UploadMotorVehicles(
            IFormFile file,
            [FromQuery] string proposalNo,
            [FromQuery] MotorVehicleUploadMode mode = MotorVehicleUploadMode.Append,
            [FromQuery] decimal otherDiscountRate = 0,
            [FromQuery] decimal otherLoadingRate = 0,
            [FromQuery] decimal proportionRate = 100,
            [FromQuery] decimal exchangeRate = 1,
            [FromQuery] string currency = "NAIRA",
            [FromQuery] int coverDays = 365,
            [FromQuery] string uploadedBy = "SYSTEM")
        {
            if (string.IsNullOrWhiteSpace(proposalNo))
                return BadRequest("Proposal number is required");

            var fileError = ValidateMotorUploadFile(file);
            if (fileError != null) return BadRequest(fileError);

            var result = new MotorVehicleUploadResultDto
            {
                ProposalNo = proposalNo,
                Mode = mode,
                Success = false
            };

            var preview = await ParseMotorUploadAsync(file);
            result.Upload = preview;
            result.VehiclesSkipped = preview.InvalidRowCount;

            // The only fatal case: not a single usable row. Anything less than
            // that imports what it can — the skipped rows are reported, not
            // treated as a reason to reject the other rows.
            if (preview.ValidRowCount == 0)
            {
                result.Message = preview.Message ?? "No valid vehicle rows could be read from the file";
                return BadRequest(result);
            }

            var vehicles = preview.Vehicles;

            if (mode == MotorVehicleUploadMode.Append)
            {
                var appended = await _proposalService.AddMotorVehiclesAsync(proposalNo, vehicles);
                result.Calculation = appended;
                result.Success = appended.Success;

                if (!appended.Success)
                {
                    result.Message = appended.Message;
                    return BadRequest(result);
                }

                result.VehiclesImported = vehicles.Count;
                result.ImportedCertificateNos = appended.CalculatedVehicles
                    .Select(v => v.CertificateNo)
                    .ToList();
                result.Message =
                    $"{vehicles.Count} vehicle(s) added to the declaration from {file.FileName}" +
                    (result.VehiclesSkipped > 0
                        ? $"; {result.VehiclesSkipped} row(s) skipped — see Upload.SkippedRows"
                        : "") +
                    ". Declaration totals recalculated.";

                return Ok(result);
            }

            // ---- Replace: same path as /calculate/motor/complete ------------
            if (coverDays <= 0 || coverDays > 366)
            {
                result.Message = "Cover days must be between 1 and 366";
                return BadRequest(result);
            }

            if (otherDiscountRate < 0 || otherDiscountRate > 100)
            {
                result.Message = "Other discount rate must be between 0 and 100";
                return BadRequest(result);
            }

            if (otherLoadingRate < 0)
            {
                result.Message = "Other loading rate cannot be negative";
                return BadRequest(result);
            }

            var request = new MotorCalculationRequestDto
            {
                ProposalNo = proposalNo,
                Vehicles = vehicles,
                OtherDiscountRate = otherDiscountRate,
                OtherLoadingRate = otherLoadingRate,
                ProportionRate = proportionRate,
                ExchangeRate = exchangeRate,
                Currency = currency,
                CoverDays = coverDays,
                CalculatedBy = uploadedBy
            };

            var calculation = await _proposalService.CalculateMotorCompleteAsync(request);
            result.Calculation = calculation;
            if (!calculation.Success)
            {
                result.Message = calculation.Message;
                return BadRequest(result);
            }

            var saveResult = await _proposalService.SaveMotorCalculationAsync(calculation, deleteExisting: true);
            if (saveResult.ErrorCode != 0)
            {
                result.Message = $"{saveResult.ErrorMessage}: {saveResult.ExtraMessage}";
                return BadRequest(result);
            }

            result.Success = true;
            result.VehiclesImported = vehicles.Count;
            result.ImportedCertificateNos = calculation.CalculatedVehicles
                .Select(v => v.CertificateNo)
                .ToList();
            result.Message =
                $"{vehicles.Count} vehicle(s) imported from {file.FileName} — the proposal's motor calculation " +
                "was REPLACED and saved" +
                (result.VehiclesSkipped > 0
                    ? $"; {result.VehiclesSkipped} row(s) skipped — see Upload.SkippedRows"
                    : "") + ".";

            return Ok(result);
        }

        // ============================================
        // HELPERS
        // ============================================

        private static string ValidateMotorUploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return "No file was uploaded (expected a multipart/form-data field named 'file')";

            if (file.Length > MaxMotorUploadBytes)
                return $"File is too large ({file.Length / 1024 / 1024} MB). Maximum is {MaxMotorUploadBytes / 1024 / 1024} MB.";

            var extension = Path.GetExtension(file.FileName) ?? "";
            if (!AllowedMotorUploadExtensions.Contains(extension))
                return $"Unsupported file type '{extension}'. Upload a .csv, .xlsx, .xlsm or .xls file.";

            return null;
        }

        private static async Task<MotorVehicleUploadPreviewDto> ParseMotorUploadAsync(IFormFile file)
        {
            // Buffered into memory because ExcelDataReader needs a seekable
            // stream; capped at MaxMotorUploadBytes above.
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            buffer.Position = 0;

            return MotorVehicleFileParser.Parse(buffer, file.FileName, MaxMotorUploadRows);
        }
    }
}
