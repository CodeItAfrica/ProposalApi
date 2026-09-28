using Dapper;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services
{
    public class ProductSettingService
    {
        private readonly string _connectionString;
        public ProductSettingService(IConfiguration configuration)
        {
            _connectionString = configuration["AppSettings:SqldbConnString"];
        }


        public async Task<IEnumerable<ProductSettingDto>> ListAsync(ProductSettingQueryDto query, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            var sql = @"
SELECT *
FROM ProductSettings
WHERE
(
    @Search IS NULL
    OR ProductID LIKE '%' + @Search + '%'
    OR Remarks LIKE '%' + @Search + '%'
    OR Tag LIKE '%' + @Search + '%'
)
ORDER BY ProductID";

            return await conn.QueryAsync<ProductSettingDto>(sql, new
            {
                Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search
            });
        }

        public async Task<ProductSettingDto?> GetAsync(string productId, CancellationToken ct)
        {

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            return await conn.QueryFirstOrDefaultAsync<ProductSettingDto>(
                @"SELECT *
                  FROM ProductSettings
                  WHERE ProductID=@ProductID",
                new { ProductID = productId });
        }

        public async Task<bool> InsertAsync(ProductSettingDto dto, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"
INSERT INTO ProductSettings
(
    ProductID,
    Min_Age,
    Max_Age,
    Min_Duration,
    Max_Duration,
    Min_Contribution,
    Max_Contribution,
    RateDiv_factor,
    Min_SA,
    Max_SA,
    Multiplier,
    A1,
    A2,
    Remarks,
    Tag
)
VALUES
(
    @ProductID,
    @Min_Age,
    @Max_Age,
    @Min_Duration,
    @Max_Duration,
    @Min_Contribution,
    @Max_Contribution,
    @RateDiv_factor,
    @Min_SA,
    @Max_SA,
    @Multiplier,
    @A1,
    @A2,
    @Remarks,
    @Tag
)";

            return await conn.ExecuteAsync(sql, dto) > 0;
        }

        public async Task<bool> UpdateAsync(ProductSettingDto dto, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

         
            const string sql = @"
UPDATE ProductSettings
SET
    Min_Age=@Min_Age,
    Max_Age=@Max_Age,
    Min_Duration=@Min_Duration,
    Max_Duration=@Max_Duration,
    Min_Contribution=@Min_Contribution,
    Max_Contribution=@Max_Contribution,
    RateDiv_factor=@RateDiv_factor,
    Min_SA=@Min_SA,
    Max_SA=@Max_SA,
    Multiplier=@Multiplier,
    A1=@A1,
    A2=@A2,
    Remarks=@Remarks,
    Tag=@Tag
WHERE ProductID=@ProductID";

            return await conn.ExecuteAsync(sql, dto) > 0;
        }

        public async Task<bool> DeleteAsync(string productId, CancellationToken ct)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);


            return await conn.ExecuteAsync(
                @"DELETE FROM ProductSettings
                  WHERE ProductID=@ProductID",
                new { ProductID = productId }) > 0;
        }
    }
}