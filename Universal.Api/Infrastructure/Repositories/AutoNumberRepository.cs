using Dapper;

using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Gibs.Infrastructure.Repositories
{
    /// <summary>
    /// Dapper repository for [dbo].[AutoNumbers].
    /// GetFormatAndIncrementAsync uses an UPDATE...OUTPUT to atomically fetch
    /// and increment NextValue — equivalent to the VB getNextSerialNo() +
    /// db.SubmitChanges() pattern but race-condition safe.
    /// </summary>
    public class AutoNumberRepository : BaseRepository
    {
        public AutoNumberRepository(IConfiguration config) : base(config) { }

        // ─────────────────────────────────────────────────────────────────────
        // ATOMIC FETCH + INCREMENT
        // Mirrors getSerialNoFormat() + getNextSerialNo() combined.
        // Priority order (matches VB logic):
        //   1. BranchID = "ALL"  (global fallback)
        //   2. BranchID = exact  (branch-specific)
        //   3. BranchID = exact + RiskID = exact  (sub-risk specific, non-CLAIM types)
        // Returns (Format, NextValue-before-increment) or (null, -1) if not found.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<(string? Format, long NextValue)> GetFormatAndIncrementAsync(
            string numType, string branchID, string riskID = "")
        {
            // Special Staco rule: for CLAIM type, RiskID is always ignored
            if (string.Equals(numType, "CLAIM", StringComparison.OrdinalIgnoreCase))
                riskID = "";

            using var conn = OpenConnection();
            conn.Open();
            using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                // ── Step 1: try BranchID = "ALL" (global) ────────────────────
                var (format, value, found) = await TryIncrementAsync(
                    conn, tx, numType, "ALL", null);

                // ── Step 2: try exact branch, no risk ────────────────────────
                if (!found)
                    (format, value, found) = await TryIncrementAsync(
                        conn, tx, numType, branchID, null);

                // ── Step 3: try exact branch + riskID (non-CLAIM only) ───────
                if (!found && !string.IsNullOrWhiteSpace(riskID))
                    (format, value, found) = await TryIncrementAsync(
                        conn, tx, numType, branchID, riskID);

                if (!found)
                {
                    tx.Rollback();
                    return (null, -1);
                }

                tx.Commit();
                return (format, value);
            }
            catch
            {
                tx.Rollback();
                return (null, -2);
            }
        }

        /// <summary>
        /// Atomically reads NextValue, increments it in the DB, and returns
        /// (Format, OldNextValue, true) if a matching row exists, else (null, -1, false).
        /// </summary>
        private static async Task<(string? Format, long Value, bool Found)> TryIncrementAsync(
            IDbConnection conn, IDbTransaction tx,
            string numType, string branchID, string? riskID)
        {
            string sql;
            object param;

            if (string.IsNullOrWhiteSpace(riskID))
            {
                sql = @"
                    UPDATE [dbo].[AutoNumbers]
                    SET    NextValue   = NextValue + 1,
                           LastUpdated = GETDATE()
                    OUTPUT DELETED.Format,
                           DELETED.NextValue          -- value BEFORE increment
                    WHERE  NumType  = @NumType
                      AND  BranchID = @BranchID
                      AND  (RiskID IS NULL OR RiskID = '')";
                param = new { NumType = numType, BranchID = branchID };
            }
            else
            {
                sql = @"
                    UPDATE [dbo].[AutoNumbers]
                    SET    NextValue   = NextValue + 1,
                           LastUpdated = GETDATE()
                    OUTPUT DELETED.Format,
                           DELETED.NextValue
                    WHERE  NumType  = @NumType
                      AND  BranchID = @BranchID
                      AND  RiskID   = @RiskID";
                param = new { NumType = numType, BranchID = branchID, RiskID = riskID };
            }

            var row = await conn.QuerySingleOrDefaultAsync<(string Format, long NextValue)>(
                sql, param, tx);

            if (row == default)
                return (null, -1, false);

            return (row.Format, row.NextValue, true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // CRUD
        // ─────────────────────────────────────────────────────────────────────

        public async Task<AutoNumber?> GetByIdAsync(long numId)
        {
            const string sql = "SELECT * FROM [dbo].[AutoNumbers] WHERE NumID = @NumID";
            using var conn = OpenConnection();
            return await conn.QuerySingleOrDefaultAsync<AutoNumber>(sql, new { NumID = numId });
        }

        public async Task<AutoNumber?> GetAsync(string numType, string branchId)
        {
            const string sql = @"
                SELECT TOP 1 * FROM [dbo].[AutoNumbers]
                WHERE NumType = @NumType AND BranchID = @BranchID";
            using var conn = OpenConnection();
            return await conn.QuerySingleOrDefaultAsync<AutoNumber>(sql,
                new { NumType = numType, BranchID = branchId });
        }

        public async Task<List<AutoNumber>> GetAllAsync()
        {
            const string sql = @"
                SELECT * FROM [dbo].[AutoNumbers]
                ORDER BY NumType, BranchID";
            using var conn = OpenConnection();
            var result = await conn.QueryAsync<AutoNumber>(sql);
            return result.AsList();
        }
        public class RepositoryResult
        {
            public bool Success { get; init; }
            public string? ErrorMessage { get; init; }
            public long? GeneratedId { get; set; }
            public static RepositoryResult Ok(long generatedId) => new() { Success = true, GeneratedId = generatedId };
            public static RepositoryResult Ok() => new() { Success = true };
            public static RepositoryResult Fail(string msg) => new() { Success = false, ErrorMessage = msg };
        }
        public async Task<RepositoryResult> InsertAsync(AutoNumber e)
        {
            const string sql = @"
                INSERT INTO [dbo].[AutoNumbers]
                    (NumType, BranchID, CompanyID, RiskID, NextValue, LastUpdated, Format, Sample)
                OUTPUT INSERTED.NumID
                VALUES
                    (@NumType, @BranchID, @CompanyID, @RiskID, @NextValue, GETDATE(), @Format, @Sample)";
            try
            {
                using var conn = OpenConnection();
                var newId = await conn.ExecuteScalarAsync<long>(sql, e);
                return RepositoryResult.Ok();
            }
            catch (Exception ex)
            {
                return RepositoryResult.Fail(ex.Message);
            }
        }

        public async Task<RepositoryResult> UpdateAsync(AutoNumber e)
        {
            const string sql = @"
                UPDATE [dbo].[AutoNumbers] SET
                    NumType     = @NumType,
                    BranchID    = @BranchID,
                    CompanyID   = @CompanyID,
                    RiskID      = @RiskID,
                    NextValue   = @NextValue,
                    LastUpdated = GETDATE(),
                    Format      = @Format,
                    Sample      = @Sample
                WHERE NumID = @NumID";
            try
            {
                using var conn = OpenConnection();
                await conn.ExecuteAsync(sql, e);
                return RepositoryResult.Ok();
            }
            catch (Exception ex)
            {
                return RepositoryResult.Fail(ex.Message);
            }
        }

        public async Task<RepositoryResult> DeleteAsync(long numId)
        {
            const string sql = "DELETE FROM [dbo].[AutoNumbers] WHERE NumID = @NumID";
            try
            {
                using var conn = OpenConnection();
                await conn.ExecuteAsync(sql, new { NumID = numId });
                return RepositoryResult.Ok();
            }
            catch (Exception ex)
            {
                return RepositoryResult.Fail(ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TOKEN LOOKUPS — used by AutoNumberService for $BR2$ and $RC$ tokens
        // ─────────────────────────────────────────────────────────────────────

        public async Task<string?> GetBranchID2Async(string branchId)
        {
            const string sql = @"
                SELECT TOP 1 BranchID2
                FROM [dbo].[Branches]
                WHERE BranchID = @BranchID";
            using var conn = OpenConnection();
            return await conn.ExecuteScalarAsync<string?>(sql, new { BranchID = branchId });
        }

        public async Task<string?> GetRiskIDBySubRiskAsync(string subRiskId)
        {
            const string sql = @"
                SELECT TOP 1 RiskID
                FROM [dbo].[SubRisks]
                WHERE SubRiskID = @SubRiskID";
            using var conn = OpenConnection();
            return await conn.ExecuteScalarAsync<string?>(sql, new { SubRiskID = subRiskId });
        }
    }
}