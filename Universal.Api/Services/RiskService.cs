
using Dapper;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services
{
    public class RiskService
    {

        private readonly string _connectionString;
        public RiskService(IConfiguration configuration)
        {
            _connectionString = configuration["AppSettings:SqldbConnString"];
        }

        public async Task<PagedResultDto<RiskListDto>> ListAsync(
         RiskQueryDto query,
         CancellationToken ct = default)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var where = string.Empty;
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                where = @"WHERE Risk LIKE @Search
               OR Description LIKE @Search
               OR RiskID LIKE @Search";

                parameters.Add("@Search", $"%{query.Search}%");
            }

            parameters.Add("@Offset", (query.PageNumber - 1) * query.PageSize);
            parameters.Add("@PageSize", query.PageSize);

            var sql = $@"
SELECT
    RiskID,
    Risk AS RiskName,
    Description
FROM Risks
{where}
ORDER BY Risk
OFFSET @Offset ROWS
FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(*)
FROM Risks
{where};";

            using var multi = await conn.QueryMultipleAsync(sql, parameters);

            var items = (await multi.ReadAsync<RiskListDto>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return new PagedResultDto<RiskListDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

    }
}
