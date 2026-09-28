using Gibs.Domain.Entities;
using Gibs.Domain.Entities.others;
using Gibs.Infrastructure;
using GibsLifesMicroWebApi.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gibs.Cornerstone.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubRiskSMIsController : ControllerBase
    {
        private readonly DataContext _context;

        public SubRiskSMIsController(DataContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all SubRiskSMIs
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubRiskSMIDto>>> GetAll()
        {
            try
            {
                var smis = await _context.SubRiskSMIs
                    .OrderBy(s => s.SMICode)
                    .ToListAsync();

                var smisDto = smis.Select(s => new SubRiskSMIDto
                {
                    SID = s.SID,
                    SMICode = s.SMICode,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SMIDetails = s.SMIDetails,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
                    AddSI = s.AddSI,
                    StockItem = s.StockItem,
                    Multiplier = s.Multiplier,
                    A1 = s.A1,
                    A2 = s.A2,
                    A3 = s.A3,
                    A4 = s.A4,
                    A5 = s.A5,
                    Active = s.Active,
                    SubmittedBy = s.SubmittedBy,
                    SubmittedOn = s.SubmittedOn,
                    ModifiedBy = s.ModifiedBy,
                    ModifiedOn = s.ModifiedOn
                }).ToList();

                return Ok(smisDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        protected ActionResult ExceptionResult(Exception ex)
        {
            if (ex is null)
                return StatusCode(500, "SecureControllerBase.ExceptionResult() ex parameter cannot be null");

            if (ex is ArgumentException || ex is ArgumentNullException || ex is KeyNotFoundException)
                return BadRequest(ex.Message);

            if (ex.InnerException != null)
                return StatusCode(500, ex.Message + "\n\n\n --- inner exception --- " + ex.InnerException.ToString());

            return StatusCode(500, ex.Message);
        }
        /// <summary>
        /// Get SubRiskSMI by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SubRiskSMIDto>> GetById(long id)
        {
            try
            {
                var smi = await _context.SubRiskSMIs.FindAsync(id);

                if (smi == null)
                    return NotFound($"SubRiskSMI with ID {id} not found.");

                var smiDto = new SubRiskSMIDto
                {
                    SID = smi.SID,
                    SMICode = smi.SMICode,
                    SectionCode = smi.SectionCode,
                    SubRiskID = smi.SubRiskID,
                    SMIDetails = smi.SMIDetails,
                    SectionName = smi.SectionName,
                    SubRiskName = smi.SubRiskName,
                    Field1 = smi.Field1,
                    Field2 = smi.Field2,
                    Rates = smi.Rates,
                    AddSI = smi.AddSI,
                    StockItem = smi.StockItem,
                    Multiplier = smi.Multiplier,
                    A1 = smi.A1,
                    A2 = smi.A2,
                    A3 = smi.A3,
                    A4 = smi.A4,
                    A5 = smi.A5,
                    Active = smi.Active,
                    SubmittedBy = smi.SubmittedBy,
                    SubmittedOn = smi.SubmittedOn,
                    ModifiedBy = smi.ModifiedBy,
                    ModifiedOn = smi.ModifiedOn
                };

                return Ok(smiDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Get SubRiskSMIs by SubRiskID
        /// </summary>
        [HttpGet("by-subrisk/{subRiskId}")]
        public async Task<ActionResult<IEnumerable<SubRiskSMIDto>>> GetBySubRiskId(string subRiskId)
        {
            try
            {
                var smis = await _context.SubRiskSMIs
                    .Where(s => s.SubRiskID == subRiskId)
                    .OrderBy(s => s.SMICode)
                    .ToListAsync();

                var smisDto = smis.Select(s => new SubRiskSMIDto
                {
                    SID = s.SID,
                    SMICode = s.SMICode,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SMIDetails = s.SMIDetails,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
                    AddSI = s.AddSI,
                    StockItem = s.StockItem,
                    Multiplier = s.Multiplier,
                    A1 = s.A1,
                    A2 = s.A2,
                    A3 = s.A3,
                    A4 = s.A4,
                    A5 = s.A5,
                    Active = s.Active,
                    SubmittedBy = s.SubmittedBy,
                    SubmittedOn = s.SubmittedOn,
                    ModifiedBy = s.ModifiedBy,
                    ModifiedOn = s.ModifiedOn
                }).ToList();

                return Ok(smisDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        /// <summary>
        /// Get SubRiskSMIs by SectionCode
        /// </summary>
        [HttpGet("by-section/{sectionCode}")]
        public async Task<ActionResult<IEnumerable<SubRiskSMIDto>>> GetBySectionCode(string sectionCode,
        [FromQuery] string RiskID)
        {
            try
            {
                var smis = await _context.SubRiskSMIs
                    .Where(s => s.SectionCode == sectionCode && s.Field1 == RiskID)
                    .OrderBy(s => s.SMICode)
                    .ToListAsync();

                var smisDto = smis.Select(s => new SubRiskSMIDto
                {
                    SID = s.SID,
                    SMICode = s.SMICode,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SMIDetails = s.SMIDetails,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
                    AddSI = s.AddSI,
                    StockItem = s.StockItem,
                    Multiplier = s.Multiplier,
                    A1 = s.A1,
                    A2 = s.A2,
                    A3 = s.A3,
                    A4 = s.A4,
                    A5 = s.A5,
                    Active = s.Active,
                    SubmittedBy = s.SubmittedBy,
                    SubmittedOn = s.SubmittedOn,
                    ModifiedBy = s.ModifiedBy,
                    ModifiedOn = s.ModifiedOn
                }).ToList();

                return Ok(smisDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Get only active SubRiskSMIs
        /// </summary>
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<SubRiskSMIDto>>> GetActive()
        {
            try
            {
                var smis = await _context.SubRiskSMIs
                    .Where(s => s.Active == 1)
                    .OrderBy(s => s.SMICode)
                    .ToListAsync();

                var smisDto = smis.Select(s => new SubRiskSMIDto
                {
                    SID = s.SID,
                    SMICode = s.SMICode,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SMIDetails = s.SMIDetails,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
                    AddSI = s.AddSI,
                    StockItem = s.StockItem,
                    Multiplier = s.Multiplier,
                    A1 = s.A1,
                    A2 = s.A2,
                    A3 = s.A3,
                    A4 = s.A4,
                    A5 = s.A5,
                    Active = s.Active,
                    SubmittedBy = s.SubmittedBy,
                    SubmittedOn = s.SubmittedOn,
                    ModifiedBy = s.ModifiedBy,
                    ModifiedOn = s.ModifiedOn
                }).ToList();

                return Ok(smisDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Get SubRiskSMIs by SMICode
        /// </summary>
        [HttpGet("by-smi-code/{smiCode}")]
        public async Task<ActionResult<IEnumerable<SubRiskSMIDto>>> GetBySMICode(string smiCode)
        {
            try
            {
                var smis = await _context.SubRiskSMIs
                    .Where(s => s.SMICode == smiCode)
                    .OrderBy(s => s.SectionCode)
                    .ToListAsync();

                var smisDto = smis.Select(s => new SubRiskSMIDto
                {
                    SID = s.SID,
                    SMICode = s.SMICode,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SMIDetails = s.SMIDetails,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
                    AddSI = s.AddSI,
                    StockItem = s.StockItem,
                    Multiplier = s.Multiplier,
                    A1 = s.A1,
                    A2 = s.A2,
                    A3 = s.A3,
                    A4 = s.A4,
                    A5 = s.A5,
                    Active = s.Active,
                    SubmittedBy = s.SubmittedBy,
                    SubmittedOn = s.SubmittedOn,
                    ModifiedBy = s.ModifiedBy,
                    ModifiedOn = s.ModifiedOn
                }).ToList();

                return Ok(smisDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Get SubRiskSMIs with specific flags
        /// </summary>
        [HttpGet("with-flags")]
        public async Task<ActionResult<IEnumerable<SubRiskSMIDto>>> GetWithFlags([FromQuery] byte? addSI = null, [FromQuery] byte? stockItem = null, [FromQuery] byte? multiplier = null)
        {
            try
            {
                var query = _context.SubRiskSMIs.AsQueryable();

                if (addSI.HasValue)
                    query = query.Where(s => s.AddSI == addSI.Value);

                if (stockItem.HasValue)
                    query = query.Where(s => s.StockItem == stockItem.Value);

                if (multiplier.HasValue)
                    query = query.Where(s => s.Multiplier == multiplier.Value);

                var smis = await query.OrderBy(s => s.SMICode).ToListAsync();

                var smisDto = smis.Select(s => new SubRiskSMIDto
                {
                    SID = s.SID,
                    SMICode = s.SMICode,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SMIDetails = s.SMIDetails,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
                    AddSI = s.AddSI,
                    StockItem = s.StockItem,
                    Multiplier = s.Multiplier,
                    A1 = s.A1,
                    A2 = s.A2,
                    A3 = s.A3,
                    A4 = s.A4,
                    A5 = s.A5,
                    Active = s.Active,
                    SubmittedBy = s.SubmittedBy,
                    SubmittedOn = s.SubmittedOn,
                    ModifiedBy = s.ModifiedBy,
                    ModifiedOn = s.ModifiedOn
                }).ToList();

                return Ok(smisDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Create a new SubRiskSMI
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SubRiskSMIDto>> Create([FromBody] CreateSubRiskSMIDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var smi = new SubRiskSMI
                {
                    SMICode = dto.SMICode,
                    SectionCode = dto.SectionCode,
                    SubRiskID = dto.SubRiskID,
                    SMIDetails = dto.SMIDetails,
                    SectionName = dto.SectionName,
                    SubRiskName = dto.SubRiskName,
                    Field1 = dto.Field1,
                    Field2 = dto.Field2,
                    Rates = dto.Rates,
                    AddSI = dto.AddSI,
                    StockItem = dto.StockItem,
                    Multiplier = dto.Multiplier,
                    A1 = dto.A1,
                    A2 = dto.A2,
                    A3 = dto.A3,
                    A4 = dto.A4,
                    A5 = dto.A5,
                    Active = dto.Active,
                    SubmittedBy = dto.SubmittedBy,
                    SubmittedOn = DateTime.UtcNow
                };

                _context.SubRiskSMIs.Add(smi);
                await _context.SaveChangesAsync();

                var smiDto = new SubRiskSMIDto
                {
                    SID = smi.SID,
                    SMICode = smi.SMICode,
                    SectionCode = smi.SectionCode,
                    SubRiskID = smi.SubRiskID,
                    SMIDetails = smi.SMIDetails,
                    SectionName = smi.SectionName,
                    SubRiskName = smi.SubRiskName,
                    Field1 = smi.Field1,
                    Field2 = smi.Field2,
                    Rates = smi.Rates,
                    AddSI = smi.AddSI,
                    StockItem = smi.StockItem,
                    Multiplier = smi.Multiplier,
                    A1 = smi.A1,
                    A2 = smi.A2,
                    A3 = smi.A3,
                    A4 = smi.A4,
                    A5 = smi.A5,
                    Active = smi.Active,
                    SubmittedBy = smi.SubmittedBy,
                    SubmittedOn = smi.SubmittedOn,
                    ModifiedBy = smi.ModifiedBy,
                    ModifiedOn = smi.ModifiedOn
                };

                return CreatedAtAction(nameof(GetById), new { id = smi.SID }, smiDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Update an existing SubRiskSMI
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<SubRiskSMIDto>> Update(long id, [FromBody] UpdateSubRiskSMIDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var smi = await _context.SubRiskSMIs.FindAsync(id);
                if (smi == null)
                    return NotFound($"SubRiskSMI with ID {id} not found.");

                // Update only provided fields (null-coalescing for partial updates)
                smi.SMICode = dto.SMICode ?? smi.SMICode;
                smi.SectionCode = dto.SectionCode ?? smi.SectionCode;
                smi.SubRiskID = dto.SubRiskID ?? smi.SubRiskID;
                smi.SMIDetails = dto.SMIDetails ?? smi.SMIDetails;
                smi.SectionName = dto.SectionName ?? smi.SectionName;
                smi.SubRiskName = dto.SubRiskName ?? smi.SubRiskName;
                smi.Field1 = dto.Field1 ?? smi.Field1;
                smi.Field2 = dto.Field2 ?? smi.Field2;
                smi.Rates = dto.Rates ?? smi.Rates;
                smi.AddSI = dto.AddSI ?? smi.AddSI;
                smi.StockItem = dto.StockItem ?? smi.StockItem;
                smi.Multiplier = dto.Multiplier ?? smi.Multiplier;
                smi.A1 = dto.A1 ?? smi.A1;
                smi.A2 = dto.A2 ?? smi.A2;
                smi.A3 = dto.A3 ?? smi.A3;
                smi.A4 = dto.A4 ?? smi.A4;
                smi.A5 = dto.A5 ?? smi.A5;
                smi.Active = dto.Active;
                smi.ModifiedBy = dto.ModifiedBy;
                smi.ModifiedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var smiDto = new SubRiskSMIDto
                {
                    SID = smi.SID,
                    SMICode = smi.SMICode,
                    SectionCode = smi.SectionCode,
                    SubRiskID = smi.SubRiskID,
                    SMIDetails = smi.SMIDetails,
                    SectionName = smi.SectionName,
                    SubRiskName = smi.SubRiskName,
                    Field1 = smi.Field1,
                    Field2 = smi.Field2,
                    Rates = smi.Rates,
                    AddSI = smi.AddSI,
                    StockItem = smi.StockItem,
                    Multiplier = smi.Multiplier,
                    A1 = smi.A1,
                    A2 = smi.A2,
                    A3 = smi.A3,
                    A4 = smi.A4,
                    A5 = smi.A5,
                    Active = smi.Active,
                    SubmittedBy = smi.SubmittedBy,
                    SubmittedOn = smi.SubmittedOn,
                    ModifiedBy = smi.ModifiedBy,
                    ModifiedOn = smi.ModifiedOn
                };

                return Ok(smiDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Delete a SubRiskSMI
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            try
            {
                var smi = await _context.SubRiskSMIs.FindAsync(id);
                if (smi == null)
                    return NotFound($"SubRiskSMI with ID {id} not found.");

                _context.SubRiskSMIs.Remove(smi);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Check if SubRiskSMI exists
        /// </summary>
        [HttpGet("{id}/exists")]
        public async Task<ActionResult<bool>> Exists(long id)
        {
            try
            {
                var exists = await _context.SubRiskSMIs.AnyAsync(s => s.SID == id);
                return Ok(exists);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Bulk update Active status
        /// </summary>
        [HttpPatch("bulk-active")]
        public async Task<ActionResult> BulkUpdateActive([FromBody] BulkUpdateActiveSMIDto dto)
        {
            try
            {
                var smis = await _context.SubRiskSMIs
                    .Where(s => dto.SIDs.Contains(s.SID))
                    .ToListAsync();

                if (!smis.Any())
                    return NotFound("No SMIs found with the provided IDs.");

                foreach (var smi in smis)
                {
                    smi.Active = dto.Active;
                    smi.ModifiedBy = dto.ModifiedBy;
                    smi.ModifiedOn = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok($"Updated {smis.Count} SMIs.");
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Bulk update flags (AddSI, StockItem, Multiplier)
        /// </summary>
        [HttpPatch("bulk-flags")]
        public async Task<ActionResult> BulkUpdateFlags([FromBody] BulkUpdateFlagsDto dto)
        {
            try
            {
                var smis = await _context.SubRiskSMIs
                    .Where(s => dto.SIDs.Contains(s.SID))
                    .ToListAsync();

                if (!smis.Any())
                    return NotFound("No SMIs found with the provided IDs.");

                foreach (var smi in smis)
                {
                    if (dto.AddSI.HasValue)
                        smi.AddSI = dto.AddSI;

                    if (dto.StockItem.HasValue)
                        smi.StockItem = dto.StockItem;

                    if (dto.Multiplier.HasValue)
                        smi.Multiplier = dto.Multiplier;

                    smi.ModifiedBy = dto.ModifiedBy;
                    smi.ModifiedOn = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok($"Updated flags for {smis.Count} SMIs.");
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
    }

    // Additional DTOs for bulk operations
    public class BulkUpdateActiveSMIDto
    {
        public List<long> SIDs { get; set; } = new List<long>();
        public byte Active { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class BulkUpdateFlagsDto
    {
        public List<long> SIDs { get; set; } = new List<long>();
        public byte? AddSI { get; set; }
        public byte? StockItem { get; set; }
        public byte? Multiplier { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
