using Dapper;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;


public class BulkUploadErrorService
{
    private readonly string _connectionString;
    public BulkUploadErrorService(IConfiguration configuration)
    {
        _connectionString = configuration["AppSettings:SqldbConnString"];
    }


    public async Task<long> InsertAsync(BulkUploadErrorCreateDto dto, CancellationToken ct)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        const string sql = @"
INSERT INTO BulkUploadError
(
    UploadBatchID,
    PolicyNo,
    MemberNo,
    MemberName,
    ErrorReason,
    RawData,
    UploadedBy,
    UploadedOn,
    IsResolved
)
VALUES
(
    @UploadBatchID,
    @PolicyNo,
    @MemberNo,
    @MemberName,
    @ErrorReason,
    @RawData,
    @UploadedBy,
    @UploadedOn,
    @IsResolved
);

SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
";

        return await conn.ExecuteScalarAsync<long>(sql, dto);
    }

    public async Task UpdateAsync(BulkUploadErrorUpdateDto dto, CancellationToken ct)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
UPDATE BulkUploadError
SET
    MemberNo = @MemberNo,
    MemberName = @MemberName,
    ErrorReason = @ErrorReason,
    RawData = @RawData,
    IsResolved = @IsResolved,
    ResolvedOn = @ResolvedOn,
    ResolvedBy = @ResolvedBy
WHERE ID = @ID";

        await conn.ExecuteAsync(sql, dto);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"DELETE FROM BulkUploadError WHERE ID=@ID";

        await conn.ExecuteAsync(sql, new { ID = id });
    }

    public async Task<BulkUploadErrorDto?> GetAsync(long id, CancellationToken ct)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT *
FROM BulkUploadError
WHERE ID=@ID";

        return await conn.QuerySingleOrDefaultAsync<BulkUploadErrorDto>(
            sql,
            new { ID = id });
    }

    public async Task<PagedResultDto<BulkUploadErrorDto>> ListAsync(BulkUploadErrorQueryDto query, CancellationToken ct)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var where = @"
WHERE
(@UploadBatchID IS NULL OR UploadBatchID=@UploadBatchID)
AND (@PolicyNo IS NULL OR PolicyNo=@PolicyNo)
AND (@MemberNo IS NULL OR MemberNo LIKE '%' + @MemberNo + '%')
AND (@MemberName IS NULL OR MemberName LIKE '%' + @MemberName + '%')
AND (@UploadedBy IS NULL OR UploadedBy=@UploadedBy)
AND (@IsResolved IS NULL OR IsResolved=@IsResolved)
";

        var sql = $@"
SELECT *
FROM BulkUploadError
{where}
ORDER BY UploadedOn DESC
OFFSET @Offset ROWS
FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(*)
FROM BulkUploadError
{where};";

        var param = new
        {
            query.UploadBatchID,
            query.PolicyNo,
            query.MemberNo,
            query.MemberName,
            query.UploadedBy,
            query.IsResolved,
            Offset = (query.PageNumber - 1) * query.PageSize,
            query.PageSize
        };

        using var multi = await conn.QueryMultipleAsync(sql, param);

        var items = (await multi.ReadAsync<BulkUploadErrorDto>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResultDto<BulkUploadErrorDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }
}