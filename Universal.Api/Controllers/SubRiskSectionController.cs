using Gibs.Domain.Entities;
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
    public class SubRiskSectionsController : ControllerBase
    {
        private readonly DataContext _context;

        public SubRiskSectionsController(DataContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all SubRiskSections
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubRiskSectionDto>>> GetAll()
        {
            try
            {
                var sections = await _context.SubRiskSections
                    .OrderBy(s => s.SectionCode)
                    .ToListAsync();

                var sectionsDto = sections.Select(s => new SubRiskSectionDto
                {
                    SectionID = s.SectionID,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
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

                return Ok(sectionsDto);
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
        /// Get SubRiskSection by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SubRiskSectionDto>> GetById(long id)
        {
            try
            {
                var section = await _context.SubRiskSections.FindAsync(id);

                if (section == null)
                    return NotFound($"SubRiskSection with ID {id} not found.");

                var sectionDto = new SubRiskSectionDto
                {
                    SectionID = section.SectionID,
                    SectionCode = section.SectionCode,
                    SubRiskID = section.SubRiskID,
                    SectionName = section.SectionName,
                    SubRiskName = section.SubRiskName,
                    Field1 = section.Field1,
                    Field2 = section.Field2,
                    Rates = section.Rates,
                    A1 = section.A1,
                    A2 = section.A2,
                    A3 = section.A3,
                    A4 = section.A4,
                    A5 = section.A5,
                    Active = section.Active,
                    SubmittedBy = section.SubmittedBy,
                    SubmittedOn = section.SubmittedOn,
                    ModifiedBy = section.ModifiedBy,
                    ModifiedOn = section.ModifiedOn
                };

                return Ok(sectionDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Get SubRiskSections by SubRiskID
        /// </summary>
        [HttpGet("by-subrisk/{subRiskId}")]
        public async Task<ActionResult<IEnumerable<SubRiskSectionDto>>> GetBySubRiskId(string subRiskId)
        {
            try
            {
                var sections = await _context.SubRiskSections
                    .Where(s => s.SubRiskID == subRiskId)
                    .OrderBy(s => s.SectionCode)
                    .ToListAsync();

                var sectionsDto = sections.Select(s => new SubRiskSectionDto
                {
                    SectionID = s.SectionID,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
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

                return Ok(sectionsDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }




        /// <summary>
        /// Get RiskSections by RiskID
        /// </summary>
        [HttpGet("by-risk/{RiskId}")]
        public async Task<ActionResult<IEnumerable<RiskSectionDto>>> GetSectionsByRiskId(string RiskId)
        {
            try
            {
                var sections = await _context.SubRiskSections
                    .Where(s => s.Field1 == RiskId)
                    .OrderBy(s => s.SectionCode)
                    .ToListAsync();

                var sectionsDto = sections.Select(s => new RiskSectionDto
                {
                    SectionID = s.SectionID,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
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

                return Ok(sectionsDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }



        /// <summary>
        /// Get only active SubRiskSections
        /// </summary>
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<SubRiskSectionDto>>> GetActive()
        {
            try
            {
                var sections = await _context.SubRiskSections
                    .Where(s => s.Active == 1)
                    .OrderBy(s => s.SectionCode)
                    .ToListAsync();

                var sectionsDto = sections.Select(s => new SubRiskSectionDto
                {
                    SectionID = s.SectionID,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
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

                return Ok(sectionsDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Create a new SubRiskSection
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SubRiskSectionDto>> Create([FromBody] CreateSubRiskSectionDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var section = new SubRiskSection
                {
                    SectionCode = dto.SectionCode,
                    SubRiskID = dto.SubRiskID,
                    SectionName = dto.SectionName,
                    SubRiskName = dto.SubRiskName,
                    Field1 = dto.Field1,
                    Field2 = dto.Field2,
                    Rates = dto.Rates,
                    A1 = dto.A1,
                    A2 = dto.A2,
                    A3 = dto.A3,
                    A4 = dto.A4,
                    A5 = dto.A5,
                    Active = 1,
                    SubmittedBy = dto.SubmittedBy,
                    SubmittedOn = DateTime.UtcNow
                };

                _context.SubRiskSections.Add(section);
                await _context.SaveChangesAsync();

                var sectionDto = new SubRiskSectionDto
                {
                    SectionID = section.SectionID,
                    SectionCode = section.SectionCode,
                    SubRiskID = section.SubRiskID,
                    SectionName = section.SectionName,
                    SubRiskName = section.SubRiskName,
                    Field1 = section.Field1,
                    Field2 = section.Field2,
                    Rates = section.Rates,
                    A1 = section.A1,
                    A2 = section.A2,
                    A3 = section.A3,
                    A4 = section.A4,
                    A5 = section.A5,
                    Active = section.Active,
                    SubmittedBy = section.SubmittedBy,
                    SubmittedOn = section.SubmittedOn,
                    ModifiedBy = section.ModifiedBy,
                    ModifiedOn = section.ModifiedOn
                };

                return CreatedAtAction(nameof(GetById), new { id = section.SectionID }, sectionDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Update an existing SubRiskSection
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<SubRiskSectionDto>> Update(long id, [FromBody] UpdateSubRiskSectionDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var section = await _context.SubRiskSections.FindAsync(id);
                if (section == null)
                    return NotFound($"SubRiskSection with ID {id} not found.");

                // Update only provided fields (null-coalescing for partial updates)
                section.SectionCode = dto.SectionCode ?? section.SectionCode;
                section.SubRiskID = dto.SubRiskID ?? section.SubRiskID;
                section.SectionName = dto.SectionName ?? section.SectionName;
                section.SubRiskName = dto.SubRiskName ?? section.SubRiskName;
                section.Field1 = dto.Field1 ?? section.Field1;
                section.Field2 = dto.Field2 ?? section.Field2;
                section.Rates = dto.Rates ?? section.Rates;
                section.A1 = dto.A1 ?? section.A1;
                section.A2 = dto.A2 ?? section.A2;
                section.A3 = dto.A3 ?? section.A3;
                section.A4 = dto.A4 ?? section.A4;
                section.A5 = dto.A5 ?? section.A5;
                section.Active = dto.Active;
                section.ModifiedBy = dto.ModifiedBy;
                section.ModifiedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var sectionDto = new SubRiskSectionDto
                {
                    SectionID = section.SectionID,
                    SectionCode = section.SectionCode,
                    SubRiskID = section.SubRiskID,
                    SectionName = section.SectionName,
                    SubRiskName = section.SubRiskName,
                    Field1 = section.Field1,
                    Field2 = section.Field2,
                    Rates = section.Rates,
                    A1 = section.A1,
                    A2 = section.A2,
                    A3 = section.A3,
                    A4 = section.A4,
                    A5 = section.A5,
                    Active = section.Active,
                    SubmittedBy = section.SubmittedBy,
                    SubmittedOn = section.SubmittedOn,
                    ModifiedBy = section.ModifiedBy,
                    ModifiedOn = section.ModifiedOn
                };

                return Ok(sectionDto);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Delete a SubRiskSection
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            try
            {
                var section = await _context.SubRiskSections.FindAsync(id);
                if (section == null)
                    return NotFound($"SubRiskSection with ID {id} not found.");

                _context.SubRiskSections.Remove(section);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Check if SubRiskSection exists
        /// </summary>
        [HttpGet("{id}/exists")]
        public async Task<ActionResult<bool>> Exists(long id)
        {
            try
            {
                var exists = await _context.SubRiskSections.AnyAsync(s => s.SectionID == id);
                return Ok(exists);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Get SubRiskSections by SectionCode
        /// </summary>
        [HttpGet("by-section-code/{sectionCode}")]
        public async Task<ActionResult<IEnumerable<SubRiskSectionDto>>> GetBySectionCode(string sectionCode)
        {
            try
            {
                var sections = await _context.SubRiskSections
                    .Where(s => s.SectionCode == sectionCode)
                    .OrderBy(s => s.SectionName)
                    .ToListAsync();

                var sectionsDto = sections.Select(s => new SubRiskSectionDto
                {
                    SectionID = s.SectionID,
                    SectionCode = s.SectionCode,
                    SubRiskID = s.SubRiskID,
                    SectionName = s.SectionName,
                    SubRiskName = s.SubRiskName,
                    Field1 = s.Field1,
                    Field2 = s.Field2,
                    Rates = s.Rates,
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

                return Ok(sectionsDto);
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
        public async Task<ActionResult> BulkUpdateActive([FromBody] BulkUpdateActiveDto dto)
        {
            try
            {
                var sections = await _context.SubRiskSections
                    .Where(s => dto.SectionIDs.Contains(s.SectionID))
                    .ToListAsync();

                if (!sections.Any())
                    return NotFound("No sections found with the provided IDs.");

                foreach (var section in sections)
                {
                    section.Active = dto.Active;
                    section.ModifiedBy = dto.ModifiedBy;
                    section.ModifiedOn = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok($"Updated {sections.Count} sections.");
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
    }

    // Additional DTO for bulk operations
    public class BulkUpdateActiveDto
    {
        public List<long> SectionIDs { get; set; } = new List<long>();
        public byte Active { get; set; }
        public string? ModifiedBy { get; set; }
    }
}

