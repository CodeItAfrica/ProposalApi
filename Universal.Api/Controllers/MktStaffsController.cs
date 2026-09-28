using Gibs.Domain.Entities;
using Gibs.Infrastructure;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gibs.Cornerstone.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MktStaffsController : ControllerBase
    {

        private readonly DataContext _context;

        public MktStaffsController(DataContext context)
        {
            _context = context;
        }

        // GET: api/MktStaffs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MktStaffDto>>> GetMktStaffs()
        {
            var staffs = await _context.MktStaffs.ToListAsync();
            var staffDtos = staffs.Select(s => MapToDto(s));
            return Ok(staffDtos);
        }

        // GET: api/MktStaffs/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<MktStaffDto>> GetMktStaff(string id)
        {
            var staff = await _context.MktStaffs.FindAsync(id);
            if (staff == null)
            {
                return NotFound();
            }
            return Ok(MapToDto(staff));
        }

        // POST: api/MktStaffs
        [HttpPost]
        public async Task<ActionResult<MktStaffDto>> CreateMktStaff(CreateMktStaffDto createDto)
        {
            var staff = new MktStaff
            {
                MktStaffID = createDto.MktStaffID,
                MktGrpID = createDto.MktGrpID,
                GroupName = createDto.GroupName,
                StaffName = createDto.StaffName,
                GroupHead = createDto.GroupHead,
                mktUnitID = createDto.mktUnitID,
                mktUnit = createDto.mktUnit,
                Budyear1 = createDto.Budyear1,
                Budyear2 = createDto.Budyear2,
                CurTarget = createDto.CurTarget,
                PrevTarget = createDto.PrevTarget,
                Deleted = createDto.Deleted,
                Active = createDto.Active,
                SubmittedBy = createDto.SubmittedBy,
                SubmittedOn = DateTime.Now,
                ModifiedBy = createDto.ModifiedBy,
                ModifiedOn = DateTime.Now
            };

            _context.MktStaffs.Add(staff);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMktStaff), new { id = staff.MktStaffID }, MapToDto(staff));
        }

        // PUT: api/MktStaffs/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMktStaff(string id, UpdateMktStaffDto updateDto)
        {
            var staff = await _context.MktStaffs.FindAsync(id);
            if (staff == null)
            {
                return NotFound();
            }

            staff.MktGrpID = updateDto.MktGrpID;
            staff.GroupName = updateDto.GroupName;
            staff.StaffName = updateDto.StaffName;
            staff.GroupHead = updateDto.GroupHead;
            staff.mktUnitID = updateDto.mktUnitID;
            staff.mktUnit = updateDto.mktUnit;
            staff.Budyear1 = updateDto.Budyear1;
            staff.Budyear2 = updateDto.Budyear2;
            staff.CurTarget = updateDto.CurTarget;
            staff.PrevTarget = updateDto.PrevTarget;
            staff.Deleted = updateDto.Deleted;
            staff.Active = updateDto.Active;
            staff.ModifiedBy = updateDto.ModifiedBy;
            staff.ModifiedOn = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MktStaffExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // DELETE: api/MktStaffs/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMktStaff(string id)
        {
            var staff = await _context.MktStaffs.FindAsync(id);
            if (staff == null)
            {
                return NotFound();
            }

            _context.MktStaffs.Remove(staff);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MktStaffExists(string id)
        {
            return _context.MktStaffs.Any(e => e.MktStaffID == id);
        }

        private static MktStaffDto MapToDto(MktStaff staff)
        {
            return new MktStaffDto
            {
                MktStaffID = staff.MktStaffID,
                MktGrpID = staff.MktGrpID,
                GroupName = staff.GroupName,
                StaffName = staff.StaffName,
                GroupHead = staff.GroupHead,
                mktUnitID = staff.mktUnitID,
                mktUnit = staff.mktUnit,
                Budyear1 = staff.Budyear1,
                Budyear2 = staff.Budyear2,
                CurTarget = staff.CurTarget,
                PrevTarget = staff.PrevTarget,
                Deleted = staff.Deleted,
                Active = staff.Active,
                SubmittedBy = staff.SubmittedBy,
                SubmittedOn = staff.SubmittedOn,
                ModifiedBy = staff.ModifiedBy,
                ModifiedOn = staff.ModifiedOn
            };
        }


    }
}
