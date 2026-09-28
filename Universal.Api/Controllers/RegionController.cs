using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration; // use System.Data.SqlClient if that's what your project uses

namespace Gibs.Cornerstone.Api.Controllers
{
    // ===================== MODEL =====================
    public class Regions
    {
        public int RegionID { get; set; }
        public string Region { get; set; }
        public string Manager { get; set; }
        public string Address { get; set; }
        public string MobilePhone { get; set; }
        public string LandPhone { get; set; }
        public string Email { get; set; }
        public string Fax { get; set; }
        public string Remarks { get; set; }
        public bool Deleted { get; set; }
        public bool Active { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string Modifiedby { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    // DTO used for create/update so callers can't set audit/system fields directly
    public class RegionDto
    {
        public string Region { get; set; }
        public string Manager { get; set; }
        public string Address { get; set; }
        public string MobilePhone { get; set; }
        public string LandPhone { get; set; }
        public string Email { get; set; }
        public string Fax { get; set; }
        public string Remarks { get; set; }
        public bool Active { get; set; } = true;
    }

    // ===================== CONTROLLER =====================
    [ApiController]
    [Route("api/[controller]")]
    public class RegionsController : ControllerBase
    {
        private readonly string _connectionString;

        public RegionsController(IConfiguration configuration)
        {
            // Adjust the connection string key to match your appsettings.json
            _connectionString = configuration["AppSettings:SqldbConnString"];

        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        // ================= GET ALL =================
        // GET: api/regions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Regions>>> GetAll()
        {
            const string sql = @"
                SELECT [RegionID], [Region], [Manager], [Address], [MobilePhone],
                       [LandPhone], [Email], [Fax], [Remarks], [Deleted], [Active],
                       [SubmittedBy], [SubmittedOn], [Modifiedby], [ModifiedOn]
                FROM [dbo].[Regions]
                WHERE [Deleted] = 0";

            using var connection = Connection;
            var regions = await connection.QueryAsync<Regions>(sql);
            return Ok(regions);
        }

        // ================= GET BY ID =================
        // GET: api/regions/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Regions>> GetById(int id)
        {
            const string sql = @"
                SELECT [RegionID], [Region], [Manager], [Address], [MobilePhone],
                       [LandPhone], [Email], [Fax], [Remarks], [Deleted], [Active],
                       [SubmittedBy], [SubmittedOn], [Modifiedby], [ModifiedOn]
                FROM [dbo].[Regions]
                WHERE [RegionID] = @RegionID AND [Deleted] = 0";

            using var connection = Connection;
            var region = await connection.QueryFirstOrDefaultAsync<Regions>(sql, new { RegionID = id });

            if (region == null)
                return NotFound(new { message = $"Region with ID {id} not found." });

            return Ok(region);
        }

        // ================= CREATE =================
        // POST: api/regions
        [HttpPost]
        public async Task<ActionResult<Regions>> Create([FromBody] RegionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // NOTE: this manual next-ID approach is only needed because RegionID is
            // NOT an IDENTITY column in the DB right now (that's what caused the
            // PK violation on value 0). Once RegionID is fixed to IDENTITY(1,1),
            // switch to the OUTPUT INSERTED.RegionID version shown further below.
            const string sql = @"
                INSERT INTO [dbo].[Regions]
                    ([RegionID], [Region], [Manager], [Address], [MobilePhone], [LandPhone],
                     [Email], [Fax], [Remarks], [Deleted], [Active],
                     [SubmittedBy], [SubmittedOn])
                VALUES
                    ((SELECT ISNULL(MAX(RegionID), 0) + 1 FROM [dbo].[Regions] WITH (UPDLOCK, HOLDLOCK)),
                     @Region, @Manager, @Address, @MobilePhone, @LandPhone,
                     @Email, @Fax, @Remarks, 0, @Active,
                     @SubmittedBy, @SubmittedOn);
                SELECT MAX(RegionID) FROM [dbo].[Regions];";

            // Replace with the actual logged-in user (e.g. from claims) instead of "system"
            var submittedBy = User?.Identity?.Name ?? "system";

            using var connection = Connection;
            connection.Open();
            using var transaction = connection.BeginTransaction();
            int newId;
            try
            {
                newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
                {
                    dto.Region,
                    dto.Manager,
                    dto.Address,
                    dto.MobilePhone,
                    dto.LandPhone,
                    dto.Email,
                    dto.Fax,
                    dto.Remarks,
                    dto.Active,
                    SubmittedBy = submittedBy,
                    SubmittedOn = DateTime.Now
                }, transaction));
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            /* ---- Once RegionID is a real IDENTITY column, replace the block above with: ----
            const string sql = @"
                INSERT INTO [dbo].[Regions]
                    ([Region], [Manager], [Address], [MobilePhone], [LandPhone],
                     [Email], [Fax], [Remarks], [Deleted], [Active],
                     [SubmittedBy], [SubmittedOn])
                OUTPUT INSERTED.RegionID
                VALUES
                    (@Region, @Manager, @Address, @MobilePhone, @LandPhone,
                     @Email, @Fax, @Remarks, 0, @Active,
                     @SubmittedBy, @SubmittedOn)";

            var submittedBy = User?.Identity?.Name ?? "system";
            using var connection = Connection;
            var newId = await connection.ExecuteScalarAsync<int>(sql, new { ... });
            ---------------------------------------------------------------------------------- */

            return CreatedAtAction(nameof(GetById), new { id = newId }, new { RegionID = newId });
        }

        // ================= UPDATE =================
        // PUT: api/regions/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RegionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            const string sql = @"
                UPDATE [dbo].[Regions]
                SET [Region] = @Region,
                    [Manager] = @Manager,
                    [Address] = @Address,
                    [MobilePhone] = @MobilePhone,
                    [LandPhone] = @LandPhone,
                    [Email] = @Email,
                    [Fax] = @Fax,
                    [Remarks] = @Remarks,
                    [Active] = @Active,
                    [Modifiedby] = @Modifiedby,
                    [ModifiedOn] = @ModifiedOn
                WHERE [RegionID] = @RegionID AND [Deleted] = 0";

            var modifiedBy = User?.Identity?.Name ?? "system";

            using var connection = Connection;
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                RegionID = id,
                dto.Region,
                dto.Manager,
                dto.Address,
                dto.MobilePhone,
                dto.LandPhone,
                dto.Email,
                dto.Fax,
                dto.Remarks,
                dto.Active,
                Modifiedby = modifiedBy,
                ModifiedOn = DateTime.Now
            });

            if (rowsAffected == 0)
                return NotFound(new { message = $"Region with ID {id} not found." });

            return NoContent();
        }

        // ================= DELETE (soft delete) =================
        // DELETE: api/regions/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            const string sql = @"
                UPDATE [dbo].[Regions]
                SET [Deleted] = 1,
                    [Active] = 0,
                    [Modifiedby] = @Modifiedby,
                    [ModifiedOn] = @ModifiedOn
                WHERE [RegionID] = @RegionID AND [Deleted] = 0";

            var modifiedBy = User?.Identity?.Name ?? "system";

            using var connection = Connection;
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                RegionID = id,
                Modifiedby = modifiedBy,
                ModifiedOn = DateTime.Now
            });

            if (rowsAffected == 0)
                return NotFound(new { message = $"Region with ID {id} not found." });

            return NoContent();
        }
    }
}