using Dapper;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Domain;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Services
{
    public class ClaimReserveService
    {
        private readonly IDbConnection _db;

        private static readonly string[] SearchableColumns =
        {
            "PolicyNo",
            "ClaimNo",
            "Assured",
            "InsuredName",
            "CustomerName",
            "Narration",
            "ClaimStatus"
        };

        public ClaimReserveService(Settings settings)
        {
            _db = new SqlConnection(settings.SqldbConnString);
        }

        public async Task<PagedResult<clmReserves>> SelectWhereINDAsync(ClmReserveSearchRequest request)
        {
            request.PageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            request.PageSize = request.PageSize <= 0 ? 20 : request.PageSize;

            int offset = (request.PageNumber - 1) * request.PageSize;

            var where = BuildWhereClause(request);

            string countSql = $@"
SELECT COUNT(*)
FROM clmReserve CR
{where}";

            string dataSql = $@"
SELECT CR.*
FROM clmReserve CR
{where}
ORDER BY CR.SubmitOn DESC
OFFSET @Offset ROWS
FETCH NEXT @PageSize ROWS ONLY";

            var param = new
            {
                request.ClassOption,
                request.TypeOption,
                request.Date1,
                request.Date2,
                SearchText = $"%{request.SearchText}%",
                Offset = offset,
                request.PageSize
            };

            var totalRecords = await _db.ExecuteScalarAsync<int>(countSql, param);

            var records = await _db.QueryAsync<clmReserves>(dataSql, param);

            return new PagedResult<clmReserves>
            {
                Data = records,
                TotalRecords = totalRecords,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }

        private string BuildWhereClause(ClmReserveSearchRequest request)
        {
            var sql = new StringBuilder();

            if (string.IsNullOrWhiteSpace(request.SearchText))
            {
                if (string.IsNullOrWhiteSpace(request.TypeOption))
                {
                    sql.AppendLine("INNER JOIN SubRisk SR ON CR.SubRiskCode = SR.SubRiskID");
                    sql.AppendLine("WHERE SR.RiskID IN ('A','B')");
                    sql.AppendLine("AND CR.RecType=@ClassOption");
                    sql.AppendLine("AND CR.TransStatus IN ('PENDING','REVERSED','DECLINE','CLOSED')");
                    sql.AppendLine("AND CR.TDate BETWEEN @Date1 AND @Date2");
                    sql.AppendLine("AND ISNULL(CR.SubRiskCode,'')<>''");
                }
                else
                {
                    sql.AppendLine("WHERE CR.RecType=@ClassOption");
                    sql.AppendLine("AND CR.TransStatus=@TypeOption");
                    sql.AppendLine("AND CR.TDate BETWEEN @Date1 AND @Date2");
                    sql.AppendLine("AND ISNULL(CR.SubRiskCode,'')<>''");
                }
            }
            else
            {
                if (!SearchableColumns.Contains(request.SearchField))
                    throw new Exception("Invalid search field.");

                sql.AppendLine("WHERE CR.RecType=@ClassOption");
                sql.AppendLine($"AND CR.{request.SearchField} LIKE @SearchText");
            }

            return sql.ToString();
        }

        public class PagedResult<T>
        {
            public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();

            public int PageNumber { get; set; }

            public int PageSize { get; set; }

            public int TotalRecords { get; set; }

            public int TotalPages =>
                (int)Math.Ceiling((double)TotalRecords / PageSize);

            public bool HasNext => PageNumber < TotalPages;

            public bool HasPrevious => PageNumber > 1;
        }

   

        public async Task<clmReserves?> GetByIdAsync(long id)
        {
            const string sql = @"
        SELECT *
        FROM clmReserve
        WHERE ClaimID=@ClaimID";

            return await _db.QueryFirstOrDefaultAsync<clmReserves>(
                sql,
                new { ClaimID = id });
        }

        public async Task<long> SaveAsync(clmReserves model)
        {
            if (model.ClaimID == 0)
                return await CreateAsync(model);

            await UpdateAsync(model);

            return model.ClaimID;
        }

        public async Task<long> CreateAsync(clmReserves model)
        {
            const string sql = @"

INSERT INTO clmReserve
(
    BrCode,
    ClaimNo,
    PolicyNo,
    DNCNNo,
    UnitID,
    UnderYr,
    RecType,
    InsuredName,
    StartDate,
    EndDate,
    RenDate,
    SubRiskCode,
    SubRiskName,
    PartyCode,
    InsuredCode,
    TDate,
    NotifyDate,
    LossDate,
    LossDetails,
    Amount,
    PaidAmt,
    AdjAmt,
    A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15,
    D1, D2, D3,
    VehRegNo,
    SubmitBy,
    SubmitOn,
    ModifiedBy,
    ModifiedOn,
    ApprovedBy,
    ApprovedOn,
    TransGUID,
    TransSTATUS,
    Remark,
    Tag
)
VALUES
(
    @BrCode,
    @ClaimNo,
    @PolicyNo,
    @DNCNNo,
    @UnitID,
    @UnderYr,
    @RecType,
    @InsuredName,
    @StartDate,
    @EndDate,
    @RenDate,
    @SubRiskCode,
    @SubRiskName,
    @PartyCode,
    @InsuredCode,
    @TDate,
    @NotifyDate,
    @LossDate,
    @LossDetails,
    @Amount,
    @PaidAmt,
    @AdjAmt,
    @A1, @A2, @A3, @A4, @A5, @A6, @A7, @A8, @A9, @A10, @A11, @A12, @A13, @A14, @A15,
    @D1, @D2, @D3,
    @VehRegNo,
    @SubmitBy,
    @SubmitOn,
    @ModifiedBy,
    @ModifiedOn,
    @ApprovedBy,
    @ApprovedOn,
    @TransGUID,
    @TransSTATUS,
    @Remark,
    @Tag
);

SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
";

            return await _db.ExecuteScalarAsync<long>(sql, model);
        }

        public async Task<bool> UpdateAsync(clmReserves model)
        {
            const string sql = @"

UPDATE clmReserve
SET
    BrCode=@BrCode,
    ClaimNo=@ClaimNo,
    PolicyNo=@PolicyNo,
    DNCNNo=@DNCNNo,
    UnitID=@UnitID,
    UnderYr=@UnderYr,
    RecType=@RecType,
    InsuredName=@InsuredName,
    StartDate=@StartDate,
    EndDate=@EndDate,
    RenDate=@RenDate,
    SubRiskCode=@SubRiskCode,
    SubRiskName=@SubRiskName,
    PartyCode=@PartyCode,
    InsuredCode=@InsuredCode,
    TDate=@TDate,
    NotifyDate=@NotifyDate,
    LossDate=@LossDate,
    LossDetails=@LossDetails,
    Amount=@Amount,
    PaidAmt=@PaidAmt,
    AdjAmt=@AdjAmt,
    A1=@A1, A2=@A2, A3=@A3, A4=@A4, A5=@A5,
    A6=@A6, A7=@A7, A8=@A8, A9=@A9, A10=@A10,
    A11=@A11, A12=@A12, A13=@A13, A14=@A14, A15=@A15,
    D1=@D1, D2=@D2, D3=@D3,
    VehRegNo=@VehRegNo,
    ModifiedBy=@ModifiedBy,
    ModifiedOn=@ModifiedOn,
    ApprovedBy=@ApprovedBy,
    ApprovedOn=@ApprovedOn,
    TransGUID=@TransGUID,
    TransSTATUS=@TransSTATUS,
    Remark=@Remark,
    Tag=@Tag
WHERE ClaimID=@ClaimID";

            return await _db.ExecuteAsync(sql, model) > 0;
        }

        public async Task<IEnumerable<clmReserves>> GetAllAsync()
        {
            const string sql = @"
        SELECT *
        FROM clmReserve
        ORDER BY SubmitOn DESC";

            return await _db.QueryAsync<clmReserves>(sql);
        }
        public async Task<bool> DeleteAsync(long id)
        {
            const string sql = @"
                DELETE FROM clmReserve
                    WHERE ClaimID=@ClaimID";

            return await _db.ExecuteAsync(sql, new { ClaimID = id }) > 0;
        }
    }
}