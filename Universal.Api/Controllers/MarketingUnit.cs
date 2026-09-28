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
    public class MktGroup
    {
        public int MktGroupID { get; set; }
        public int BranchID { get; set; }
        public string GroupName { get; set; }
        public string Description { get; set; }
        public string GroupHead { get; set; }
        public bool Deleted { get; set; }
        public bool Active { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    // DTO used for create/update so callers can't set audit/system fields directly
    public class MktGroupDto
    {
        public int BranchID { get; set; }
        public string GroupName { get; set; }
        public string Description { get; set; }
        public string GroupHead { get; set; }
        public bool Active { get; set; } = true;
    }

    // ===================== CONTROLLER =====================
    [ApiController]
    [Route("api/[controller]")]
    public class MarketingUnitController : ControllerBase
    {
        private readonly string _connectionString;

        public MarketingUnitController(IConfiguration configuration)
        {
            // Adjust the connection string key to match your appsettings.json
            _connectionString = configuration["AppSettings:SqldbConnString"];
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        // ================= GET ALL =================
        // GET: api/mktgroups
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MktGroup>>> GetAll()
        {
            const string sql = @"
                SELECT [MktGroupID], [BranchID], [GroupName], [Description], [GroupHead],
                       [Deleted], [Active], [SubmittedBy], [SubmittedOn], [ModifiedBy], [ModifiedOn]
                FROM [dbo].[MktGroups]
                WHERE [Deleted] = 0";

            using var connection = Connection;
            var mktGroups = await connection.QueryAsync<MktGroup>(sql);
            return Ok(mktGroups);
        }

        // ================= GET BY ID =================
        // GET: api/mktgroups/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<MktGroup>> GetById(int id)
        {
            const string sql = @"
                SELECT [MktGroupID], [BranchID], [GroupName], [Description], [GroupHead],
                       [Deleted], [Active], [SubmittedBy], [SubmittedOn], [ModifiedBy], [ModifiedOn]
                FROM [dbo].[MktGroups]
                WHERE [MktGroupID] = @MktGroupID AND [Deleted] = 0";

            using var connection = Connection;
            var mktGroup = await connection.QueryFirstOrDefaultAsync<MktGroup>(sql, new { MktGroupID = id });

            if (mktGroup == null)
                return NotFound(new { message = $"MktGroup with ID {id} not found." });

            return Ok(mktGroup);
        }

        // ================= CREATE =================
        // POST: api/mktgroups
        [HttpPost]
        public async Task<ActionResult<MktGroup>> Create([FromBody] MktGroupDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // NOTE: MktGroupID is assumed NOT to be an IDENTITY column, same as
            // Regions.RegionID in this database. If you confirm MktGroupID IS
            // identity (check with the COLUMNPROPERTY query below), swap this
            // block for the simpler OUTPUT INSERTED.MktGroupID version further down.
            //
            //   SELECT COLUMNPROPERTY(OBJECT_ID('dbo.MktGroups'), 'MktGroupID', 'IsIdentity');
            //
            const string sql = @"
                INSERT INTO [dbo].[MktGroups]
                    ([MktGroupID], [BranchID], [GroupName], [Description], [GroupHead],
                     [Deleted], [Active], [SubmittedBy], [SubmittedOn])
                VALUES
                    ((SELECT ISNULL(MAX(MktGroupID), 0) + 1 FROM [dbo].[MktGroups] WITH (UPDLOCK, HOLDLOCK)),
                     @BranchID, @GroupName, @Description, @GroupHead,
                     0, @Active, @SubmittedBy, @SubmittedOn);
                SELECT MAX(MktGroupID) FROM [dbo].[MktGroups];";

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
                    dto.BranchID,
                    dto.GroupName,
                    dto.Description,
                    dto.GroupHead,
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

            /* ---- If MktGroupID turns out to be a real IDENTITY column, use this instead: ----
            const string sql = @"
                INSERT INTO [dbo].[MktGroups]
                    ([BranchID], [GroupName], [Description], [GroupHead],
                     [Deleted], [Active], [SubmittedBy], [SubmittedOn])
                OUTPUT INSERTED.MktGroupID
                VALUES
                    (@BranchID, @GroupName, @Description, @GroupHead,
                     0, @Active, @SubmittedBy, @SubmittedOn)";

            var submittedBy = User?.Identity?.Name ?? "system";
            using var connection = Connection;
            var newId = await connection.ExecuteScalarAsync<int>(sql, new { ... });
            ------------------------------------------------------------------------------------- */

            return CreatedAtAction(nameof(GetById), new { id = newId }, new { MktGroupID = newId });
        }

        // ================= UPDATE =================
        // PUT: api/mktgroups/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] MktGroupDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            const string sql = @"
                UPDATE [dbo].[MktGroups]
                SET [BranchID] = @BranchID,
                    [GroupName] = @GroupName,
                    [Description] = @Description,
                    [GroupHead] = @GroupHead,
                    [Active] = @Active,
                    [ModifiedBy] = @ModifiedBy,
                    [ModifiedOn] = @ModifiedOn
                WHERE [MktGroupID] = @MktGroupID AND [Deleted] = 0";

            var modifiedBy = User?.Identity?.Name ?? "system";

            using var connection = Connection;
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                MktGroupID = id,
                dto.BranchID,
                dto.GroupName,
                dto.Description,
                dto.GroupHead,
                dto.Active,
                ModifiedBy = modifiedBy,
                ModifiedOn = DateTime.Now
            });

            if (rowsAffected == 0)
                return NotFound(new { message = $"MktGroup with ID {id} not found." });

            return NoContent();
        }

        // ================= DELETE (soft delete) =================
        // DELETE: api/mktgroups/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            const string sql = @"
                UPDATE [dbo].[MktGroups]
                SET [Deleted] = 1,
                    [Active] = 0,
                    [ModifiedBy] = @ModifiedBy,
                    [ModifiedOn] = @ModifiedOn
                WHERE [MktGroupID] = @MktGroupID AND [Deleted] = 0";

            var modifiedBy = User?.Identity?.Name ?? "system";

            using var connection = Connection;
            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                MktGroupID = id,
                ModifiedBy = modifiedBy,
                ModifiedOn = DateTime.Now
            });

            if (rowsAffected == 0)
                return NotFound(new { message = $"MktGroup with ID {id} not found." });

            return NoContent();
        }
    }
}