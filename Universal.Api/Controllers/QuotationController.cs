using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuotationController : SecureControllerBase
    {
        public QuotationController(Repository repository, AuthContext authContext)
            : base(repository, authContext) { }


        [HttpPost("calculate")]
        public async Task<IActionResult> CalculateQuote([FromBody] QuotationRequest request)
        {
            

            try
            {
                if (request == null)
                    return BadRequest("Request is null");

                var result = await _repository.CalculateAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }


        //[HttpPut("edit/{quoteId}")]
        //public async Task<IActionResult> EditQuotation(int quoteId, [FromBody] QuotationRequest request)
        //{
        //    try
        //    {
        //        var result = await _repository.UpdateAsync(quoteId, request);
        //        return Ok(result);
        //    }
        //    catch (KeyNotFoundException)
        //    {
        //        return NotFound("Quotation not found.");
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(ex.Message);
        //    }
        //}


        //[HttpPut("trad/edit/{quoteId}")]
        //public async Task<IActionResult> EditTraditionalQuotation(int quoteId, [FromBody] TraditionalQuotationRequest request)
        //{
        //    try
        //    {
        //        var result = await _repository.UpdateTraditionalAsync(quoteId, request);
        //        return Ok(result);
        //    }
        //    catch (KeyNotFoundException)
        //    {
        //        return NotFound("Quotation not found.");
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(ex.Message);
        //    }
        //}

        /// <summary>
        /// Calculate a traditional quotation
        /// </summary>
        [HttpPost("tradcalculate")]
        public async Task<IActionResult> CalculateTraditional([FromBody] TraditionalQuotationRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request is null.");

                var result = await _repository.CalculateTraditionalAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        //[HttpPut("{quoteId}")]
        //public async Task<IActionResult> UpdateQuote(long quoteId, [FromBody] QuotationRequest request)
        //{
        //    try
        //    {
        //        var result = await _repository.UpdateAsync(quoteId, request);
        //        return Ok(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}


        [HttpGet("filterquotation")]
        public async Task<IActionResult> GetQuotation(
    [FromQuery] string quoteNo,
    [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var result = await _repository.GetQuotationByNoAsync(quoteNo, fromDate, toDate);
                if (result == null || result.Count == 0)
                    return NotFound("No quotations found matching the criteria.");

                return Ok(result);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }


        [HttpGet("quotations")]
        public async Task<IActionResult> GetAllQuotes(
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 10,
      [FromQuery] string tag = null)     // <-- new optional param
        {
            var pagedResult = await _repository.GetAllAsync(page, pageSize, tag);
            return Ok(pagedResult);
        }

        /// <summary>
        /// Get quotation by quote number (returns master + all members)
        /// </summary>
        [HttpGet("quote-no")]
        public async Task<IActionResult> GetByQuoteNo([FromQuery] string quoteNo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(quoteNo))
                    return BadRequest("Quote number is required.");

                var quote = await _repository.GetByQuoteNoAsync(quoteNo);
                if (quote == null)
                    return NotFound($"Quote with number '{quoteNo}' not found.");

                return Ok(quote);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }


        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var quote = await _repository.GetByIdAsync(id);
            if (quote == null)
                return NotFound();

            return Ok(quote);
        }

        /// <summary>
        /// Delete quotation by quote number (deletes master + all members)
        /// </summary>
        [HttpDelete("quote-no")]
        public async Task<IActionResult> DeleteByQuoteNo([FromQuery] string quoteNo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(quoteNo))
                    return BadRequest("Quote number is required.");

                await _repository.DeleteByQuoteNoAsync(quoteNo);
                return NoContent();
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Permanently delete a member (hard delete - use with caution)
        /// </summary>
        /// <param name="memberId">The member ID to permanently delete</param>
        /// <returns>No content on success</returns>
        [HttpDelete("member/{memberId:long}/permanent")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PermanentlyDeleteMember(long memberId)
        {
            try
            {
                await _repository.PermanentlyDeleteMemberAsync(memberId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpGet("ProductSettings")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _repository.GetAllProductAsync();
            return Ok(list);
        }

        [HttpGet("product/{code}")]
        public async Task<IActionResult> GetProductByCode(string code)
        {
            var item = await _repository.GetByCodeAsync(code);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("productsettings")]
        public async Task<IActionResult> AddProductSetting([FromBody] ProductSettings config)
        {
            if (config == null)
                return BadRequest("Invalid product config");

            await _repository.AddAsync(config);
            return CreatedAtAction(nameof(GetProductByCode), new { code = config.ProductID }, config);
        }

        [HttpPut("ProductSettings/{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ProductSettings model)
        {
            if (id != model.ProductID)
                return BadRequest("ID mismatch");

            await _repository.UpdateAsync(model);
            return NoContent();
        }

        [HttpDelete("ProductSettings/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _repository.DeleteAsync(id);
            return NoContent();
        }
        [HttpGet("Branches")]
        public async Task<IActionResult> GetAllBranches()
        {
            var branches = await _repository.GetAllBranchesAsync();
            return Ok(branches);
        }

        [HttpPost]
        public async Task<IActionResult> CreateQuotation([FromBody] QuotationRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var result = await _repository.CreateQuotationAsync(request, currentUser);

                return Ok(new { Success = true, Data = result, Message = "Quotation created successfully" });
            }
            //catch (Exception ex)
            //{
            //    return StatusCode(500, new { Success = false, Message = ex.Message });
            //}
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Get quotation by quotation number
        /// </summary>
        [HttpGet("GroupLife/{quotationNumber}")]
        public async Task<IActionResult> GetQuotation(string quotationNumber)
        {
            try
            {
                var result = await _repository.GetQuotationAsync(quotationNumber);

                if (result == null)
                {
                    return NotFound(new { Success = false, Message = "Quotation not found" });
                }

                return Ok(new { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Update an existing quotation - FIXED VERSION
        /// </summary>
        [HttpPut("GroupLife/{quotationNumber}")]
        public async Task<IActionResult> UpdateQuotation(string quotationNumber, [FromBody] QuotationRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate quotation exists
                var existingQuote = await _repository.GetQuoteByNumberAsync(quotationNumber);
                if (existingQuote == null)
                {
                    return NotFound(new { Success = false, Message = "Quotation not found" });
                }

                var currentUser = GetCurrentUser();

                // Ensure quotation number matches
                request.QuotationNumber = quotationNumber;

                var result = await _repository.UpdateQuotationAsync(quotationNumber, request, currentUser);

                if (result == null)
                {
                    return NotFound(new { Success = false, Message = "Failed to update quotation" });
                }

                return Ok(new { Success = true, Data = result, Message = "Quotation updated successfully" });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }


        /// <summary>
        /// Update a specific member and recalculate the entire quotation
        /// </summary>
        [HttpPut("{quotationNumber}/member/{memberId}")]
        public async Task<IActionResult> UpdateMember(string quotationNumber, long memberId, [FromBody] MemberDetailsDto memberDetails)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var result = await _repository.UpdateMemberAndRecalculateAsync(quotationNumber, memberId, memberDetails, currentUser);

                if (result == null)
                {
                    return NotFound(new { Success = false, Message = "Quotation or member not found" });
                }

                return Ok(new { Success = true, Data = result, Message = "Member updated and quotation recalculated successfully" });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Add a new member to existing quotation and recalculate
        /// </summary>
        [HttpPost("{quotationNumber}/member")]
        public async Task<IActionResult> AddMemberToQuotation(string quotationNumber, [FromBody] MemberDetailsDto memberDetails)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var result = await _repository.AddMemberAndRecalculateAsync(quotationNumber, memberDetails, currentUser);

                if (result == null)
                {
                    return NotFound(new { Success = false, Message = "Quotation not found" });
                }

                return Ok(new { Success = true, Data = result, Message = "Member added and quotation recalculated successfully" });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Remove a member from quotation and recalculate
        /// </summary>
        [HttpDelete("{quotationNumber}/member/{memberId}")]
        public async Task<IActionResult> RemoveMemberFromQuotation(string quotationNumber, long memberId)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var result = await _repository.RemoveMemberAndRecalculateAsync(quotationNumber, memberId, currentUser);

                if (result == null)
                {
                    return NotFound(new { Success = false, Message = "Quotation or member not found" });
                }

                return Ok(new { Success = true, Data = result, Message = "Member removed and quotation recalculated successfully" });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Delete a quotation
        /// </summary>
        [HttpDelete("GroupLife/{quotationNumber}")]
        public async Task<IActionResult> DeleteQuotation(string quotationNumber)
        {
            try
            {
                var result = await _repository.DeleteQuotationAsync(quotationNumber);

                if (!result)
                {
                    return NotFound(new { Success = false, Message = "Quotation not found" });
                }

                return Ok(new { Success = true, Message = "Quotation deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// Compute quotation without saving (for preview/calculation purposes)
        /// </summary>
        [HttpPost("compute")]
        public async Task<IActionResult> ComputeQuotation([FromBody] QuotationRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _repository.ComputeQuotationAsync(request);

                return Ok(new { Success = true, Data = result, Message = "Quotation computed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        private string GetCurrentUser()
        {
            // This should be implemented based on your authentication system
            // For now, returning a default value
            return User?.Identity?.Name ?? "SYSTEM";
        }

        /// <summary>
        /// Get all members for a specific quotation
        /// </summary>
        [HttpGet("quotationmember/{quotationNumber}")]
        public async Task<IActionResult> GetMembersByQuotation(string quotationNumber)
        {
            try
            {
                var members = await _repository.GetMembersByQuotationAsync(quotationNumber);
                return Ok(new { Success = true, Data = members });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// Get member by ID
        /// </summary>
        [HttpGet("GroupLifeMember/{memberId}")]
        public async Task<IActionResult> GetMember(long memberId)
        {
            try
            {
                var member = await _repository.GetMemberByIdAsync(memberId);

                if (member == null)
                {
                    return NotFound(new { Success = false, Message = "Member not found" });
                }

                return Ok(new { Success = true, Data = member });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// Delete a member
        /// </summary>
        [HttpDelete("GroupLifeMember/{memberId}")]
        public async Task<IActionResult> DeleteMember(long memberId)
        {
            try
            {
                var result = await _repository.DeleteMemberAsync(memberId);

                if (!result)
                {
                    return NotFound(new { Success = false, Message = "Member not found" });
                }

                return Ok(new { Success = true, Message = "Member deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }


        /// <summary>
        /// Upload and process Excel or CSV files containing member details
        /// </summary>
        /// <param name="file">The Excel (.xlsx, .xls) or CSV file to upload</param>
        /// <returns>Parsed member details from the file</returns>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(FileImportResponse), 200)]
        [ProducesResponseType(typeof(string), 400)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded or file is empty");

                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                List<MemberDetailsDto> members;
                List<string> errors = new List<string>();

                switch (fileExtension)
                {
                    case ".csv":
                        members = await ProcessCsvFile(file, errors);
                        break;
                    case ".xlsx":
                    case ".xls":
                        members = await ProcessExcelFile(file, errors);
                        break;
                    default:
                        return BadRequest("Unsupported file format. Please upload CSV, XLS, or XLSX files only.");
                }

                var response = new FileImportResponse
                {
                    Success = true,
                    TotalRecords = members.Count,
                    ValidRecords = members.Count(m => ValidateMember(m).Count == 0),
                    Members = members,
                    Errors = errors,
                    ProcessedAt = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Download CSV template for member details import
        /// </summary>
        /// <returns>CSV template file</returns>
        [HttpGet("template/csv")]
        [ProducesResponseType(typeof(FileContentResult), 200)]
        public IActionResult DownloadCsvTemplate()
        {
            try
            {
                var csvContent = GenerateCsvTemplate();
                var bytes = Encoding.UTF8.GetBytes(csvContent);

                return File(bytes, "text/csv", "group_life_import_template.csv");
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Download Excel template for member details import
        /// </summary>
        /// <returns>Excel template file</returns>
        [HttpGet("template/excel")]
        [ProducesResponseType(typeof(FileContentResult), 200)]
        public IActionResult DownloadExcelTemplate()
        {
            try
            {
                var excelBytes = GenerateExcelTemplate();
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                           "group_life_import_template.xlsx");
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Validate uploaded data and return validation results
        /// </summary>
        /// <param name="file">The file to validate</param>
        /// <returns>Validation results without processing the data</returns>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ValidationResponse), 200)]
        [ProducesResponseType(typeof(string), 400)]
        public async Task<IActionResult> ValidateFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded or file is empty");

                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                List<MemberDetailsDto> members;
                List<string> errors = new List<string>();

                switch (fileExtension)
                {
                    case ".csv":
                        members = await ProcessCsvFile(file, errors);
                        break;
                    case ".xlsx":
                    case ".xls":
                        members = await ProcessExcelFile(file, errors);
                        break;
                    default:
                        return BadRequest("Unsupported file format. Please upload CSV, XLS, or XLSX files only.");
                }

                var validationResults = new List<MemberValidationResult>();

                for (int i = 0; i < members.Count; i++)
                {
                    var validationErrors = ValidateMember(members[i]);
                    validationResults.Add(new MemberValidationResult
                    {
                        RowNumber = i + 2, // +2 because row 1 is header and arrays are 0-based
                        StaffNo = members[i].StaffNo,
                        EmployeeName = members[i].EmployeeNames,
                        IsValid = validationErrors.Count == 0,
                        ValidationErrors = validationErrors
                    });
                }

                var response = new ValidationResponse
                {
                    TotalRecords = members.Count,
                    ValidRecords = validationResults.Count(v => v.IsValid),
                    InvalidRecords = validationResults.Count(v => !v.IsValid),
                    ValidationResults = validationResults,
                    FileErrors = errors,
                    ValidatedAt = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        private async Task<List<MemberDetailsDto>> ProcessCsvFile(IFormFile file, List<string> errors)
        {
            var members = new List<MemberDetailsDto>();

            using var reader = new StreamReader(file.OpenReadStream());
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim
            });

            try
            {
                csv.Read();
                csv.ReadHeader();

                var headerRecord = csv.HeaderRecord;
                if (headerRecord == null || headerRecord.Length == 0)
                {
                    errors.Add("CSV file has no headers");
                    return members;
                }

                int rowNumber = 1;
                while (csv.Read())
                {
                    rowNumber++;
                    try
                    {
                        var member = new MemberDetailsDto
                        {
                            StaffNo = csv.GetField("STAFF NO")?.Trim(),
                            EmployeeNames = csv.GetField("EMPLOYEENAMES")?.Trim(),
                            Sex = csv.GetField("SEX")?.Trim(),
                            DateOfBirth = ParseDate(csv.GetField("DOB")),
                            BasicSalary = ParseDecimal(csv.GetField("BASIC SALARY")),
                            HousingAllowance = ParseDecimal(csv.GetField("HOUSING ALLOWANCE")),
                            TransportAllowance = ParseDecimal(csv.GetField("TRANSPORT ALLOWANCE")),
                            PermanentDisabilityRate = ParseDecimal(csv.GetField("PERM. DISABILITY RATE (%)")),
                            TemporaryDisabilityRate = ParseDecimal(csv.GetField("TEMPORARY DISABILITY RATE (%)")),
                            MedicalExpensesRate = ParseDecimal(csv.GetField("MEDICAL EXPENSES RATE (%)")),
                            FuneralExtensionRate = ParseDecimal(csv.GetField("FUNERAL EXTENTION RATE (%)")),
                            CriticalIllnessRate = ParseDecimal(csv.GetField("CRITICAL ILLNESS RATE (%)")),
                            HireDate = ParseDate(csv.GetField("HIRE DATE"))
                        };

                        members.Add(member);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error processing row {rowNumber}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error reading CSV file: {ex.Message}");
            }

            return members;
        }
        public class FileImportResponse
        {
            public bool Success { get; set; }
            public int TotalRecords { get; set; }
            public int ValidRecords { get; set; }
            public List<MemberDetailsDto> Members { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public DateTime ProcessedAt { get; set; }
        }

        public class ValidationResponse
        {
            public int TotalRecords { get; set; }
            public int ValidRecords { get; set; }
            public int InvalidRecords { get; set; }
            public List<MemberValidationResult> ValidationResults { get; set; } = new();
            public List<string> FileErrors { get; set; } = new();
            public DateTime ValidatedAt { get; set; }
        }

        public class MemberValidationResult
        {
            public int RowNumber { get; set; }
            public string StaffNo { get; set; }
            public string EmployeeName { get; set; }
            public bool IsValid { get; set; }
            public List<string> ValidationErrors { get; set; } = new();
        }
        private async Task<List<MemberDetailsDto>> ProcessExcelFile(IFormFile file, List<string> errors)
        {
            var members = new List<MemberDetailsDto>();

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);

                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    errors.Add("Excel file contains no worksheets");
                    return members;
                }

                var headerRow = worksheet.Row(1);
                var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

                if (lastColumn == 0)
                {
                    errors.Add("Excel file has no data");
                    return members;
                }

                // Create header mapping
                var headers = new Dictionary<string, int>();
                for (int col = 1; col <= lastColumn; col++)
                {
                    var headerValue = headerRow.Cell(col).GetString().Trim().ToUpperInvariant();
                    headers[headerValue] = col;
                }

                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

                for (int row = 2; row <= lastRow; row++)
                {
                    try
                    {
                        var member = new MemberDetailsDto
                        {
                            StaffNo = GetCellValue(worksheet, row, headers, "STAFF NO"),
                            EmployeeNames = GetCellValue(worksheet, row, headers, "EMPLOYEENAMES"),
                            Sex = GetCellValue(worksheet, row, headers, "SEX"),
                            DateOfBirth = ParseDate(GetCellValue(worksheet, row, headers, "DOB")),
                            BasicSalary = ParseDecimal(GetCellValue(worksheet, row, headers, "BASIC SALARY")),
                            HousingAllowance = ParseDecimal(GetCellValue(worksheet, row, headers, "HOUSING ALLOWANCE")),
                            TransportAllowance = ParseDecimal(GetCellValue(worksheet, row, headers, "TRANSPORT ALLOWANCE")),
                            PermanentDisabilityRate = ParseDecimal(GetCellValue(worksheet, row, headers, "PERM. DISABILITY RATE (%)")),
                            TemporaryDisabilityRate = ParseDecimal(GetCellValue(worksheet, row, headers, "TEMPORARY DISABILITY RATE (%)")),
                            MedicalExpensesRate = ParseDecimal(GetCellValue(worksheet, row, headers, "MEDICAL EXPENSES RATE (%)")),
                            FuneralExtensionRate = ParseDecimal(GetCellValue(worksheet, row, headers, "FUNERAL EXTENTION RATE (%)")),
                            CriticalIllnessRate = ParseDecimal(GetCellValue(worksheet, row, headers, "CRITICAL ILLNESS RATE (%)")),
                            HireDate = ParseDate(GetCellValue(worksheet, row, headers, "HIRE DATE"))
                        };

                        members.Add(member);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error processing row {row}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error reading Excel file: {ex.Message}");
            }

            return members;
        }

        private string GetCellValue(IXLWorksheet worksheet, int row, Dictionary<string, int> headers, string headerName)
        {
            if (!headers.ContainsKey(headerName.ToUpperInvariant()))
                return string.Empty;

            var col = headers[headerName.ToUpperInvariant()];
            return worksheet.Cell(row, col).GetString().Trim();
        }

        private DateTime ParseDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return DateTime.MinValue;

            if (DateTime.TryParse(dateString, out DateTime result))
                return result;

            return DateTime.MinValue;
        }

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            if (decimal.TryParse(value, out decimal result))
                return result;

            return 0;
        }

        private List<string> ValidateMember(MemberDetailsDto member)
        {
            var errors = new List<string>();
            var context = new ValidationContext(member);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(member, context, results, true))
            {
                errors.AddRange(results.Select(r => r.ErrorMessage));
            }

            // Additional custom validations
            if (member.DateOfBirth > DateTime.Today)
                errors.Add("Date of birth cannot be in the future");

            if (member.HireDate > DateTime.Today)
                errors.Add("Hire date cannot be in the future");

            if (!string.IsNullOrEmpty(member.Sex) && !new[] { "M", "F", "MALE", "FEMALE" }.Contains(member.Sex.ToUpperInvariant()))
                errors.Add("Sex must be M, F, Male, or Female");

            return errors;
        }

        private string GenerateCsvTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("NO,STAFF NO,EMPLOYEENAMES,SEX,DOB,ANB,BASIC SALARY,HOUSING ALLOWANCE,TRANSPORT ALLOWANCE,PERM. DISABILITY RATE (%),TEMPORARY DISABILITY RATE (%),TOTAL EMOLUMENT,MEDICAL EXPENSES RATE (%),FUNERAL EXTENTION RATE (%),CRITICAL ILLNESS RATE (%),DATE OF BIRTH,HIRE DATE");
            sb.AppendLine("1,EMP001,\"John Doe\",M,1994-01-01,30,300000,100000,50000,2.5,1.5,450000,1.0,0.5,2.0,1994-01-01,2020-01-01");
            sb.AppendLine("2,EMP002,\"Jane Smith\",F,1999-05-15,25,280000,90000,40000,2.5,1.5,410000,1.0,0.5,2.0,1999-05-15,2021-03-01");
            return sb.ToString();
        }

        private byte[] GenerateExcelTemplate()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Member Details");

            // Headers
            var headers = new[]
            {
                "NO", "STAFF NO", "EMPLOYEENAMES", "SEX", "DOB", "ANB", "BASIC SALARY",
                "HOUSING ALLOWANCE", "TRANSPORT ALLOWANCE", "PERM. DISABILITY RATE (%)",
                "TEMPORARY DISABILITY RATE (%)", "TOTAL EMOLUMENT", "MEDICAL EXPENSES RATE (%)",
                "FUNERAL EXTENTION RATE (%)", "CRITICAL ILLNESS RATE (%)", "DATE OF BIRTH", "HIRE DATE"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            // Sample data
            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 2).Value = "EMP001";
            worksheet.Cell(2, 3).Value = "John Doe";
            worksheet.Cell(2, 4).Value = "M";
            worksheet.Cell(2, 5).Value = new DateTime(1994, 1, 1);
            worksheet.Cell(2, 6).Value = 30;
            worksheet.Cell(2, 7).Value = 300000;
            worksheet.Cell(2, 8).Value = 100000;
            worksheet.Cell(2, 9).Value = 50000;
            worksheet.Cell(2, 10).Value = 2.5;
            worksheet.Cell(2, 11).Value = 1.5;
            worksheet.Cell(2, 12).Value = 450000;
            worksheet.Cell(2, 13).Value = 1.0;
            worksheet.Cell(2, 14).Value = 0.5;
            worksheet.Cell(2, 15).Value = 2.0;
            worksheet.Cell(2, 16).Value = new DateTime(1994, 1, 1);
            worksheet.Cell(2, 17).Value = new DateTime(2020, 1, 1);

            worksheet.Cell(3, 1).Value = 2;
            worksheet.Cell(3, 2).Value = "EMP002";
            worksheet.Cell(3, 3).Value = "Jane Smith";
            worksheet.Cell(3, 4).Value = "F";
            worksheet.Cell(3, 5).Value = new DateTime(1999, 5, 15);
            worksheet.Cell(3, 6).Value = 25;
            worksheet.Cell(3, 7).Value = 280000;
            worksheet.Cell(3, 8).Value = 90000;
            worksheet.Cell(3, 9).Value = 40000;
            worksheet.Cell(3, 10).Value = 2.5;
            worksheet.Cell(3, 11).Value = 1.5;
            worksheet.Cell(3, 12).Value = 410000;
            worksheet.Cell(3, 13).Value = 1.0;
            worksheet.Cell(3, 14).Value = 0.5;
            worksheet.Cell(3, 15).Value = 2.0;
            worksheet.Cell(3, 16).Value = new DateTime(1999, 5, 15);
            worksheet.Cell(3, 17).Value = new DateTime(2021, 3, 1);

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Format date columns
            worksheet.Column(5).Style.NumberFormat.Format = "yyyy-mm-dd";
            worksheet.Column(16).Style.NumberFormat.Format = "yyyy-mm-dd";
            worksheet.Column(17).Style.NumberFormat.Format = "yyyy-mm-dd";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    
    ///// <summary>
    ///// Import members from CSV file
    ///// </summary>
    //[HttpPost("csv")]
    //[RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    //public async Task<IActionResult> ImportFromCsv([FromForm] IFormFile file)
    //{
    //    try
    //    {
    //        if (file == null || file.Length == 0)
    //        {
    //            return BadRequest(new { Success = false, Message = "No file uploaded or file is empty" });
    //        }

    //        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
    //        {
    //            return BadRequest(new { Success = false, Message = "File must be a CSV file (.csv extension)" });
    //        }

    //        if (file.Length > 10 * 1024 * 1024) // 10MB limit
    //        {
    //            return BadRequest(new { Success = false, Message = "File size cannot exceed 10MB" });
    //        }

    //        var members = await _repository.ImportMembersFromCsvAsync(file);

    //        if (members == null || !members.Any())
    //        {
    //            return BadRequest(new
    //            {
    //                Success = false,
    //                Message = "No valid member data found in the CSV file. Please check the file format and ensure required fields are populated."
    //            });
    //        }

    //        return Ok(new
    //        {
    //            Success = true,
    //            Data = members,
    //            Count = members.Count,
    //            Message = $"Successfully imported {members.Count} members from CSV file",
    //            FileInfo = new
    //            {
    //                FileName = file.FileName,
    //                FileSize = file.Length,
    //                ContentType = file.ContentType
    //            }
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        return StatusCode(500, new
    //        {
    //            Success = false,
    //            Message = $"Error processing CSV file: {ex.Message}",
    //            Details = ex.InnerException?.Message
    //        });
    //    }
    //}

    ///// <summary>
    ///// Import members from Excel file
    ///// </summary>
    //[HttpPost("excel")]
    //[RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    //public async Task<IActionResult> ImportFromExcel([FromForm] IFormFile file)
    //{
    //    try
    //    {
    //        if (file == null || file.Length == 0)
    //        {
    //            return BadRequest(new { Success = false, Message = "No file uploaded or file is empty" });
    //        }

    //        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
    //            !file.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
    //        {
    //            return BadRequest(new { Success = false, Message = "File must be an Excel file (.xlsx or .xls extension)" });
    //        }

    //        if (file.Length > 10 * 1024 * 1024) // 10MB limit
    //        {
    //            return BadRequest(new { Success = false, Message = "File size cannot exceed 10MB" });
    //        }

    //        var members = await _repository.ImportMembersFromExcelAsync(file);

    //        if (members == null || !members.Any())
    //        {
    //            return BadRequest(new
    //            {
    //                Success = false,
    //                Message = "No valid member data found in the Excel file. Please check the file format and ensure required fields are populated."
    //            });
    //        }

    //        return Ok(new
    //        {
    //            Success = true,
    //            Data = members,
    //            Count = members.Count,
    //            Message = $"Successfully imported {members.Count} members from Excel file",
    //            FileInfo = new
    //            {
    //                FileName = file.FileName,
    //                FileSize = file.Length,
    //                ContentType = file.ContentType
    //            }
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        return StatusCode(500, new
    //        {
    //            Success = false,
    //            Message = $"Error processing Excel file: {ex.Message}",
    //            Details = ex.InnerException?.Message
    //        });
    //    }
    //}

    ///// <summary>
    ///// Download CSV template
    ///// </summary>
    //[HttpGet("template/csv")]
    //public IActionResult DownloadCsvTemplate()
    //{
    //    var csv = "NO,STAFF NO,EMPLOYEENAMES,SEX,DOB,ANB,BASIC SALARY,HOUSING ALLOWANCE,TRANSPORT ALLOWANCE,PERM. DISABILITY RATE (%),TEMPORARY DISABILITY RATE (%),TOTAL EMOLUMENT,MEDICAL EXPENSES RATE (%),FUNERAL EXTENTION RATE (%),CRITICAL ILLNESS RATE (%),DATE OF BIRTH,HIRE DATE\n";
    //    csv += "1,EMP001,\"John Doe\",M,1994-01-01,30,300000,100000,50000,2.5,1.5,450000,1.0,0.5,2.0,1994-01-01,2020-01-01\n";
    //    csv += "2,EMP002,\"Jane Smith\",F,1999-05-15,25,280000,90000,40000,2.5,1.5,410000,1.0,0.5,2.0,1999-05-15,2021-03-01";

    //    var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
    //    return File(bytes, "text/csv", "group_life_import_template.csv");
    //}

    ///// <summary>
    ///// Download Excel template
    ///// </summary>
    //[HttpGet("template/excel")]
    //public IActionResult DownloadExcelTemplate()
    //{
    //    try
    //    {
    //        using var package = new ExcelPackage();
    //        var worksheet = package.Workbook.Worksheets.Add("Group Life Members");

    //        // Add headers
    //        var headers = new[]
    //        {
    //            "NO", "STAFF NO", "EMPLOYEENAMES", "SEX", "DOB", "ANB",
    //            "BASIC SALARY", "HOUSING ALLOWANCE", "TRANSPORT ALLOWANCE",
    //            "PERM. DISABILITY RATE (%)", "TEMPORARY DISABILITY RATE (%)",
    //            "TOTAL EMOLUMENT", "MEDICAL EXPENSES RATE (%)",
    //            "FUNERAL EXTENTION RATE (%)", "CRITICAL ILLNESS RATE (%)",
    //            "DATE OF BIRTH", "HIRE DATE"
    //        };

    //        for (int i = 0; i < headers.Length; i++)
    //        {
    //            worksheet.Cells[1, i + 1].Value = headers[i];
    //            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
    //            worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
    //            worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
    //        }

    //        // Add sample data
    //        worksheet.Cells[2, 1].Value = 1;
    //        worksheet.Cells[2, 2].Value = "EMP001";
    //        worksheet.Cells[2, 3].Value = "John Doe";
    //        worksheet.Cells[2, 4].Value = "M";
    //        worksheet.Cells[2, 5].Value = new DateTime(1994, 1, 1);
    //        worksheet.Cells[2, 5].Style.Numberformat.Format = "yyyy-mm-dd";
    //        worksheet.Cells[2, 6].Value = 30;
    //        worksheet.Cells[2, 7].Value = 300000;
    //        worksheet.Cells[2, 8].Value = 100000;
    //        worksheet.Cells[2, 9].Value = 50000;
    //        worksheet.Cells[2, 10].Value = 2.5;
    //        worksheet.Cells[2, 11].Value = 1.5;
    //        worksheet.Cells[2, 12].Value = 450000;
    //        worksheet.Cells[2, 13].Value = 1.0;
    //        worksheet.Cells[2, 14].Value = 0.5;
    //        worksheet.Cells[2, 15].Value = 2.0;
    //        worksheet.Cells[2, 16].Value = new DateTime(1994, 1, 1);
    //        worksheet.Cells[2, 16].Style.Numberformat.Format = "yyyy-mm-dd";
    //        worksheet.Cells[2, 17].Value = new DateTime(2020, 1, 1);
    //        worksheet.Cells[2, 17].Style.Numberformat.Format = "yyyy-mm-dd";

    //        // Add second sample row
    //        worksheet.Cells[3, 1].Value = 2;
    //        worksheet.Cells[3, 2].Value = "EMP002";
    //        worksheet.Cells[3, 3].Value = "Jane Smith";
    //        worksheet.Cells[3, 4].Value = "F";
    //        worksheet.Cells[3, 5].Value = new DateTime(1999, 5, 15);
    //        worksheet.Cells[3, 5].Style.Numberformat.Format = "yyyy-mm-dd";
    //        worksheet.Cells[3, 6].Value = 25;
    //        worksheet.Cells[3, 7].Value = 280000;
    //        worksheet.Cells[3, 8].Value = 90000;
    //        worksheet.Cells[3, 9].Value = 40000;
    //        worksheet.Cells[3, 10].Value = 2.5;
    //        worksheet.Cells[3, 11].Value = 1.5;
    //        worksheet.Cells[3, 12].Value = 410000;
    //        worksheet.Cells[3, 13].Value = 1.0;
    //        worksheet.Cells[3, 14].Value = 0.5;
    //        worksheet.Cells[3, 15].Value = 2.0;
    //        worksheet.Cells[3, 16].Value = new DateTime(1999, 5, 15);
    //        worksheet.Cells[3, 16].Style.Numberformat.Format = "yyyy-mm-dd";
    //        worksheet.Cells[3, 17].Value = new DateTime(2021, 3, 1);
    //        worksheet.Cells[3, 17].Style.Numberformat.Format = "yyyy-mm-dd";

    //        // Auto-fit columns
    //        worksheet.Cells.AutoFitColumns();

    //        // Add data validation for Sex column
    //        var sexValidation = worksheet.DataValidations.AddListValidation("D2:D1000");
    //        sexValidation.Formula.Values.Add("M");
    //        sexValidation.Formula.Values.Add("F");
    //        sexValidation.ShowErrorMessage = true;
    //        sexValidation.ErrorTitle = "Invalid Sex";
    //        sexValidation.Error = "Please select M or F";

    //        // Add instructions in a separate worksheet
    //        var instructionsSheet = package.Workbook.Worksheets.Add("Instructions");
    //        instructionsSheet.Cells[1, 1].Value = "GROUP LIFE QUOTATION - MEMBER IMPORT INSTRUCTIONS";
    //        instructionsSheet.Cells[1, 1].Style.Font.Bold = true;
    //        instructionsSheet.Cells[1, 1].Style.Font.Size = 16;

    //        instructionsSheet.Cells[3, 1].Value = "Column Descriptions:";
    //        instructionsSheet.Cells[3, 1].Style.Font.Bold = true;

    //        var instructions = new[]
    //        {
    //            "NO: Sequential number (optional)",
    //            "STAFF NO: Employee staff number/ID",
    //            "EMPLOYEENAMES: Full name of employee",
    //            "SEX: M for Male, F for Female",
    //            "DOB: Date of Birth (YYYY-MM-DD format)",
    //            "ANB: Age Next Birthday (calculated automatically)",
    //            "BASIC SALARY: Basic salary amount",
    //            "HOUSING ALLOWANCE: Housing allowance amount",
    //            "TRANSPORT ALLOWANCE: Transport allowance amount",
    //            "PERM. DISABILITY RATE (%): Permanent disability rate percentage",
    //            "TEMPORARY DISABILITY RATE (%): Temporary disability rate percentage",
    //            "TOTAL EMOLUMENT: Total emolument (calculated automatically)",
    //            "MEDICAL EXPENSES RATE (%): Medical expenses rate percentage",
    //            "FUNERAL EXTENTION RATE (%): Funeral extension rate percentage",
    //            "CRITICAL ILLNESS RATE (%): Critical illness rate percentage",
    //            "DATE OF BIRTH: Same as DOB (for compatibility)",
    //            "HIRE DATE: Date employee was hired"
    //        };

    //        for (int i = 0; i < instructions.Length; i++)
    //        {
    //            instructionsSheet.Cells[i + 5, 1].Value = instructions[i];
    //        }

    //        instructionsSheet.Cells.AutoFitColumns();

    //        var stream = new MemoryStream();
    //        package.SaveAs(stream);
    //        stream.Position = 0;

    //        return File(stream.ToArray(),
    //            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    //            "group_life_import_template.xlsx");
    //    }
    //    catch (Exception ex)
    //    {
    //        return StatusCode(500, new { Success = false, Message = $"Error creating Excel template: {ex.Message}" });
    //    }
    //}

    ///// <summary>
    ///// Create a new Group Life quote + members. Computes premiums and saves to DB.
    ///// </summary>
    //[HttpPost("creategrouplife")]
    //public async Task<IActionResult> Create([FromBody] CreateQuoteRequest request, [FromQuery] string approvedBy = "system", CancellationToken ct = default)
    //{
    //    var qm = await _repository.CreateQuoteAsync(request, approvedBy, ct);
    //    return CreatedAtAction(nameof(Get), new { id = qm.QuoteID }, qm);
    //}

    ///// <summary>
    ///// Get quote master by id.
    ///// </summary>
    //[HttpGet("grouplife/{id}")]
    //public async Task<IActionResult> Get([FromRoute] string id, CancellationToken ct = default)
    //{
    //    var qm = await _repository.GetQuoteAsync(id, ct);
    //    return qm is null ? NotFound() : Ok(qm);
    //}

    ///// <summary>
    ///// Get members under a quote.
    ///// </summary>
    //[HttpGet("members/{id}")]
    //public async Task<IActionResult> GetMembers([FromRoute] string id, CancellationToken ct = default)
    //{
    //    var members = await _repository.GetQuoteMembersAsync(id, ct);
    //    return Ok(members);
    //}

    ///// <summary>
    ///// Recompute all member premiums under a quote. You may pass optional overrides (e.g., GroupFactor, FixedRate).
    ///// </summary>
    //[HttpPost("recompute/{id}")]
    //public async Task<IActionResult> Recompute([FromRoute] string id, [FromBody] CreateQuoteRequest? overrides, [FromQuery] string approvedBy = "system", CancellationToken ct = default)
    //{
    //    var qm = await _repository.RecomputeAsync(id, overrides, approvedBy, ct);
    //    return qm is null ? NotFound() : Ok(qm);
    //}
}
}
