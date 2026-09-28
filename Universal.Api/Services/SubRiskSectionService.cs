using Dapper;
using Gibs.Domain.Entities;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services
{
    public class SubRiskSectionService
    {
        private readonly string _connectionString;
        public SubRiskSectionService(IConfiguration configuration)
        {
            _connectionString = configuration["AppSettings:SqldbConnString"];
        }


        public async Task<IEnumerable<SubRiskSectionDto>> ListAsync(SubRiskSectionQueryDto query, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            var sql = @"
SELECT *
FROM SubRiskSections
WHERE
(
    @Search IS NULL
    OR SectionCode LIKE '%' + @Search + '%'
    OR SectionName LIKE '%' + @Search + '%'
    OR SubRiskID LIKE '%' + @Search + '%'
    OR SubRiskName LIKE '%' + @Search + '%'
)
ORDER BY SectionName";

            return await conn.QueryAsync<SubRiskSectionDto>(sql, new
            {
                Search = string.IsNullOrWhiteSpace(query.Search)
                    ? null
                    : query.Search
            });
        }

        public async Task<SubRiskSectionDto?> GetAsync(long sectionId, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            return await conn.QueryFirstOrDefaultAsync<SubRiskSectionDto>(
                @"SELECT *
                  FROM SubRiskSections
                  WHERE SectionID=@SectionID",
                new { SectionID = sectionId });
        }

        public async Task<bool> InsertAsync(SubRiskSectionDto dto, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            const string sql = @"

INSERT INTO SubRiskSections
(
SectionCode,
SubRiskID,
SectionName,
SubRiskName,
SubmittedBy,
SubmittedOn,
ModifiedBy,
ModifiedOn
)
VALUES
(
@SectionCode,
@SubRiskID,
@SectionName,
@SubRiskName,
@SubmittedBy,
@SubmittedOn,
@ModifiedBy,
@ModifiedOn
)";

            return await conn.ExecuteAsync(sql, dto) > 0;
        }

        public async Task<bool> UpdateAsync(SubRiskSectionDto dto, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"

UPDATE SubRiskSections
SET
SectionCode=@SectionCode,
SubRiskID=@SubRiskID,
SectionName=@SectionName,
SubRiskName=@SubRiskName,
SubmittedBy=@SubmittedBy,
SubmittedOn=@SubmittedOn,
ModifiedBy=@ModifiedBy,
ModifiedOn=@ModifiedOn
WHERE SectionID=@SectionID";

            return await conn.ExecuteAsync(sql, dto) > 0;
        }

        public async Task<bool> DeleteAsync(long sectionId, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            return await conn.ExecuteAsync(
                @"DELETE FROM SubRiskSections
                  WHERE SectionID=@SectionID",
                new { SectionID = sectionId }) > 0;
        }
    }
}