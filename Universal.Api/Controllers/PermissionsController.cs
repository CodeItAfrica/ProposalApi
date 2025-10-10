using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [ApiController]
    [Route("api/permissions")]
    public class PermissionsController : ControllerBase
    {
        private readonly DataContext _db;
        public PermissionsController(DataContext db) => _db = db;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePermissionRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var newId = _db.Permissions.Max(p => (int?)p.PermissionID) ?? 0;

            var exists = await _db.Permissions.AnyAsync(p => p.PermissionName == req.PermissionName);
            if (exists) return Conflict("Permission already exists.");

            var perm = new Permission
            {
                PermissionID = newId + 1,
                PermissionName = req.PermissionName,
                Module = req.Module,
                Category = req.Category,
                Remarks = req.Remarks
            };

            _db.Permissions.Add(perm);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = perm.PermissionID }, perm);
        }


        [HttpPost("add")]
        public async Task<IActionResult> AddPermission(string permissionName)
        {
            var newId = _db.Permissions.Max(p => (int?)p.PermissionID) ?? 0;
            var permission = new Permission
            {
                PermissionID = newId + 1,
                PermissionName = permissionName
            };
            _db.Permissions.Add(permission);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Permission added", data = permission });
        }

        //public async Task<Permission> AddPermissionAsync(string permissionName)
        //{
        //    var permission = new Permission { PermissionName = permissionName };
        //    _db.Permissions.Add(permission);
        //    await _db.SaveChangesAsync();
        //    return permission;
        //}

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Permissions.AsNoTracking().OrderBy(p => p.PermissionName).ToListAsync();
            return Ok(list);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var perm = await _db.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.PermissionID == id);
            if (perm == null) return NotFound();
            return Ok(perm);
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdatePermissionRequest req)
        {
            var perm = await _db.Permissions.FirstOrDefaultAsync(p => p.PermissionID == id);
            if (perm == null) return NotFound();

            if (req.PermissionName != null) perm.PermissionName = req.PermissionName;
            if (req.Module != null) perm.Module = req.Module;
            if (req.Category != null) perm.Category = req.Category;
            if (req.Remarks != null) perm.Remarks = req.Remarks;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var perm = await _db.Permissions.FirstOrDefaultAsync(p => p.PermissionID == id);
            if (perm == null) return NotFound();

            // Optional: guard if linked to roles
            _db.Permissions.Remove(perm);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
