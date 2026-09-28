using Dapper;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubRisksController : ControllerBase
{
    private readonly string _connectionString;

    public SubRisksController(IConfiguration configuration)
    {
        _connectionString = configuration["AppSettings:ConnStrings:SqlDb"];
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    // GET: api/subrisks
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        const string sql = @"
            SELECT [SubRiskID], [RiskID], [SubRisk] AS SubRiskName, 
                   [Description], [InsuranceTypeID], [Deleted], [Active], 
                   [SubmittedBy], [SubmittedOn], [ModifiedBy], [ModifiedOn]
            FROM [dbo].[SubRisks]
            WHERE [Deleted] = 0
            ORDER BY [SubRiskID]";

        using var connection = CreateConnection();
        var subRisks = await connection.QueryAsync<SubRisk>(sql);

        return Ok(subRisks);
    }

    // GET: api/subrisks/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        const string sql = @"
            SELECT [SubRiskID], [RiskID], [SubRisk] AS SubRiskName, 
                   [Description], [InsuranceTypeID], [Deleted], [Active],
                   [SubmittedBy], [SubmittedOn], [ModifiedBy], [ModifiedOn]
            FROM [dbo].[SubRisks]
            WHERE [SubRiskID] = @SubRiskId AND [Deleted] = 0";

        using var connection = CreateConnection();
        var subRisk = await connection.QueryFirstOrDefaultAsync<SubRisk>(sql, new { SubRiskId = id });

        if (subRisk == null)
            return NotFound(new { message = $"SubRisk with ID {id} not found." });

        return Ok(subRisk);
    }

    // POST: api/subrisks
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SubRisk subRisk)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Since SubRiskID is varchar and NOT NULL with no IDENTITY, caller must supply it
        // (or generate one here — see note below)
        if (string.IsNullOrWhiteSpace(subRisk.SubRiskID))
            return BadRequest(new { message = "SubRiskID is required." });

        const string sql = @"
            INSERT INTO [dbo].[SubRisks]
                ([SubRiskID], [RiskID], [SubRisk],  [Description], [InsuranceTypeID],
                 [Deleted], [Active], [SubmittedBy], [SubmittedOn])
            VALUES
                (@SubRiskID, @RiskID, @SubRiskName,  @Description, @InsuranceTypeID,
                 0, @Active,  @SubmittedBy, GETDATE())";

        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@SubRiskID", subRisk.SubRiskID);
        parameters.Add("@RiskID", subRisk.RiskID);
        parameters.Add("@SubRiskName", subRisk.SubRiskName);
       
        parameters.Add("@Description", subRisk.Description);
        parameters.Add("@InsuranceTypeID", subRisk.InsuranceTypeID);
        parameters.Add("@Active", subRisk.Active);
        parameters.Add("@SubmittedBy", subRisk.SubmittedBy);

        await connection.ExecuteAsync(sql, parameters);

        return CreatedAtAction(nameof(GetById), new { id = subRisk.SubRiskID }, subRisk);
    }

    // PUT: api/subrisks/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SubRisk subRisk)
    {
        if (id != subRisk.SubRiskID)
            return BadRequest(new { message = "Route ID does not match payload SubRiskID." });

        using var connection = CreateConnection();

        const string checkSql = @"
            SELECT COUNT(1) FROM [dbo].[SubRisks]
            WHERE [SubRiskID] = @SubRiskId AND [Deleted] = 0";

        var exists = await connection.ExecuteScalarAsync<int>(checkSql, new { SubRiskId = id });
        if (exists == 0)
            return NotFound(new { message = $"SubRisk with ID {id} not found." });

        const string updateSql = @"
            UPDATE [dbo].[SubRisks]
            SET [RiskID] = @RiskID,
                [SubRisk] = @SubRiskName,
               
                [Description] = @Description,
                [InsuranceTypeID] = @InsuranceTypeID,
                [Active] = @Active,
                [ModifiedBy] = @ModifiedBy,
                [ModifiedOn] = GETDATE()
            WHERE [SubRiskID] = @SubRiskID AND [Deleted] = 0";

        var rowsAffected = await connection.ExecuteAsync(updateSql, subRisk);
        if (rowsAffected == 0)
            return StatusCode(500, new { message = "Update failed." });

        return NoContent();
    }

    // DELETE: api/subrisks/5?modifiedBy=bosun
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string modifiedBy)
    {
        using var connection = CreateConnection();

        const string checkSql = @"
            SELECT COUNT(1) FROM [dbo].[SubRisks]
            WHERE [SubRiskID] = @SubRiskId AND [Deleted] = 0";

        var exists = await connection.ExecuteScalarAsync<int>(checkSql, new { SubRiskId = id });
        if (exists == 0)
            return NotFound(new { message = $"SubRisk with ID {id} not found." });

        const string deleteSql = @"
            UPDATE [dbo].[SubRisks]
            SET [Deleted] = 1,
                [ModifiedBy] = @ModifiedBy,
                [ModifiedOn] = GETDATE()
            WHERE [SubRiskID] = @SubRiskId";

        var rowsAffected = await connection.ExecuteAsync(deleteSql, new { SubRiskId = id, ModifiedBy = modifiedBy });
        if (rowsAffected == 0)
            return StatusCode(500, new { message = "Delete failed." });

        return NoContent();
    }
}