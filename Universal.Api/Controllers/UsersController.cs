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
    public class UsersController : ControllerBase
    {
        private readonly DataContext _db;
        public UsersController(DataContext db) => _db = db;

        // CREATE (Register)
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterUserRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var exists = await _db.Users.AnyAsync(u => u.Username == req.Username);
            if (exists) return Conflict("Username already exists.");

            var user = new User
            {
                Username = req.Username,
                // In production: user.Password = BCrypt.Net.BCrypt.HashPassword(req.Password);
                Password = req.Password,
                FirstName = req.FirstName,
                LastName = req.LastName,
                Address = req.Address,
                Email = req.Email,
                Phone = req.Phone,
                Active = (byte)(req.Active ? 1 : 0),
                SubmittedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = user.UserID }, new { user.UserID, user.Username });
        }

        // READ list
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var query = _db.Users.AsNoTracking().OrderBy(u => u.UserID);
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new { total, items });
        }

        // READ by id
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserID == id);

            if (user == null) return NotFound();
            return Ok(new
            {
                user.UserID,
                user.Username,
                user.FirstName,
                user.LastName,
                user.Address,
                user.Email,
                user.Phone,
                Active = user.Active == 1,
                Roles = user.UserRoles.Select(ur => new { ur.RoleID, ur.Role.RoleName })
            });
        }

        // UPDATE profile
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateUserRequest req)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null) return NotFound();

            if (req.FirstName != null) user.FirstName = req.FirstName;
            if (req.LastName != null) user.LastName = req.LastName;
            if (req.Address != null) user.Address = req.Address;
            if (req.Email != null) user.Email = req.Email;
            if (req.Phone != null) user.Phone = req.Phone;
            if (req.Active.HasValue) user.Active = (byte)(req.Active.Value ? 1 : 0);

            user.ModifiedOn = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // CHANGE PASSWORD
        [HttpPut("{id:long}/change-password")]
        public async Task<IActionResult> ChangePassword(long id, [FromBody] ChangePasswordRequest req)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null) return NotFound();

            // Plain text comparison (matches your DB today). Replace with BCrypt Verify when you switch.
            if (user.Password != req.CurrentPassword)
                return BadRequest("Current password is incorrect.");

            user.Password = req.NewPassword; // In production: BCrypt.HashPassword(req.NewPassword)
            user.PwdExpiry = DateTime.UtcNow.AddDays(90);
            user.ModifiedOn = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null) return NotFound();

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ASSIGN ROLE to USER
        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleToUserRequest req)
        {
            var userExists = await _db.Users.AnyAsync(u => u.UserID == req.UserId);
            if (!userExists) return NotFound("User not found.");

            var roleExists = await _db.Roles.AnyAsync(r => r.RoleID == req.RoleId);
            if (!roleExists) return NotFound("Role not found.");

            var already = await _db.UserRoles.AnyAsync(ur => ur.UserID == req.UserId && ur.RoleID == req.RoleId);
            if (already) return Conflict("User already has this role.");

            _db.UserRoles.Add(new UserRole
            {
                UserID = req.UserId,
                RoleID = req.RoleId,
                SubmittedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok("Role assigned to user.");
        }

        // REMOVE ROLE from USER
        [HttpPost("remove-role")]
        public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleFromUserRequest req)
        {
            var link = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.UserID == req.UserId && ur.RoleID == req.RoleId);
            if (link == null) return NotFound("Assignment not found.");

            _db.UserRoles.Remove(link);
            await _db.SaveChangesAsync();
            return Ok("Role removed from user.");
        }
    }
}
