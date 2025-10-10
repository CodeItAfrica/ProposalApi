using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly DataContext _db;
        public RolesController(DataContext db) => _db = db;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoleRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var exists = await _db.Roles.AnyAsync(r => r.RoleName == req.RoleName);
            if (exists) return Conflict("Role already exists.");

            var role = new Role
            {
                RoleName = req.RoleName,
                RoleDescription = req.RoleDescription,
                SubmittedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow
            };

            _db.Roles.Add(role);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = role.RoleID }, role);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _db.Roles.AsNoTracking().ToListAsync();
            return Ok(roles);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var role = await _db.Roles
                .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleID == id);

            if (role == null) return NotFound();

            return Ok(new
            {
                role.RoleID,
                role.RoleName,
                role.RoleDescription,
                Permissions = role.RolePermissions.Select(rp => new
                {
                    rp.PermissionID,
                    rp.Permission.PermissionName
                })
            });
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateRoleRequest req)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleID == id);
            if (role == null) return NotFound();

            if (req.RoleName != null) role.RoleName = req.RoleName;
            if (req.RoleDescription != null) role.RoleDescription = req.RoleDescription;
            role.ModifiedOn = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleID == id);
            if (role == null) return NotFound();

            // optionally: prevent delete if users linked
            _db.Roles.Remove(role);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ASSIGN PERMISSION to ROLE
        [HttpPost("assign-permission")]
        public async Task<IActionResult> AssignPermission([FromBody] AssignPermissionToRoleRequest req)
        {
            var role = await _db.Roles.Include(r => r.RolePermissions)
                                      .FirstOrDefaultAsync(r => r.RoleID == req.RoleId);
            if (role == null) return NotFound("Role not found.");

            var permExists = await _db.Permissions.AnyAsync(p => p.PermissionID == req.PermissionId);
            if (!permExists) return NotFound("Permission not found.");

            if (role.RolePermissions.Any(rp => rp.PermissionID == req.PermissionId))
                return Conflict("Permission already assigned to role.");

            _db.RolePermissions.Add(new RolePermission
            {
                RoleID = req.RoleId,
                PermissionID = req.PermissionId,
                SubmittedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok("Permission assigned to role.");
        }

        // REMOVE PERMISSION from ROLE
        [HttpPost("remove-permission")]
        public async Task<IActionResult> RemovePermission([FromBody] RemovePermissionFromRoleRequest req)
        {
            var link = await _db.RolePermissions.FirstOrDefaultAsync(rp =>
                rp.RoleID == req.RoleId && rp.PermissionID == req.PermissionId);

            if (link == null) return NotFound("Assignment not found.");

            _db.RolePermissions.Remove(link);
            await _db.SaveChangesAsync();
            return Ok("Permission removed from role.");
        }
    }
}
