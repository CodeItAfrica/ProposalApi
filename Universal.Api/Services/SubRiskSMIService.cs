using Dapper;
using Gibs.Domain.Entities.others;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services
{
    public class SubRiskSMIService
    {
        
        private readonly string _connectionString;
        public SubRiskSMIService(IConfiguration configuration)
        {
            _connectionString = configuration["AppSettings:SqldbConnString"];
        }


        public async Task<IEnumerable<SubRiskSMIDto>> ListAsync(SubRiskSMIQueryDto query, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            const string sql = @"
SELECT *
FROM SubRiskSMIs
WHERE
(
    @Search IS NULL
    OR SMICode LIKE '%' + @Search + '%'
    OR SectionCode LIKE '%' + @Search + '%'
    OR SubRiskID LIKE '%' + @Search + '%'
    OR SMIDetails LIKE '%' + @Search + '%'
    OR SectionName LIKE '%' + @Search + '%'
    OR SubRiskName LIKE '%' + @Search + '%'
)
ORDER BY SMICode";

            return await conn.QueryAsync<SubRiskSMIDto>(sql, new
            {
                Search = string.IsNullOrWhiteSpace(query.Search)
                    ? null
                    : query.Search
            });
        }

        public async Task<SubRiskSMIDto?> GetAsync(long sid, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            return await conn.QueryFirstOrDefaultAsync<SubRiskSMIDto>(
                @"SELECT *
                  FROM SubRiskSMIs
                  WHERE SID=@SID",
                new { SID = sid });
        }

        public async Task<bool> InsertAsync(SubRiskSMIDto dto, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"
INSERT INTO SubRiskSMIs
(
SMICode,
SectionCode,
SubRiskID,
SMIDetails,
SectionName,
SubRiskName,
Field1,
Field2,
Rates,
AddSI,
stockItem,
Multiplier,
A1,
A2,
A3,
A4,
A5,
Active,
SubmittedBy,
SubmittedOn,
ModifiedBy,
ModifiedOn
)
VALUES
(
@SMICode,
@SectionCode,
@SubRiskID,
@SMIDetails,
@SectionName,
@SubRiskName,
@Field1,
@Field2,
@Rates,
@AddSI,
@stockItem,
@Multiplier,
@A1,
@A2,
@A3,
@A4,
@A5,
@Active,
@SubmittedBy,
@SubmittedOn,
@ModifiedBy,
@ModifiedOn
)";

            return await conn.ExecuteAsync(sql, dto) > 0;
        }

        public async Task<bool> UpdateAsync(SubRiskSMIDto dto, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"
UPDATE SubRiskSMIs
SET
SMICode=@SMICode,
SectionCode=@SectionCode,
SubRiskID=@SubRiskID,
SMIDetails=@SMIDetails,
SectionName=@SectionName,
SubRiskName=@SubRiskName,
Field1=@Field1,
Field2=@Field2,
Rates=@Rates,
AddSI=@AddSI,
stockItem=@stockItem,
Multiplier=@Multiplier,
A1=@A1,
A2=@A2,
A3=@A3,
A4=@A4,
A5=@A5,
Active=@Active,
SubmittedBy=@SubmittedBy,
SubmittedOn=@SubmittedOn,
ModifiedBy=@ModifiedBy,
ModifiedOn=@ModifiedOn
WHERE SID=@SID";

            return await conn.ExecuteAsync(sql, dto) > 0;
        }

        public async Task<bool> DeleteAsync(long sid, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            return await conn.ExecuteAsync(
                @"DELETE FROM SubRiskSMIs
                  WHERE SID=@SID",
                new { SID = sid }) > 0;
        }
    }
}