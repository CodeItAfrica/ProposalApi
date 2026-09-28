    using Gibs.Application.Services;
    using Gibs.Domain.Entities;
    using Gibs.Infrastructure;
    using GibsLifesMicroWebApi.Data.Repositories;
    using GibsLifesMicroWebApi.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore.SqlServer.Scaffolding.Internal;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Intrinsics.X86;
    using System.Threading.Tasks;
    using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

    namespace GibsLifesMicroWebApi.Infrastructure.Repositories
    {
        public partial class ProposalService
        {
            private readonly string _connectionString;
            private readonly ILogger<ProposalService> _logger;

            private readonly AutoNumberServices _autoNumbers;

            public ProposalService(
                IConfiguration configuration,
                ILogger<ProposalService> logger,
                AutoNumberServices autoNumbers)
            {
                _connectionString = configuration["AppSettings:SqldbConnString"];
                _logger = logger;
                _autoNumbers = autoNumbers;
            }


            /// <summary>Calculation-level cert. Legacy: DECLARATION_NO + branch + subrisk + endorse type.</summary>
            private async Task<string> NextCalcCertAsync(
                string branchId, string subRiskId, EndorseType endorseType = EndorseType.None)
            {
                var result = await _autoNumbers.GenerateAsync(new GenerateAutoNumberRequest
                {
                    CustomNo = "[AUTO]",
                    NumType = AutoNumType.DECLARATION_NO,
                    BranchID = branchId,
                    SubRiskID = subRiskId,
                    EndorseType = endorseType      // wrapper method doesn't forward this; call GenerateAsync directly
                });

                if (!result.Success || string.IsNullOrWhiteSpace(result.Data?.GeneratedNumber))
                    throw new InvalidOperationException(
                        $"AutoNumber DECLARATION_NO not configured (branch '{branchId}', subrisk '{subRiskId}').");

                return result.Data.GeneratedNumber;
            }

            /// <summary>Section batch number. Legacy: PROPOSAL + branch.</summary>
            private async Task<string> NextDeclarationNoAsync(string branchId)
            {
                var number = await _autoNumbers.GetNextAutoNumberAsync(
                    AutoNumType.PROPOSAL.ToString(), branchId);

                if (string.IsNullOrWhiteSpace(number))
                    throw new InvalidOperationException(
                        $"AutoNumber PROPOSAL not configured (branch '{branchId}').");

                return number;
            }

            /// <summary>Per-risk-item certificate. Legacy: CERTNO + branch, one per grid row.</summary>
            private async Task<string> NextItemCertNoAsync(string branchId)
            {
                var number = await _autoNumbers.GetNextAutoNumberAsync(
                    AutoNumType.CERTNO.ToString(), branchId);

                if (string.IsNullOrWhiteSpace(number))
                    throw new InvalidOperationException(
                        $"AutoNumber CERTNO not configured (branch '{branchId}').");

                return number;
            }


            /// <summary>
            /// LEGACY: maps free-text endorsement types onto the AutoNumber EndorseType enum
            /// (legacy passed the transaction's EndorseType straight into getNextAutoNumber
            /// for DECLARATION_NO). Unknown values fall back to EndorseType.None so number
            /// generation never breaks on unmapped text.
            /// </summary>
            private static EndorseType MapEndorseType(string endorsementType)
            {
                if (string.IsNullOrWhiteSpace(endorsementType)) return EndorseType.None;
                var key = endorsementType.Trim().Replace(" ", "");
                return Enum.TryParse<EndorseType>(key, true, out var parsed) ? parsed : EndorseType.None;
            }

            /// <summary>
            /// EXCHANGE RATE: loads the currency/rate saved on the proposal header (Policies) at
            /// creation time. This is the canonical source for every calculation that runs after
            /// creation — edits, additions, renewals, endorsements, recalculation — so the rate
            /// used to convert amounts can't silently drift from what the proposal was opened
            /// with just because a caller resupplies a different value on the request. Falls back
            /// to ExRate=1/"NAIRA" when the proposal row can't be found or the stored rate is 0,
            /// matching the fallback already used by GetFireDeclarationContextAsync.
            /// </summary>
            private async Task<(decimal ExRate, string ExCurrency, decimal ProportionRate)> GetSavedExchangeContextAsync(
                string proposalNo, SqlConnection connection, SqlTransaction transaction)
            {
                decimal exRate = 1;
                string exCurrency = "NAIRA";
                decimal proportionRate = 100;

                var sql = @"
                    SELECT TOP 1 ExRate, ExCurrency, ProportionRate
                    FROM Policies
                    WHERE PolicyNo = @PolicyNo AND Deleted = 0";
                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var fx = reader.GetValueOrDefault<decimal>("ExRate");
                    exRate = fx > 0 ? fx : 1;
                    var currency = reader.GetStringOrDefault("ExCurrency");
                    exCurrency = string.IsNullOrEmpty(currency) ? "NAIRA" : currency;
                    var rate = reader.GetValueOrDefault<decimal>("ProportionRate");
                    proportionRate = rate > 0 ? rate : 100;
                }

                return (exRate, exCurrency, proportionRate);
            }

            /// <summary>
            /// EXCHANGE RATE: converts an amount as entered by the user (in the proposal's
            /// declared currency) into the base-currency value that gets persisted, by applying
            /// the proposal's saved exchange rate. A non-positive rate is treated as 1 (no-op),
            /// same convention used throughout this file for local/NAIRA proposals.
            /// </summary>
            private static decimal ToStoredAmount(decimal enteredAmount, decimal exRate)
                => enteredAmount * (exRate > 0 ? exRate : 1m);

            /// <summary>
            /// EXCHANGE RATE: reverses ToStoredAmount — reconstructs the amount as originally
            /// entered by the user from a base-currency value read back from the database.
            /// </summary>
            private static decimal FromStoredAmount(decimal storedAmount, decimal exRate)
                => storedAmount / (exRate > 0 ? exRate : 1m);

            /// <summary>
            /// LEGACY: section/item auto numbers (PROPOSAL, CERTNO) are keyed by branch,
            /// exactly like getNextAutoNumber(..., P.BranchID) in fire.ascx.vb.
            /// </summary>
            private async Task<string> GetBranchIdAsync(string policyNo, SqlConnection connection, SqlTransaction transaction)
            {
                var sql = "SELECT TOP 1 BranchID FROM Policies WHERE PolicyNo = @PolicyNo";
                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@PolicyNo", policyNo);

                var result = await cmd.ExecuteScalarAsync();
                var branchId = result?.ToString();

                if (string.IsNullOrWhiteSpace(branchId))
                    throw new InvalidOperationException($"BranchID not found for policy '{policyNo}' — cannot generate legacy auto numbers.");

                return branchId;
            }

            // OPTIMIZED VERSION - Replace your CreateProposalAsync method with this

            public async Task<ResponseInfo> CreateProposalAsync(EnhancedCreateProposalRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                try
                {
                    var proposalNo = GenerateProposalNumberGuidBased(request.BranchID, request.SubriskID);

                    // STEP 1: Get lookup data with SINGLE optimized query
                    var lookupData = await GetLookupDataOptimizedAsync(request, connection, null);

                    InsuredDetailsData insuredDetails;

                    // STEP 2: Handle insured details
                    if (!string.IsNullOrEmpty(request.InsuredID))
                    {
                        // Use EXISTING customer - add command timeout
                        insuredDetails = await GetInsuredDetailsFromInsuredIdAsync(request.InsuredID, connection, null);
                        if (insuredDetails == null)
                        {
                            return new ResponseInfo
                            {
                                ErrorCode = 1,
                                ErrorMessage = $"InsuredID {request.InsuredID} not found. To create a new customer, do not provide InsuredID."
                            };
                        }
                    }
                    else if (!string.IsNullOrEmpty(request.Surname))
                    {
                        if (!request.IsOrg.HasValue)
                        {
                            return new ResponseInfo
                            {
                                ErrorCode = 1,
                                ErrorMessage = "IsOrg must be specified when creating a new customer"
                            };
                        }

                        var newInsuredId = GenerateInsuredId();
                        string insuredType = request.IsOrg.Value ? "CORPORATE" : "INDIVIDUAL";

                        insuredDetails = new InsuredDetailsData
                        {
                            InsuredID = newInsuredId,
                            Surname = request.Surname ?? "",
                            FirstName = request.FirstName ?? "",
                            OtherNames = request.OtherNames ?? "",
                            Address = request.InsAddress ?? "",
                            MobilePhone = request.InsMobilePhone ?? "",
                            LandPhone = "",
                            Email = request.InsEmail ?? "",
                            Occupation = request.InsOccupation ?? "",
                            InsuredType = insuredType
                        };
                    }
                    else
                    {
                        return new ResponseInfo
                        {
                            ErrorCode = 1,
                            ErrorMessage = "Either InsuredID (for existing customer) or Surname (for new customer) must be provided"
                        };
                    }

                    // STEP 3: Start transaction for write operations
                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        // ONLY save new insured if creating new customer
                        if (string.IsNullOrEmpty(request.InsuredID) && !string.IsNullOrEmpty(request.Surname))
                        {
                            await SaveInsuredClientAsync(insuredDetails, connection, transaction);
                        }

                        // Insert proposal
                        await InsertPolicyWithInsuredDetailsAsync(proposalNo, request, lookupData,
                            insuredDetails, connection, transaction);

                        // Save initial policy details
                        await SaveInitialPolicyDetailsAsync(proposalNo, request, lookupData,
                            connection, transaction);

                        await transaction.CommitAsync();

                        return new ResponseInfo
                        {
                            ErrorCode = 0,
                            ErrorMessage = proposalNo,
                            TotalSuccess = 1
                        };
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating proposal");
                    return new ResponseInfo
                    {
                        ErrorCode = 1,
                        ErrorMessage = "Failed to create proposal",
                        ExtraMessage = ex.Message
                    };
                }
            }

            /// <summary>
            /// Update an existing proposal
            /// </summary>
            public async Task<ResponseInfo> UpdateProposalAsync(string proposalNo, UpdateProposalRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // 1. Check if proposal exists and is not deleted
                    var checkSql = @"
                SELECT COUNT(*) 
                FROM Policies 
                WHERE PolicyNo = @ProposalNo AND Deleted = 0";

                    using (var checkCmd = new SqlCommand(checkSql, connection, transaction))
                    {
                        checkCmd.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        var count = (int)await checkCmd.ExecuteScalarAsync();

                        if (count == 0)
                        {
                            return new ResponseInfo
                            {
                                ErrorCode = 1,
                                ErrorMessage = "Proposal not found or already deleted"
                            };
                        }
                    }

                    // 2. Get current proposal data to merge with updates
                    var currentProposal = await GetProposalAsync(proposalNo);
                    if (currentProposal == null)
                    {
                        return new ResponseInfo
                        {
                            ErrorCode = 1,
                            ErrorMessage = "Proposal not found"
                        };
                    }

                    // 3. Get lookup data if branch/party details are being updated
                    LookupData lookupData = null;
                    if (!string.IsNullOrEmpty(request.BranchID) ||
                        !string.IsNullOrEmpty(request.PartyID) ||
                        !string.IsNullOrEmpty(request.MktStaffID))
                    {
                        lookupData = await GetLookupDataOptimizedAsync(
                            new EnhancedCreateProposalRequestDto
                            {
                                BranchID = request.BranchID ?? currentProposal.BranchID,
                                SubriskID = currentProposal.SubRiskID,
                                PartyID = request.PartyID ?? currentProposal.PartyID,
                                MktStaffID = request.MktStaffID ?? currentProposal.MktStaffID
                            },
                            connection,
                            transaction);
                    }

                    // 4. Update Policies table
                    var updatePolicySql = @"
                UPDATE Policies SET 
                    BranchID = ISNULL(@BranchID, BranchID),
                    Branch = ISNULL(@Branch, Branch),
                    PartyID = ISNULL(@PartyID, PartyID),
                    Party = ISNULL(@Party, Party),
                    MktStaffID = ISNULL(@MktStaffID, MktStaffID),
                    MktStaff = ISNULL(@MktStaff, MktStaff),
                    InsSurname = ISNULL(@InsSurname, InsSurname),
                    InsFirstname = ISNULL(@InsFirstname, InsFirstname),
                    InsOthernames = ISNULL(@InsOthernames, InsOthernames),
                    InsAddress = ISNULL(@InsAddress, InsAddress),
                    InsMobilePhone = ISNULL(@InsMobilePhone, InsMobilePhone),
                    InsEmail = ISNULL(@InsEmail, InsEmail),
                    InsOccupation = ISNULL(@InsOccupation, InsOccupation),
                    StartDate = ISNULL(@StartDate, StartDate),
                    EndDate = ISNULL(@EndDate, EndDate),
                    BizSource = ISNULL(@BizSource, BizSource),
                    SourceType = ISNULL(@SourceType, SourceType),
                    ProportionRate = ISNULL(@ProportionRate, ProportionRate),
                    ExCurrency = ISNULL(@ExCurrency, ExCurrency),
                    ExRate = ISNULL(@ExRate, ExRate),
                    Remarks = ISNULL(@Remarks, Remarks),
                    ModifiedBy = @ModifiedBy,
                    ModifiedOn = @ModifiedOn
                WHERE PolicyNo = @PolicyNo";

                    using (var cmd = new SqlCommand(updatePolicySql, connection, transaction))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);

                        // Branch and party details
                        cmd.Parameters.AddWithValue("@BranchID", (object)request.BranchID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Branch", (object)lookupData?.BranchName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PartyID", (object)request.PartyID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Party", (object)lookupData?.PartyName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MktStaffID", (object)request.MktStaffID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MktStaff", (object)lookupData?.MktStaffName ?? DBNull.Value);

                        // Insured details
                        cmd.Parameters.AddWithValue("@InsSurname", (object)request.Surname ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsFirstname", (object)request.FirstName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsOthernames", (object)request.OtherNames ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsAddress", (object)request.InsAddress ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsMobilePhone", (object)request.InsMobilePhone ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsEmail", (object)request.InsEmail ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsOccupation", (object)request.InsOccupation ?? DBNull.Value);

                        // Policy period
                        cmd.Parameters.AddWithValue("@StartDate", (object)request.StartDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EndDate", (object)request.EndDate ?? DBNull.Value);

                        // Business details
                        cmd.Parameters.AddWithValue("@BizSource", (object)request.BusinessType ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SourceType", (object)request.BizSource ?? DBNull.Value);

                        // Financial parameters
                        cmd.Parameters.AddWithValue("@ProportionRate", (object)request.ProportionRate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ExCurrency", (object)request.Currency ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ExRate", (object)request.ExRate ?? DBNull.Value);

                        cmd.Parameters.AddWithValue("@Remarks", (object)request.Remarks ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ModifiedBy", request.ModifiedBy);
                        cmd.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 5. Update PolicyDetails if dates or rates changed
                    if (request.StartDate.HasValue || request.EndDate.HasValue ||
                        request.ProportionRate.HasValue || request.ExRate.HasValue)
                    {
                        var updateDetailsSql = @"
                    UPDATE PolicyDetails SET 
                        StartDate = ISNULL(@StartDate, StartDate),
                        EndDate = ISNULL(@EndDate, EndDate),
                        ProRataDays = CASE 
                            WHEN @StartDate IS NOT NULL AND @EndDate IS NOT NULL 
                            THEN DATEDIFF(DAY, @StartDate, @EndDate)
                            ELSE ProRataDays
                        END,
                        ProportionRate = ISNULL(@ProportionRate, ProportionRate),
                        ExRate = ISNULL(@ExRate, ExRate),
                        ExCurrency = ISNULL(@ExCurrency, ExCurrency),
                        ModifiedBy = @ModifiedBy,
                        ModifiedOn = @ModifiedOn
                    WHERE PolicyNo = @PolicyNo AND Deleted = 0";

                        using (var cmd = new SqlCommand(updateDetailsSql, connection, transaction))
                        {
                            cmd.CommandTimeout = 60;
                            cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                            cmd.Parameters.AddWithValue("@StartDate", (object)request.StartDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EndDate", (object)request.EndDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ProportionRate", (object)request.ProportionRate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ExRate", (object)request.ExRate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ExCurrency", (object)request.Currency ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ModifiedBy", request.ModifiedBy);
                            cmd.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 6. Update InsuredClients table if insured details changed and InsuredID exists
                    if (!string.IsNullOrEmpty(currentProposal.InsuredID) &&
                        (request.Surname != null || request.FirstName != null || request.OtherNames != null ||
                         request.InsAddress != null || request.InsMobilePhone != null ||
                         request.InsEmail != null || request.InsOccupation != null))
                    {
                        var updateInsuredSql = @"
                    UPDATE InsuredClients SET 
                        SurName = ISNULL(@SurName, SurName),
                        OtherNames = ISNULL(@OtherNames, OtherNames),
                        FullName = LTRIM(RTRIM(ISNULL(@SurName, SurName) + ' ' + 
                                   ISNULL(@OtherNames, OtherNames))),
                        Address = ISNULL(@Address, Address),
                        MobilePhone = ISNULL(@MobilePhone, MobilePhone),
                        Email = ISNULL(@Email, Email),
                        Occupation = ISNULL(@Occupation, Occupation)
                    WHERE InsuredID = @InsuredID";

                        using (var cmd = new SqlCommand(updateInsuredSql, connection, transaction))
                        {
                            cmd.CommandTimeout = 60;
                            cmd.Parameters.AddWithValue("@InsuredID", currentProposal.InsuredID);
                            cmd.Parameters.AddWithValue("@SurName", (object)request.Surname ?? DBNull.Value);
                            // LEGACY: no FirstName column — merge it into OtherNames.
                            var otherNames = $"{request.FirstName} {request.OtherNames}".Trim();
                            cmd.Parameters.AddWithValue("@OtherNames",
                                string.IsNullOrEmpty(otherNames) ? (object)DBNull.Value : otherNames);
                            cmd.Parameters.AddWithValue("@Address", (object)request.InsAddress ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MobilePhone", (object)request.InsMobilePhone ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Email", (object)request.InsEmail ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Occupation", (object)request.InsOccupation ?? DBNull.Value);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Proposal {ProposalNo} updated successfully by {ModifiedBy}",
                        proposalNo, request.ModifiedBy);

                    return new ResponseInfo
                    {
                        ErrorCode = 0,
                        ErrorMessage = proposalNo,
                        TotalSuccess = 1
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating proposal {ProposalNo}", proposalNo);

                    return new ResponseInfo
                    {
                        ErrorCode = 1,
                        ErrorMessage = "Failed to update proposal",
                        ExtraMessage = ex.Message
                    };
                }
            }
            /// <summary>
            /// Renew an existing proposal
            /// </summary>
            public async Task<ResponseInfo> RenewProposalAsync(string proposalNo, UpdateProposalRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // 1. Check if proposal exists and is not deleted
                    var checkSql = @"
                SELECT COUNT(*) 
                FROM Policies 
                WHERE PolicyNo = @ProposalNo AND Deleted = 0";

                    using (var checkCmd = new SqlCommand(checkSql, connection, transaction))
                    {
                        checkCmd.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        var count = (int)await checkCmd.ExecuteScalarAsync();

                        if (count == 0)
                        {
                            return new ResponseInfo
                            {
                                ErrorCode = 1,
                                ErrorMessage = "Proposal not found or already deleted"
                            };
                        }
                    }

                    // 2. Get current proposal data to merge with updates
                    var currentProposal = await GetProposalAsync(proposalNo);
                    if (currentProposal == null)
                    {
                        return new ResponseInfo
                        {
                            ErrorCode = 1,
                            ErrorMessage = "Proposal not found"
                        };
                    }

                    // 3. Get lookup data if branch/party details are being updated
                    LookupData lookupData = null;
                    if (!string.IsNullOrEmpty(request.BranchID) ||
                        !string.IsNullOrEmpty(request.PartyID) ||
                        !string.IsNullOrEmpty(request.MktStaffID))
                    {
                        lookupData = await GetLookupDataOptimizedAsync(
                            new EnhancedCreateProposalRequestDto
                            {
                                BranchID = request.BranchID ?? currentProposal.BranchID,
                                SubriskID = currentProposal.SubRiskID,
                                PartyID = request.PartyID ?? currentProposal.PartyID,
                                MktStaffID = request.MktStaffID ?? currentProposal.MktStaffID
                            },
                            connection,
                            transaction);
                    }

                    // 4. Update Policies table
                    var updatePolicySql = @"
                UPDATE Policies SET 
                    BranchID = ISNULL(@BranchID, BranchID),
                    Branch = ISNULL(@Branch, Branch),
                    PartyID = ISNULL(@PartyID, PartyID),
                    Party = ISNULL(@Party, Party),
                    MktStaffID = ISNULL(@MktStaffID, MktStaffID),
                    MktStaff = ISNULL(@MktStaff, MktStaff),
                    InsSurname = ISNULL(@InsSurname, InsSurname),
                    InsFirstname = ISNULL(@InsFirstname, InsFirstname),
                    InsOthernames = ISNULL(@InsOthernames, InsOthernames),
                    InsAddress = ISNULL(@InsAddress, InsAddress),
                    InsMobilePhone = ISNULL(@InsMobilePhone, InsMobilePhone),
                    InsEmail = ISNULL(@InsEmail, InsEmail),
                    InsOccupation = ISNULL(@InsOccupation, InsOccupation),
                    StartDate = ISNULL(@StartDate, StartDate),
                    EndDate = ISNULL(@EndDate, EndDate),
                    BizSource = ISNULL(@BizSource, BizSource),
                    SourceType = ISNULL(@SourceType, SourceType),
                    ProportionRate = ISNULL(@ProportionRate, ProportionRate),
                    ExCurrency = ISNULL(@ExCurrency, ExCurrency),
                    ExRate = ISNULL(@ExRate, ExRate),
                    Remarks = ISNULL(@Remarks, Remarks),
                    ModifiedBy = @ModifiedBy,
                    ModifiedOn = @ModifiedOn
                WHERE PolicyNo = @PolicyNo";

                    using (var cmd = new SqlCommand(updatePolicySql, connection, transaction))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);

                        // Branch and party details
                        cmd.Parameters.AddWithValue("@BranchID", (object)request.BranchID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Branch", (object)lookupData?.BranchName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PartyID", (object)request.PartyID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Party", (object)lookupData?.PartyName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MktStaffID", (object)request.MktStaffID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MktStaff", (object)lookupData?.MktStaffName ?? DBNull.Value);

                        // Insured details
                        cmd.Parameters.AddWithValue("@InsSurname", (object)request.Surname ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsFirstname", (object)request.FirstName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsOthernames", (object)request.OtherNames ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsAddress", (object)request.InsAddress ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsMobilePhone", (object)request.InsMobilePhone ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsEmail", (object)request.InsEmail ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@InsOccupation", (object)request.InsOccupation ?? DBNull.Value);

                        // Policy period
                        cmd.Parameters.AddWithValue("@StartDate", (object)request.StartDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EndDate", (object)request.EndDate ?? DBNull.Value);

                        // Business details
                        cmd.Parameters.AddWithValue("@BizSource", (object)request.BusinessType ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SourceType", (object)request.BizSource ?? DBNull.Value);

                        // Financial parameters
                        cmd.Parameters.AddWithValue("@ProportionRate", (object)request.ProportionRate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ExCurrency", (object)request.Currency ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ExRate", (object)request.ExRate ?? DBNull.Value);

                        cmd.Parameters.AddWithValue("@Remarks", (object)request.Remarks ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ModifiedBy", request.ModifiedBy);
                        cmd.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 5. Update PolicyDetails if dates or rates changed
                    if (request.StartDate.HasValue || request.EndDate.HasValue ||
                        request.ProportionRate.HasValue || request.ExRate.HasValue)
                    {
                        var updateDetailsSql = @"
                    UPDATE PolicyDetails SET 
                        StartDate = ISNULL(@StartDate, StartDate),
                        EndDate = ISNULL(@EndDate, EndDate),
                        ProRataDays = CASE 
                            WHEN @StartDate IS NOT NULL AND @EndDate IS NOT NULL 
                            THEN DATEDIFF(DAY, @StartDate, @EndDate)
                            ELSE ProRataDays
                        END,
                        ProportionRate = ISNULL(@ProportionRate, ProportionRate),
                        ExRate = ISNULL(@ExRate, ExRate),
                        ExCurrency = ISNULL(@ExCurrency, ExCurrency),
                        ModifiedBy = @ModifiedBy,
                        ModifiedOn = @ModifiedOn
                    WHERE PolicyNo = @PolicyNo AND Deleted = 0";

                        using (var cmd = new SqlCommand(updateDetailsSql, connection, transaction))
                        {
                            cmd.CommandTimeout = 60;
                            cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                            cmd.Parameters.AddWithValue("@StartDate", (object)request.StartDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EndDate", (object)request.EndDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ProportionRate", (object)request.ProportionRate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ExRate", (object)request.ExRate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ExCurrency", (object)request.Currency ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ModifiedBy", request.ModifiedBy);
                            cmd.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 6. Update InsuredClients table if insured details changed and InsuredID exists
                    if (!string.IsNullOrEmpty(currentProposal.InsuredID) &&
                        (request.Surname != null || request.FirstName != null || request.OtherNames != null ||
                         request.InsAddress != null || request.InsMobilePhone != null ||
                         request.InsEmail != null || request.InsOccupation != null))
                    {
                        var updateInsuredSql = @"
                    UPDATE InsuredClients SET 
                        SurName = ISNULL(@SurName, SurName),
                        OtherNames = ISNULL(@OtherNames, OtherNames),
                        FullName = LTRIM(RTRIM(ISNULL(@SurName, SurName) + ' ' + 
                                   ISNULL(@OtherNames, OtherNames))),
                        Address = ISNULL(@Address, Address),
                        MobilePhone = ISNULL(@MobilePhone, MobilePhone),
                        Email = ISNULL(@Email, Email),
                        Occupation = ISNULL(@Occupation, Occupation)
                    WHERE InsuredID = @InsuredID";

                        using (var cmd = new SqlCommand(updateInsuredSql, connection, transaction))
                        {
                            cmd.CommandTimeout = 60;
                            cmd.Parameters.AddWithValue("@InsuredID", currentProposal.InsuredID);
                            cmd.Parameters.AddWithValue("@SurName", (object)request.Surname ?? DBNull.Value);
                            // LEGACY: no FirstName column — merge it into OtherNames.
                            var otherNames = $"{request.FirstName} {request.OtherNames}".Trim();
                            cmd.Parameters.AddWithValue("@OtherNames",
                                string.IsNullOrEmpty(otherNames) ? (object)DBNull.Value : otherNames);
                            cmd.Parameters.AddWithValue("@Address", (object)request.InsAddress ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MobilePhone", (object)request.InsMobilePhone ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Email", (object)request.InsEmail ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Occupation", (object)request.InsOccupation ?? DBNull.Value);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    await MarkOldPolicyCalculationsAsOldAsync(proposalNo, connection, transaction);

                    await transaction.CommitAsync();

                    _logger.LogInformation("Proposal {ProposalNo} updated successfully by {ModifiedBy}",
                        proposalNo, request.ModifiedBy);

                    return new ResponseInfo
                    {
                        ErrorCode = 0,
                        ErrorMessage = proposalNo,
                        TotalSuccess = 1
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating proposal {ProposalNo}", proposalNo);

                    return new ResponseInfo
                    {
                        ErrorCode = 1,
                        ErrorMessage = "Failed to update proposal",
                        ExtraMessage = ex.Message
                    };
                }
            }
            /// <summary>
            /// Create policy renewal
            /// </summary>
            public async Task<PolicyRenewalResponseDto> CreatePolicyRenewalAsync(CreatePolicyRenewalRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // 1. Validate original policy exists
                    var originalPolicy = await GetProposalAsync(request.OriginalPolicyNo);
                    if (originalPolicy == null)
                    {
                        return new PolicyRenewalResponseDto
                        {
                            Success = false,
                            Message = "Original policy not found",
                            ValidationErrors = { $"Policy {request.OriginalPolicyNo} does not exist" }
                        };
                    }

                    // 2. Check if already renewed
                    var alreadyRenewed = await CheckIfAlreadyRenewedAsync(request.OriginalPolicyNo, connection, transaction);
                    if (alreadyRenewed.Item1)
                    {
                        return new PolicyRenewalResponseDto
                        {
                            Success = false,
                            Message = "Policy already renewed",
                            ValidationErrors = { $"This policy was already renewed as {alreadyRenewed.Item2}" }
                        };
                    }

                    // 3. Generate new policy number
                    var newPolicyNo = GenerateProposalNumberGuidBased(originalPolicy.BranchID, originalPolicy.SubRiskID);

                    // 4. Get lookup data
                    var lookupData = await GetLookupDataOptimizedAsync(
                        new EnhancedCreateProposalRequestDto
                        {
                            BranchID = originalPolicy.BranchID,
                            SubriskID = originalPolicy.SubRiskID,
                            PartyID = originalPolicy.PartyID,
                            MktStaffID = originalPolicy.MktStaffID
                        },
                        connection,
                        transaction);

                    // 5. Create insured details from request (allowing edits)
                    var insuredDetails = new InsuredDetailsData
                    {
                        InsuredID = originalPolicy.InsuredID,
                        Surname = request.InsSurname ?? originalPolicy.InsSurname,
                        FirstName = request.InsFirstname ?? originalPolicy.InsFirstname,
                        OtherNames = request.InsOthernames ?? originalPolicy.InsOthernames,
                        Address = request.InsAddress ?? originalPolicy.InsAddress,
                        MobilePhone = request.InsMobilePhone ?? originalPolicy.InsMobilePhone,
                        Email = request.InsEmail ?? originalPolicy.InsEmail,
                        Occupation = request.InsOccupation ?? originalPolicy.InsOccupation,
                        InsuredType = originalPolicy.InsuredID?.StartsWith("INS") == true ? "INDIVIDUAL" : "CORPORATE"
                    };

                    // 6. Create renewal policy record
                    await InsertRenewalPolicyAsync(
                        newPolicyNo,
                        request.OriginalPolicyNo,
                        request,
                        originalPolicy,
                        lookupData,
                        insuredDetails,
                        connection,
                        transaction);

                    // 7. Create initial PolicyDetails for renewal
                    await SaveInitialRenewalPolicyDetailsAsync(
                        newPolicyNo,
                        request.OriginalPolicyNo,
                        request,
                        originalPolicy,
                        lookupData,
                        connection,
                        transaction);

                    // 8. Mark old policy calculations as "Old"
                    await MarkOldPolicyCalculationsAsOldAsync(request.OriginalPolicyNo, connection, transaction);

                    await transaction.CommitAsync();

                    // Determine calculation type from risk class
                    var calculationType = originalPolicy.RiskID?.Contains("FIRE") == true ? "fire" : "motor";

                    return new PolicyRenewalResponseDto
                    {
                        OriginalPolicyNo = request.OriginalPolicyNo,
                        NewPolicyNo = newPolicyNo,
                        RenewalDate = DateTime.Now,
                        NewStartDate = request.StartDate,
                        NewEndDate = request.EndDate,
                        InsuredName = $"{insuredDetails.Surname} {insuredDetails.FirstName} {insuredDetails.OtherNames}".Trim(),
                        SubRisk = originalPolicy.SubRisk,
                        Branch = originalPolicy.Branch,
                        Success = true,
                        Message = "Policy renewal created successfully",
                        NextStep = "Calculate renewal premium using the calculation endpoint",
                        CalculationEndpoint = $"/api/PolicyAdmin/{newPolicyNo}/renewal/calculate/{calculationType}"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating policy renewal for {OriginalPolicyNo}", request.OriginalPolicyNo);

                    return new PolicyRenewalResponseDto
                    {
                        Success = false,
                        Message = "Failed to create renewal",
                        ValidationErrors = { ex.Message }
                    };
                }
            }
            /// <summary>
            /// Check if a policy can be renewed
            /// </summary>
            public async Task<CanRenewResponseDto> CanRenewPolicyAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                try
                {
                    var policy = await GetProposalAsync(proposalNo);
                    if (policy == null)
                    {
                        return new CanRenewResponseDto
                        {
                            CanRenew = false,
                            Reason = "Policy not found"
                        };
                    }

                    // Check if already renewed
                    var (alreadyRenewed, renewalPolicyNo) = await CheckIfAlreadyRenewedAsync(proposalNo, connection, null);
                    if (alreadyRenewed)
                    {
                        return new CanRenewResponseDto
                        {
                            CanRenew = false,
                            Reason = "Policy already renewed",
                            AlreadyRenewed = true,
                            ExistingRenewalPolicyNo = renewalPolicyNo
                        };
                    }

                    var isExpired = policy.EndDate < DateTime.Now;

                    return new CanRenewResponseDto
                    {
                        CanRenew = true,
                        Reason = "Policy can be renewed",
                        PolicyEndDate = policy.EndDate,
                        IsExpired = isExpired
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking if policy can be renewed: {ProposalNo}", proposalNo);
                    return new CanRenewResponseDto
                    {
                        CanRenew = false,
                        Reason = "Error checking renewal eligibility"
                    };
                }
            }


            private async Task<LastCalculationSummaryDto> GetLastCalculationSummaryAsync(
                string proposalNo,
                SqlConnection connection)
            {
                var sql = @"
                    SELECT TOP 1 
                        pd.BizOption,
                        pd.TotalRiskValue,
                        pd.GrossPremium,
                        pd.SubmittedOn,
                        pd.SubmittedBy,
                        (SELECT COUNT(DISTINCT SectionID) 
                         FROM PolicyTempDetails 
                         WHERE PolicyNo = pd.PolicyNo
                         AND ISNULL(Tag, '') <> 'PROPOSAL_ADJUSTMENTS'
                         AND ISNULL(Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS')) as SectionCount,
                        (SELECT COUNT(*) 
                         FROM PolicyPremDetails 
                         WHERE PolicyNo = pd.PolicyNo) as RiskItemCount
                    FROM PolicyDetails pd
                    WHERE pd.PolicyNo = @PolicyNo AND pd.Deleted = 0
                    ORDER BY pd.SubmittedOn DESC";

                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new LastCalculationSummaryDto
                    {
                        CalculationType = reader.GetStringOrDefault("BizOption"),
                        TotalSumInsured = reader.GetValueOrDefault<decimal>("TotalRiskValue"),
                        TotalPremium = reader.GetValueOrDefault<decimal>("GrossPremium"),
                        CalculatedOn = reader.GetValueOrDefault<DateTime>("SubmittedOn"),
                        CalculatedBy = reader.GetStringOrDefault("SubmittedBy"),
                        SectionCount = reader.GetValueOrDefault<int>("SectionCount"),
                        RiskItemCount = reader.GetValueOrDefault<int>("RiskItemCount")
                    };
                }

                return null;
            }

            private async Task<(bool, string)> CheckIfAlreadyRenewedAsync(
                string proposalNo,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
                    SELECT PolicyNo 
                    FROM Policies 
                    WHERE CoPolicyNo = @PolicyNo AND Deleted = 0";

                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);

                var renewalPolicyNo = await cmd.ExecuteScalarAsync() as string;
                return (renewalPolicyNo != null, renewalPolicyNo);
            }

            private async Task InsertRenewalPolicyAsync(
                string newPolicyNo,
                string originalPolicyNo,
                CreatePolicyRenewalRequestDto request,
                ProposalDto originalPolicy,
                LookupData lookupData,
                InsuredDetailsData insuredDetails,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
                    INSERT INTO Policies (
                        PolicyNo, CoPolicyNo, TransDate, BranchID, Branch, BizSource, SourceType,
                        SubRiskID, SubRisk, PartyID, Party, MktStaffID, MktStaff,
                        InsuredID, InsSurname, InsFirstname, InsOthernames, InsAddress, 
                        InsMobilePhone, InsLandPhone, InsEmail, InsOccupation, 
                        StartDate, EndDate, ExRate, ExCurrency, 
                        ProportionRate, isProposal, Active, Deleted, SubmittedBy, SubmittedOn, 
                        TransSTATUS
                    ) VALUES (
                        @PolicyNo, @CoPolicyNo, @TransDate, @BranchID, @Branch, @BizSource, @SourceType,
                        @SubRiskID, @SubRisk, @PartyID, @Party, @MktStaffID, @MktStaff,
                        @InsuredID, @InsSurname, @InsFirstname, @InsOthernames, @InsAddress,
                        @InsMobilePhone, @InsLandPhone, @InsEmail, @InsOccupation,
                        @StartDate, @EndDate, @ExRate, @ExCurrency,
                        @ProportionRate, 1, 1, 0, @SubmittedBy, @SubmittedOn,
                        'PENDING'
                    )";

                using var command = new SqlCommand(sql, connection, transaction);
                command.CommandTimeout = 60;

                command.Parameters.AddWithValue("@PolicyNo", newPolicyNo);
                command.Parameters.AddWithValue("@CoPolicyNo", originalPolicyNo); // LINK TO ORIGINAL
                command.Parameters.AddWithValue("@TransDate", DateTime.Now);
                command.Parameters.AddWithValue("@BranchID", originalPolicy.BranchID);
                command.Parameters.AddWithValue("@Branch", lookupData.BranchName ?? "");
                command.Parameters.AddWithValue("@BizSource", request.BusinessType ?? originalPolicy.BizSource ?? "");
                command.Parameters.AddWithValue("@SourceType", request.BizSource ?? originalPolicy.SourceType ?? "DIRECT");
                command.Parameters.AddWithValue("@SubRiskID", originalPolicy.SubRiskID);
                command.Parameters.AddWithValue("@SubRisk", lookupData.SubRiskName ?? "");
                command.Parameters.AddWithValue("@PartyID", originalPolicy.PartyID);
                command.Parameters.AddWithValue("@Party", lookupData.PartyName ?? "");
                command.Parameters.AddWithValue("@MktStaffID", originalPolicy.MktStaffID);
                command.Parameters.AddWithValue("@MktStaff", lookupData.MktStaffName ?? "");
                command.Parameters.AddWithValue("@InsuredID", insuredDetails.InsuredID);
                command.Parameters.AddWithValue("@InsSurname", insuredDetails.Surname);
                command.Parameters.AddWithValue("@InsFirstname", insuredDetails.FirstName);
                command.Parameters.AddWithValue("@InsOthernames", insuredDetails.OtherNames);
                command.Parameters.AddWithValue("@InsAddress", insuredDetails.Address);
                command.Parameters.AddWithValue("@InsMobilePhone", insuredDetails.MobilePhone);
                command.Parameters.AddWithValue("@InsLandPhone", insuredDetails.LandPhone ?? "");
                command.Parameters.AddWithValue("@InsEmail", insuredDetails.Email);
                command.Parameters.AddWithValue("@InsOccupation", insuredDetails.Occupation);
                command.Parameters.AddWithValue("@StartDate", request.StartDate);
                command.Parameters.AddWithValue("@EndDate", request.EndDate);
                command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);
                command.Parameters.AddWithValue("@SubmittedBy", request.CreatedBy);
                command.Parameters.AddWithValue("@SubmittedOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }

            private async Task SaveInitialRenewalPolicyDetailsAsync(
                string newPolicyNo,
                string originalPolicyNo,
                CreatePolicyRenewalRequestDto request,
                ProposalDto originalPolicy,
                LookupData lookupData,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                // LEGACY: DECLARATION_NO auto number for the renewal's opening calculation
                var certOrDocNo = await NextCalcCertAsync(originalPolicy.BranchID, originalPolicy.SubRiskID, MapEndorseType("Renewal"));
                var fullName = $"{request.InsSurname ?? originalPolicy.InsSurname} {request.InsFirstname ?? originalPolicy.InsFirstname} {request.InsOthernames ?? originalPolicy.InsOthernames}".Trim();

                var sql = @"
                    INSERT INTO PolicyDetails (
                        PolicyNo, CoPolicyNo, CertOrDocNo, EntryDate, BizOption, InsuredName,
                        StartDate, EndDate, ExRate, ExCurrency, ProportionRate,
                        SumInsured, GrossPremium, SumInsuredFrgn, GrossPremiumFrgn,
                        ProRataDays, ProRataPremium, NetAmount, TotalRiskValue,
                        Field1, Field2, Field50,
                        SubmittedBy, SubmittedOn, Active, Deleted
                    ) VALUES (
                        @PolicyNo, @CoPolicyNo, @CertOrDocNo, @EntryDate, @BizOption, @InsuredName,
                        @StartDate, @EndDate, @ExRate, @ExCurrency, @ProportionRate,
                        0, 0, 0, 0,
                        @ProRataDays, 0, 0, 0,
                        @Field1, @Field2, @Field50,
                        @SubmittedBy, @SubmittedOn, 1, 0
                    )";

                using var command = new SqlCommand(sql, connection, transaction);
                command.CommandTimeout = 60;

                command.Parameters.AddWithValue("@PolicyNo", newPolicyNo);
                command.Parameters.AddWithValue("@CoPolicyNo", originalPolicyNo); // LINK TO ORIGINAL
                command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                command.Parameters.AddWithValue("@EntryDate", DateTime.Now);
                command.Parameters.AddWithValue("@BizOption", "Renewal"); // MARK AS RENEWAL
                command.Parameters.AddWithValue("@InsuredName", fullName);
                command.Parameters.AddWithValue("@StartDate", request.StartDate);
                command.Parameters.AddWithValue("@EndDate", request.EndDate);
                command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);
                command.Parameters.AddWithValue("@ProRataDays", (request.EndDate - request.StartDate).Days);
                command.Parameters.AddWithValue("@Field1", "RENEWAL_CREATED");
                command.Parameters.AddWithValue("@Field2", $"Renewal created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                command.Parameters.AddWithValue("@Field50", "RENEWAL");
                command.Parameters.AddWithValue("@SubmittedBy", request.CreatedBy);
                command.Parameters.AddWithValue("@SubmittedOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }

            private async Task MarkOldPolicyCalculationsAsOldAsync(
          string originalPolicyNo,
          SqlConnection connection,
          SqlTransaction transaction)
            {
                var sql = @"
            UPDATE PolicyDetails 
            SET BizOption = CASE 
                WHEN BizOption = 'Additional' THEN 'Old-Additional'
                ELSE 'Old'
            END,
                ModifiedBy = 'SYSTEM',
                ModifiedOn = GETDATE()
            WHERE PolicyNo = @PolicyNo AND Deleted = 0";

                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@PolicyNo", originalPolicyNo);
                await cmd.ExecuteNonQueryAsync();
            }


            // OPTIMIZED: Single query for all lookups
            private async Task<LookupData> GetLookupDataOptimizedAsync(
                EnhancedCreateProposalRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
            SELECT 
        b.Description as BranchName,
        sr.SubRisk as SubRiskName,
        p.Party as PartyName,
        ms.StaffName as MktStaffName
    FROM (SELECT @BranchID as BranchID, @SubRiskID as SubRiskID, 
                 @PartyID as PartyID, @MktStaffID as MktStaffID) inp
    LEFT JOIN Branches b ON inp.BranchID = b.BranchID
    LEFT JOIN SubRisks sr ON inp.SubRiskID = sr.SubRiskID
    LEFT JOIN Parties p ON inp.PartyID = p.PartyID
    LEFT JOIN MktStaffs ms ON inp.MktStaffID = ms.MktStaffID";

                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.CommandTimeout = 60; // 60 seconds timeout

                cmd.Parameters.AddWithValue("@BranchID", request.BranchID);
                cmd.Parameters.AddWithValue("@SubRiskID", request.SubriskID);
                cmd.Parameters.AddWithValue("@PartyID", request.PartyID);
                cmd.Parameters.AddWithValue("@MktStaffID", request.MktStaffID);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new LookupData
                    {
                        BranchName = reader.GetStringOrDefault("BranchName"),
                        SubRiskName = reader.GetStringOrDefault("SubRiskName"),
                        PartyName = reader.GetStringOrDefault("PartyName"),
                        MktStaffName = reader.GetStringOrDefault("MktStaffName")
                    };
                }

                return new LookupData(); // Return empty if not found
            }

            // Update GetInsuredDetailsFromInsuredIdAsync to add timeout
            private async Task<InsuredDetailsData> GetInsuredDetailsFromInsuredIdAsync(
                string insuredId,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var query = @"
            SELECT InsuredID, SurName, OtherNames, Address, 
                   MobilePhone, Landphone, Email, Occupation, ClientType
            FROM InsuredClients WITH (NOLOCK)
            WHERE InsuredID = @InsuredID AND Active = 1 AND Deleted = 0";

                using var cmd = new SqlCommand(query, connection, transaction);
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddWithValue("@InsuredID", insuredId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new InsuredDetailsData
                    {
                        InsuredID = reader["InsuredID"].ToString(),
                        // LEGACY: InsuredClients uses a two-part name (SurName + OtherNames)
                        // and calls the discriminator ClientType. There is no FirstName column.
                        Surname = reader["SurName"].ToString(),
                        FirstName = "",
                        OtherNames = reader["OtherNames"].ToString(),
                        Address = reader["Address"].ToString(),
                        MobilePhone = reader["MobilePhone"].ToString(),
                        LandPhone = reader["Landphone"].ToString(),
                        Email = reader["Email"].ToString(),
                        Occupation = reader["Occupation"].ToString(),
                        InsuredType = reader["ClientType"].ToString()
                    };
                }

                return null;
            }

            private async Task InsertPolicyWithInsuredDetailsAsync(
         string policyNo,
         EnhancedCreateProposalRequestDto request,
         LookupData lookupData,
         InsuredDetailsData insuredDetails,
         SqlConnection connection,
         SqlTransaction transaction)
            {
                var sql = @"
            INSERT INTO Policies (
                PolicyNo, TransDate, BranchID, Branch, BizSource, SourceType,
                SubRiskID, SubRisk, PartyID, Party, MktStaffID, MktStaff,
                InsuredID, InsSurname, InsFirstname, InsOthernames, InsAddress, 
                InsMobilePhone, InsLandPhone, InsEmail, InsOccupation, 
                StartDate, EndDate, ExRate, ExCurrency, 
                ProportionRate, isProposal, Active, Deleted, SubmittedBy, SubmittedOn, 
                TransSTATUS
            ) VALUES (
                @PolicyNo, @TransDate, @BranchID, @Branch, @BizSource, @SourceType,
                @SubRiskID, @SubRisk, @PartyID, @Party, @MktStaffID, @MktStaff,
                @InsuredID, @InsSurname, @InsFirstname, @InsOthernames, @InsAddress,
                @InsMobilePhone, @InsLandPhone, @InsEmail, @InsOccupation,
                @StartDate, @EndDate, @ExRate, @ExCurrency,
                @ProportionRate, 1, 1, 0, @SubmittedBy, @SubmittedOn,
                'PENDING'
            )";

                using var command = new SqlCommand(sql, connection, transaction);
                command.CommandTimeout = 60;

                command.Parameters.AddWithValue("@PolicyNo", policyNo);
                command.Parameters.AddWithValue("@TransDate", DateTime.Now);
                command.Parameters.AddWithValue("@BranchID", request.BranchID);
                command.Parameters.AddWithValue("@Branch", lookupData.BranchName ?? "");

                // UPDATED: BusinessType goes to BizSource, BizSource goes to SourceType
                command.Parameters.AddWithValue("@BizSource", request.BusinessType ?? "");
                command.Parameters.AddWithValue("@SourceType", request.BizSource ?? "DIRECT");

                command.Parameters.AddWithValue("@SubRiskID", request.SubriskID);
                command.Parameters.AddWithValue("@SubRisk", lookupData.SubRiskName ?? "");
                command.Parameters.AddWithValue("@PartyID", request.PartyID);
                command.Parameters.AddWithValue("@Party", lookupData.PartyName ?? "");
                command.Parameters.AddWithValue("@MktStaffID", request.MktStaffID);
                command.Parameters.AddWithValue("@MktStaff", lookupData.MktStaffName ?? "");

                command.Parameters.AddWithValue("@InsuredID", insuredDetails.InsuredID);
                command.Parameters.AddWithValue("@InsSurname", insuredDetails.Surname);
                command.Parameters.AddWithValue("@InsFirstname", insuredDetails.FirstName);
                command.Parameters.AddWithValue("@InsOthernames", insuredDetails.OtherNames);
                command.Parameters.AddWithValue("@InsAddress", insuredDetails.Address);
                command.Parameters.AddWithValue("@InsMobilePhone", insuredDetails.MobilePhone);
                command.Parameters.AddWithValue("@InsLandPhone", insuredDetails.LandPhone);
                command.Parameters.AddWithValue("@InsEmail", insuredDetails.Email);
                command.Parameters.AddWithValue("@InsOccupation", insuredDetails.Occupation);

                command.Parameters.AddWithValue("@StartDate", request.StartDate);
                command.Parameters.AddWithValue("@EndDate", request.EndDate);
                command.Parameters.AddWithValue("@ExRate", request.ExRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);
                command.Parameters.AddWithValue("@SubmittedBy", "SYSTEM");
                command.Parameters.AddWithValue("@SubmittedOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }

            /// <summary>
            /// Get all fire calculations for a proposal
            /// </summary>
            /// <summary>
            /// Get all fire calculations for a proposal
            /// FIXED: Now properly retrieves calculated values instead of initial zeros
            /// </summary>
            public async Task<List<FireCalculationListItemDto>> GetAllFireCalculationsAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var calculations = new List<FireCalculationListItemDto>();

                // and only return actual calculations
                var sql = @"
            SELECT 
                pd.DetailID,
                pd.PolicyNo,
                pd.CertOrDocNo,
                pd.SubmittedOn as CalculatedOn,
                pd.SubmittedBy as CalculatedBy,
                pd.BizOption as CalculationType,
                pd.TotalRiskValue as TotalSumInsured,
                pd.GrossPremium as TotalPremium,
                pd.GrossPremium as NetPremium,
                pd.ProRataPremium,
                pd.NetAmount as FinalPremiumDue,
                pd.Active,
                pd.Deleted,
                pd.Field1 as Remarks,
                (SELECT COUNT(DISTINCT SectionID) 
                 FROM PolicyTempDetails 
                 WHERE PolicyNo = pd.PolicyNo 
                 AND Field20 = pd.CertOrDocNo 
                 AND ISNULL(Tag, '') <> 'PROPOSAL_ADJUSTMENTS'
                 AND ISNULL(Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS')) as SectionCount
            FROM PolicyDetails pd
            WHERE pd.PolicyNo = @ProposalNo 
       
            ORDER BY pd.SubmittedOn DESC";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ProposalNo", proposalNo);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var item = new FireCalculationListItemDto
                    {
                        DetailID = reader.GetStringOrDefault("DetailID"),
                        ProposalNo = reader.GetStringOrDefault("PolicyNo"),
                        CertOrDocNo = reader.GetStringOrDefault("CertOrDocNo"),
                        CalculatedOn = reader.GetValueOrDefault<DateTime>("CalculatedOn"),
                        CalculatedBy = reader.GetStringOrDefault("CalculatedBy"),
                        CalculationType = reader.GetStringOrDefault("CalculationType"),
                        TotalSumInsured = reader.GetValueOrDefault<decimal>("TotalSumInsured"),
                        TotalPremium = reader.GetValueOrDefault<decimal>("TotalPremium"),
                        NetPremium = reader.GetValueOrDefault<decimal>("NetPremium"),
                        ProRataPremium = reader.GetValueOrDefault<decimal>("ProRataPremium"),
                        SectionCount = reader.GetValueOrDefault<int>("SectionCount"),
                        Remarks = reader.GetStringOrDefault("Remarks")
                    };

                    // Determine status
                    if (reader.GetValueOrDefault<int>("Deleted") == 1)
                    {
                        item.Status = "DELETED";
                    }
                    else if (reader.GetValueOrDefault<int>("Active") == 1)
                    {
                        item.Status = "ACTIVE";
                    }
                    else
                    {
                        item.Status = "SUPERSEDED";
                    }

                    calculations.Add(item);
                }

                return calculations;
            }

            /// <summary>
            /// Get all branches (optionally filtered by company, region, or state)
            /// </summary>
            public async Task<List<object>> GetAllBranchesAsync(string regionId = null, string stateId = null)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var branches = new List<object>();

                var sql = @"
            SELECT BranchID, RegionID, StateID, BranchID2, Description, 
                   Manager, Address, MobilePhone, LandPhone, Email, Fax, 
                   Deleted, Active, Remarks, SubmittedBy, SubmittedOn, ModifiedBy, ModifiedOn
            FROM Branches
            WHERE Deleted = 0";

                // LEGACY: Branches has no CompanyID column in this database.
                var conditions = new List<string>();
                if (!string.IsNullOrEmpty(regionId))
                {
                    conditions.Add("RegionID = @RegionId");
                }
                if (!string.IsNullOrEmpty(stateId))
                {
                    conditions.Add("StateID = @StateId");
                }

                if (conditions.Any())
                {
                    sql += " AND " + string.Join(" AND ", conditions);
                }

                sql += " ORDER BY Description";

                using var command = new SqlCommand(sql, connection);
                command.CommandTimeout = 60;

                if (!string.IsNullOrEmpty(regionId))
                {
                    command.Parameters.AddWithValue("@RegionId", regionId);
                }
                if (!string.IsNullOrEmpty(stateId))
                {
                    command.Parameters.AddWithValue("@StateId", stateId);
                }

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    branches.Add(new
                    {
                        BranchID = reader.GetStringOrDefault("BranchID"),
                        RegionID = reader.GetStringOrDefault("RegionID"),
                        StateID = reader.GetStringOrDefault("StateID"),
                        BranchID2 = reader.GetStringOrDefault("BranchID2"),
                        Description = reader.GetStringOrDefault("Description"),
                        Manager = reader.GetStringOrDefault("Manager"),
                        Address = reader.GetStringOrDefault("Address"),
                        MobilePhone = reader.GetStringOrDefault("MobilePhone"),
                        LandPhone = reader.GetStringOrDefault("LandPhone"),
                        Email = reader.GetStringOrDefault("Email"),
                        Fax = reader.GetStringOrDefault("Fax"),
                        Deleted = reader.GetValueOrDefault<int>("Deleted"),
                        Active = reader.GetValueOrDefault<int>("Active"),
                        Remarks = reader.GetStringOrDefault("Remarks"),
                        SubmittedBy = reader.GetStringOrDefault("SubmittedBy"),
                        SubmittedOn = reader.GetValueOrDefault<DateTime>("SubmittedOn"),
                        ModifiedBy = reader.GetStringOrDefault("ModifiedBy"),
                        ModifiedOn = reader.GetNullableValue<DateTime>("ModifiedOn")
                    });
                }

                return branches;
            }

            /// <summary>
            /// Get all wax codes (optionally filtered by sub-risk)
            /// </summary>
            public async Task<List<object>> GetAllWaxCodesAsync(string subRiskId = null)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var waxCodes = new List<object>();

                var sql = @"
SELECT WaxID, SubRiskID, EntitledTo, EntitledToB, Limitiation, LimitationB,
       VehUsage, Covertype, [Delete], SubmitBy, SubmitOn, ModifiedBy, ModifiedOn
FROM WaxCodes
WHERE [Delete] = 0";

                if (!string.IsNullOrEmpty(subRiskId))
                {
                    sql += " AND SubRiskID = @SubRiskId";
                }

                sql += " ORDER BY WaxID";

                using var command = new SqlCommand(sql, connection);
                command.CommandTimeout = 60;

                if (!string.IsNullOrEmpty(subRiskId))
                {
                    command.Parameters.AddWithValue("@SubRiskId", subRiskId);
                }

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    waxCodes.Add(new
                    {
                        WaxID = reader.GetStringOrDefault("WaxID"),
                        SubRiskID = reader.GetStringOrDefault("SubRiskID"),
                        EntitledTo = reader.GetStringOrDefault("EntitledTo"),
                        EntitledToB = reader.GetStringOrDefault("EntitledToB"),
                        Limitiation = reader.GetStringOrDefault("Limitiation"),
                        LimitationB = reader.GetStringOrDefault("LimitationB"),
                        VehUsage = reader.GetStringOrDefault("VehUsage"),
                        Covertype = reader.GetStringOrDefault("Covertype"),
                        Delete = reader.GetValueOrDefault<int>("Delete"),
                        SubmitBy = reader.GetStringOrDefault("SubmitBy"),
                        SubmitOn = reader.GetValueOrDefault<DateTime>("SubmitOn"),
                        ModifiedBy = reader.GetStringOrDefault("ModifiedBy"),
                        ModifiedOn = reader.GetNullableValue<DateTime>("ModifiedOn")
                    });
                }

                return waxCodes;
            }
            /// <summary>
            /// Get all marketing groups (optionally filtered by branch)
            /// </summary>
            public async Task<List<object>> GetAllGroupsAsync(string branchId = null)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var groups = new List<object>();

                var sql = @"
            SELECT MktGroupID, BranchID, GroupName, Description, GroupHead, 
                   Deleted, Active, SubmittedBy, SubmittedOn, ModifiedBy, ModifiedOn
            FROM MktGroups
            WHERE Deleted = 0";

                if (!string.IsNullOrEmpty(branchId))
                {
                    sql += " AND BranchID = @BranchId";
                }

                sql += " ORDER BY GroupName";

                using var command = new SqlCommand(sql, connection);
                command.CommandTimeout = 60;

                if (!string.IsNullOrEmpty(branchId))
                {
                    command.Parameters.AddWithValue("@BranchId", branchId);
                }

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    groups.Add(new
                    {
                        MktGroupID = reader.GetStringOrDefault("MktGroupID"),
                        BranchID = reader.GetStringOrDefault("BranchID"),
                        GroupName = reader.GetStringOrDefault("GroupName"),
                        Description = reader.GetStringOrDefault("Description"),
                        GroupHead = reader.GetStringOrDefault("GroupHead"),
                        Deleted = reader.GetValueOrDefault<int>("Deleted"),
                        Active = reader.GetValueOrDefault<int>("Active"),
                        SubmittedBy = reader.GetStringOrDefault("SubmittedBy"),
                        SubmittedOn = reader.GetValueOrDefault<DateTime>("SubmittedOn"),
                        ModifiedBy = reader.GetStringOrDefault("ModifiedBy"),
                        ModifiedOn = reader.GetNullableValue<DateTime>("ModifiedOn")
                    });
                }

                return groups;
            }
            /// <summary>
            /// Get all occupations (optionally filtered by tag)
            /// </summary>
            public async Task<List<object>> GetAllOccupationsAsync(string tag = null)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var occupations = new List<object>();

                var sql = @"
    SELECT Occupation, Description, Tag
    FROM undOccupations";

                if (!string.IsNullOrEmpty(tag))
                {
                    sql += " WHERE Tag = @Tag";
                }

                sql += " ORDER BY Occupation";

                using var command = new SqlCommand(sql, connection);
                command.CommandTimeout = 60;

                if (!string.IsNullOrEmpty(tag))
                {
                    command.Parameters.AddWithValue("@Tag", tag);
                }

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    occupations.Add(new
                    {
                        Occupation = reader.GetStringOrDefault("Occupation"),
                        Description = reader.GetStringOrDefault("Description"),
                        Tag = reader.GetStringOrDefault("Tag")
                    });
                }

                return occupations;
            }
            /// <summary>
            /// Get all marketing channels
            /// </summary>
            public async Task<List<object>> GetAllChannelsAsync()
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var channels = new List<object>();

                var sql = @"
            SELECT ChannelID, Channels, Description
            FROM MktChannels
            ORDER BY Channels";

                using var command = new SqlCommand(sql, connection);
                command.CommandTimeout = 60;

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    channels.Add(new
                    {
                        ChannelID = reader.GetStringOrDefault("ChannelID"),
                        Channels = reader.GetStringOrDefault("Channels"),
                        Description = reader.GetStringOrDefault("Description")
                    });
                }

                return channels;
            }

            /// <summary>
            /// FIXED: Get breakdown for a specific fire calculation by DetailID
            /// The issue was in GetCalculatedSectionsByDetailIDAsync where Field10 (nvarchar) 
            /// was being used in a query that expected bigint
            /// </summary>
            public async Task<FireCalculationBreakdownDto> GetFireCalculationBreakdownByDetailIDAsync(
                string proposalNo,
                string detailID)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                try
                {
                    // Get the specific PolicyDetail record
                    var calculationSql = @"
                SELECT * FROM PolicyDetails 
                WHERE PolicyNo = @ProposalNo 
                AND DetailID = @DetailID 
                AND Deleted = 0";

                    PolicyDetailDto calculation = null;
                    using (var command = new SqlCommand(calculationSql, connection))
                    {
                        command.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        command.Parameters.AddWithValue("@DetailID", detailID);

                        using var reader = await command.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            calculation = MapPolicyDetailFromReader(reader);
                        }
                    }

                    if (calculation == null)
                    {
                        return null;
                    }

                    // FIXED: Get sections for this specific calculation using CertOrDocNo as link
                    var sections = await GetCalculatedSectionsByDetailIDAsync(
                        proposalNo,
                        calculation.CertOrDocNo,
                        connection);

                    // LEGACY: a calculation with no section rows is still a valid calculation
                    // (e.g. legacy motor policies store vehicles in PolicyPremDetails and have
                    // no fire-style sections). Returning null here made the API answer
                    // "calculation not found" for records that clearly exist.
                    if (!sections.Any())
                    {
                        _logger.LogInformation(
                            "No section rows found for {ProposalNo} / {CertOrDocNo} — returning breakdown with empty sections",
                            proposalNo, calculation.CertOrDocNo);
                    }

                    // Get proposal-level adjustments
                    var proposalAdjustments = await GetProposalLevelAdjustmentsByDetailIDAsync(
                        proposalNo,
                        calculation.CertOrDocNo,
                        connection);

                    var proposal = await GetProposalAsync(proposalNo);
                    if (proposal == null)
                    {
                        _logger.LogWarning("PolicyDetail found but parent Proposal {ProposalNo} not found", proposalNo);
                        return null;
                    }

                    decimal otherDiscountsRate = calculation.A10;
                    decimal otherLoadingsRate = calculation.A11;

                    return new FireCalculationBreakdownDto
                    {
                        ProposalNo = proposalNo,
                        CalculatedOn = calculation.SubmittedOn,
                        CalculatedBy = calculation.SubmittedBy ?? "Unknown",
                        CalculationType = calculation.BizOption ?? "CALCULATED",
                        Remarks = calculation.Field1,

                        Inputs = new FireCalculationInputsDto
                        {
                            ProportionRate = calculation.ProportionRate,
                            Currency = calculation.ExCurrency ?? "NAIRA",
                            ExchangeRate = calculation.ExRate,
                            CoverDays = calculation.ProRataDays > 0 ? calculation.ProRataDays : 365,
                            StartDate = calculation.StartDate,
                            EndDate = calculation.EndDate,
                            SectionInputs = sections,
                            ProposalAdjustments = new ProposalAdjustmentInputsDto
                            {
                                OtherDiscountsRate = otherDiscountsRate,
                                OtherLoadingsRate = otherLoadingsRate
                            }
                        },

                        CalculationSteps = new FireCalculationStepsDto
                        {
                            SectionCalculations = sections.Select(s => new SectionCalculationBreakdownDto
                            {
                                SectionID = s.SectionID,
                                SectionName = s.SectionName,
                                RiskItemCalculations = new RiskItemCalculationBreakdownDto
                                {
                                    Items = s.RiskItems,
                                    Totals = new RiskItemTotalsDto
                                    {
                                        TotalActualValue = s.RiskItems.Sum(r => r.ActualValue),
                                        TotalShareValue = s.RiskItems.Sum(r => r.ShareValue),
                                        TotalSharePremium = s.RiskItems.Sum(r => r.PremiumValue),
                                        TotalNetPremiumAfterDiscounts = s.RiskItems.Sum(r => r.NetPremiumAfterDiscounts),
                                        ItemCount = s.RiskItems.Count
                                    }
                                },
                                SectionAdjustments = s.SectionAdjustments
                            }).ToList(),

                            ProposalAdjustments = proposalAdjustments,

                            ProRataCalculations = new ProRataBreakdownDto
                            {
                                NetPremiumBeforeProRata = calculation.GrossPremium,
                                CoverDays = calculation.ProRataDays > 0 ? calculation.ProRataDays : 365,
                                StandardDays = 365,
                                ProRataFactor = calculation.ProRataDays > 0 ? calculation.ProRataDays / 365m : 1m,
                                ProRataPremium = calculation.ProRataPremium,
                                FinalPremiumDue = calculation.NetAmount,
                                IsProRated = calculation.ProRataDays < 365,
                                ProRataFormula = calculation.ProRataDays < 365
                                    ? $"({calculation.GrossPremium:N2} × {calculation.ProRataDays} ÷ 365) = {calculation.ProRataPremium:N2}"
                                    : "No pro-rata applied (full year coverage)"
                            }
                        },

                        FinalResults = new FireCalculationResultsDto
                        {
                            TotalSumInsured = calculation.TotalRiskValue,
                            TotalPremium = calculation.GrossPremium,
                            NetPremium = calculation.GrossPremium,
                            ProRataPremium = calculation.ProRataPremium,
                            ShareSumInsured = calculation.SumInsured,
                            SharePremium = calculation.NetAmount,
                            ForeignSumInsured = calculation.SumInsuredFrgn,
                            ForeignPremium = calculation.GrossPremiumFrgn,
                            ForeignCurrency = calculation.ExCurrency,
                            OverallPremiumRate = calculation.TotalRiskValue > 0
                                ? (calculation.GrossPremium / calculation.TotalRiskValue) * 100 : 0,
                            ProportionShare = calculation.ProportionRate
                        }
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting fire calculation breakdown for DetailID {DetailID}", detailID);
                    throw;
                }
            }


            private async Task<List<SectionInputBreakdownDto>> GetCalculatedSectionsByDetailIDAsync(
          string proposalNo,
          string certOrDocNo,
          SqlConnection connection)
            {
                var sections = new List<SectionInputBreakdownDto>();

                // UPDATED: Retrieve TransGUID to uniquely identify each section instance
                var sectionSql = @"
            SELECT SectionID, ContentSection, Location, RiskSum, SectionPremium,
                   A1, A2, A3, A4, A5, A6, A7, A8, A9, A11, A12, A13, A14, A15, A16, 
                   Field11, Field12, A17,
                   Field26, Field7, Field8,
                   ENDTNum,   -- LEGACY: the section's declaration number
                   TransGUID,  -- CRITICAL: Retrieve this
                   SubmitOn, SubmitBy
            FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo 
            AND Field20 = @CertOrDocNo
            AND ISNULL(Tag, '') <> 'PROPOSAL_ADJUSTMENTS'
            AND ISNULL(Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS') -- LEGACY: legacy rows store a cert number in Field1, not 'SECTION'
            ORDER BY SubmitOn, SectionID";

                using var sectionCommand = new SqlCommand(sectionSql, connection);
                sectionCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);
                sectionCommand.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo ?? "");

                // CRITICAL FIX: Read all sections into memory FIRST before querying risk items
                var sectionDataList = new List<(SectionInputBreakdownDto Section, string TransGUID, string DeclarationNo)>();

                using (var sectionReader = await sectionCommand.ExecuteReaderAsync())
                {
                    while (await sectionReader.ReadAsync())
                    {
                        var section = new SectionInputBreakdownDto
                        {
                            SectionID = sectionReader.GetStringOrDefault("SectionID"),
                            SectionName = sectionReader.GetStringOrDefault("ContentSection"),
                            Location = sectionReader.GetStringOrDefault("Location"),
                            SectionSumInsured = sectionReader.GetValueOrDefault<decimal>("A1"),
                            SectionGrossPremium = sectionReader.GetValueOrDefault<decimal>("A2"),
                            SectionNetPremium = sectionReader.GetValueOrDefault<decimal>("A11"),

                            // NEW: Optional section information fields
                            Field26 = sectionReader.GetStringOrDefault("Field26"),
                            Field7 = sectionReader.GetStringOrDefault("Field7"),
                            Field8 = sectionReader.GetStringOrDefault("Field8"),

                            SectionAdjustments = new SectionAdjustmentsBreakdownDto
                            {
                                StartingPremium = sectionReader.GetValueOrDefault<decimal>("A2"),
                                FinalNetPremium = sectionReader.GetValueOrDefault<decimal>("A11"),
                                DiscountsApplied = new List<AdjustmentDetailDto>(),
                                LoadingsApplied = new List<AdjustmentDetailDto>()
                            }
                        };

                        var sectionTransGUID = sectionReader.GetStringOrDefault("TransGUID");
                        var sectionDeclarationNo = sectionReader.GetStringOrDefault("ENDTNum"); // LEGACY: link to risk items

                        // Get adjustment rates
                        decimal specialDiscountRate = sectionReader.GetValueOrDefault<decimal>("A12");
                        decimal deductibleDiscountRate = sectionReader.GetValueOrDefault<decimal>("A13");
                        decimal spreadDiscountRate = sectionReader.GetValueOrDefault<decimal>("A14");
                        decimal ltaDiscountRate = sectionReader.GetValueOrDefault<decimal>("A15");
                        decimal theftLoadingRate = sectionReader.GetValueOrDefault<decimal>("A16");

                        decimal srccLoadingRate = 0;
                        decimal otherLoading2Rate = 0;
                        if (!string.IsNullOrEmpty(sectionReader.GetStringOrDefault("Field11")))
                        {
                            decimal.TryParse(sectionReader.GetStringOrDefault("Field11"), out srccLoadingRate);
                        }
                        if (!string.IsNullOrEmpty(sectionReader.GetStringOrDefault("Field12")))
                        {
                            decimal.TryParse(sectionReader.GetStringOrDefault("Field12"), out otherLoading2Rate);
                        }

                        // Add discounts if applied
                        if (sectionReader.GetValueOrDefault<decimal>("A3") > 0)
                        {
                            section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                            {
                                Name = "Special Discount",
                                Type = "DISCOUNT",
                                Rate = specialDiscountRate,
                                Amount = sectionReader.GetValueOrDefault<decimal>("A3"),
                                SequenceOrder = 1
                            });
                        }

                        if (sectionReader.GetValueOrDefault<decimal>("A4") > 0)
                        {
                            section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                            {
                                Name = "Deductible Discount",
                                Type = "DISCOUNT",
                                Rate = deductibleDiscountRate,
                                Amount = sectionReader.GetValueOrDefault<decimal>("A4"),
                                SequenceOrder = 2
                            });
                        }

                        if (sectionReader.GetValueOrDefault<decimal>("A5") > 0)
                        {
                            section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                            {
                                Name = "Spread Discount",
                                Type = "DISCOUNT",
                                Rate = spreadDiscountRate,
                                Amount = sectionReader.GetValueOrDefault<decimal>("A5"),
                                SequenceOrder = 3
                            });
                        }

                        if (sectionReader.GetValueOrDefault<decimal>("A6") > 0)
                        {
                            section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                            {
                                Name = "LTA Discount",
                                Type = "DISCOUNT",
                                Rate = ltaDiscountRate,
                                Amount = sectionReader.GetValueOrDefault<decimal>("A6"),
                                SequenceOrder = 4
                            });
                        }

                        // Add loadings if applied
                        if (sectionReader.GetValueOrDefault<decimal>("A7") > 0)
                        {
                            section.SectionAdjustments.LoadingsApplied.Add(new AdjustmentDetailDto
                            {
                                Name = "Theft Loading",
                                Type = "LOADING",
                                Rate = theftLoadingRate,
                                Amount = sectionReader.GetValueOrDefault<decimal>("A7"),
                                SequenceOrder = 1
                            });
                        }

                        if (sectionReader.GetValueOrDefault<decimal>("A8") > 0)
                        {
                            section.SectionAdjustments.LoadingsApplied.Add(new AdjustmentDetailDto
                            {
                                Name = "SRCC Loading",
                                Type = "LOADING",
                                Rate = srccLoadingRate,
                                Amount = sectionReader.GetValueOrDefault<decimal>("A8"),
                                SequenceOrder = 2
                            });
                        }

                        if (sectionReader.GetValueOrDefault<decimal>("A9") > 0)
                        {
                            section.SectionAdjustments.LoadingsApplied.Add(new AdjustmentDetailDto
                            {
                                Name = "Other Loading 2",
                                Type = "LOADING",
                                Rate = otherLoading2Rate,
                                Amount = sectionReader.GetValueOrDefault<decimal>("A9"),
                                SequenceOrder = 3
                            });
                        }

                        // Store section with its TransGUID for later processing
                        sectionDataList.Add((section, sectionTransGUID, sectionDeclarationNo));
                    }
                } // CRITICAL: Reader is now closed before we query risk items

                // NOW fetch risk items for each section (reader is closed, so we can execute new queries)
                foreach (var (section, transGUID, declarationNo) in sectionDataList)
                {
                    // LEGACY (CreateTempGrid): items are fetched by the section's declaration number
                    section.RiskItems = await GetRiskItemsForSectionByDeclarationNoAsync(
                        proposalNo,
                        declarationNo,
                        certOrDocNo,
                        transGUID,
                        connection);

                    sections.Add(section);
                }

                return sections;
            }



            /// <summary>
            /// LEGACY read (CreateTempGrid / SelectThisByPolicyNoFire): risk items belong to
            /// a section through Field10 = the section's declaration number
            /// (PolicyTempDetails.ENDTNum). Falls back to the old TransGUID linkage for rows
            /// saved before the legacy numbering was wired in (their section ENDTNum is empty).
            /// </summary>
            private async Task<List<CalculatedRiskItemBreakdownDto>> GetRiskItemsForSectionByDeclarationNoAsync(
                string proposalNo,
                string sectionDeclarationNo,
                string certOrDocNo,
                string sectionTransGUID,
                SqlConnection connection)
            {
                // BACKWARD COMPATIBILITY: rows saved by the pre-legacy code have no
                // declaration number on the section — use the old TransGUID linkage.
                if (string.IsNullOrWhiteSpace(sectionDeclarationNo))
                {
                    return string.IsNullOrWhiteSpace(certOrDocNo)
                        ? await GetRiskItemsForSectionByTransGUIDAsyncNocert(proposalNo, sectionTransGUID, connection)
                        : await GetRiskItemsForSectionByTransGUIDAsync(proposalNo, certOrDocNo, sectionTransGUID, connection);
                }

                var riskItems = new List<CalculatedRiskItemBreakdownDto>();

                // LEGACY: declaration numbers are unique per section, so PolicyNo + Field10
                // is an exact match. No Tag filter — legacy rows carry Tag = 'NEW' while the
                // newer save paths use 'RISK_ITEM'.
                var riskItemSql = @"
            SELECT SMICode, ContentDesc, Location, ENDTNum,
                   A1 as ActualValue, A2 as ItemRate, A3 as ActualPremium,
                   A4 as ShareValue, A5 as PremiumValue, A6 as MultiplyRate,
                   A7 as StockSumInsured, A8 as StockGrossPremium, A9 as StockDiscountAmount,
                   A10 as StockNetPremium, A11 as TotalSumInsured, A12 as TotalGrossPremium,
                   A13 as FeaDiscountRate, A14 as FeaDiscountAmount, A15 as NetPremiumAfterDiscounts,
                   A16 as ItemNo, A17 as StockDiscountRate,
                   Section as SectionID,
                   ISNULL(Field6, '') as StockCode, 
                   ISNULL(Field7, '') as StockDescription, 
                   ISNULL(Field8, '0') as StockSumInsuredStr, 
                   ISNULL(Field9, '0') as StockRateStr
            FROM PolicyPremDetails 
            WHERE PolicyNo = @PolicyNo 
            AND Field10 = @SectionDeclarationNo  -- LEGACY: item -> section link
            ORDER BY A16";

                using var itemCommand = new SqlCommand(riskItemSql, connection);
                itemCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);
                itemCommand.Parameters.AddWithValue("@SectionDeclarationNo", sectionDeclarationNo);

                using var itemReader = await itemCommand.ExecuteReaderAsync();
                while (await itemReader.ReadAsync())
                {
                    var calculatedItem = new CalculatedRiskItemBreakdownDto
                    {
                        ItemNo = itemReader.GetValueOrDefault<int>("ItemNo"),
                        SMICode = itemReader.GetStringOrDefault("SMICode"),
                        ItemDescription = itemReader.GetStringOrDefault("ContentDesc"),
                        ActualValue = itemReader.GetValueOrDefault<decimal>("ActualValue"),
                        ItemRate = itemReader.GetValueOrDefault<decimal>("ItemRate"),
                        MultiplyRate = itemReader.GetValueOrDefault<decimal>("MultiplyRate"),
                        SectionId = itemReader.GetStringOrDefault("SectionID"),
                        Location = itemReader.GetStringOrDefault("Location"),
                        ActualPremium = itemReader.GetValueOrDefault<decimal>("ActualPremium"),
                        ShareValue = itemReader.GetValueOrDefault<decimal>("ShareValue"),
                        PremiumValue = itemReader.GetValueOrDefault<decimal>("PremiumValue"),
                        StockSumInsured = itemReader.GetValueOrDefault<decimal>("StockSumInsured"),
                        StockGrossPremium = itemReader.GetValueOrDefault<decimal>("StockGrossPremium"),
                        StockDiscountRate = itemReader.GetValueOrDefault<decimal>("StockDiscountRate"),
                        StockDiscountAmount = itemReader.GetValueOrDefault<decimal>("StockDiscountAmount"),
                        StockNetPremium = itemReader.GetValueOrDefault<decimal>("StockNetPremium"),
                        FeaDiscountRate = itemReader.GetValueOrDefault<decimal>("FeaDiscountRate"),
                        FeaDiscountAmount = itemReader.GetValueOrDefault<decimal>("FeaDiscountAmount"),
                        NetPremiumAfterDiscounts = itemReader.GetValueOrDefault<decimal>("NetPremiumAfterDiscounts"),
                        ActualPremiumFormula = $"{itemReader.GetValueOrDefault<decimal>("ActualValue"):N2} × {itemReader.GetValueOrDefault<decimal>("ItemRate"):N2}%",
                        ShareValueFormula = $"{itemReader.GetValueOrDefault<decimal>("ActualValue"):N2} × proportion%",
                        PremiumValueFormula = $"{itemReader.GetValueOrDefault<decimal>("ShareValue"):N2} × {itemReader.GetValueOrDefault<decimal>("ItemRate"):N2}%"
                        // NOTE: the per-item certificate number (legacy CERTNO) is available
                        // here as itemReader.GetStringOrDefault("ENDTNum") — add an ItemCertNo
                        // property to CalculatedRiskItemBreakdownDto to expose it in responses.
                    };

                    // Populate StockItem if stock data exists
                    var stockSumInsured = itemReader.GetValueOrDefault<decimal>("StockSumInsured");
                    if (stockSumInsured > 0)
                    {
                        var stockCode = itemReader.GetStringOrDefault("StockCode");
                        var stockDescription = itemReader.GetStringOrDefault("StockDescription");
                        var stockRateStr = itemReader.GetStringOrDefault("StockRateStr");
                        decimal calculatedStockRate = 0;
                        decimal.TryParse(stockRateStr, out calculatedStockRate);

                        calculatedItem.StockItem = new StockItemDto
                        {
                            StockCode = string.IsNullOrEmpty(stockCode) ? "STOCK" : stockCode,
                            StockDescription = string.IsNullOrEmpty(stockDescription) ? "Stock Item" : stockDescription,
                            StockSumInsured = stockSumInsured,
                            StockRate = calculatedStockRate,
                            StockDiscountRate = itemReader.GetValueOrDefault<decimal>("StockDiscountRate")
                        };
                    }

                    riskItems.Add(calculatedItem);
                }
                itemReader.Close();

                return riskItems;
            }

            /// <summary>
            /// NEW: Get risk items for a specific section instance using TransGUID
            /// </summary>
            private async Task<List<CalculatedRiskItemBreakdownDto>> GetRiskItemsForSectionByTransGUIDAsyncNocert(
                string proposalNo,
                string sectionTransGUID,
                SqlConnection connection)
            {
                var riskItems = new List<CalculatedRiskItemBreakdownDto>();

                // UPDATED: Use Field1 which now stores the parent section's TransGUID
                var riskItemSql = @"
            SELECT SMICode, ContentDesc, Location, 
                   A1 as ActualValue, A2 as ItemRate, A3 as ActualPremium,
                   A4 as ShareValue, A5 as PremiumValue, A6 as MultiplyRate,
                   A7 as StockSumInsured, A8 as StockGrossPremium, A9 as StockDiscountAmount,
                   A10 as StockNetPremium, A11 as TotalSumInsured, A12 as TotalGrossPremium,
                   A13 as FeaDiscountRate, A14 as FeaDiscountAmount, A15 as NetPremiumAfterDiscounts,
                   A16 as ItemNo, A17 as StockDiscountRate,
                   Section as SectionID,
                   ISNULL(Field6, '') as StockCode, 
                   ISNULL(Field7, '') as StockDescription, 
                   ISNULL(Field8, '0') as StockSumInsuredStr, 
                   ISNULL(Field9, '0') as StockRateStr
            FROM PolicyPremDetails 
            WHERE PolicyNo = @PolicyNo 
            AND Field1 = @SectionTransGUID  -- CRITICAL: Filter by parent section's TransGUID
            AND Tag = 'RISK_ITEM'
            ORDER BY A16";

                using var itemCommand = new SqlCommand(riskItemSql, connection);
                itemCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);
                itemCommand.Parameters.AddWithValue("@SectionTransGUID", sectionTransGUID);

                using var itemReader = await itemCommand.ExecuteReaderAsync();
                while (await itemReader.ReadAsync())
                {
                    var calculatedItem = new CalculatedRiskItemBreakdownDto
                    {
                        ItemNo = itemReader.GetValueOrDefault<int>("ItemNo"),
                        SMICode = itemReader.GetStringOrDefault("SMICode"),
                        ItemDescription = itemReader.GetStringOrDefault("ContentDesc"),
                        ActualValue = itemReader.GetValueOrDefault<decimal>("ActualValue"),
                        ItemRate = itemReader.GetValueOrDefault<decimal>("ItemRate"),
                        MultiplyRate = itemReader.GetValueOrDefault<decimal>("MultiplyRate"),
                        SectionId = itemReader.GetStringOrDefault("SectionID"),
                        Location = itemReader.GetStringOrDefault("Location"),
                        ActualPremium = itemReader.GetValueOrDefault<decimal>("ActualPremium"),
                        ShareValue = itemReader.GetValueOrDefault<decimal>("ShareValue"),
                        PremiumValue = itemReader.GetValueOrDefault<decimal>("PremiumValue"),
                        StockSumInsured = itemReader.GetValueOrDefault<decimal>("StockSumInsured"),
                        StockGrossPremium = itemReader.GetValueOrDefault<decimal>("StockGrossPremium"),
                        StockDiscountRate = itemReader.GetValueOrDefault<decimal>("StockDiscountRate"),
                        StockDiscountAmount = itemReader.GetValueOrDefault<decimal>("StockDiscountAmount"),
                        StockNetPremium = itemReader.GetValueOrDefault<decimal>("StockNetPremium"),
                        FeaDiscountRate = itemReader.GetValueOrDefault<decimal>("FeaDiscountRate"),
                        FeaDiscountAmount = itemReader.GetValueOrDefault<decimal>("FeaDiscountAmount"),
                        NetPremiumAfterDiscounts = itemReader.GetValueOrDefault<decimal>("NetPremiumAfterDiscounts"),
                        ActualPremiumFormula = $"{itemReader.GetValueOrDefault<decimal>("ActualValue"):N2} × {itemReader.GetValueOrDefault<decimal>("ItemRate"):N2}%",
                        ShareValueFormula = $"{itemReader.GetValueOrDefault<decimal>("ActualValue"):N2} × proportion%",
                        PremiumValueFormula = $"{itemReader.GetValueOrDefault<decimal>("ShareValue"):N2} × {itemReader.GetValueOrDefault<decimal>("ItemRate"):N2}%"
                    };

                    // Populate StockItem if stock data exists
                    var stockSumInsured = itemReader.GetValueOrDefault<decimal>("StockSumInsured");
                    if (stockSumInsured > 0)
                    {
                        var stockCode = itemReader.GetStringOrDefault("StockCode");
                        var stockDescription = itemReader.GetStringOrDefault("StockDescription");
                        var stockRateStr = itemReader.GetStringOrDefault("StockRateStr");
                        decimal calculatedStockRate = 0;
                        decimal.TryParse(stockRateStr, out calculatedStockRate);

                        calculatedItem.StockItem = new StockItemDto
                        {
                            StockCode = string.IsNullOrEmpty(stockCode) ? "STOCK" : stockCode,
                            StockDescription = string.IsNullOrEmpty(stockDescription) ? "Stock Item" : stockDescription,
                            StockSumInsured = stockSumInsured,
                            StockRate = calculatedStockRate,
                            StockDiscountRate = itemReader.GetValueOrDefault<decimal>("StockDiscountRate")
                        };
                    }

                    riskItems.Add(calculatedItem);
                }
                itemReader.Close();

                return riskItems;
            }
            /// <summary>
            /// NEW: Get risk items for a specific section instance using TransGUID
            /// </summary>
            private async Task<List<CalculatedRiskItemBreakdownDto>> GetRiskItemsForSectionByTransGUIDAsync(
                string proposalNo,
                string certOrDocNo,
                string sectionTransGUID,
                SqlConnection connection)
            {
                var riskItems = new List<CalculatedRiskItemBreakdownDto>();

                // UPDATED: Use Field1 which now stores the parent section's TransGUID
                var riskItemSql = @"
            SELECT SMICode, ContentDesc, Location, 
                   A1 as ActualValue, A2 as ItemRate, A3 as ActualPremium,
                   A4 as ShareValue, A5 as PremiumValue, A6 as MultiplyRate,
                   A7 as StockSumInsured, A8 as StockGrossPremium, A9 as StockDiscountAmount,
                   A10 as StockNetPremium, A11 as TotalSumInsured, A12 as TotalGrossPremium,
                   A13 as FeaDiscountRate, A14 as FeaDiscountAmount, A15 as NetPremiumAfterDiscounts,
                   A16 as ItemNo, A17 as StockDiscountRate,
                   Section as SectionID,
                   ISNULL(Field6, '') as StockCode, 
                   ISNULL(Field7, '') as StockDescription, 
                   ISNULL(Field8, '0') as StockSumInsuredStr, 
                   ISNULL(Field9, '0') as StockRateStr
            FROM PolicyPremDetails 
            WHERE PolicyNo = @PolicyNo 
            AND Field10 = @CertOrDocNo
            AND Field1 = @SectionTransGUID  -- CRITICAL: Filter by parent section's TransGUID
            AND Tag = 'RISK_ITEM'
            ORDER BY A16";

                using var itemCommand = new SqlCommand(riskItemSql, connection);
                itemCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);
                itemCommand.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo ?? "");
                itemCommand.Parameters.AddWithValue("@SectionTransGUID", sectionTransGUID);

                using var itemReader = await itemCommand.ExecuteReaderAsync();
                while (await itemReader.ReadAsync())
                {
                    var calculatedItem = new CalculatedRiskItemBreakdownDto
                    {
                        ItemNo = itemReader.GetValueOrDefault<int>("ItemNo"),
                        SMICode = itemReader.GetStringOrDefault("SMICode"),
                        ItemDescription = itemReader.GetStringOrDefault("ContentDesc"),
                        ActualValue = itemReader.GetValueOrDefault<decimal>("ActualValue"),
                        ItemRate = itemReader.GetValueOrDefault<decimal>("ItemRate"),
                        MultiplyRate = itemReader.GetValueOrDefault<decimal>("MultiplyRate"),
                        SectionId = itemReader.GetStringOrDefault("SectionID"),
                        Location = itemReader.GetStringOrDefault("Location"),
                        ActualPremium = itemReader.GetValueOrDefault<decimal>("ActualPremium"),
                        ShareValue = itemReader.GetValueOrDefault<decimal>("ShareValue"),
                        PremiumValue = itemReader.GetValueOrDefault<decimal>("PremiumValue"),
                        StockSumInsured = itemReader.GetValueOrDefault<decimal>("StockSumInsured"),
                        StockGrossPremium = itemReader.GetValueOrDefault<decimal>("StockGrossPremium"),
                        StockDiscountRate = itemReader.GetValueOrDefault<decimal>("StockDiscountRate"),
                        StockDiscountAmount = itemReader.GetValueOrDefault<decimal>("StockDiscountAmount"),
                        StockNetPremium = itemReader.GetValueOrDefault<decimal>("StockNetPremium"),
                        FeaDiscountRate = itemReader.GetValueOrDefault<decimal>("FeaDiscountRate"),
                        FeaDiscountAmount = itemReader.GetValueOrDefault<decimal>("FeaDiscountAmount"),
                        NetPremiumAfterDiscounts = itemReader.GetValueOrDefault<decimal>("NetPremiumAfterDiscounts"),
                        ActualPremiumFormula = $"{itemReader.GetValueOrDefault<decimal>("ActualValue"):N2} × {itemReader.GetValueOrDefault<decimal>("ItemRate"):N2}%",
                        ShareValueFormula = $"{itemReader.GetValueOrDefault<decimal>("ActualValue"):N2} × proportion%",
                        PremiumValueFormula = $"{itemReader.GetValueOrDefault<decimal>("ShareValue"):N2} × {itemReader.GetValueOrDefault<decimal>("ItemRate"):N2}%"
                    };

                    // Populate StockItem if stock data exists
                    var stockSumInsured = itemReader.GetValueOrDefault<decimal>("StockSumInsured");
                    if (stockSumInsured > 0)
                    {
                        var stockCode = itemReader.GetStringOrDefault("StockCode");
                        var stockDescription = itemReader.GetStringOrDefault("StockDescription");
                        var stockRateStr = itemReader.GetStringOrDefault("StockRateStr");
                        decimal calculatedStockRate = 0;
                        decimal.TryParse(stockRateStr, out calculatedStockRate);

                        calculatedItem.StockItem = new StockItemDto
                        {
                            StockCode = string.IsNullOrEmpty(stockCode) ? "STOCK" : stockCode,
                            StockDescription = string.IsNullOrEmpty(stockDescription) ? "Stock Item" : stockDescription,
                            StockSumInsured = stockSumInsured,
                            StockRate = calculatedStockRate,
                            StockDiscountRate = itemReader.GetValueOrDefault<decimal>("StockDiscountRate")
                        };
                    }

                    riskItems.Add(calculatedItem);
                }
                itemReader.Close();

                return riskItems;
            }
            /// <summary>
            /// FIXED: Get proposal-level adjustments with proper null handling
            /// </summary>
            private async Task<ProposalLevelAdjustmentsBreakdownDto> GetProposalLevelAdjustmentsByDetailIDAsync(
                string proposalNo,
                string certOrDocNo,
                SqlConnection connection)
            {
                var sql = @"
            SELECT A1, A2, A10, A11, Field18, Field28, Field29
            FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo 
            AND Field20 = @CertOrDocNo
            AND Tag = 'PROPOSAL_ADJUSTMENTS'";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo ?? "");

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var breakdown = new ProposalLevelAdjustmentsBreakdownDto
                    {
                        StartingPremium = reader.GetValueOrDefault<decimal>("A1"),
                        FinalNetPremium = reader.GetValueOrDefault<decimal>("A2"),
                        DiscountsApplied = new List<AdjustmentDetailDto>(),
                        LoadingsApplied = new List<AdjustmentDetailDto>()
                    };

                    var otherDiscountsAmount = reader.GetValueOrDefault<decimal>("A10");
                    if (otherDiscountsAmount > 0)
                    {
                        breakdown.DiscountsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Other Discounts",
                            Type = "DISCOUNT",
                            Amount = otherDiscountsAmount,
                            SequenceOrder = 1
                        });
                    }

                    var otherLoadingsAmount = reader.GetValueOrDefault<decimal>("A11");
                    if (otherLoadingsAmount > 0)
                    {
                        breakdown.LoadingsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Other Loadings",
                            Type = "LOADING",
                            Amount = otherLoadingsAmount,
                            SequenceOrder = 1
                        });
                    }

                    breakdown.TotalDiscountAmount = breakdown.DiscountsApplied.Sum(d => d.Amount);
                    breakdown.TotalLoadingAmount = breakdown.LoadingsApplied.Sum(l => l.Amount);
                    breakdown.NetAdjustmentAmount = breakdown.TotalLoadingAmount - breakdown.TotalDiscountAmount;

                    return breakdown;
                }

                return new ProposalLevelAdjustmentsBreakdownDto
                {
                    StartingPremium = 0,
                    FinalNetPremium = 0,
                    DiscountsApplied = new List<AdjustmentDetailDto>(),
                    LoadingsApplied = new List<AdjustmentDetailDto>()
                };
            }

            /// <summary>
            /// Create additional fire calculation for existing proposal
            /// </summary>
            public async Task<CompleteCalculationResponseDto> CreateAdditionalFireCalculationAsync(
                CreateAdditionalFireCalculationRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Check if proposal exists
                    var proposalExists = await ProposalExistsAsync(request.ProposalNo);
                    if (!proposalExists)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = request.ProposalNo,
                            Success = false,
                            Message = "Proposal not found",
                            ValidationErrors = { "The specified proposal does not exist" }
                        };
                    }

                    // Generate new CertOrDocNo for this calculation
                    var proposal = await GetProposalAsync(request.ProposalNo);
                    // LEGACY (InsertIntoPolicyDetails): calculation-level number is the
                    // DECLARATION_NO auto number, keyed by branch + subrisk + endorse type.
                    var certOrDocNo = await NextCalcCertAsync(
                        proposal.BranchID,
                        proposal.SubRiskID,
                        EndorseType.None);

                    var sectionCalculations = new List<SectionCalculationResponseDto>();
                    decimal totalSumInsured = 0;
                    decimal totalGrossPremium = 0;

                    // Process each section
                    foreach (var section in request.Sections)
                    {
                        var itemCalcRequest = new RiskItemCalculationRequestDto
                        {
                            RiskItems = section.RiskItems,
                            ProportionRate = request.ProportionRate
                        };
                        var itemCalcResponse = await CalculateRiskItemsAsync(itemCalcRequest);

                        if (!itemCalcResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} calculation failed: {itemCalcResponse.Message}",
                                ValidationErrors = { itemCalcResponse.Message }
                            };
                        }

                        var sectionAggregate = new SectionAggregateResponseDto
                        {
                            AggregateSumInsured = itemCalcResponse.TotalShareValue,
                            AggregatePremium = itemCalcResponse.TotalNetPremiumAfterDiscounts,
                            Success = true
                        };

                        var sectionAdjustmentRequest = new SectionAdjustmentRequestDto
                        {
                            AggregatePremium = sectionAggregate.AggregatePremium,
                            SpecialDiscountRate = section.SpecialDiscountRate,
                            DeductibleDiscountRate = section.DeductibleDiscountRate,
                            SpreadDiscountRate = section.SpreadDiscountRate,
                            LtaDiscountRate = section.LtaDiscountRate,
                            TheftLoadingRate = section.TheftLoadingRate,
                            SrccLoadingRate = section.SrccLoadingRate,
                            OtherLoading2Rate = section.OtherLoading2Rate
                        };

                        var sectionAdjustmentResponse = await ApplySectionAdjustmentsAsync(sectionAdjustmentRequest);

                        if (!sectionAdjustmentResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} adjustments failed: {sectionAdjustmentResponse.Message}",
                                ValidationErrors = { sectionAdjustmentResponse.Message }
                            };
                        }

                        var sectionResult = new SectionCalculationResponseDto
                        {
                            SectionID = section.SectionID,
                            SectionName = section.SectionName,
                            CalculatedItems = itemCalcResponse.CalculatedItems,
                            SectionSumInsured = sectionAggregate.AggregateSumInsured,
                            SectionGrossPremium = sectionAggregate.AggregatePremium,
                            SectionNetPremium = sectionAdjustmentResponse.NetPremiumDue,
                            SectionAdjustments = sectionAdjustmentResponse,
                            Success = true
                        };

                        sectionCalculations.Add(sectionResult);
                        totalSumInsured += sectionAggregate.AggregateSumInsured;
                        totalGrossPremium += sectionAdjustmentResponse.NetPremiumDue;

                        // Save section with CertOrDocNo link
                        await SaveSectionWithCertOrDocNoAsync(
                            request.ProposalNo,
                            certOrDocNo,
                            sectionResult,
                            section,
                            request,
                             connection,
                transaction);
                    }

                    // Apply proposal-level adjustments
                    var proposalAdjustmentRequest = new ProposalAdjustmentRequestDto
                    {
                        TotalAggregatePremium = totalGrossPremium,
                        OtherDiscountsRate = request.OtherDiscountsRate,
                        OtherLoadingsRate = request.OtherLoadingsRate
                    };

                    var proposalAdjustmentResponse = await ApplyProposalLevelAdjustmentsAsync(proposalAdjustmentRequest);

                    if (!proposalAdjustmentResponse.Success)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = request.ProposalNo,
                            Success = false,
                            Message = $"Proposal adjustments failed: {proposalAdjustmentResponse.Message}",
                            ValidationErrors = { proposalAdjustmentResponse.Message }
                        };
                    }

                    decimal totalNetPremium = proposalAdjustmentResponse.NetPremiumDue;

                    // Save proposal-level adjustments with CertOrDocNo link
                    await SaveProposalLevelAdjustmentsWithCertOrDocNoAsync(
                        request.ProposalNo,
                        certOrDocNo,
                        proposalAdjustmentResponse,
                        request,
                        connection,
                        transaction);

                    // Calculate pro-rata
                    var proRataRequest = new ProRataCalculationRequestDto
                    {
                        NetPremiumDue = totalNetPremium,
                        CoverDays = request.CoverDays
                    };
                    var proRataResponse = await CalculateProRataAsync(proRataRequest);

                    // Create NEW PolicyDetails record
                    await CreateNewPolicyDetailsRecordAsync(
                        request.ProposalNo,
                        certOrDocNo,
                        totalSumInsured,
                        totalGrossPremium,
                        proRataResponse,
                        request,
                        connection,
                        transaction);

                    await transaction.CommitAsync();

                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        SectionCalculations = sectionCalculations,
                        TotalSumInsured = totalSumInsured,
                        TotalPremium = totalGrossPremium,
                        NetPremium = totalNetPremium,
                        ProRataPremium = proRataResponse.ProRataPremium,
                        ShareSumInsured = totalSumInsured * (request.ProportionRate / 100),
                        SharePremium = proRataResponse.PremiumDue * (request.ProportionRate / 100),
                        ForeignSumInsured = totalSumInsured * request.ExchangeRate,
                        ForeignPremium = proRataResponse.PremiumDue * request.ExchangeRate,
                        ProposalLevelAdjustments = proposalAdjustmentResponse,
                        ProRataCalculations = proRataResponse,
                        Success = true,
                        CalculatedOn = DateTime.Now,
                        Message = "Additional fire calculation created successfully"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating additional fire calculation for {ProposalNo}", request.ProposalNo);
                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        Success = false,
                        Message = "Calculation failed",
                        ValidationErrors = { ex.Message }
                    };
                }
            }
            /// <summary>
            /// Create additional fire calculation for existing proposal
            /// </summary>
            public async Task<CompleteCalculationResponseDto> CreateRenewalFireCalculationAsync(
                CreateRenewalFireCalculationRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Check if proposal exists
                    var proposalExists = await ProposalExistsAsync(request.ProposalNo);
                    if (!proposalExists)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = request.ProposalNo,
                            Success = false,
                            Message = "Proposal not found",
                            ValidationErrors = { "The specified proposal does not exist" }
                        };
                    }
                    // Convert to regular fire calculation request
                    var fireCalcRequest = new CompleteFireCalculationRequestDto
                    {
                        ProposalNo = request.NewPolicyNo,
                        Sections = request.Sections,
                        ProportionRate = request.ProportionRate,
                        ExchangeRate = request.ExchangeRate,
                        Currency = request.Currency,
                        CoverDays = request.CoverDays,
                        OtherDiscountsRate = request.OtherDiscountsRate,
                        OtherLoadingsRate = request.OtherLoadingsRate,
                        CalculatedBy = request.CalculatedBy,
                        Remarks = $"RENEWAL from {request.OriginalPolicyNo}. {request.Remarks}"
                    };

                    // Generate new CertOrDocNo for this calculation
                    var proposal = await GetProposalAsync(request.ProposalNo);
                    // LEGACY (InsertIntoPolicyDetails): calculation-level number is the
                    // DECLARATION_NO auto number, keyed by branch + subrisk + endorse type.
                    var certOrDocNo = await NextCalcCertAsync(
                        proposal.BranchID,
                        proposal.SubRiskID,
                        MapEndorseType("Renewal"));

                    var sectionCalculations = new List<SectionCalculationResponseDto>();
                    decimal totalSumInsured = 0;
                    decimal totalGrossPremium = 0;

                    // Process each section
                    foreach (var section in request.Sections)
                    {
                        var itemCalcRequest = new RiskItemCalculationRequestDto
                        {
                            RiskItems = section.RiskItems,
                            ProportionRate = request.ProportionRate
                        };
                        var itemCalcResponse = await CalculateRiskItemsAsync(itemCalcRequest);

                        if (!itemCalcResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} calculation failed: {itemCalcResponse.Message}",
                                ValidationErrors = { itemCalcResponse.Message }
                            };
                        }

                        var sectionAggregate = new SectionAggregateResponseDto
                        {
                            AggregateSumInsured = itemCalcResponse.TotalShareValue,
                            AggregatePremium = itemCalcResponse.TotalNetPremiumAfterDiscounts,
                            Success = true
                        };

                        var sectionAdjustmentRequest = new SectionAdjustmentRequestDto
                        {
                            AggregatePremium = sectionAggregate.AggregatePremium,
                            SpecialDiscountRate = section.SpecialDiscountRate,
                            DeductibleDiscountRate = section.DeductibleDiscountRate,
                            SpreadDiscountRate = section.SpreadDiscountRate,
                            LtaDiscountRate = section.LtaDiscountRate,
                            TheftLoadingRate = section.TheftLoadingRate,
                            SrccLoadingRate = section.SrccLoadingRate,
                            OtherLoading2Rate = section.OtherLoading2Rate
                        };

                        var sectionAdjustmentResponse = await ApplySectionAdjustmentsAsync(sectionAdjustmentRequest);

                        if (!sectionAdjustmentResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} adjustments failed: {sectionAdjustmentResponse.Message}",
                                ValidationErrors = { sectionAdjustmentResponse.Message }
                            };
                        }

                        var sectionResult = new SectionCalculationResponseDto
                        {
                            SectionID = section.SectionID,
                            SectionName = section.SectionName,
                            CalculatedItems = itemCalcResponse.CalculatedItems,
                            SectionSumInsured = sectionAggregate.AggregateSumInsured,
                            SectionGrossPremium = sectionAggregate.AggregatePremium,
                            SectionNetPremium = sectionAdjustmentResponse.NetPremiumDue,
                            SectionAdjustments = sectionAdjustmentResponse,
                            Success = true
                        };

                        sectionCalculations.Add(sectionResult);
                        totalSumInsured += sectionAggregate.AggregateSumInsured;
                        totalGrossPremium += sectionAdjustmentResponse.NetPremiumDue;

                        // Save section with CertOrDocNo link
                        await SaveRenewalSectionWithCertOrDocNoAsync(
                            request.ProposalNo,
                            certOrDocNo,
                            sectionResult,
                            section,
                            request,
                             connection,
                transaction);
                    }

                    // Apply proposal-level adjustments
                    var proposalAdjustmentRequest = new ProposalAdjustmentRequestDto
                    {
                        TotalAggregatePremium = totalGrossPremium,
                        OtherDiscountsRate = request.OtherDiscountsRate,
                        OtherLoadingsRate = request.OtherLoadingsRate
                    };

                    var proposalAdjustmentResponse = await ApplyProposalLevelAdjustmentsAsync(proposalAdjustmentRequest);

                    if (!proposalAdjustmentResponse.Success)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = request.ProposalNo,
                            Success = false,
                            Message = $"Proposal adjustments failed: {proposalAdjustmentResponse.Message}",
                            ValidationErrors = { proposalAdjustmentResponse.Message }
                        };
                    }

                    decimal totalNetPremium = proposalAdjustmentResponse.NetPremiumDue;

                    // Save proposal-level adjustments with CertOrDocNo link
                    await SaveRenewalProposalLevelAdjustmentsWithCertOrDocNoAsync(
                        request.ProposalNo,
                        certOrDocNo,
                        proposalAdjustmentResponse,
                        request,
                        connection,
                        transaction);

                    // Calculate pro-rata
                    var proRataRequest = new ProRataCalculationRequestDto
                    {
                        NetPremiumDue = totalNetPremium,
                        CoverDays = request.CoverDays
                    };
                    var proRataResponse = await CalculateProRataAsync(proRataRequest);

                    // Create NEW PolicyDetails record
                    await CreateRenewalPolicyDetailsRecordAsync(
                        request.ProposalNo,
                        certOrDocNo,
                        totalSumInsured,
                        totalGrossPremium,
                        proRataResponse,
                        request,
                        connection,
                        transaction);
                    // Update Policy table summary
                    await UpdateProposalSummaryAsync(fireCalcRequest.ProposalNo, totalSumInsured, totalNetPremium,
                        proRataResponse, fireCalcRequest.ExchangeRate, connection, transaction);


                    await transaction.CommitAsync();

                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        SectionCalculations = sectionCalculations,
                        TotalSumInsured = totalSumInsured,
                        TotalPremium = totalGrossPremium,
                        NetPremium = totalNetPremium,
                        ProRataPremium = proRataResponse.ProRataPremium,
                        ShareSumInsured = totalSumInsured * (request.ProportionRate / 100),
                        SharePremium = proRataResponse.PremiumDue * (request.ProportionRate / 100),
                        ForeignSumInsured = totalSumInsured * request.ExchangeRate,
                        ForeignPremium = proRataResponse.PremiumDue * request.ExchangeRate,
                        ProposalLevelAdjustments = proposalAdjustmentResponse,
                        ProRataCalculations = proRataResponse,
                        Success = true,
                        CalculatedOn = DateTime.Now,
                        Message = "Additional fire calculation created successfully"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating additional fire calculation for {ProposalNo}", request.ProposalNo);
                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        Success = false,
                        Message = "Calculation failed",
                        ValidationErrors = { ex.Message }
                    };
                }
            }

            /// <summary>
            /// Update/recalculate a specific fire calculation by DetailID
            /// </summary>
            public async Task<CompleteCalculationResponseDto> UpdateFireCalculationAsync(
            UpdateFireCalculationRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();
                try
                {
                    // Get the PolicyDetail record to update
                    var existingDetailSql = @"
            SELECT PolicyNo, CertOrDocNo 
            FROM PolicyDetails 
            WHERE DetailID = @DetailID AND Deleted = 0";

                    string proposalNo = null;
                    string certOrDocNo = null;

                    using (var cmd = new SqlCommand(existingDetailSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@DetailID", request.DetailID);
                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            proposalNo = reader.GetStringOrDefault("PolicyNo");
                            certOrDocNo = reader.GetStringOrDefault("CertOrDocNo");
                        }
                    }

                    if (string.IsNullOrEmpty(proposalNo) || string.IsNullOrEmpty(certOrDocNo))
                    {
                        return new CompleteCalculationResponseDto
                        {
                            Success = false,
                            Message = "Fire calculation not found",
                            ValidationErrors = { $"DetailID {request.DetailID} not found" }
                        };
                    }

                    // Delete previous calculation data for this specific calculation
                    await DeleteCalculationDataByCertOrDocNoAsync(
                        proposalNo,
                        certOrDocNo,
                        connection,
                        transaction);

                    var sectionCalculations = new List<SectionCalculationResponseDto>();
                    decimal totalSumInsured = 0;
                    decimal totalGrossPremium = 0;

                    // Process each section (same as create)
                    foreach (var section in request.Sections)
                    {
                        var itemCalcRequest = new RiskItemCalculationRequestDto
                        {
                            RiskItems = section.RiskItems,
                            ProportionRate = request.ProportionRate
                        };
                        var itemCalcResponse = await CalculateRiskItemsAsync(itemCalcRequest);

                        if (!itemCalcResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = proposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} calculation failed: {itemCalcResponse.Message}",
                                ValidationErrors = { itemCalcResponse.Message }
                            };
                        }

                        var sectionAggregate = new SectionAggregateResponseDto
                        {
                            AggregateSumInsured = itemCalcResponse.TotalShareValue,
                            AggregatePremium = itemCalcResponse.TotalNetPremiumAfterDiscounts,
                            Success = true
                        };

                        var sectionAdjustmentRequest = new SectionAdjustmentRequestDto
                        {
                            AggregatePremium = sectionAggregate.AggregatePremium,
                            SpecialDiscountRate = section.SpecialDiscountRate,
                            DeductibleDiscountRate = section.DeductibleDiscountRate,
                            SpreadDiscountRate = section.SpreadDiscountRate,
                            LtaDiscountRate = section.LtaDiscountRate,
                            TheftLoadingRate = section.TheftLoadingRate,
                            SrccLoadingRate = section.SrccLoadingRate,
                            OtherLoading2Rate = section.OtherLoading2Rate
                        };

                        var sectionAdjustmentResponse = await ApplySectionAdjustmentsAsync(sectionAdjustmentRequest);

                        if (!sectionAdjustmentResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = proposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} adjustments failed: {sectionAdjustmentResponse.Message}",
                                ValidationErrors = { sectionAdjustmentResponse.Message }
                            };
                        }

                        var sectionResult = new SectionCalculationResponseDto
                        {
                            SectionID = section.SectionID,
                            SectionName = section.SectionName,
                            CalculatedItems = itemCalcResponse.CalculatedItems,
                            SectionSumInsured = sectionAggregate.AggregateSumInsured,
                            SectionGrossPremium = sectionAggregate.AggregatePremium,
                            SectionNetPremium = sectionAdjustmentResponse.NetPremiumDue,
                            SectionAdjustments = sectionAdjustmentResponse,
                            Success = true
                        };

                        sectionCalculations.Add(sectionResult);
                        totalSumInsured += sectionAggregate.AggregateSumInsured;
                        totalGrossPremium += sectionAdjustmentResponse.NetPremiumDue;

                        // Save section with same CertOrDocNo
                        await SaveSectionWithCertOrDocNoAsync(
                            proposalNo,
                            certOrDocNo,
                            sectionResult,
                            section,
                            new CreateAdditionalFireCalculationRequestDto
                            {
                                ProposalNo = proposalNo,
                                ProportionRate = request.ProportionRate,
                                ExchangeRate = request.ExchangeRate,
                                Currency = request.Currency,
                                CoverDays = request.CoverDays,
                                CalculatedBy = request.RecalculatedBy
                            },
                            connection,
                            transaction);
                    }

                    // Apply proposal-level adjustments
                    var proposalAdjustmentRequest = new ProposalAdjustmentRequestDto
                    {
                        TotalAggregatePremium = totalGrossPremium,
                        OtherDiscountsRate = request.OtherDiscountsRate,
                        OtherLoadingsRate = request.OtherLoadingsRate
                    };

                    var proposalAdjustmentResponse = await ApplyProposalLevelAdjustmentsAsync(proposalAdjustmentRequest);

                    if (!proposalAdjustmentResponse.Success)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = proposalNo,
                            Success = false,
                            Message = $"Proposal adjustments failed: {proposalAdjustmentResponse.Message}",
                            ValidationErrors = { proposalAdjustmentResponse.Message }
                        };
                    }

                    decimal totalNetPremium = proposalAdjustmentResponse.NetPremiumDue;

                    // Save proposal-level adjustments
                    await SaveProposalLevelAdjustmentsWithCertOrDocNoAsync(
                        proposalNo,
                        certOrDocNo,
                        proposalAdjustmentResponse,
                        new CreateAdditionalFireCalculationRequestDto
                        {
                            ProposalNo = proposalNo,
                            CalculatedBy = request.RecalculatedBy
                        },
                        connection,
                        transaction);

                    // Calculate pro-rata
                    var proRataRequest = new ProRataCalculationRequestDto
                    {
                        NetPremiumDue = totalNetPremium,
                        CoverDays = request.CoverDays
                    };
                    var proRataResponse = await CalculateProRataAsync(proRataRequest);

                    // Update the EXISTING PolicyDetails record
                    await UpdateExistingPolicyDetailsRecordAsync(
                        request.DetailID,
                        totalSumInsured,
                        totalGrossPremium,
                        proRataResponse,
                        new UpdateFireCalculationRequestDto
                        {
                            DetailID = request.DetailID,
                            ProportionRate = request.ProportionRate,
                            ExchangeRate = request.ExchangeRate,
                            Currency = request.Currency,
                            CoverDays = request.CoverDays,
                            OtherDiscountsRate = request.OtherDiscountsRate,
                            OtherLoadingsRate = request.OtherLoadingsRate,
                            RecalculatedBy = request.RecalculatedBy,
                            Remarks = request.Remarks
                        },
                        connection,
                        transaction);

                    await transaction.CommitAsync();

                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        SectionCalculations = sectionCalculations,
                        TotalSumInsured = totalSumInsured,
                        TotalPremium = totalGrossPremium,
                        NetPremium = totalNetPremium,
                        ProRataPremium = proRataResponse.ProRataPremium,
                        ShareSumInsured = totalSumInsured * (request.ProportionRate / 100),
                        SharePremium = proRataResponse.PremiumDue * (request.ProportionRate / 100),
                        ForeignSumInsured = totalSumInsured * request.ExchangeRate,
                        ForeignPremium = proRataResponse.PremiumDue * request.ExchangeRate,
                        ProposalLevelAdjustments = proposalAdjustmentResponse,
                        ProRataCalculations = proRataResponse,
                        Success = true,
                        CalculatedOn = DateTime.Now,
                        Message = "Fire calculation updated successfully"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating fire calculation {DetailID}", request.DetailID);
                    return new CompleteCalculationResponseDto
                    {
                        Success = false,
                        Message = "Update failed",
                        ValidationErrors = { ex.Message }
                    };
                }
            }

            // NOTE: superseded by NextCalcCertAsync (legacy DECLARATION_NO auto number).
            // Kept because ProposalService is a partial class and other files may still call it.
            private async Task<string> GenerateNewCertOrDocNoAsync(
    string proposalNo,
    string riskClass,
    SqlConnection connection,
    SqlTransaction transaction)
            {
                var year = DateTime.Now.Year;
                var prefix = $"{riskClass}{year:yy}";
                // Count ALL existing CertOrDocNo with this prefix
                var sql = @"
        SELECT COUNT(*) 
        FROM PolicyDetails 
        WHERE CertOrDocNo LIKE @Prefix";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@Prefix", $"{prefix}%");

                var count = (int)await command.ExecuteScalarAsync();
                return $"{prefix}{(count + 1):D6}";
            }

            // Updated SaveSectionWithCertOrDocNoAsync method
            private async Task SaveSectionWithCertOrDocNoAsync(
                string proposalNo,
                string certOrDocNo,
                SectionCalculationResponseDto sectionResult,
                FireSectionDto section,
                CreateAdditionalFireCalculationRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                // LEGACY (AddItemSectionTempGridDetails): every section gets its own
                // declaration number = PROPOSAL auto number, keyed by branch, stored in ENDTNum.
                var branchId = await GetBranchIdAsync(proposalNo, connection, transaction);
                var sectionDeclarationNo = await NextDeclarationNoAsync(branchId);
                var sectionTransGuid = Guid.NewGuid().ToString();

                var sql = @"
    INSERT INTO PolicyTempDetails (
        PolicyNo, ENDTNum, SectionID, ContentSection, Location,
        Field1, Field2, Field3, Field4, Field5, Field20, Field11, Field12,
        Field26, Field7, Field8,
        RiskSum, OtherSum, SectionPremium, TStartDate, TEndDate,
        A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16, A17, A18, A19, A20,
        Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
        @PolicyNo, @ENDTNum, @SectionID, @ContentSection, @Location,
        @Field1, @Field2, @Field3, @Field4, @Field5, @Field20, @Field11, @Field12,
        @Field26, @Field7, @Field8,
        @RiskSum, @OtherSum, @SectionPremium, @TStartDate, @TEndDate,
        @A1, @A2, @A3, @A4, @A5, @A6, @A7, @A8, @A9, @A10, @A11, @A12, @A13, @A14, @A15, @A16, @A17, @A18, @A19, @A20,
        @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@Field20", certOrDocNo);
                command.Parameters.AddWithValue("@ENDTNum", sectionDeclarationNo); // LEGACY: section declaration number (PROPOSAL auto number)
                command.Parameters.AddWithValue("@SectionID", sectionResult.SectionID);
                command.Parameters.AddWithValue("@ContentSection", sectionResult.SectionName);
                command.Parameters.AddWithValue("@Location", sectionResult.CalculatedItems.FirstOrDefault()?.Location ?? "");

                command.Parameters.AddWithValue("@Field1", "SECTION");
                command.Parameters.AddWithValue("@Field2", sectionResult.SectionName);
                command.Parameters.AddWithValue("@Field3", $"Items: {sectionResult.CalculatedItems.Count}");
                command.Parameters.AddWithValue("@Field4", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@Field5", "CALCULATED_WITH_ADJUSTMENTS");

                // NEW: Optional section information fields
                command.Parameters.AddWithValue("@Field26", (object)section.Field26 ?? DBNull.Value);
                command.Parameters.AddWithValue("@Field7", (object)section.Field7 ?? DBNull.Value);
                command.Parameters.AddWithValue("@Field8", (object)section.Field8 ?? DBNull.Value);

                command.Parameters.AddWithValue("@RiskSum", sectionResult.SectionSumInsured);
                command.Parameters.AddWithValue("@OtherSum", 0);
                command.Parameters.AddWithValue("@SectionPremium", sectionResult.SectionNetPremium);
                command.Parameters.AddWithValue("@TStartDate", DateTime.Now);
                command.Parameters.AddWithValue("@TEndDate", DateTime.Now.AddDays(request.CoverDays));

                command.Parameters.AddWithValue("@A1", sectionResult.SectionSumInsured);
                command.Parameters.AddWithValue("@A2", sectionResult.SectionGrossPremium);
                command.Parameters.AddWithValue("@A3", sectionResult.SectionAdjustments?.SpecialDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A4", sectionResult.SectionAdjustments?.DeductibleDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A5", sectionResult.SectionAdjustments?.SpreadDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A6", sectionResult.SectionAdjustments?.LtaDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A7", sectionResult.SectionAdjustments?.TheftLoadingAmount ?? 0);
                command.Parameters.AddWithValue("@A8", sectionResult.SectionAdjustments?.SrccLoadingAmount ?? 0);
                command.Parameters.AddWithValue("@A9", sectionResult.SectionAdjustments?.OtherLoading2Amount ?? 0);
                command.Parameters.AddWithValue("@A10", 0);
                command.Parameters.AddWithValue("@A11", sectionResult.SectionNetPremium);
                command.Parameters.AddWithValue("@A17", sectionResult.CalculatedItems.Count);
                command.Parameters.AddWithValue("@A18", GetTopLocationValueForSection(sectionResult.CalculatedItems));
                command.Parameters.AddWithValue("@A19", sectionResult.SectionSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@A20", sectionResult.SectionNetPremium * (request.ProportionRate / 100));

                command.Parameters.AddWithValue("@A12", section.SpecialDiscountRate);
                command.Parameters.AddWithValue("@A13", section.DeductibleDiscountRate);
                command.Parameters.AddWithValue("@A14", section.SpreadDiscountRate);
                command.Parameters.AddWithValue("@A15", section.LtaDiscountRate);
                command.Parameters.AddWithValue("@A16", section.TheftLoadingRate);
                command.Parameters.AddWithValue("@Field11", section.SrccLoadingRate.ToString("F2"));
                command.Parameters.AddWithValue("@Field12", section.OtherLoading2Rate.ToString("F2"));

                command.Parameters.AddWithValue("@Tag", "SECTION");
                command.Parameters.AddWithValue("@TransGUID", sectionTransGuid);
                command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();

                // Save risk items for this section
                await SaveSectionRiskItemsWithCertOrDocNoAsync(
                    proposalNo,
                    branchId,
                    certOrDocNo,
                    sectionDeclarationNo,
                    sectionResult,
                    request,
                    connection,
                    transaction);
            }
            private async Task SaveRenewalSectionWithCertOrDocNoAsync(
                string proposalNo,
                string certOrDocNo,
                SectionCalculationResponseDto sectionResult,
                FireSectionDto section,
                CreateRenewalFireCalculationRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                // LEGACY (AddItemSectionTempGridDetails): every section gets its own
                // declaration number = PROPOSAL auto number, keyed by branch, stored in ENDTNum.
                var branchId = await GetBranchIdAsync(proposalNo, connection, transaction);
                var sectionDeclarationNo = await NextDeclarationNoAsync(branchId);
                var sectionTransGuid = Guid.NewGuid().ToString();

                var sql = @"
    INSERT INTO PolicyTempDetails (
        PolicyNo, ENDTNum, SectionID, ContentSection, Location,
        Field1, Field2, Field3, Field4, Field5, Field20, Field11, Field12,
        Field26, Field7, Field8,
        RiskSum, OtherSum, SectionPremium, TStartDate, TEndDate,
        A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16, A17, A18, A19, A20,
        Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
        @PolicyNo, @ENDTNum, @SectionID, @ContentSection, @Location,
        @Field1, @Field2, @Field3, @Field4, @Field5, @Field20, @Field11, @Field12,
        @Field26, @Field7, @Field8,
        @RiskSum, @OtherSum, @SectionPremium, @TStartDate, @TEndDate,
        @A1, @A2, @A3, @A4, @A5, @A6, @A7, @A8, @A9, @A10, @A11, @A12, @A13, @A14, @A15, @A16, @A17, @A18, @A19, @A20,
        @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@Field20", certOrDocNo);
                command.Parameters.AddWithValue("@ENDTNum", sectionDeclarationNo); // LEGACY: section declaration number (PROPOSAL auto number)
                command.Parameters.AddWithValue("@SectionID", sectionResult.SectionID);
                command.Parameters.AddWithValue("@ContentSection", sectionResult.SectionName);
                command.Parameters.AddWithValue("@Location", sectionResult.CalculatedItems.FirstOrDefault()?.Location ?? "");

                command.Parameters.AddWithValue("@Field1", "SECTION");
                command.Parameters.AddWithValue("@Field2", sectionResult.SectionName);
                command.Parameters.AddWithValue("@Field3", $"Items: {sectionResult.CalculatedItems.Count}");
                command.Parameters.AddWithValue("@Field4", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@Field5", "CALCULATED_WITH_ADJUSTMENTS");

                // NEW: Optional section information fields
                command.Parameters.AddWithValue("@Field26", (object)section.Field26 ?? DBNull.Value);
                command.Parameters.AddWithValue("@Field7", (object)section.Field7 ?? DBNull.Value);
                command.Parameters.AddWithValue("@Field8", (object)section.Field8 ?? DBNull.Value);

                command.Parameters.AddWithValue("@RiskSum", sectionResult.SectionSumInsured);
                command.Parameters.AddWithValue("@OtherSum", 0);
                command.Parameters.AddWithValue("@SectionPremium", sectionResult.SectionNetPremium);
                command.Parameters.AddWithValue("@TStartDate", DateTime.Now);
                command.Parameters.AddWithValue("@TEndDate", DateTime.Now.AddDays(request.CoverDays));

                command.Parameters.AddWithValue("@A1", sectionResult.SectionSumInsured);
                command.Parameters.AddWithValue("@A2", sectionResult.SectionGrossPremium);
                command.Parameters.AddWithValue("@A3", sectionResult.SectionAdjustments?.SpecialDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A4", sectionResult.SectionAdjustments?.DeductibleDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A5", sectionResult.SectionAdjustments?.SpreadDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A6", sectionResult.SectionAdjustments?.LtaDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A7", sectionResult.SectionAdjustments?.TheftLoadingAmount ?? 0);
                command.Parameters.AddWithValue("@A8", sectionResult.SectionAdjustments?.SrccLoadingAmount ?? 0);
                command.Parameters.AddWithValue("@A9", sectionResult.SectionAdjustments?.OtherLoading2Amount ?? 0);
                command.Parameters.AddWithValue("@A10", 0);
                command.Parameters.AddWithValue("@A11", sectionResult.SectionNetPremium);
                command.Parameters.AddWithValue("@A17", sectionResult.CalculatedItems.Count);
                command.Parameters.AddWithValue("@A18", GetTopLocationValueForSection(sectionResult.CalculatedItems));
                command.Parameters.AddWithValue("@A19", sectionResult.SectionSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@A20", sectionResult.SectionNetPremium * (request.ProportionRate / 100));

                command.Parameters.AddWithValue("@A12", section.SpecialDiscountRate);
                command.Parameters.AddWithValue("@A13", section.DeductibleDiscountRate);
                command.Parameters.AddWithValue("@A14", section.SpreadDiscountRate);
                command.Parameters.AddWithValue("@A15", section.LtaDiscountRate);
                command.Parameters.AddWithValue("@A16", section.TheftLoadingRate);
                command.Parameters.AddWithValue("@Field11", section.SrccLoadingRate.ToString("F2"));
                command.Parameters.AddWithValue("@Field12", section.OtherLoading2Rate.ToString("F2"));

                command.Parameters.AddWithValue("@Tag", "SECTION");
                command.Parameters.AddWithValue("@TransGUID", sectionTransGuid);
                command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();

                // Save risk items for this section
                await SaveRenewalSectionRiskItemsWithCertOrDocNoAsync(
                    proposalNo,
                    branchId,
                    certOrDocNo,
                    sectionDeclarationNo,
                    sectionResult,
                    request,
                    connection,
                    transaction);
            }

            /// <summary>
            /// FIXED: Save section risk items WITHOUT Field20 (doesn't exist)
            /// Use Field10 to store CertOrDocNo link
            /// </summary>
            private async Task SaveSectionRiskItemsWithCertOrDocNoAsync(
                string proposalNo,
                string branchId,
                string certOrDocNo,
                string sectionDeclarationNo,
                SectionCalculationResponseDto sectionResult,
                CreateAdditionalFireCalculationRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
    INSERT INTO PolicyPremDetails (
        PolicyNo, ENDTNum, Section, ContentDesc, SMICode, SMIDetail, Location,
        Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9, Field10,
        A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16,
        Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
        @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode, @SMIDetail, @Location,
        @Field1, @Field2, @Field3, @Field4, @Field5, @Field6, @Field7, @Field8, @Field9, @Field10,
        @A1, @A2, @A3, @A4, @A5, @A6, @A7, @A8, @A9, @A10, @A11, @A12, @A13, @A14, @A15, @A16,
        @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";
                // LEGACY: items no longer link through the section's TransGUID.
                // They link through Field10 = the section's declaration number,
                // exactly like AddItemToTempGridDetails in fire.ascx.vb.

                foreach (var riskItem in sectionResult.CalculatedItems)
                {
                    // LEGACY (AddItemToTempGridDetails): a fresh CERTNO auto number is
                    // generated for EVERY risk item row (txtCertNo resets to [AUTO] per row).
                    var itemCertNo = await NextItemCertNoAsync(branchId);

                    using var command = new SqlCommand(sql, connection, transaction);

                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@ENDTNum", itemCertNo); // LEGACY: per-item certificate number (CERTNO auto number)
                    command.Parameters.AddWithValue("@Section", sectionResult.SectionID);
                    command.Parameters.AddWithValue("@ContentDesc", riskItem.ItemDescription ?? "");
                    command.Parameters.AddWithValue("@SMICode", riskItem.SMICode ?? "");
                    command.Parameters.AddWithValue("@SMIDetail", riskItem.ItemDescription ?? "");
                    command.Parameters.AddWithValue("@Location", riskItem.Location ?? "");

                    // Store base risk item and stock item info in Field fields
                    command.Parameters.AddWithValue("@Field1", sectionResult.SectionID);   // LEGACY: SectionID (cdbSectionDetails.SelectedItem.Value)
                    command.Parameters.AddWithValue("@Field2", certOrDocNo);               // LEGACY: item -> calculation link (txtCertOrDocNo)
                    command.Parameters.AddWithValue("@Field3", sectionResult.SectionID);
                    command.Parameters.AddWithValue("@Field4", "RISK_ITEM");
                    command.Parameters.AddWithValue("@Field5", riskItem.Location ?? "");
                    command.Parameters.AddWithValue("@Field6", riskItem.StockItem?.StockCode ?? "");
                    command.Parameters.AddWithValue("@Field7", riskItem.StockItem?.StockDescription ?? "");
                    command.Parameters.AddWithValue("@Field8", riskItem.StockItem?.StockSumInsured.ToString("F2") ?? "0");
                    command.Parameters.AddWithValue("@Field9", riskItem.StockItem?.StockRate.ToString("F2") ?? "0");

                    // LEGACY: Field10 = parent section's declaration number (PolicyTempDetails.ENDTNum)
                    command.Parameters.AddWithValue("@Field10", sectionDeclarationNo);

                    // Store ALL calculation fields including stock item fields
                    command.Parameters.AddWithValue("@A1", riskItem.ActualValue);
                    command.Parameters.AddWithValue("@A2", riskItem.ItemRate);
                    command.Parameters.AddWithValue("@A3", riskItem.ActualPremium);
                    command.Parameters.AddWithValue("@A4", riskItem.ShareValue);
                    command.Parameters.AddWithValue("@A5", riskItem.PremiumValue);
                    command.Parameters.AddWithValue("@A6", riskItem.MultiplyRate);
                    command.Parameters.AddWithValue("@A7", riskItem.StockSumInsured);
                    command.Parameters.AddWithValue("@A8", riskItem.StockGrossPremium);
                    command.Parameters.AddWithValue("@A9", riskItem.StockDiscountAmount);
                    command.Parameters.AddWithValue("@A10", riskItem.StockNetPremium);
                    command.Parameters.AddWithValue("@A11", riskItem.TotalSumInsured);
                    command.Parameters.AddWithValue("@A12", riskItem.TotalGrossPremium);
                    command.Parameters.AddWithValue("@A13", riskItem.FeaDiscountRate);
                    command.Parameters.AddWithValue("@A14", riskItem.FeaDiscountAmount);
                    command.Parameters.AddWithValue("@A15", riskItem.NetPremiumAfterDiscounts);
                    command.Parameters.AddWithValue("@A16", riskItem.ItemNo);
                    command.Parameters.AddWithValue("@A17", riskItem.StockItem?.StockDiscountRate ?? 0);
                    command.Parameters.AddWithValue("@A18", 0);

                    command.Parameters.AddWithValue("@Tag", "RISK_ITEM");
                    command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                    command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
            private async Task SaveRenewalSectionRiskItemsWithCertOrDocNoAsync(
                string proposalNo,
                string branchId,
                string certOrDocNo,
                string sectionDeclarationNo,
                SectionCalculationResponseDto sectionResult,
                CreateRenewalFireCalculationRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
    INSERT INTO PolicyPremDetails (
        PolicyNo, ENDTNum, Section, ContentDesc, SMICode, SMIDetail, Location,
        Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9, Field10,
        A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16,
        Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
        @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode, @SMIDetail, @Location,
        @Field1, @Field2, @Field3, @Field4, @Field5, @Field6, @Field7, @Field8, @Field9, @Field10,
        @A1, @A2, @A3, @A4, @A5, @A6, @A7, @A8, @A9, @A10, @A11, @A12, @A13, @A14, @A15, @A16,
        @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";
                // LEGACY: items no longer link through the section's TransGUID.
                // They link through Field10 = the section's declaration number,
                // exactly like AddItemToTempGridDetails in fire.ascx.vb.
                foreach (var riskItem in sectionResult.CalculatedItems)
                {
                    // LEGACY (AddItemToTempGridDetails): a fresh CERTNO auto number is
                    // generated for EVERY risk item row (txtCertNo resets to [AUTO] per row).
                    var itemCertNo = await NextItemCertNoAsync(branchId);

                    using var command = new SqlCommand(sql, connection, transaction);

                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@ENDTNum", itemCertNo); // LEGACY: per-item certificate number (CERTNO auto number)
                    command.Parameters.AddWithValue("@Section", sectionResult.SectionID);
                    command.Parameters.AddWithValue("@ContentDesc", riskItem.ItemDescription ?? "");
                    command.Parameters.AddWithValue("@SMICode", riskItem.SMICode ?? "");
                    command.Parameters.AddWithValue("@SMIDetail", riskItem.ItemDescription ?? "");
                    command.Parameters.AddWithValue("@Location", riskItem.Location ?? "");

                    // Store base risk item and stock item info in Field fields
                    command.Parameters.AddWithValue("@Field1", sectionResult.SectionID);   // LEGACY: SectionID (cdbSectionDetails.SelectedItem.Value)
                    command.Parameters.AddWithValue("@Field2", certOrDocNo);               // LEGACY: item -> calculation link (txtCertOrDocNo)
                    command.Parameters.AddWithValue("@Field3", sectionResult.SectionID);
                    command.Parameters.AddWithValue("@Field4", "RISK_ITEM");
                    command.Parameters.AddWithValue("@Field5", riskItem.Location ?? "");
                    command.Parameters.AddWithValue("@Field6", riskItem.StockItem?.StockCode ?? "");
                    command.Parameters.AddWithValue("@Field7", riskItem.StockItem?.StockDescription ?? "");
                    command.Parameters.AddWithValue("@Field8", riskItem.StockItem?.StockSumInsured.ToString("F2") ?? "0");
                    command.Parameters.AddWithValue("@Field9", riskItem.StockItem?.StockRate.ToString("F2") ?? "0");

                    // LEGACY: Field10 = parent section's declaration number (PolicyTempDetails.ENDTNum)
                    command.Parameters.AddWithValue("@Field10", sectionDeclarationNo);

                    // Store ALL calculation fields including stock item fields
                    command.Parameters.AddWithValue("@A1", riskItem.ActualValue);
                    command.Parameters.AddWithValue("@A2", riskItem.ItemRate);
                    command.Parameters.AddWithValue("@A3", riskItem.ActualPremium);
                    command.Parameters.AddWithValue("@A4", riskItem.ShareValue);
                    command.Parameters.AddWithValue("@A5", riskItem.PremiumValue);
                    command.Parameters.AddWithValue("@A6", riskItem.MultiplyRate);
                    command.Parameters.AddWithValue("@A7", riskItem.StockSumInsured);
                    command.Parameters.AddWithValue("@A8", riskItem.StockGrossPremium);
                    command.Parameters.AddWithValue("@A9", riskItem.StockDiscountAmount);
                    command.Parameters.AddWithValue("@A10", riskItem.StockNetPremium);
                    command.Parameters.AddWithValue("@A11", riskItem.TotalSumInsured);
                    command.Parameters.AddWithValue("@A12", riskItem.TotalGrossPremium);
                    command.Parameters.AddWithValue("@A13", riskItem.FeaDiscountRate);
                    command.Parameters.AddWithValue("@A14", riskItem.FeaDiscountAmount);
                    command.Parameters.AddWithValue("@A15", riskItem.NetPremiumAfterDiscounts);
                    command.Parameters.AddWithValue("@A16", riskItem.ItemNo);
                    command.Parameters.AddWithValue("@A17", riskItem.StockItem?.StockDiscountRate ?? 0);
                    command.Parameters.AddWithValue("@A18", 0);

                    command.Parameters.AddWithValue("@Tag", "RISK_ITEM");
                    command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                    command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }

            /// <summary>
            /// Save proposal-level adjustments with CertOrDocNo link
            /// </summary>
            private async Task SaveProposalLevelAdjustmentsWithCertOrDocNoAsync(
            string proposalNo,
            string certOrDocNo,
            ProposalLevelAdjustmentResponseDto adjustments,
            CreateAdditionalFireCalculationRequestDto request,
            SqlConnection connection,
            SqlTransaction transaction)
            {
                var sql = @"
    INSERT INTO PolicyTempDetails (
    PolicyNo, ENDTNum, SectionID, ContentSection, Field20,
    Field1, Field18, Field28, Field29,
    A1, A2, A10, A11,
    Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
    @PolicyNo, @ENDTNum, @SectionID, @ContentSection, @Field20,
    @Field1, @Field18, @Field28, @Field29,
    @A1, @A2, @A10, @A11,
    @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";
                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@Field20", certOrDocNo); // LINK TO CALCULATION
                command.Parameters.AddWithValue("@ENDTNum", "");
                command.Parameters.AddWithValue("@SectionID", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@ContentSection", "Proposal Level Adjustments");

                command.Parameters.AddWithValue("@Field1", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@Field18", adjustments.StartingPremium.ToString());
                command.Parameters.AddWithValue("@Field28", adjustments.OtherDiscountsAmount.ToString());
                command.Parameters.AddWithValue("@Field29", adjustments.OtherLoadingsAmount.ToString());

                command.Parameters.AddWithValue("@A1", adjustments.StartingPremium);
                command.Parameters.AddWithValue("@A2", adjustments.NetPremiumDue);
                command.Parameters.AddWithValue("@A10", adjustments.OtherDiscountsAmount);
                command.Parameters.AddWithValue("@A11", adjustments.OtherLoadingsAmount);

                command.Parameters.AddWithValue("@Tag", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }
            /// <summary>
            /// Save proposal-level adjustments with CertOrDocNo link
            /// </summary>
            private async Task SaveRenewalProposalLevelAdjustmentsWithCertOrDocNoAsync(
            string proposalNo,
            string certOrDocNo,
            ProposalLevelAdjustmentResponseDto adjustments,
            CreateRenewalFireCalculationRequestDto request,
            SqlConnection connection,
            SqlTransaction transaction)
            {
                var sql = @"
    INSERT INTO PolicyTempDetails (
    PolicyNo, ENDTNum, SectionID, ContentSection, Field20,
    Field1, Field18, Field28, Field29,
    A1, A2, A10, A11,
    Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
    @PolicyNo, @ENDTNum, @SectionID, @ContentSection, @Field20,
    @Field1, @Field18, @Field28, @Field29,
    @A1, @A2, @A10, @A11,
    @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";
                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@Field20", certOrDocNo); // LINK TO CALCULATION
                command.Parameters.AddWithValue("@ENDTNum", "");
                command.Parameters.AddWithValue("@SectionID", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@ContentSection", "Proposal Level Adjustments");

                command.Parameters.AddWithValue("@Field1", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@Field18", adjustments.StartingPremium.ToString());
                command.Parameters.AddWithValue("@Field28", adjustments.OtherDiscountsAmount.ToString());
                command.Parameters.AddWithValue("@Field29", adjustments.OtherLoadingsAmount.ToString());

                command.Parameters.AddWithValue("@A1", adjustments.StartingPremium);
                command.Parameters.AddWithValue("@A2", adjustments.NetPremiumDue);
                command.Parameters.AddWithValue("@A10", adjustments.OtherDiscountsAmount);
                command.Parameters.AddWithValue("@A11", adjustments.OtherLoadingsAmount);

                command.Parameters.AddWithValue("@Tag", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }


            /// <summary>
            /// Create new PolicyDetails record for additional calculation
            /// </summary>
            private async Task CreateNewPolicyDetailsRecordAsync(
            string proposalNo,
            string certOrDocNo,
            decimal totalSumInsured,
            decimal totalPremium,
            ProRataCalculationResponseDto proRataResponse,
            CreateAdditionalFireCalculationRequestDto request,
            SqlConnection connection,
            SqlTransaction transaction)
            {
                // Get insured name from proposal
                var fullName = await GetInsuredFullNameAsync(null, connection, transaction, proposalNo);
                var sql = @"
        INSERT INTO PolicyDetails (
            PolicyNo, CertOrDocNo, EntryDate, BizOption, InsuredName,
            StartDate, EndDate, ExRate, ExCurrency, ProportionRate,
            SumInsured, GrossPremium, SumInsuredFrgn, GrossPremiumFrgn,
            ProRataDays, ProRataPremium, NetAmount, TotalRiskValue,
            A1, A2, A10, A11, A13, A14,
            Field1, Field2, Field50,
            SubmittedBy, SubmittedOn, Active, Deleted
        ) VALUES (
            @PolicyNo, @CertOrDocNo, @EntryDate, @BizOption, @InsuredName,
            @StartDate, @EndDate, @ExRate, @ExCurrency, @ProportionRate,
            @SumInsured, @GrossPremium, @SumInsuredFrgn, @GrossPremiumFrgn,
            @ProRataDays, @ProRataPremium, @NetAmount, @TotalRiskValue,
            @A1, @A2, @A10, @A11, @A13, @A14,
            @Field1, @Field2, @Field50,
            @SubmittedBy, @SubmittedOn, 1, 0
        )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                command.Parameters.AddWithValue("@EntryDate", DateTime.Now);
                command.Parameters.AddWithValue("@BizOption", request.CalculationType);
                command.Parameters.AddWithValue("@InsuredName", fullName);
                command.Parameters.AddWithValue("@StartDate", DateTime.Now);
                command.Parameters.AddWithValue("@EndDate", DateTime.Now.AddDays(request.CoverDays));
                command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);

                command.Parameters.AddWithValue("@SumInsured", totalSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@GrossPremium", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@SumInsuredFrgn", totalSumInsured * request.ExchangeRate);
                command.Parameters.AddWithValue("@GrossPremiumFrgn", proRataResponse.PremiumDue * request.ExchangeRate);
                command.Parameters.AddWithValue("@ProRataDays", request.CoverDays);
                command.Parameters.AddWithValue("@ProRataPremium", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@NetAmount", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@TotalRiskValue", totalSumInsured);
                command.Parameters.AddWithValue("@A1", totalSumInsured);
                command.Parameters.AddWithValue("@A2", totalPremium);
                command.Parameters.AddWithValue("@A10", request.OtherDiscountsRate);
                command.Parameters.AddWithValue("@A11", request.OtherLoadingsRate);
                command.Parameters.AddWithValue("@A13", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@A14", proRataResponse.PremiumDue);

                command.Parameters.AddWithValue("@Field1", request.Remarks ?? "ADDITIONAL_CALCULATION");
                command.Parameters.AddWithValue("@Field2", $"Calculated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                command.Parameters.AddWithValue("@Field50", request.CalculationType);
                command.Parameters.AddWithValue("@SubmittedBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmittedOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }
            /// <summary>
            /// Create new PolicyDetails record for additional calculation
            /// </summary>
            private async Task CreateRenewalPolicyDetailsRecordAsync(
            string proposalNo,
            string certOrDocNo,
            decimal totalSumInsured,
            decimal totalPremium,
            ProRataCalculationResponseDto proRataResponse,
            CreateRenewalFireCalculationRequestDto request,
            SqlConnection connection,
            SqlTransaction transaction)
            {
                // Get insured name from proposal
                var fullName = await GetInsuredFullNameAsync(null, connection, transaction, proposalNo);
                var sql = @"
        INSERT INTO PolicyDetails (
            PolicyNo, CoPolicyNo, CertOrDocNo, EntryDate, BizOption, InsuredName,
            StartDate, EndDate, ExRate, ExCurrency, ProportionRate,
            SumInsured, GrossPremium, SumInsuredFrgn, GrossPremiumFrgn,
            ProRataDays, ProRataPremium, NetAmount, TotalRiskValue,
            A1, A2, A10, A11, A13, A14,
            Field1, Field2, Field50,
            SubmittedBy, SubmittedOn, Active, Deleted
        ) VALUES (
            @PolicyNo, @CoPolicyNo, @CertOrDocNo, @EntryDate, @BizOption, @InsuredName,
            @StartDate, @EndDate, @ExRate, @ExCurrency, @ProportionRate,
            @SumInsured, @GrossPremium, @SumInsuredFrgn, @GrossPremiumFrgn,
            @ProRataDays, @ProRataPremium, @NetAmount, @TotalRiskValue,
            @A1, @A2, @A10, @A11, @A13, @A14,
            @Field1, @Field2, @Field50,
            @SubmittedBy, @SubmittedOn, 1, 0
        )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", request.NewPolicyNo);
                command.Parameters.AddWithValue("@CoPolicyNo", request.OriginalPolicyNo);
                command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                command.Parameters.AddWithValue("@EntryDate", DateTime.Now);
                command.Parameters.AddWithValue("@BizOption", "RENEWAL");
                command.Parameters.AddWithValue("@InsuredName", fullName);
                command.Parameters.AddWithValue("@StartDate", DateTime.Now);
                command.Parameters.AddWithValue("@EndDate", DateTime.Now.AddDays(request.CoverDays));
                command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);

                command.Parameters.AddWithValue("@SumInsured", totalSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@GrossPremium", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@SumInsuredFrgn", totalSumInsured * request.ExchangeRate);
                command.Parameters.AddWithValue("@GrossPremiumFrgn", proRataResponse.PremiumDue * request.ExchangeRate);
                command.Parameters.AddWithValue("@ProRataDays", request.CoverDays);
                command.Parameters.AddWithValue("@ProRataPremium", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@NetAmount", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@TotalRiskValue", totalSumInsured);
                command.Parameters.AddWithValue("@A1", totalSumInsured);
                command.Parameters.AddWithValue("@A2", totalPremium);
                command.Parameters.AddWithValue("@A10", request.OtherDiscountsRate);
                command.Parameters.AddWithValue("@A11", request.OtherLoadingsRate);
                command.Parameters.AddWithValue("@A13", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@A14", proRataResponse.PremiumDue);

                command.Parameters.AddWithValue("@Field1", request.Remarks ?? "ADDITIONAL_CALCULATION");
                command.Parameters.AddWithValue("@Field2", $"Calculated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                command.Parameters.AddWithValue("@Field50", "ADDITIONAL");
                command.Parameters.AddWithValue("@SubmittedBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmittedOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }
            /// <summary>
            /// Update existing PolicyDetails record
            /// </summary>
            private async Task UpdateExistingPolicyDetailsRecordAsync(
            string detailID,
            decimal totalSumInsured,
            decimal totalPremium,
            ProRataCalculationResponseDto proRataResponse,
            UpdateFireCalculationRequestDto request,
            SqlConnection connection,
            SqlTransaction transaction)
            {
                var sql = @"
    UPDATE PolicyDetails SET
    SumInsured = @SumInsured,
    GrossPremium = @GrossPremium,
    SumInsuredFrgn = @SumInsuredFrgn,
    GrossPremiumFrgn = @GrossPremiumFrgn,
    ProRataDays = @ProRataDays,
    ProRataPremium = @ProRataPremium,
    NetAmount = @NetAmount,
    TotalRiskValue = @TotalRiskValue,
    ProportionRate = @ProportionRate,
    ExRate = @ExRate,
    ExCurrency = @ExCurrency,
    A1 = @A1,
    A2 = @A2,
    A10 = @A10,
    A11 = @A11,
    A13 = @A13,
    A14 = @A14,
    Field1 = @Field1,
    Field2 = @Field2,
    Field50 = CASE WHEN Field50 = 'UNCOMPLETED' THEN 'COMPLETED' ELSE Field50 END,
    ModifiedBy = @ModifiedBy,
    ModifiedOn = @ModifiedOn
    WHERE DetailID = @DetailID";
                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@SumInsured", totalSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@GrossPremium", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@SumInsuredFrgn", totalSumInsured * request.ExchangeRate);
                command.Parameters.AddWithValue("@GrossPremiumFrgn", proRataResponse.PremiumDue * request.ExchangeRate);
                command.Parameters.AddWithValue("@ProRataDays", request.CoverDays);
                command.Parameters.AddWithValue("@ProRataPremium", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@NetAmount", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@TotalRiskValue", totalSumInsured);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);
                command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);

                command.Parameters.AddWithValue("@A1", totalSumInsured);
                command.Parameters.AddWithValue("@A2", totalPremium);
                command.Parameters.AddWithValue("@A10", request.OtherDiscountsRate);
                command.Parameters.AddWithValue("@A11", request.OtherLoadingsRate);
                command.Parameters.AddWithValue("@A13", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@A14", proRataResponse.PremiumDue);

                command.Parameters.AddWithValue("@Field1", request.Remarks ?? "RECALCULATED");
                command.Parameters.AddWithValue("@Field2", $"Recalculated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                command.Parameters.AddWithValue("@ModifiedBy", request.RecalculatedBy);
                command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                command.Parameters.AddWithValue("@DetailID", detailID);

                await command.ExecuteNonQueryAsync();
            }
            /// <summary>
            /// FIXED: Delete calculation data - use Field10 instead of Field20
            /// </summary>
            private async Task DeleteCalculationDataByCertOrDocNoAsync(
                string proposalNo,
                string certOrDocNo,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                // LEGACY: items link to the calculation through Field2 = CertOrDocNo.
                // Field10 is matched as well so rows saved by the pre-legacy code
                // (which stored the CertOrDocNo in Field10) are also cleaned up.
                var deleteRiskItemsSql = @"
    DELETE FROM PolicyPremDetails
    WHERE PolicyNo = @PolicyNo AND (Field2 = @CertOrDocNo OR Field10 = @CertOrDocNo)";

                using (var cmd = new SqlCommand(deleteRiskItemsSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    cmd.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Delete sections using Field20 (this table DOES have Field20)
                var deleteSectionsSql = @"
    DELETE FROM PolicyTempDetails 
    WHERE PolicyNo = @PolicyNo AND Field20 = @CertOrDocNo";

                using (var cmd = new SqlCommand(deleteSectionsSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    cmd.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                    await cmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation("Deleted calculation data for CertOrDocNo {CertOrDocNo}", certOrDocNo);
            }
            /// <summary>
            /// Overload for GetInsuredFullNameAsync with proposalNo parameter
            /// </summary>
            private async Task<string> GetInsuredFullNameAsync(
            string insuredId,
            SqlConnection connection,
            SqlTransaction transaction,
            string proposalNo = null)
            {
                if (!string.IsNullOrEmpty(proposalNo))
                {
                    var sql = @"
    SELECT InsSurname, InsFirstname, InsOthernames
    FROM Policies WITH (NOLOCK)
    WHERE PolicyNo = @PolicyNo";
                    using var cmd = new SqlCommand(sql, connection, transaction);
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        return $"{reader.GetStringOrDefault("InsSurname")} {reader.GetStringOrDefault("InsFirstname")} {reader.GetStringOrDefault("InsOthernames")}".Trim();
                    }
                }

                // Existing logic for insuredId
                if (!string.IsNullOrEmpty(insuredId))
                {
                    var sql = @"
            SELECT SurName, OtherNames 
            FROM InsuredClients WITH (NOLOCK)
            WHERE InsuredID = @InsuredID";

                    using var cmd = new SqlCommand(sql, connection, transaction);
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@InsuredID", insuredId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        // LEGACY: two-part name model. Corporates carry the whole company
                        // name in SurName with OtherNames empty, so one concatenation is
                        // correct for both. ClientType is not a reliable discriminator here.
                        return $"{reader.GetStringOrDefault("SurName")} {reader.GetStringOrDefault("OtherNames")}".Trim();
                    }
                }

                return "Unknown";
            }

            // Add timeout to SaveInitialPolicyDetailsAsync as well
            private async Task SaveInitialPolicyDetailsAsync(
                string proposalNo,
                EnhancedCreateProposalRequestDto request,
                LookupData lookupData,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                // LEGACY: DECLARATION_NO auto number (branch + subrisk), as in InsertIntoPolicyDetails
                var certOrDocNo = await NextCalcCertAsync(request.BranchID, request.SubriskID, EndorseType.None);

                // Get insured name from request
                var fullName = string.IsNullOrEmpty(request.InsuredID)
                    ? $"{request.Surname} {request.FirstName} {request.OtherNames}".Trim()
                    : await GetInsuredFullNameAsync(request.InsuredID, connection, transaction);

                var sql = @"
            INSERT INTO PolicyDetails (
                PolicyNo, CertOrDocNo, EntryDate, BizOption, InsuredName,
                StartDate, EndDate, ExRate, ExCurrency, ProportionRate,
                SumInsured, GrossPremium, SumInsuredFrgn, GrossPremiumFrgn,
                ProRataDays, ProRataPremium, NetAmount, TotalRiskValue,
                Field1, Field2,Field45, Field48, Field50,
                SubmittedBy, SubmittedOn, Active, Deleted
            ) VALUES (
                @PolicyNo, @CertOrDocNo, @EntryDate, @BizOption, @InsuredName,
                @StartDate, @EndDate, @ExRate, @ExCurrency, @ProportionRate,
                0, 0, 0, 0,
                @ProRataDays, 0, 0, 0,
                @Field1, @Field2, @Field45, @Field48, @Field50,
                @SubmittedBy, @SubmittedOn, 1, 0
            )";

                using var command = new SqlCommand(sql, connection, transaction);
                command.CommandTimeout = 60;

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                command.Parameters.AddWithValue("@EntryDate", DateTime.Now);
                command.Parameters.AddWithValue("@BizOption", "New");
                command.Parameters.AddWithValue("@InsuredName", fullName);
                command.Parameters.AddWithValue("@StartDate", request.StartDate);
                command.Parameters.AddWithValue("@EndDate", request.EndDate);
                command.Parameters.AddWithValue("@ExRate", request.ExRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);
                command.Parameters.AddWithValue("@ProRataDays", (request.EndDate - request.StartDate).Days);
                command.Parameters.AddWithValue("@Field1", "PROPOSAL_CREATED");
                command.Parameters.AddWithValue("@Field2", $"Created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                command.Parameters.AddWithValue("@Field45", request.AccountType);
                command.Parameters.AddWithValue("@Field48", request.BusChannel);
                command.Parameters.AddWithValue("@Field50", "CREATED");
                command.Parameters.AddWithValue("@SubmittedBy", "SYSTEM");
                command.Parameters.AddWithValue("@SubmittedOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }

            // Helper to get full name without fetching entire proposal
            private async Task<string> GetInsuredFullNameAsync(
                string insuredId,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
            SELECT SurName, OtherNames 
            FROM InsuredClients WITH (NOLOCK)
            WHERE InsuredID = @InsuredID";

                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddWithValue("@InsuredID", insuredId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // LEGACY: two-part name model (see overload above).
                    return $"{reader.GetStringOrDefault("SurName")} {reader.GetStringOrDefault("OtherNames")}".Trim();
                }
                return "Unknown";
            }
            private string GenerateInsuredId() // Remove Async suffix
            {
                var now = DateTime.Now;
                var dayOfYear = now.DayOfYear;
                var millisecondsSinceMidnight = (int)now.TimeOfDay.TotalMilliseconds;

                return $"INS{now:yy}{dayOfYear:D3}{millisecondsSinceMidnight:D8}";
            }

            // Parse insured name for individuals
            private (string Surname, string FirstName, string OtherNames) ParseInsuredName(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName))
                    return ("", "", "");

                var parts = fullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0)
                    return ("", "", "");
                if (parts.Length == 1)
                    return (parts[0], "", "");
                if (parts.Length == 2)
                    return (parts[0], parts[1], "");

                // Surname FirstName OtherNames
                return (parts[0], parts[1], string.Join(" ", parts.Skip(2)));
            }




            // Save new insured client to InsuredClients table
            private async Task SaveInsuredClientAsync(InsuredDetailsData insured, SqlConnection connection, SqlTransaction transaction)
            {
                var query = @"
            INSERT INTO InsuredClients (
                InsuredID, SurName, OtherNames, FullName, 
                Address, MobilePhone, Landphone, Email, Occupation, 
                ClientType, Active, Deleted, SubmittedOn
            ) VALUES (
                @InsuredID, @SurName, @OtherNames, @FullName,
                @Address, @MobilePhone, @Landphone, @Email, @Occupation,
                @ClientType, 1, 0, GETDATE()
            )";

                using var cmd = new SqlCommand(query, connection, transaction);
                // LEGACY: InsuredClients has no FirstName column — the incoming first name
                // is folded into OtherNames (SurName + OtherNames is the whole name model).
                var otherNames = $"{insured.FirstName} {insured.OtherNames}".Trim();

                string fullName = insured.InsuredType == "CORPORATE"
                    ? insured.Surname ?? ""
                    : $"{insured.Surname} {otherNames}".Trim();

                cmd.Parameters.AddWithValue("@InsuredID", insured.InsuredID);
                cmd.Parameters.AddWithValue("@SurName", insured.Surname ?? "");
                cmd.Parameters.AddWithValue("@OtherNames", otherNames);
                cmd.Parameters.AddWithValue("@FullName", fullName);
                cmd.Parameters.AddWithValue("@Address", insured.Address ?? "");
                cmd.Parameters.AddWithValue("@MobilePhone", insured.MobilePhone ?? "");
                cmd.Parameters.AddWithValue("@Landphone", insured.LandPhone ?? "");
                cmd.Parameters.AddWithValue("@Email", insured.Email ?? "");
                cmd.Parameters.AddWithValue("@Occupation", insured.Occupation ?? "");
                cmd.Parameters.AddWithValue("@ClientType", insured.InsuredType ?? "");

                await cmd.ExecuteNonQueryAsync();
            }




            private async Task<InsuredDetailsData> GetInsuredDetailsFromCompanyIdAsync(
                string companyId, SqlConnection connection, SqlTransaction transaction)
            {
                var sql = @"
            SELECT CompanyID, Company, Address, MobilePhone, LandPhone, Email
            FROM Companies 
            WHERE CompanyID = @CompanyID AND Deleted = 0";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@CompanyID", companyId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var companyName = reader.GetStringOrDefault("Company");
                    return new InsuredDetailsData
                    {
                        InsuredID = companyId,
                        Surname = companyName,
                        FirstName = "",
                        OtherNames = "",
                        Address = reader.GetStringOrDefault("Address"),
                        MobilePhone = reader.GetStringOrDefault("MobilePhone"),
                        LandPhone = reader.GetStringOrDefault("LandPhone"),
                        Email = reader.GetStringOrDefault("Email"),
                        Occupation = "CORPORATE"
                    };
                }

                return null;
            }




        // Helper method to get existing CertOrDocNo or generate new one
        private async Task<string> GetOrGenerateCertOrDocNoAsync(
 string proposalNo, SqlConnection connection, SqlTransaction transaction)
        {
            if (string.IsNullOrWhiteSpace(proposalNo))
                throw new ArgumentException(
                    "ProposalNo is empty. The request body's ProposalNo was not set from the route.",
                    nameof(proposalNo));

            // Reuse the calculation row created with the proposal.
            // ORDER BY makes the pick deterministic when a policy has several calculations
            // (ASC = the original one; the Additional endpoint creates its own rows).
            const string existingSql = @"
        SELECT TOP 1 CertOrDocNo
        FROM PolicyDetails
        WHERE PolicyNo = @PolicyNo
          AND Deleted = 0
          AND ISNULL(CertOrDocNo, '') <> ''
          AND ISNULL(BizOption, '') NOT LIKE 'Old%'
          AND ISNULL(BizOption, '') <> 'EXPIRED'
        ORDER BY SubmittedOn ASC, DetailID ASC";

            using (var command = new SqlCommand(existingSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                var existing = Convert.ToString(await command.ExecuteScalarAsync());
                if (!string.IsNullOrWhiteSpace(existing))
                    return existing;
            }

            // No calculation row yet: issue a DECLARATION_NO (branch + subrisk).
            const string proposalSql =
                "SELECT TOP 1 BranchID, SubRiskID FROM Policies WHERE PolicyNo = @PolicyNo";

            string branchId = null;
            string subRiskId = null;
            using (var proposalCommand = new SqlCommand(proposalSql, connection, transaction))
            {
                proposalCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);
                using var reader = await proposalCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    branchId = reader["BranchID"]?.ToString();
                    subRiskId = reader["SubRiskID"]?.ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(branchId) || string.IsNullOrWhiteSpace(subRiskId))
                throw new InvalidOperationException(
                    $"Cannot issue DECLARATION_NO: policy '{proposalNo}' was not found in database " +
                    $"'{connection.Database}' or has no BranchID/SubRiskID.");

            return await NextCalcCertAsync(branchId, subRiskId, EndorseType.None);
        }
        // 2. UPDATE GetProposalAsync method - change SELECT from Proposals to Policies
        public async Task<ProposalDto> GetProposalAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
            SELECT p.PolicyNo, p.TransDate, p.CoPolicyNo, p.CompanyID, p.BranchID, p.Branch,
                   p.BizSource, p.SourceType, p.SubRiskID, p.SubRisk, p.PartyID, p.Party, p.MktStaffID, p.MktStaff,
                   p.InsuredID, p.InsSurname, p.InsFirstname, p.InsOthernames, p.InsAddress,
                   p.InsStateID, p.InsMobilePhone, p.InsLandPhone, p.InsEmail, p.InsFaxNo,
                   p.InsOccupation, p.isProposal, p.StartDate, p.EndDate, p.ExRate, p.ExCurrency,
                   p.PremiumRate, p.ProportionRate, p.SumInsured, p.GrossPremium, p.SumInsuredFrgn,
                   p.GrossPremiumFrgn, p.ProRataDays, p.ProRataPremium, p.Remarks, p.Deleted,
                   p.Active, p.TransSTATUS, p.SubmittedBy, p.SubmittedOn,
                   p.ModifiedBy, p.ModifiedOn, p.LeadID as LeaderID, p.Leader,
                   sr.RiskID
            FROM Policies p
            LEFT JOIN SubRisks sr ON p.SubRiskID = sr.SubRiskID
            WHERE p.PolicyNo = @ProposalNo AND p.Deleted = 0";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ProposalNo", proposalNo);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapProposalFromReader(reader);
                }

                return null;
            }

            /// <summary>
            /// The currency and exchange rate saved on a policy's header row -- the rate
            /// the policy was opened with, and the one every calculation for it is
            /// converted against (see GetSavedExchangeContextAsync). Returns null if the
            /// policy doesn't exist, rather than the ExRate=1/"NAIRA" fallback that
            /// method uses internally for safe calculation defaults.
            /// </summary>
            public async Task<PolicyExchangeRateDto> GetPolicyExchangeRateAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
        SELECT TOP 1 PolicyNo, ExRate, ExCurrency
        FROM Policies
        WHERE PolicyNo = @ProposalNo AND Deleted = 0";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ProposalNo", proposalNo);

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return new PolicyExchangeRateDto
                {
                    ProposalNo = reader.GetStringOrDefault("PolicyNo"),
                    Currency = reader.GetStringOrDefault("ExCurrency") ?? "NAIRA",
                    ExchangeRate = reader.GetValueOrDefault<decimal>("ExRate")
                };
            }

            /// <summary>
            /// Calculate individual risk items with clean separation of input and output
            /// </summary>
            public async Task<RiskItemCalculationResponseDto> CalculateRiskItemsAsync(RiskItemCalculationRequestDto request)
            {
                try
                {
                    var response = new RiskItemCalculationResponseDto
                    {
                        Success = true
                    };

                    decimal totalActualValue = 0;
                    decimal totalActualPremium = 0;
                    decimal totalShareValue = 0;
                    decimal totalSharePremium = 0;
                    decimal totalNetPremiumAfterDiscounts = 0;

                    foreach (var inputItem in request.RiskItems)
                    {
                        var calculatedItem = new CalculatedRiskItemDto
                        {
                            // Copy input properties
                            ItemNo = inputItem.ItemNo,
                            SMICode = inputItem.SMICode,
                            SMIDetail = inputItem.SMIDetail,
                            ItemDescription = inputItem.ItemDescription,
                            ActualValue = inputItem.ActualValue,
                            ItemRate = inputItem.ItemRate,
                            MultiplyRate = inputItem.MultiplyRate,
                            SectionID = inputItem.SectionID,
                            Location = inputItem.Location,
                            StockItem = inputItem.StockItem,
                            FeaDiscountRate = inputItem.FeaDiscountRate,
                            Discounts = inputItem.Discounts,   // NEW
                            Loadings = inputItem.Loadings      // NEW
                        };

                        // Calculate share value based on proportion rate
                        calculatedItem.ShareValue = calculatedItem.ActualValue * (request.ProportionRate / 100);

                        // Calculate premiums
                        if (calculatedItem.MultiplyRate > 0)
                        {
                            calculatedItem.ActualPremium = calculatedItem.ActualValue * (calculatedItem.ItemRate / 100) * (calculatedItem.MultiplyRate / 100);
                            calculatedItem.PremiumValue = calculatedItem.ShareValue * (calculatedItem.ItemRate / 100) * (calculatedItem.MultiplyRate / 100);
                        }
                        else
                        {
                            calculatedItem.ActualPremium = calculatedItem.ActualValue * (calculatedItem.ItemRate / 100);
                            calculatedItem.PremiumValue = calculatedItem.ShareValue * (calculatedItem.ItemRate / 100);
                        }

                        // STEP 2: Calculate stock item if provided
                        decimal stockNetPremium = 0;
                        if (calculatedItem.StockItem != null)
                        {
                            var stock = calculatedItem.StockItem;

                            // Calculate stock gross premium
                            calculatedItem.StockGrossPremium = stock.StockSumInsured * (stock.StockRate / 100);

                            // Apply stock discount
                            if (stock.StockDiscountRate > 0)
                            {
                                calculatedItem.StockDiscountAmount = calculatedItem.StockGrossPremium * (stock.StockDiscountRate / 100);
                                calculatedItem.StockNetPremium = calculatedItem.StockGrossPremium - calculatedItem.StockDiscountAmount;
                            }
                            else
                            {
                                calculatedItem.StockDiscountAmount = 0;
                                calculatedItem.StockNetPremium = calculatedItem.StockGrossPremium;
                            }

                            // Store in calculated item
                            calculatedItem.StockSumInsured = stock.StockSumInsured;

                        }

                        // STEP 3: Aggregate risk item + stock item
                        calculatedItem.TotalSumInsured = calculatedItem.ActualValue + calculatedItem.StockSumInsured;
                        calculatedItem.TotalGrossPremium = calculatedItem.PremiumValue + calculatedItem.StockNetPremium;

                        // STEP 4: Apply item-level adjustments to TOTAL premium —
                        // dynamic (name + basis) if supplied, else the legacy
                        // FEA-only path (unchanged for every existing caller).
                        var itemDiscounts = new List<FireAdjustmentDto>(inputItem.Discounts ?? new List<FireAdjustmentDto>());
                        var itemLoadings = new List<FireAdjustmentDto>(inputItem.Loadings ?? new List<FireAdjustmentDto>());

                        if (itemDiscounts.Any() || itemLoadings.Any())
                        {
                            // Don't silently drop a legacy FeaDiscountRate that was set
                            // alongside the new lists — fold it in first, on Premium basis,
                            // exactly where the old code applied it.
                            if (calculatedItem.FeaDiscountRate > 0 &&
                                FindFireAdjustment(itemDiscounts, FireAdjustmentTypes.FeaDiscount) == null)
                            {
                                itemDiscounts.Insert(0, new FireAdjustmentDto
                                {
                                    AdjustmentName = FireAdjustmentTypes.FeaDiscount,
                                    AdjustmentType = "DISCOUNT",
                                    Rate = calculatedItem.FeaDiscountRate,
                                    AppliedOn = AdjustmentBasis.Premium,
                                    SequenceOrder = 0
                                });
                            }

                            var itemAdjustments = ApplyRiskItemDynamicAdjustments(new FireRiskItemDynamicAdjustmentRequestDto
                            {
                                ItemSumInsured = calculatedItem.TotalSumInsured,
                                ItemGrossPremium = calculatedItem.TotalGrossPremium,
                                Discounts = itemDiscounts,
                                Loadings = itemLoadings
                            });

                            if (!itemAdjustments.Success)
                            {
                                return new RiskItemCalculationResponseDto
                                {
                                    Success = false,
                                    Message = $"Item {calculatedItem.ItemNo} adjustments failed: {itemAdjustments.Message}"
                                };
                            }

                            calculatedItem.AppliedLoadings = itemAdjustments.LoadingsApplied;
                            calculatedItem.AppliedDiscounts = itemAdjustments.DiscountsApplied;
                            calculatedItem.TotalItemLoadingAmount = itemAdjustments.TotalLoadingAmount;
                            calculatedItem.TotalItemDiscountAmount = itemAdjustments.TotalDiscountAmount;
                            calculatedItem.NetPremiumAfterDiscounts = itemAdjustments.NetPremiumDue;

                            // Back-fill the one item-level name with a fixed-column home
                            // (A13 rate / A14 amount) so legacy reads stay correct.
                            var fea = FindFireAdjustment(itemAdjustments.DiscountsApplied, FireAdjustmentTypes.FeaDiscount);
                            calculatedItem.FeaDiscountRate = fea?.Rate ?? 0;
                            calculatedItem.FeaDiscountAmount = fea?.Amount ?? 0;
                        }
                        else if (calculatedItem.FeaDiscountRate > 0)
                        {
                            calculatedItem.FeaDiscountAmount = calculatedItem.TotalGrossPremium * (calculatedItem.FeaDiscountRate / 100);
                            calculatedItem.NetPremiumAfterDiscounts = calculatedItem.TotalGrossPremium - calculatedItem.FeaDiscountAmount;
                        }
                        else
                        {
                            calculatedItem.FeaDiscountAmount = 0;
                            calculatedItem.NetPremiumAfterDiscounts = calculatedItem.TotalGrossPremium;
                        }

                        response.CalculatedItems.Add(calculatedItem);
                        decimal stockShareValue = calculatedItem.StockSumInsured * (request.ProportionRate / 100);

                        // Update totals (using aggregated values)
                        totalActualValue += calculatedItem.TotalSumInsured;                         // full SI (main + stock), unproportioned
                        totalActualPremium += calculatedItem.ActualPremium + calculatedItem.StockGrossPremium; // full GROSS premium
                        totalShareValue += calculatedItem.ShareValue + stockShareValue;            // proportioned SI (SI units — FIXED)
                        totalSharePremium += calculatedItem.TotalGrossPremium;                       // share premium before FEA (main share + stock net)
                        totalNetPremiumAfterDiscounts += calculatedItem.NetPremiumAfterDiscounts;
                    }

                    response.Subrisk = request.SubRisk;
                    response.TotalActualValue = totalActualValue;
                    response.TotalActualPremium = totalActualPremium;
                    response.TotalShareValue = totalShareValue;
                    response.TotalSharePremium = totalSharePremium;
                    response.TotalNetPremiumAfterDiscounts = totalNetPremiumAfterDiscounts;


                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating risk items");
                    return new RiskItemCalculationResponseDto
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }


            // Add this method to generate CertOrDocNo
            // NOTE: superseded by NextCalcCertAsync (legacy DECLARATION_NO auto number).
            // Kept because ProposalService is a partial class and other files may still call it.
            private async Task<string> GenerateCertOrDocNoAsync(string proposalNo, string riskClass, SqlConnection connection, SqlTransaction transaction)
            {
                var now = DateTime.Now;
                var prefix = $"{riskClass}{now:yy}";

                // MAX-based approach avoids duplicates from deletions or gaps
                var sql = @"
            SELECT ISNULL(MAX(CAST(SUBSTRING(CertOrDocNo, @PrefixLen + 1, 6) AS INT)), 0)
            FROM PolicyDetails 
            WHERE CertOrDocNo LIKE @Prefix
            AND LEN(CertOrDocNo) = @PrefixLen + 6
            AND ISNUMERIC(SUBSTRING(CertOrDocNo, @PrefixLen + 1, 6)) = 1";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@Prefix", $"{prefix}%");
                command.Parameters.AddWithValue("@PrefixLen", prefix.Length);

                var maxSeq = (int)await command.ExecuteScalarAsync();
                return $"{prefix}{(maxSeq + 1):D6}";
            }

            /// <summary>
            /// Calculate section aggregates
            /// </summary>
            public async Task<SectionAggregateResponseDto> CalculateSectionAggregatesAsync(SectionAggregateRequestDto request)
            {
                try
                {
                    return new SectionAggregateResponseDto
                    {
                        AggregateSumInsured = request.CalculatedItems.Sum(x => x.TotalSumInsured),
                        AggregatePremium = request.CalculatedItems.Sum(x => x.NetPremiumAfterDiscounts),
                        Success = true,
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating section aggregates");
                    return new SectionAggregateResponseDto
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            // Updated ApplySectionAdjustmentsAsync - loadings applied BEFORE discounts
            public async Task<SectionAdjustmentResponseDto> ApplySectionAdjustmentsAsync(SectionAdjustmentRequestDto request)
            {
                try
                {
                    var response = new SectionAdjustmentResponseDto
                    {
                        StartingPremium = request.AggregatePremium,
                        Success = true
                    };

                    decimal workingPremium = request.AggregatePremium;

                    // Apply loadings in sequence FIRST
                    if (request.TheftLoadingRate > 0)
                    {
                        response.TheftLoadingAmount = workingPremium * (request.TheftLoadingRate / 100);
                        workingPremium += response.TheftLoadingAmount;
                        response.TheftLoadingNetAmount = workingPremium;
                    }

                    if (request.SrccLoadingRate > 0)
                    {
                        response.SrccLoadingAmount = workingPremium * (request.SrccLoadingRate / 100);
                        workingPremium += response.SrccLoadingAmount;
                        response.SrccLoadingNetAmount = workingPremium;
                    }

                    if (request.OtherLoading2Rate > 0)
                    {
                        response.OtherLoading2Amount = workingPremium * (request.OtherLoading2Rate / 100);
                        workingPremium += response.OtherLoading2Amount;
                        response.OtherLoading2NetAmount = workingPremium;
                    }

                    // Apply discounts in sequence AFTER loadings
                    if (request.SpecialDiscountRate > 0)
                    {
                        response.SpecialDiscountAmount = workingPremium * (request.SpecialDiscountRate / 100);
                        workingPremium -= response.SpecialDiscountAmount;
                        response.SpecialDiscountNetAmount = workingPremium;
                    }

                    if (request.DeductibleDiscountRate > 0)
                    {
                        response.DeductibleDiscountAmount = workingPremium * (request.DeductibleDiscountRate / 100);
                        workingPremium -= response.DeductibleDiscountAmount;
                        response.DeductibleDiscountNetAmount = workingPremium;
                    }

                    if (request.SpreadDiscountRate > 0)
                    {
                        response.SpreadDiscountAmount = workingPremium * (request.SpreadDiscountRate / 100);
                        workingPremium -= response.SpreadDiscountAmount;
                        response.SpreadDiscountNetAmount = workingPremium;
                    }

                    if (request.LtaDiscountRate > 0)
                    {
                        response.LtaDiscountAmount = workingPremium * (request.LtaDiscountRate / 100);
                        workingPremium -= response.LtaDiscountAmount;
                        response.LtaDiscountNetAmount = workingPremium;
                    }

                    response.NetPremiumDue = workingPremium;

                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying section adjustments");
                    return new SectionAdjustmentResponseDto
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            /// <summary>
            /// Calculate pro-rata premium
            /// </summary>
            public async Task<ProRataCalculationResponseDto> CalculateProRataAsync(ProRataCalculationRequestDto request)
            {
                try
                {
                    var response = new ProRataCalculationResponseDto
                    {
                        NetPremiumDue = request.NetPremiumDue,
                        CoverDays = request.CoverDays,
                        Success = true
                    };

                    if (request.CoverDays < 365)
                    {
                        response.ProRataPremium = request.NetPremiumDue * request.CoverDays / 365;
                        response.PremiumDue = response.ProRataPremium;
                        response.IsProRated = true;
                    }
                    else
                    {
                        response.ProRataPremium = 0;
                        response.PremiumDue = request.NetPremiumDue;
                        response.IsProRated = false;
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating pro-rata");
                    return new ProRataCalculationResponseDto
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            // NEW: Apply proposal-level adjustments (only 2 rates)
            public async Task<ProposalLevelAdjustmentResponseDto> ApplyProposalLevelAdjustmentsAsync(
                ProposalAdjustmentRequestDto request)
            {
                try
                {
                    var response = new ProposalLevelAdjustmentResponseDto
                    {
                        StartingPremium = request.TotalAggregatePremium,
                        Success = true
                    };

                    decimal workingPremium = request.TotalAggregatePremium;

                    // Apply other discounts
                    if (request.OtherDiscountsRate > 0)
                    {
                        response.OtherDiscountsAmount = workingPremium * (request.OtherDiscountsRate / 100);
                        workingPremium -= response.OtherDiscountsAmount;
                        response.OtherDiscountsNetAmount = workingPremium;
                    }

                    // Apply other loadings
                    if (request.OtherLoadingsRate > 0)
                    {
                        response.OtherLoadingsAmount = workingPremium * (request.OtherLoadingsRate / 100);
                        workingPremium += response.OtherLoadingsAmount;
                        response.OtherLoadingsNetAmount = workingPremium;
                    }

                    response.NetPremiumDue = workingPremium;
                    response.Message = "Proposal-level adjustments applied successfully";

                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying proposal-level adjustments");
                    return new ProposalLevelAdjustmentResponseDto
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            // 3. UPDATE GetAllProposalsAsync method - change SELECT from Proposals to Policies
            public async Task<PaginatedResult<ProposalDto>> GetAllProposalsAsync(ProposalFilterDto filter)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var whereConditions = new List<string>();
                var parameters = new List<SqlParameter>();

                whereConditions.Add("p.Deleted = 0");

                // Build dynamic WHERE clause (same as before)
                if (!string.IsNullOrEmpty(filter.BranchId))
                {
                    whereConditions.Add("BranchID = @BranchId");
                    parameters.Add(new SqlParameter("@BranchId", filter.BranchId));
                }

                if (!string.IsNullOrEmpty(filter.Status))
                {
                    whereConditions.Add("TransSTATUS = @Status");
                    parameters.Add(new SqlParameter("@Status", filter.Status));
                }

                if (!string.IsNullOrEmpty(filter.PartyId))
                {
                    whereConditions.Add("PartyID = @PartyId");
                    parameters.Add(new SqlParameter("@PartyId", filter.PartyId));
                }

                if (!string.IsNullOrEmpty(filter.MktStaffId))
                {
                    whereConditions.Add("MktStaffID = @MktStaffId");
                    parameters.Add(new SqlParameter("@MktStaffId", filter.MktStaffId));
                }

                if (!string.IsNullOrEmpty(filter.RiskClass))
                {
                    whereConditions.Add("sr.RiskID = @RiskClass");
                    parameters.Add(new SqlParameter("@RiskClass", filter.RiskClass));
                }

                if (filter.StartDate.HasValue)
                {
                    whereConditions.Add("TransDate >= @StartDate");
                    parameters.Add(new SqlParameter("@StartDate", filter.StartDate.Value));
                }

                if (filter.EndDate.HasValue)
                {
                    whereConditions.Add("TransDate <= @EndDate");
                    parameters.Add(new SqlParameter("@EndDate", filter.EndDate.Value));
                }

                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    whereConditions.Add("(PolicyNo LIKE @SearchTerm OR InsSurname LIKE @SearchTerm OR InsFirstname LIKE @SearchTerm OR Party LIKE @SearchTerm)");
                    parameters.Add(new SqlParameter("@SearchTerm", $"%{filter.SearchTerm}%"));
                }

                var whereClause = string.Join(" AND ", whereConditions);

                // Get total count
                var countSql = $@"
            SELECT COUNT(*) 
            FROM Policies p
            LEFT JOIN SubRisks sr ON p.SubRiskID = sr.SubRiskID
            WHERE {whereClause}";

                int totalCount;
                using (var countCommand = new SqlCommand(countSql, connection))
                {
                    countCommand.Parameters.AddRange(parameters.ToArray());
                    totalCount = (int)await countCommand.ExecuteScalarAsync();
                    countCommand.Parameters.Clear(); // release params so dataCommand can reuse them

                }

                // Get paginated results
                // Get paginated results - UPDATED to include SourceType
                var offset = (filter.Page - 1) * filter.PageSize;
                var orderBy = GetOrderByClause(filter.SortBy, filter.SortDirection);

                var dataSql = $@"
            SELECT p.PolicyNo, p.TransDate, p.CoPolicyNo, p.CompanyID, p.BranchID, p.Branch,
                   p.BizSource, p.SourceType, p.SubRiskID, p.SubRisk, p.PartyID, p.Party, p.MktStaffID, p.MktStaff,
                   p.InsuredID, p.InsSurname, p.InsFirstname, p.InsOthernames, p.InsAddress,
                   p.InsStateID, p.InsMobilePhone, p.InsLandPhone, p.InsEmail, p.InsFaxNo,
                   p.InsOccupation, p.isProposal, p.StartDate, p.EndDate, p.ExRate, p.ExCurrency,
                   p.PremiumRate, p.ProportionRate, p.SumInsured, p.GrossPremium, p.SumInsuredFrgn,
                   p.GrossPremiumFrgn, p.ProRataDays, p.ProRataPremium, p.Remarks, p.Deleted,
                   p.Active, p.TransSTATUS, p.SubmittedBy, p.SubmittedOn,
                   p.ModifiedBy, p.ModifiedOn, p.LeadID as LeaderID, p.Leader,
                   sr.RiskID
            FROM Policies p
            LEFT JOIN SubRisks sr ON p.SubRiskID = sr.SubRiskID
            WHERE {whereClause}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY";

                var proposals = new List<ProposalDto>();
                using (var dataCommand = new SqlCommand(dataSql, connection))
                {
                    dataCommand.Parameters.AddRange(parameters.ToArray());
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", filter.PageSize));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        proposals.Add(MapProposalFromReader(reader));
                    }
                }

                return new PaginatedResult<ProposalDto>
                {
                    Items = proposals,
                    TotalCount = totalCount,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                };
            }
            /// <summary>
            /// Get Fire calculation breakdown with section-level adjustments
            /// </summary>
            public async Task<FireCalculationBreakdownDto> GetFireCalculationBreakdownAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                try
                {
                    var sectionBreakdown = await GetSectionBasedCalculationBreakdownAsync(proposalNo, connection);
                    return sectionBreakdown;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting fire calculation breakdown for {ProposalNo}", proposalNo);
                    throw;
                }
            }

            private async Task<FireCalculationBreakdownDto> GetSectionBasedCalculationBreakdownAsync(
          string proposalNo,
          SqlConnection connection)
            {
                // Get the latest calculation from PolicyDetails
                var calculationSql = @"
            SELECT TOP 1 * FROM PolicyDetails 
            WHERE PolicyNo = @PolicyNo AND Deleted = 0
            ORDER BY SubmittedOn DESC";

                PolicyDetailDto calculation = null;
                using (var command = new SqlCommand(calculationSql, connection))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        calculation = MapPolicyDetailFromReader(reader);
                    }
                }

                if (calculation == null)
                {
                    return null;
                }

                // Get sections with section-level adjustments
                var sections = await GetCalculatedSectionsWithAdjustmentsBreakdownAsync(proposalNo, connection);

                // LEGACY: no section rows is a valid state (legacy motor calcs, etc.) —
                // return the breakdown with empty sections instead of pretending the
                // calculation doesn't exist.
                if (!sections.Any())
                {
                    _logger.LogInformation(
                        "No section rows found for {ProposalNo} — returning breakdown with empty sections",
                        proposalNo);
                }

                // Get proposal-level adjustments (only 2 rates)
                var proposalAdjustments = await GetProposalLevelAdjustmentsAsync(proposalNo, connection);

                var proposal = await GetProposalAsync(proposalNo);
                if (proposal == null)
                {
                    _logger.LogWarning("PolicyDetail found but parent Proposal {ProposalNo} not found", proposalNo);
                    return null;
                }

                // Get adjustment rates from PolicyDetails
                decimal otherDiscountsRate = calculation.A10;
                decimal otherLoadingsRate = calculation.A11;

                return new FireCalculationBreakdownDto
                {
                    ProposalNo = proposalNo,
                    CalculatedOn = calculation.SubmittedOn,
                    CalculatedBy = calculation.SubmittedBy ?? "Unknown",
                    CalculationType = calculation.BizOption ?? "NEW",
                    Remarks = calculation.Field1,

                    Inputs = new FireCalculationInputsDto
                    {
                        ProportionRate = calculation.ProportionRate,
                        Currency = calculation.ExCurrency ?? "NAIRA",
                        ExchangeRate = calculation.ExRate,
                        CoverDays = calculation.ProRataDays > 0 ? calculation.ProRataDays : 365,
                        StartDate = calculation.StartDate,
                        EndDate = calculation.EndDate,

                        // Sections with section-level adjustments
                        SectionInputs = sections,

                        // Proposal-level adjustments (only 2 rates)
                        ProposalAdjustments = new ProposalAdjustmentInputsDto
                        {
                            OtherDiscountsRate = otherDiscountsRate,
                            OtherLoadingsRate = otherLoadingsRate
                        }
                    },

                    CalculationSteps = new FireCalculationStepsDto
                    {
                        SectionCalculations = sections.Select(s => new SectionCalculationBreakdownDto
                        {
                            SectionID = s.SectionID,
                            SectionName = s.SectionName,
                            RiskItemCalculations = new RiskItemCalculationBreakdownDto
                            {
                                Items = s.RiskItems,
                                Totals = new RiskItemTotalsDto
                                {
                                    TotalActualValue = s.RiskItems.Sum(r => r.ActualValue),
                                    TotalShareValue = s.RiskItems.Sum(r => r.ShareValue),
                                    TotalSharePremium = s.RiskItems.Sum(r => r.PremiumValue),
                                    TotalNetPremiumAfterDiscounts = s.RiskItems.Sum(r => r.NetPremiumAfterDiscounts),
                                    ItemCount = s.RiskItems.Count
                                }
                            },
                            SectionAdjustments = s.SectionAdjustments
                        }).ToList(),

                        ProposalAdjustments = proposalAdjustments,

                        ProRataCalculations = new ProRataBreakdownDto
                        {
                            NetPremiumBeforeProRata = calculation.GrossPremium,
                            CoverDays = calculation.ProRataDays > 0 ? calculation.ProRataDays : 365,
                            StandardDays = 365,
                            ProRataFactor = calculation.ProRataDays > 0 ? calculation.ProRataDays / 365m : 1m,
                            ProRataPremium = calculation.ProRataPremium,
                            FinalPremiumDue = calculation.NetAmount,
                            IsProRated = calculation.ProRataDays < 365,
                            ProRataFormula = calculation.ProRataDays < 365
                                ? $"({calculation.GrossPremium:N2} × {calculation.ProRataDays} ÷ 365) = {calculation.ProRataPremium:N2}"
                                : "No pro-rata applied (full year coverage)"
                        }
                    },

                    FinalResults = new FireCalculationResultsDto
                    {
                        TotalSumInsured = calculation.TotalRiskValue,
                        TotalPremium = calculation.GrossPremium,
                        NetPremium = calculation.GrossPremium,
                        ProRataPremium = calculation.ProRataPremium,
                        ShareSumInsured = calculation.SumInsured,
                        SharePremium = calculation.NetAmount,
                        ForeignSumInsured = calculation.SumInsuredFrgn,
                        ForeignPremium = calculation.GrossPremiumFrgn,
                        ForeignCurrency = calculation.ExCurrency,
                        OverallPremiumRate = calculation.TotalRiskValue > 0
                            ? (calculation.GrossPremium / calculation.TotalRiskValue) * 100 : 0,
                        ProportionShare = calculation.ProportionRate
                    }
                };
            }


            private async Task<List<SectionInputBreakdownDto>> GetCalculatedSectionsWithAdjustmentsBreakdownAsync(
         string proposalNo,
         SqlConnection connection)
            {
                var sections = new List<SectionInputBreakdownDto>();

                // Get sections from PolicyTempDetails with section-level adjustments
                var sectionSql = @"
            SELECT SectionID, ContentSection, Location, RiskSum, SectionPremium,
                   A1, A2, A3, A4, A5, A6, A7, A8, A9, A11, A12, A13, A14, A15, A16, 
                   Field11, Field12, A17,
                   Field26, Field7, Field8,
                   ENDTNum,   -- LEGACY: the section's declaration number
                   TransGUID,  -- CRITICAL: Retrieve this
                   SubmitOn, SubmitBy
            FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo 
            AND ISNULL(Tag, '') <> 'PROPOSAL_ADJUSTMENTS'
            AND ISNULL(Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS') -- LEGACY: legacy rows store a cert number in Field1, not 'SECTION'
            ORDER BY SectionID";

                using var sectionCommand = new SqlCommand(sectionSql, connection);
                sectionCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);

                using var sectionReader = await sectionCommand.ExecuteReaderAsync();
                while (await sectionReader.ReadAsync())
                {
                    var section = new SectionInputBreakdownDto
                    {
                        SectionID = sectionReader.GetStringOrDefault("SectionID"),
                        SectionName = sectionReader.GetStringOrDefault("ContentSection"),
                        Location = sectionReader.GetStringOrDefault("Location"),
                        SectionSumInsured = sectionReader.GetValueOrDefault<decimal>("A1"),
                        SectionGrossPremium = sectionReader.GetValueOrDefault<decimal>("A2"),
                        SectionNetPremium = sectionReader.GetValueOrDefault<decimal>("A11"),

                        SectionAdjustments = new SectionAdjustmentsBreakdownDto
                        {
                            StartingPremium = sectionReader.GetValueOrDefault<decimal>("A2"),
                            FinalNetPremium = sectionReader.GetValueOrDefault<decimal>("A11"),
                            DiscountsApplied = new List<AdjustmentDetailDto>(),
                            LoadingsApplied = new List<AdjustmentDetailDto>()
                        }
                    };

                    var sectionTransGUID = sectionReader.GetStringOrDefault("TransGUID");
                    var sectionDeclarationNo = sectionReader.GetStringOrDefault("ENDTNum"); // LEGACY: link to risk items

                    // Get adjustment RATES from A12-A16
                    decimal specialDiscountRate = sectionReader.GetValueOrDefault<decimal>("A12");
                    decimal deductibleDiscountRate = sectionReader.GetValueOrDefault<decimal>("A13");
                    decimal spreadDiscountRate = sectionReader.GetValueOrDefault<decimal>("A14");
                    decimal ltaDiscountRate = sectionReader.GetValueOrDefault<decimal>("A15");
                    decimal theftLoadingRate = sectionReader.GetValueOrDefault<decimal>("A16");

                    // Get SRCC and OtherLoading2 rates from Field columns
                    decimal srccLoadingRate = 0;
                    decimal otherLoading2Rate = 0;
                    if (!string.IsNullOrEmpty(sectionReader.GetStringOrDefault("Field11")))
                    {
                        decimal.TryParse(sectionReader.GetStringOrDefault("Field11"), out srccLoadingRate);
                    }
                    if (!string.IsNullOrEmpty(sectionReader.GetStringOrDefault("Field12")))
                    {
                        decimal.TryParse(sectionReader.GetStringOrDefault("Field12"), out otherLoading2Rate);
                    }

                    // Add section-level discounts
                    if (sectionReader.GetValueOrDefault<decimal>("A3") > 0)
                    {
                        section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Special Discount",
                            Type = "DISCOUNT",
                            Rate = specialDiscountRate,
                            Amount = sectionReader.GetValueOrDefault<decimal>("A3"),
                            SequenceOrder = 1
                        });
                    }

                    if (sectionReader.GetValueOrDefault<decimal>("A4") > 0)
                    {
                        section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Deductible Discount",
                            Type = "DISCOUNT",
                            Rate = deductibleDiscountRate,
                            Amount = sectionReader.GetValueOrDefault<decimal>("A4"),
                            SequenceOrder = 2
                        });
                    }

                    if (sectionReader.GetValueOrDefault<decimal>("A5") > 0)
                    {
                        section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Spread Discount",
                            Type = "DISCOUNT",
                            Rate = spreadDiscountRate,
                            Amount = sectionReader.GetValueOrDefault<decimal>("A5"),
                            SequenceOrder = 3
                        });
                    }

                    if (sectionReader.GetValueOrDefault<decimal>("A6") > 0)
                    {
                        section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "LTA Discount",
                            Type = "DISCOUNT",
                            Rate = ltaDiscountRate,
                            Amount = sectionReader.GetValueOrDefault<decimal>("A6"),
                            SequenceOrder = 4
                        });
                    }

                    // Add section-level loadings
                    if (sectionReader.GetValueOrDefault<decimal>("A7") > 0)
                    {
                        section.SectionAdjustments.LoadingsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Theft Loading",
                            Type = "LOADING",
                            Rate = theftLoadingRate,
                            Amount = sectionReader.GetValueOrDefault<decimal>("A7"),
                            SequenceOrder = 1
                        });
                    }

                    if (sectionReader.GetValueOrDefault<decimal>("A8") > 0)
                    {
                        section.SectionAdjustments.LoadingsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "SRCC Loading",
                            Type = "LOADING",
                            Rate = srccLoadingRate,
                            Amount = sectionReader.GetValueOrDefault<decimal>("A8"),
                            SequenceOrder = 2
                        });
                    }

                    if (sectionReader.GetValueOrDefault<decimal>("A9") > 0)
                    {
                        section.SectionAdjustments.LoadingsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Other Loading 2",
                            Type = "LOADING",
                            Rate = otherLoading2Rate,
                            Amount = sectionReader.GetValueOrDefault<decimal>("A9"),
                            SequenceOrder = 3
                        });
                    }

                    sections.Add(section);

                    // LEGACY (CreateTempGrid): items are fetched by the section's declaration number
                    section.RiskItems = await GetRiskItemsForSectionByDeclarationNoAsync(
                        proposalNo,
                        sectionDeclarationNo,
                        null,
                        sectionTransGUID,
                        connection);
                }
                sectionReader.Close();

                // LEGACY: the old per-SectionID re-query that ran here has been removed.
                // It appended items on top of the declaration-number fetch above,
                // duplicating every risk item, and it matched items across ALL
                // calculations of the policy because SectionID is not unique per
                // calculation. Items are now fetched once per section through
                // Field10 = the section's declaration number (with a TransGUID
                // fallback for rows saved before the legacy numbering existed).

                return sections; // THIS IS THE CRITICAL LINE - must return the list
            }

            /// <summary>
            /// Get proposal-level adjustments (only 2 rates)
            /// </summary>
            private async Task<ProposalLevelAdjustmentsBreakdownDto> GetProposalLevelAdjustmentsAsync(
                string proposalNo,
                SqlConnection connection)
            {
                var sql = @"
            SELECT A1, A2, A10, A11, Field18, Field28, Field29
            FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo AND Tag = 'PROPOSAL_ADJUSTMENTS'";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var breakdown = new ProposalLevelAdjustmentsBreakdownDto
                    {
                        StartingPremium = reader.GetValueOrDefault<decimal>("A1"),
                        FinalNetPremium = reader.GetValueOrDefault<decimal>("A2"),
                        DiscountsApplied = new List<AdjustmentDetailDto>(),
                        LoadingsApplied = new List<AdjustmentDetailDto>()
                    };

                    // Add other discounts if applied
                    var otherDiscountsAmount = reader.GetValueOrDefault<decimal>("A10");
                    if (otherDiscountsAmount > 0)
                    {
                        breakdown.DiscountsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Other Discounts",
                            Type = "DISCOUNT",
                            Amount = otherDiscountsAmount,
                            SequenceOrder = 1
                        });
                    }

                    // Add other loadings if applied
                    var otherLoadingsAmount = reader.GetValueOrDefault<decimal>("A11");
                    if (otherLoadingsAmount > 0)
                    {
                        breakdown.LoadingsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Other Loadings",
                            Type = "LOADING",
                            Amount = otherLoadingsAmount,
                            SequenceOrder = 1
                        });
                    }

                    breakdown.TotalDiscountAmount = breakdown.DiscountsApplied.Sum(d => d.Amount);
                    breakdown.TotalLoadingAmount = breakdown.LoadingsApplied.Sum(l => l.Amount);
                    breakdown.NetAdjustmentAmount = breakdown.TotalLoadingAmount - breakdown.TotalDiscountAmount;

                    return breakdown;
                }

                // Return empty if not found
                return new ProposalLevelAdjustmentsBreakdownDto
                {
                    StartingPremium = 0,
                    FinalNetPremium = 0,
                    DiscountsApplied = new List<AdjustmentDetailDto>(),
                    LoadingsApplied = new List<AdjustmentDetailDto>()
                };
            }

            private async Task<List<SectionInputBreakdownDto>> GetCalculatedSectionsBreakdownAsync(string proposalNo, SqlConnection connection)
            {
                var sections = new List<SectionInputBreakdownDto>();

                // Get sections from PolicyTempDetails
                var sectionSql = @"
            SELECT SectionID, ContentSection, Location, RiskSum, SectionPremium,
                   A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A17,
                   SubmitOn, SubmitBy
            FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo
            AND ISNULL(Tag, '') <> 'PROPOSAL_ADJUSTMENTS'
            AND ISNULL(Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS') -- LEGACY: legacy rows store a cert number in Field1, not 'SECTION'
            ORDER BY SectionID";

                using var sectionCommand = new SqlCommand(sectionSql, connection);
                sectionCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);

                using var sectionReader = await sectionCommand.ExecuteReaderAsync();
                while (await sectionReader.ReadAsync())
                {
                    var section = new SectionInputBreakdownDto
                    {
                        SectionID = sectionReader.GetStringOrDefault("SectionID"),
                        SectionName = sectionReader.GetStringOrDefault("ContentSection"),
                        Location = sectionReader.GetStringOrDefault("Location"),
                        SectionSumInsured = sectionReader.GetValueOrDefault<decimal>("A1"),
                        SectionGrossPremium = sectionReader.GetValueOrDefault<decimal>("A2"),
                        SectionNetPremium = sectionReader.GetValueOrDefault<decimal>("A11"),

                        SectionAdjustments = new SectionAdjustmentsBreakdownDto
                        {
                            StartingPremium = sectionReader.GetValueOrDefault<decimal>("A2"),
                            FinalNetPremium = sectionReader.GetValueOrDefault<decimal>("A11"),
                            DiscountsApplied = new List<AdjustmentDetailDto>(),
                            LoadingsApplied = new List<AdjustmentDetailDto>()
                        }
                    };

                    // Add discount and loading details if amounts > 0
                    if (sectionReader.GetValueOrDefault<decimal>("A3") > 0)
                    {
                        section.SectionAdjustments.DiscountsApplied.Add(new AdjustmentDetailDto
                        {
                            Name = "Special Discount",
                            Type = "DISCOUNT",
                            Amount = sectionReader.GetValueOrDefault<decimal>("A3"),
                            SequenceOrder = 1
                        });
                    }

                    // Similar for other adjustments...

                    sections.Add(section);
                }
                sectionReader.Close();

                // UPDATED: Get risk items for each section with new stock item fields
                foreach (var section in sections)
                {
                    var riskItemSql = @"
                SELECT SMICode, ContentDesc, Location, 
                       A1 as ActualValue, A2 as ItemRate, A3 as ActualPremium,
                       A4 as ShareValue, A5 as PremiumValue, A6 as MultiplyRate,
                       A7 as StockSumInsured, A8 as StockGrossPremium, A9 as StockDiscountAmount,
                       A10 as StockNetPremium, A11 as TotalSumInsured, A12 as TotalGrossPremium,
                       A13 as FeaDiscountRate, A14 as FeaDiscountAmount, A15 as NetPremiumAfterDiscounts,
                       A16 as ItemNo, A17 as StockDiscountRate,
                       Field6 as StockCode, Field7 as StockDescription, 
                       Field8 as StockSumInsuredStr, Field9 as StockRateStr
                FROM PolicyPremDetails 
                WHERE PolicyNo = @PolicyNo AND Section = @SectionID AND Tag = 'RISK_ITEM'
                ORDER BY A16";

                    using var itemCommand = new SqlCommand(riskItemSql, connection);
                    itemCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    itemCommand.Parameters.AddWithValue("@SectionID", section.SectionID);

                    using var itemReader = await itemCommand.ExecuteReaderAsync();
                    while (await itemReader.ReadAsync())
                    {
                        var calculatedItem = new CalculatedRiskItemBreakdownDto
                        {
                            ItemNo = itemReader.GetValueOrDefault<int>("ItemNo"),
                            SMICode = itemReader.GetStringOrDefault("SMICode"),
                            ItemDescription = itemReader.GetStringOrDefault("ContentDesc"),
                            ActualValue = itemReader.GetValueOrDefault<decimal>("ActualValue"),
                            ItemRate = itemReader.GetValueOrDefault<decimal>("ItemRate"),
                            MultiplyRate = itemReader.GetValueOrDefault<decimal>("MultiplyRate"),
                            SectionId = section.SectionID,
                            Location = itemReader.GetStringOrDefault("Location"),

                            // Base calculations
                            ActualPremium = itemReader.GetValueOrDefault<decimal>("ActualPremium"),
                            ShareValue = itemReader.GetValueOrDefault<decimal>("ShareValue"),
                            PremiumValue = itemReader.GetValueOrDefault<decimal>("PremiumValue"),

                            // NEW: Stock item values
                            StockDiscountRate = itemReader.GetValueOrDefault<decimal>("StockDiscountRate"),
                            StockDiscountAmount = itemReader.GetValueOrDefault<decimal>("StockDiscountAmount"),

                            // NEW: Aggregated totals
                            FeaDiscountRate = itemReader.GetValueOrDefault<decimal>("FeaDiscountRate"),
                            FeaDiscountAmount = itemReader.GetValueOrDefault<decimal>("FeaDiscountAmount"),
                            NetPremiumAfterDiscounts = itemReader.GetValueOrDefault<decimal>("NetPremiumAfterDiscounts"),

                            // Formulas for transparency
                            ActualPremiumFormula = $"{itemReader.GetValueOrDefault<decimal>("ActualValue"):N2} × {itemReader.GetValueOrDefault<decimal>("ItemRate"):N2}%",
                            ShareValueFormula = $"{itemReader.GetValueOrDefault<decimal>("ActualValue"):N2} × proportion%",
                            PremiumValueFormula = $"{itemReader.GetValueOrDefault<decimal>("ShareValue"):N2} × {itemReader.GetValueOrDefault<decimal>("ItemRate"):N2}%"
                        };

                        section.RiskItems.Add(calculatedItem);
                    }
                    itemReader.Close();
                }

                return sections;
            }

            private async Task<bool> ProposalExistsAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT COUNT(1) FROM Policies WHERE PolicyNo = @ProposalNo";
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ProposalNo", proposalNo);

                var count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }


            // Also update GetOrderByClause to use table alias 'p.'
            private string GetOrderByClause(string sortBy, string sortDirection)
            {
                var validSortColumns = new Dictionary<string, string>
        {
            { "TransDate", "p.TransDate" },
            { "ProposalNo", "p.ProposalNo" },
            { "InsuredName", "p.InsSurname" },
            { "Status", "p.TransSTATUS" },
            { "Branch", "p.Branch" },
            { "SumInsured", "p.SumInsured" },
            { "GrossPremium", "p.GrossPremium" },
            { "SubmittedOn", "p.SubmittedOn" }
        };

                var column = validSortColumns.ContainsKey(sortBy) ? validSortColumns[sortBy] : "p.TransDate";
                var direction = sortDirection?.ToUpper() == "ASC" ? "ASC" : "DESC";

                return $"{column} {direction}";
            }
            /// <summary>
            /// Calculate fire calculation for renewal
            /// </summary>
            public async Task<CompleteCalculationResponseDto> CalculateRenewalFireCalculationAsync(
                RenewalFireCalculationRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Validate the source calculation exists
                    PolicyDetailDto sourceCalculation = null;

                    if (!string.IsNullOrEmpty(request.SourceDetailID))
                    {
                        // Use specific calculation
                        sourceCalculation = await GetPolicyDetailByDetailIDAsync(
                            request.OriginalPolicyNo,
                            request.SourceDetailID,
                            connection,
                            transaction);

                        if (sourceCalculation == null)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.NewPolicyNo,
                                Success = false,
                                Message = "Source calculation not found",
                                ValidationErrors = { $"DetailID {request.SourceDetailID} not found for policy {request.OriginalPolicyNo}" }
                            };
                        }
                    }
                    else
                    {
                        // Use latest active calculation
                        sourceCalculation = await GetLatestPolicyDetailAsync(
                            request.OriginalPolicyNo,
                            connection,
                            transaction);

                        if (sourceCalculation == null)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.NewPolicyNo,
                                Success = false,
                                Message = "No calculation found for renewal",
                                ValidationErrors = { $"No active calculation found for policy {request.OriginalPolicyNo}" }
                            };
                        }
                    }

                    // Convert to regular fire calculation request
                    var fireCalcRequest = new CompleteFireCalculationRequestDto
                    {
                        ProposalNo = request.NewPolicyNo,
                        Sections = request.Sections,
                        ProportionRate = request.ProportionRate,
                        ExchangeRate = request.ExchangeRate,
                        Currency = request.Currency,
                        CoverDays = request.CoverDays,
                        OtherDiscountsRate = request.OtherDiscountsRate,
                        OtherLoadingsRate = request.OtherLoadingsRate,
                        CalculatedBy = request.CalculatedBy,
                        Remarks = $"RENEWAL from {request.OriginalPolicyNo} (DetailID: {sourceCalculation.DetailID}). {request.Remarks}"
                    };

                    var sectionCalculations = new List<SectionCalculationResponseDto>();
                    decimal totalSumInsured = 0;
                    decimal totalGrossPremium = 0;

                    // Process each section (same as regular calculation)
                    foreach (var section in fireCalcRequest.Sections)
                    {
                        var itemCalcRequest = new RiskItemCalculationRequestDto
                        {
                            RiskItems = section.RiskItems,
                            ProportionRate = fireCalcRequest.ProportionRate
                        };
                        var itemCalcResponse = await CalculateRiskItemsAsync(itemCalcRequest);

                        if (!itemCalcResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = fireCalcRequest.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} calculation failed: {itemCalcResponse.Message}",
                                ValidationErrors = { itemCalcResponse.Message }
                            };
                        }

                        var sectionAggregate = new SectionAggregateResponseDto
                        {
                            AggregateSumInsured = itemCalcResponse.TotalShareValue,
                            AggregatePremium = itemCalcResponse.TotalNetPremiumAfterDiscounts,
                            Success = true
                        };

                        var sectionAdjustmentRequest = new SectionAdjustmentRequestDto
                        {
                            AggregatePremium = sectionAggregate.AggregatePremium,
                            SpecialDiscountRate = section.SpecialDiscountRate,
                            DeductibleDiscountRate = section.DeductibleDiscountRate,
                            SpreadDiscountRate = section.SpreadDiscountRate,
                            LtaDiscountRate = section.LtaDiscountRate,
                            TheftLoadingRate = section.TheftLoadingRate,
                            SrccLoadingRate = section.SrccLoadingRate,
                            OtherLoading2Rate = section.OtherLoading2Rate
                        };

                        var sectionAdjustmentResponse = await ApplySectionAdjustmentsAsync(sectionAdjustmentRequest);

                        if (!sectionAdjustmentResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = fireCalcRequest.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} adjustments failed: {sectionAdjustmentResponse.Message}",
                                ValidationErrors = { sectionAdjustmentResponse.Message }
                            };
                        }

                        var sectionResult = new SectionCalculationResponseDto
                        {
                            SectionID = section.SectionID,
                            SectionName = section.SectionName,
                            CalculatedItems = itemCalcResponse.CalculatedItems,
                            SectionSumInsured = sectionAggregate.AggregateSumInsured,
                            SectionGrossPremium = sectionAggregate.AggregatePremium,
                            SectionNetPremium = sectionAdjustmentResponse.NetPremiumDue,
                            SectionAdjustments = sectionAdjustmentResponse,
                            Success = true
                        };

                        sectionCalculations.Add(sectionResult);
                        totalSumInsured += sectionAggregate.AggregateSumInsured;
                        totalGrossPremium += sectionAdjustmentResponse.NetPremiumDue;

                        // Save section with "Renewal" BizOption
                        await SaveRenewalSectionAsync(
                            fireCalcRequest.ProposalNo,
                            request.OriginalPolicyNo,
                            sectionResult,
                            section,
                            fireCalcRequest,
                            connection,
                            transaction);
                    }

                    // Apply proposal-level adjustments
                    var proposalAdjustmentRequest = new ProposalAdjustmentRequestDto
                    {
                        TotalAggregatePremium = totalGrossPremium,
                        OtherDiscountsRate = fireCalcRequest.OtherDiscountsRate,
                        OtherLoadingsRate = fireCalcRequest.OtherLoadingsRate
                    };

                    var proposalAdjustmentResponse = await ApplyProposalLevelAdjustmentsAsync(proposalAdjustmentRequest);

                    if (!proposalAdjustmentResponse.Success)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = fireCalcRequest.ProposalNo,
                            Success = false,
                            Message = $"Proposal adjustments failed: {proposalAdjustmentResponse.Message}",
                            ValidationErrors = { proposalAdjustmentResponse.Message }
                        };
                    }

                    decimal totalNetPremium = proposalAdjustmentResponse.NetPremiumDue;

                    // Save proposal-level adjustments
                    await SaveProposalLevelAdjustmentsAsync(fireCalcRequest.ProposalNo, proposalAdjustmentResponse,
                        fireCalcRequest, connection, transaction);

                    // Save adjustment rates
                    await SaveAdjustmentRatesToPolicyDetailsAsync(fireCalcRequest.ProposalNo, fireCalcRequest, connection, transaction);

                    // Calculate pro-rata
                    var proRataRequest = new ProRataCalculationRequestDto
                    {
                        NetPremiumDue = totalNetPremium,
                        CoverDays = fireCalcRequest.CoverDays
                    };
                    var proRataResponse = await CalculateProRataAsync(proRataRequest);

                    // Update PolicyDetails with "Renewal" BizOption
                    await UpdateRenewalPolicyDetailsAsync(
                        fireCalcRequest.ProposalNo,
                        request.OriginalPolicyNo,
                        totalSumInsured,
                        totalGrossPremium,
                        proRataResponse,
                        fireCalcRequest,
                        connection,
                        transaction);

                    // Update Policy table summary
                    await UpdateProposalSummaryAsync(fireCalcRequest.ProposalNo, totalSumInsured, totalNetPremium,
                        proRataResponse, fireCalcRequest.ExchangeRate, connection, transaction);

                    await transaction.CommitAsync();

                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = fireCalcRequest.ProposalNo,
                        SectionCalculations = sectionCalculations,
                        TotalSumInsured = totalSumInsured,
                        TotalPremium = totalGrossPremium,
                        NetPremium = totalNetPremium,
                        ProRataPremium = proRataResponse.ProRataPremium,
                        ShareSumInsured = totalSumInsured * (fireCalcRequest.ProportionRate / 100),
                        SharePremium = proRataResponse.PremiumDue * (fireCalcRequest.ProportionRate / 100),
                        ForeignSumInsured = totalSumInsured * fireCalcRequest.ExchangeRate,
                        ForeignPremium = proRataResponse.PremiumDue * fireCalcRequest.ExchangeRate,
                        ProposalLevelAdjustments = proposalAdjustmentResponse,
                        ProRataCalculations = proRataResponse,
                        Success = true,
                        CalculatedOn = DateTime.Now,
                        Message = $"Renewal fire calculation completed successfully (based on DetailID: {sourceCalculation.DetailID})"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error calculating renewal fire for {NewPolicyNo}", request.NewPolicyNo);

                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.NewPolicyNo,
                        Success = false,
                        Message = "Renewal calculation failed",
                        ValidationErrors = { ex.Message }
                    };
                }
            }


            /// <summary>
            /// Get a specific PolicyDetail by DetailID
            /// </summary>
            private async Task<PolicyDetailDto> GetPolicyDetailByDetailIDAsync(
                string policyNo,
                string detailID,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
            SELECT * FROM PolicyDetails 
            WHERE PolicyNo = @PolicyNo 
            AND DetailID = @DetailID 
            AND Deleted = 0";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@PolicyNo", policyNo);
                command.Parameters.AddWithValue("@DetailID", detailID);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapPolicyDetailFromReader(reader);
                }

                return null;
            }

            /// <summary>
            /// Get the latest active PolicyDetail for a policy
            /// </summary>
            private async Task<PolicyDetailDto> GetLatestPolicyDetailAsync(
                string policyNo,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
            SELECT TOP 1 * FROM PolicyDetails 
            WHERE PolicyNo = @PolicyNo 
            AND Deleted = 0 
            AND Active = 1
            ORDER BY SubmittedOn DESC";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@PolicyNo", policyNo);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapPolicyDetailFromReader(reader);
                }

                return null;
            }

            private async Task UpdateRenewalPolicyDetailsAsync(
               string newPolicyNo,
               string originalPolicyNo,
               decimal totalSumInsured,
               decimal totalPremium,
               ProRataCalculationResponseDto proRataResponse,
               CompleteFireCalculationRequestDto request,
               SqlConnection connection,
               SqlTransaction transaction)
            {
                var sql = @"
                    UPDATE PolicyDetails SET 
                        SumInsured = @SumInsured,
                        GrossPremium = @GrossPremium,
                        SumInsuredFrgn = @SumInsuredFrgn,
                        GrossPremiumFrgn = @GrossPremiumFrgn,
                        ProRataDays = @ProRataDays,
                        ProRataPremium = @ProRataPremium,
                        NetAmount = @NetAmount,
                        TotalRiskValue = @TotalRiskValue,
                        ProportionRate = @ProportionRate,
                        ExRate = @ExRate,
                        ExCurrency = @ExCurrency,
                        BizOption = 'Renewal',
                        A1 = @A1,
                        A2 = @A2,
                        A13 = @A13,
                        A14 = @A14,
                        Field1 = @Field1,
                        Field2 = @Field2,
                        Field50 = 'RENEWAL',
                        ModifiedBy = @ModifiedBy,
                        ModifiedOn = @ModifiedOn
                    WHERE PolicyNo = @PolicyNo";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@SumInsured", totalSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@GrossPremium", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@SumInsuredFrgn", totalSumInsured * request.ExchangeRate);
                command.Parameters.AddWithValue("@GrossPremiumFrgn", proRataResponse.PremiumDue * request.ExchangeRate);
                command.Parameters.AddWithValue("@ProRataDays", request.CoverDays);
                command.Parameters.AddWithValue("@ProRataPremium", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@NetAmount", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@TotalRiskValue", totalSumInsured);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);
                command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);
                command.Parameters.AddWithValue("@A1", totalSumInsured);
                command.Parameters.AddWithValue("@A2", totalPremium);
                command.Parameters.AddWithValue("@A13", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@A14", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@Field1", "RENEWAL_CALCULATED");
                command.Parameters.AddWithValue("@Field2", $"Renewal calculated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                command.Parameters.AddWithValue("@ModifiedBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                command.Parameters.AddWithValue("@PolicyNo", newPolicyNo);

                await command.ExecuteNonQueryAsync();
            }
            private async Task SaveRenewalSectionAsync(
               string newPolicyNo,
               string originalPolicyNo,
               SectionCalculationResponseDto sectionResult,
               FireSectionDto section,
               CompleteFireCalculationRequestDto request,
               SqlConnection connection,
               SqlTransaction transaction)
            {
                // Use existing SaveSectionWithAdjustmentsToPolicyTempDetailsAsync
                // but with BizOption marker for renewal
                await SaveSectionWithAdjustmentsToPolicyTempDetailsAsync(
                    newPolicyNo,
                    sectionResult,
                    section,
                    request,
                    connection,
                    transaction);
            }

            // Updated CompleteFireCalculationAsync with section-level adjustments
            public async Task<CompleteCalculationResponseDto> CompleteFireCalculationAsync(CompleteFireCalculationRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {

                    var sectionCalculations = new List<SectionCalculationResponseDto>();
                    decimal totalSumInsured = 0;
                    decimal totalGrossPremium = 0; // Aggregate before proposal-level adjustments

                    // STEP 1 & 2 & 3: Process each section - calculate risk items, aggregate, and apply SECTION-LEVEL adjustments
                    foreach (var section in request.Sections)
                    {
                        // Calculate risk items for this section
                        var itemCalcRequest = new RiskItemCalculationRequestDto
                        {
                            RiskItems = section.RiskItems,
                            ProportionRate = request.ProportionRate
                        };
                        var itemCalcResponse = await CalculateRiskItemsAsync(itemCalcRequest);

                        if (!itemCalcResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} calculation failed: {itemCalcResponse.Message}",
                                ValidationErrors = { itemCalcResponse.Message }
                            };
                        }

                        // Aggregate this section's premium
                        var sectionAggregate = new SectionAggregateResponseDto
                        {
                            AggregateSumInsured = itemCalcResponse.TotalShareValue,
                            AggregatePremium = itemCalcResponse.TotalNetPremiumAfterDiscounts,
                            Success = true
                        };

                        // APPLY SECTION-LEVEL ADJUSTMENTS (7 rates)
                        var sectionAdjustmentRequest = new SectionAdjustmentRequestDto
                        {
                            AggregatePremium = sectionAggregate.AggregatePremium,
                            SpecialDiscountRate = section.SpecialDiscountRate,
                            DeductibleDiscountRate = section.DeductibleDiscountRate,
                            SpreadDiscountRate = section.SpreadDiscountRate,
                            LtaDiscountRate = section.LtaDiscountRate,
                            TheftLoadingRate = section.TheftLoadingRate,
                            SrccLoadingRate = section.SrccLoadingRate,
                            OtherLoading2Rate = section.OtherLoading2Rate
                        };

                        var sectionAdjustmentResponse = await ApplySectionAdjustmentsAsync(sectionAdjustmentRequest);

                        if (!sectionAdjustmentResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} adjustments failed: {sectionAdjustmentResponse.Message}",
                                ValidationErrors = { sectionAdjustmentResponse.Message }
                            };
                        }

                        // Store section calculation WITH adjustments
                        var sectionResult = new SectionCalculationResponseDto
                        {
                            SectionID = section.SectionID,
                            SectionName = section.SectionName,
                            CalculatedItems = itemCalcResponse.CalculatedItems,
                            SectionSumInsured = sectionAggregate.AggregateSumInsured,
                            SectionGrossPremium = sectionAggregate.AggregatePremium,
                            SectionNetPremium = sectionAdjustmentResponse.NetPremiumDue,
                            SectionAdjustments = sectionAdjustmentResponse,
                            Success = true
                        };

                        sectionCalculations.Add(sectionResult);

                        // Add to totals (using section net premium after section-level adjustments)
                        totalSumInsured += sectionAggregate.AggregateSumInsured;
                        totalGrossPremium += sectionAdjustmentResponse.NetPremiumDue; // Use adjusted premium

                        // Save section WITH adjustments to database
                        await SaveSectionWithAdjustmentsToPolicyTempDetailsAsync(
                            request.ProposalNo, sectionResult, section, request, connection, transaction);
                    }

                    // STEP 4: Apply PROPOSAL-LEVEL adjustments (only 2 rates) to the aggregate
                    var proposalAdjustmentRequest = new ProposalAdjustmentRequestDto
                    {
                        TotalAggregatePremium = totalGrossPremium,
                        OtherDiscountsRate = request.OtherDiscountsRate,
                        OtherLoadingsRate = request.OtherLoadingsRate
                    };

                    var proposalAdjustmentResponse = await ApplyProposalLevelAdjustmentsAsync(proposalAdjustmentRequest);

                    if (!proposalAdjustmentResponse.Success)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = request.ProposalNo,
                            Success = false,
                            Message = $"Proposal adjustments failed: {proposalAdjustmentResponse.Message}",
                            ValidationErrors = { proposalAdjustmentResponse.Message }
                        };
                    }

                    decimal totalNetPremium = proposalAdjustmentResponse.NetPremiumDue;

                    // Save proposal-level adjustments
                    await SaveProposalLevelAdjustmentsAsync(request.ProposalNo, proposalAdjustmentResponse,
                        request, connection, transaction);

                    // Save adjustment rates to PolicyDetails
                    await SaveAdjustmentRatesToPolicyDetailsAsync(request.ProposalNo, request, connection, transaction);

                    // STEP 5: Calculate pro-rata
                    var proRataRequest = new ProRataCalculationRequestDto
                    {
                        NetPremiumDue = totalNetPremium,
                        CoverDays = request.CoverDays
                    };
                    var proRataResponse = await CalculateProRataAsync(proRataRequest);

                    // Update PolicyDetails table with final calculated values
                    await UpdatePolicyDetailsWithCalculationAsync(request.ProposalNo, totalSumInsured,
                        totalGrossPremium, proRataResponse, request, connection, transaction);

                    // Update Proposals table summary
                    await UpdateProposalSummaryAsync(request.ProposalNo, totalSumInsured, totalNetPremium,
                        proRataResponse, request.ExchangeRate, connection, transaction);

                    await transaction.CommitAsync();

                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        SectionCalculations = sectionCalculations,
                        TotalSumInsured = totalSumInsured,
                        TotalPremium = totalGrossPremium,
                        NetPremium = totalNetPremium,
                        ProRataPremium = proRataResponse.ProRataPremium,
                        ShareSumInsured = totalSumInsured * (request.ProportionRate / 100),
                        SharePremium = proRataResponse.PremiumDue * (request.ProportionRate / 100),
                        ForeignSumInsured = totalSumInsured * request.ExchangeRate,
                        ForeignPremium = proRataResponse.PremiumDue * request.ExchangeRate,
                        ProposalLevelAdjustments = proposalAdjustmentResponse,
                        ProRataCalculations = proRataResponse,
                        Success = true,
                        CalculatedOn = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error completing fire calculation for {ProposalNo}", request.ProposalNo);
                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        Success = false,
                        Message = "Calculation failed",
                        ValidationErrors = { ex.Message }
                    };
                }
            }

            // Updated SaveAdjustmentRatesToPolicyDetailsAsync (only 2 rates)
            private async Task SaveAdjustmentRatesToPolicyDetailsAsync(
                string proposalNo,
                CompleteFireCalculationRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
            UPDATE PolicyDetails SET 
                A10 = @OtherDiscountsRate,
                A11 = @OtherLoadingsRate,
                ModifiedBy = @ModifiedBy,
                ModifiedOn = @ModifiedOn
            WHERE PolicyNo = @ProposalNo";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@OtherDiscountsRate", request.OtherDiscountsRate);
                command.Parameters.AddWithValue("@OtherLoadingsRate", request.OtherLoadingsRate);
                command.Parameters.AddWithValue("@ModifiedBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                command.Parameters.AddWithValue("@ProposalNo", proposalNo);

                await command.ExecuteNonQueryAsync();
            }

            // Updated SaveSectionWithAdjustmentsToPolicyTempDetailsAsync method
            private async Task SaveSectionWithAdjustmentsToPolicyTempDetailsAsync(
                string proposalNo,
                SectionCalculationResponseDto sectionResult,
                FireSectionDto section,
                CompleteFireCalculationRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var certOrDocNo = await GetOrGenerateCertOrDocNoAsync(proposalNo, connection, transaction);

                // LEGACY (AddItemSectionTempGridDetails): every section gets its own
                // declaration number = PROPOSAL auto number, keyed by branch, stored in ENDTNum.
                var branchId = await GetBranchIdAsync(proposalNo, connection, transaction);
                var sectionDeclarationNo = await NextDeclarationNoAsync(branchId);
                var sectionTransGuid = Guid.NewGuid().ToString();

                var sql = @"
    INSERT INTO PolicyTempDetails (
        PolicyNo, ENDTNum, SectionID, ContentSection, Location,
        Field1, Field2, Field3, Field4, Field5, Field20, Field11, Field12,
        Field26, Field7, Field8,
        RiskSum, OtherSum, SectionPremium, TStartDate, TEndDate,
        A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16, A17, A18, A19, A20,
        Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
        @PolicyNo, @ENDTNum, @SectionID, @ContentSection, @Location,
        @Field1, @Field2, @Field3, @Field4, @Field5, @Field20, @Field11, @Field12,
        @Field26, @Field7, @Field8,
        @RiskSum, @OtherSum, @SectionPremium, @TStartDate, @TEndDate,
        @A1, @A2, @A3, @A4, @A5, @A6, @A7, @A8, @A9, @A10, @A11, @A12, @A13, @A14, @A15, @A16, @A17, @A18, @A19, @A20,
        @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@Field20", certOrDocNo);
                command.Parameters.AddWithValue("@ENDTNum", sectionDeclarationNo); // LEGACY: section declaration number (PROPOSAL auto number)
                command.Parameters.AddWithValue("@SectionID", sectionResult.SectionID);
                command.Parameters.AddWithValue("@ContentSection", sectionResult.SectionName);
                command.Parameters.AddWithValue("@Location", sectionResult.CalculatedItems.FirstOrDefault()?.Location ?? "");

                command.Parameters.AddWithValue("@Field1", "SECTION");
                command.Parameters.AddWithValue("@Field2", sectionResult.SectionName);
                command.Parameters.AddWithValue("@Field3", $"Items: {sectionResult.CalculatedItems.Count}");
                command.Parameters.AddWithValue("@Field4", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@Field5", "CALCULATED_WITH_ADJUSTMENTS");

                // NEW: Optional section information fields
                command.Parameters.AddWithValue("@Field26", (object)section.Field26 ?? DBNull.Value);
                command.Parameters.AddWithValue("@Field7", (object)section.Field7 ?? DBNull.Value);
                command.Parameters.AddWithValue("@Field8", (object)section.Field8 ?? DBNull.Value);

                command.Parameters.AddWithValue("@RiskSum", sectionResult.SectionSumInsured);
                command.Parameters.AddWithValue("@OtherSum", 0);
                command.Parameters.AddWithValue("@SectionPremium", sectionResult.SectionNetPremium);
                command.Parameters.AddWithValue("@TStartDate", DateTime.Now);
                command.Parameters.AddWithValue("@TEndDate", DateTime.Now.AddDays(request.CoverDays));

                // Store section calculations WITH adjustments
                command.Parameters.AddWithValue("@A1", sectionResult.SectionSumInsured);
                command.Parameters.AddWithValue("@A2", sectionResult.SectionGrossPremium);
                command.Parameters.AddWithValue("@A3", sectionResult.SectionAdjustments?.SpecialDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A4", sectionResult.SectionAdjustments?.DeductibleDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A5", sectionResult.SectionAdjustments?.SpreadDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A6", sectionResult.SectionAdjustments?.LtaDiscountAmount ?? 0);
                command.Parameters.AddWithValue("@A7", sectionResult.SectionAdjustments?.TheftLoadingAmount ?? 0);
                command.Parameters.AddWithValue("@A8", sectionResult.SectionAdjustments?.SrccLoadingAmount ?? 0);
                command.Parameters.AddWithValue("@A9", sectionResult.SectionAdjustments?.OtherLoading2Amount ?? 0);
                command.Parameters.AddWithValue("@A10", 0);
                command.Parameters.AddWithValue("@A11", sectionResult.SectionNetPremium);
                command.Parameters.AddWithValue("@A17", sectionResult.CalculatedItems.Count);
                command.Parameters.AddWithValue("@A18", GetTopLocationValueForSection(sectionResult.CalculatedItems));
                command.Parameters.AddWithValue("@A19", sectionResult.SectionSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@A20", sectionResult.SectionNetPremium * (request.ProportionRate / 100));

                // Store adjustment RATES
                command.Parameters.AddWithValue("@A12", section.SpecialDiscountRate);
                command.Parameters.AddWithValue("@A13", section.DeductibleDiscountRate);
                command.Parameters.AddWithValue("@A14", section.SpreadDiscountRate);
                command.Parameters.AddWithValue("@A15", section.LtaDiscountRate);
                command.Parameters.AddWithValue("@A16", section.TheftLoadingRate);
                command.Parameters.AddWithValue("@Field11", section.SrccLoadingRate.ToString("F2"));
                command.Parameters.AddWithValue("@Field12", section.OtherLoading2Rate.ToString("F2"));

                command.Parameters.AddWithValue("@Tag", "SECTION");
                command.Parameters.AddWithValue("@TransGUID", sectionTransGuid);
                command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();

                // Save risk items for this section
                await SaveSectionRiskItemsToPolicyPremDetailsAsync(proposalNo, branchId, certOrDocNo, sectionDeclarationNo, sectionResult, request, connection, transaction);
            }

            /// <summary>
            /// Save section data WITHOUT adjustments (adjustments are proposal-level)
            /// </summary>
            private async Task SaveSectionWithoutAdjustmentsToPolicyTempDetailsAsync(
                string proposalNo,
                SectionCalculationResponseDto sectionResult,
                FireSectionDto section,
                CompleteFireCalculationRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {

                var certOrDocNo = await GetOrGenerateCertOrDocNoAsync(proposalNo, connection, transaction);

                // LEGACY (AddItemSectionTempGridDetails): every section gets its own
                // declaration number = PROPOSAL auto number, keyed by branch, stored in ENDTNum.
                var branchId = await GetBranchIdAsync(proposalNo, connection, transaction);
                var sectionDeclarationNo = await NextDeclarationNoAsync(branchId);
                var sectionTransGuid = Guid.NewGuid().ToString();

                var sql = @"
            INSERT INTO PolicyTempDetails (
                PolicyNo, ENDTNum, SectionID, ContentSection, Location,
                Field1, Field2, Field3, Field4, Field5, Field20,
                RiskSum, OtherSum, SectionPremium, TStartDate, TEndDate,
                A1, A2, A17, A18, A19, A20,
                Tag, TransGUID, SubmitBy, SubmitOn
            ) VALUES (
                @PolicyNo, @ENDTNum, @SectionID, @ContentSection, @Location,
                @Field1, @Field2, @Field3, @Field4, @Field5, @Field20,
                @RiskSum, @OtherSum, @SectionPremium, @TStartDate, @TEndDate,
                @A1, @A2, @A17, @A18, @A19, @A20,
                @Tag, @TransGUID, @SubmitBy, @SubmitOn
            )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@Field20", certOrDocNo);

                command.Parameters.AddWithValue("@ENDTNum", sectionDeclarationNo); // LEGACY: section declaration number (PROPOSAL auto number)
                command.Parameters.AddWithValue("@SectionID", sectionResult.SectionID);
                command.Parameters.AddWithValue("@ContentSection", sectionResult.SectionName);
                command.Parameters.AddWithValue("@Location", sectionResult.CalculatedItems.FirstOrDefault()?.Location ?? "");

                command.Parameters.AddWithValue("@Field1", "SECTION");
                command.Parameters.AddWithValue("@Field2", sectionResult.SectionName);
                command.Parameters.AddWithValue("@Field3", $"Items: {sectionResult.CalculatedItems.Count}");
                command.Parameters.AddWithValue("@Field4", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@Field5", "CALCULATED_NO_ADJUSTMENTS");

                // UPDATED: RiskSum and SectionPremium now include stock items
                command.Parameters.AddWithValue("@RiskSum", sectionResult.SectionSumInsured);
                command.Parameters.AddWithValue("@OtherSum", 0);
                command.Parameters.AddWithValue("@SectionPremium", sectionResult.SectionGrossPremium);
                command.Parameters.AddWithValue("@TStartDate", DateTime.Now);
                command.Parameters.AddWithValue("@TEndDate", DateTime.Now.AddDays(request.CoverDays));

                // UPDATED: Store section calculations including stock items
                command.Parameters.AddWithValue("@A1", sectionResult.SectionSumInsured);  // Total including stock
                command.Parameters.AddWithValue("@A2", sectionResult.SectionGrossPremium); // Total premium including stock
                command.Parameters.AddWithValue("@A17", sectionResult.CalculatedItems.Count);
                command.Parameters.AddWithValue("@A18", GetTopLocationValueForSection(sectionResult.CalculatedItems));
                command.Parameters.AddWithValue("@A19", sectionResult.SectionSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@A20", sectionResult.SectionGrossPremium * (request.ProportionRate / 100));

                command.Parameters.AddWithValue("@Tag", "SECTION");
                command.Parameters.AddWithValue("@TransGUID", sectionTransGuid);
                command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();

                // Save risk items for this section
                await SaveSectionRiskItemsToPolicyPremDetailsAsync(proposalNo, branchId, certOrDocNo, sectionDeclarationNo, sectionResult, request, connection, transaction);
            }
            // Updated SaveProposalLevelAdjustmentsAsync (only 2 rates)
            private async Task SaveProposalLevelAdjustmentsAsync(
                string proposalNo,
                ProposalLevelAdjustmentResponseDto adjustments,
                CompleteFireCalculationRequestDto request,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
            INSERT INTO PolicyTempDetails (
                PolicyNo, ENDTNum, SectionID, ContentSection,
                Field1, Field18, Field28, Field29,
                A1, A2, A10, A11,
                Tag, TransGUID, SubmitBy, SubmitOn
            ) VALUES (
                @PolicyNo, @ENDTNum, @SectionID, @ContentSection,
                @Field1, @Field18, @Field28, @Field29,
                @A1, @A2, @A10, @A11,
                @Tag, @TransGUID, @SubmitBy, @SubmitOn
            )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@ENDTNum", "");
                command.Parameters.AddWithValue("@SectionID", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@ContentSection", "Proposal Level Adjustments");

                command.Parameters.AddWithValue("@Field1", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@Field18", adjustments.StartingPremium.ToString());
                command.Parameters.AddWithValue("@Field28", adjustments.OtherDiscountsAmount.ToString());
                command.Parameters.AddWithValue("@Field29", adjustments.OtherLoadingsAmount.ToString());

                command.Parameters.AddWithValue("@A1", adjustments.StartingPremium);
                command.Parameters.AddWithValue("@A2", adjustments.NetPremiumDue);
                command.Parameters.AddWithValue("@A10", adjustments.OtherDiscountsAmount);
                command.Parameters.AddWithValue("@A11", adjustments.OtherLoadingsAmount);

                command.Parameters.AddWithValue("@Tag", "PROPOSAL_ADJUSTMENTS");
                command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }


            private async Task SaveSectionRiskItemsToPolicyPremDetailsAsync(
         string proposalNo,
         string branchId,
         string certOrDocNo,
         string sectionDeclarationNo,
         SectionCalculationResponseDto sectionResult,
         CompleteFireCalculationRequestDto request,
         SqlConnection connection,
         SqlTransaction transaction)
            {
                var sql = @"
    INSERT INTO PolicyPremDetails (
        PolicyNo, ENDTNum, Section, ContentDesc, SMICode,  Location,
        Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9, Field10,
        A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16,
        Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
        @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode,@Location,
        @Field1, @Field2, @Field3, @Field4, @Field5, @Field6, @Field7, @Field8, @Field9, @Field10,
        @A1, @A2, @A3, @A4, @A5, @A6, @A7, @A8, @A9, @A10, @A11, @A12, @A13, @A14, @A15, @A16,
        @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";

                // LEGACY: certOrDocNo, branchId and the section's declaration number are
                // passed in from the section save — items link through Field10 = declaration
                // number, exactly like AddItemToTempGridDetails in fire.ascx.vb.

                foreach (var riskItem in sectionResult.CalculatedItems)
                {
                    // LEGACY (AddItemToTempGridDetails): a fresh CERTNO auto number is
                    // generated for EVERY risk item row (txtCertNo resets to [AUTO] per row).
                    var itemCertNo = await NextItemCertNoAsync(branchId);

                    using var command = new SqlCommand(sql, connection, transaction);

                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@ENDTNum", itemCertNo); // LEGACY: per-item certificate number (CERTNO auto number)
                    command.Parameters.AddWithValue("@Section", sectionResult.SectionID);
                    command.Parameters.AddWithValue("@ContentDesc", riskItem.ItemDescription ?? "");
                    command.Parameters.AddWithValue("@SMICode", riskItem.SMICode ?? "");
                    command.Parameters.AddWithValue("@Location", riskItem.Location ?? "");

                    // LEGACY: Field1 = SectionID (cdbSectionDetails.SelectedItem.Value)
                    command.Parameters.AddWithValue("@Field1", sectionResult.SectionID);
                    command.Parameters.AddWithValue("@Field2", certOrDocNo);               // LEGACY: item -> calculation link (txtCertOrDocNo)
                    command.Parameters.AddWithValue("@Field3", sectionResult.SectionID);
                    command.Parameters.AddWithValue("@Field4", "RISK_ITEM");
                    command.Parameters.AddWithValue("@Field5", riskItem.Location ?? "");
                    command.Parameters.AddWithValue("@Field6", riskItem.StockItem?.StockCode ?? "");
                    command.Parameters.AddWithValue("@Field7", riskItem.StockItem?.StockDescription ?? "");
                    command.Parameters.AddWithValue("@Field8", riskItem.StockItem?.StockSumInsured.ToString("F2") ?? "0");
                    command.Parameters.AddWithValue("@Field9", riskItem.StockItem?.StockRate.ToString("F2") ?? "0");
                    // LEGACY: Field10 = parent section's declaration number (PolicyTempDetails.ENDTNum)
                    command.Parameters.AddWithValue("@Field10", sectionDeclarationNo);

                    // Store ALL calculation fields
                    command.Parameters.AddWithValue("@A1", riskItem.ActualValue);
                    command.Parameters.AddWithValue("@A2", riskItem.ItemRate);
                    command.Parameters.AddWithValue("@A3", riskItem.ActualPremium);
                    command.Parameters.AddWithValue("@A4", riskItem.ShareValue);
                    command.Parameters.AddWithValue("@A5", riskItem.PremiumValue);
                    command.Parameters.AddWithValue("@A6", riskItem.MultiplyRate);
                    command.Parameters.AddWithValue("@A7", riskItem.StockSumInsured);
                    command.Parameters.AddWithValue("@A8", riskItem.StockGrossPremium);
                    command.Parameters.AddWithValue("@A9", riskItem.StockDiscountAmount);
                    command.Parameters.AddWithValue("@A10", riskItem.StockNetPremium);
                    command.Parameters.AddWithValue("@A11", riskItem.TotalSumInsured);
                    command.Parameters.AddWithValue("@A12", riskItem.TotalGrossPremium);
                    command.Parameters.AddWithValue("@A13", riskItem.FeaDiscountRate);
                    command.Parameters.AddWithValue("@A14", riskItem.FeaDiscountAmount);
                    command.Parameters.AddWithValue("@A15", riskItem.NetPremiumAfterDiscounts);
                    command.Parameters.AddWithValue("@A16", riskItem.ItemNo);

                    command.Parameters.AddWithValue("@Tag", "NEW");
                    command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@SubmitBy", request.CalculatedBy);
                    command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }

            /// <summary>
            /// NEW: Get the TransGUID of the most recent section matching the criteria
            /// </summary>
            // NOTE: no longer used by the legacy-numbering save path (items now link via
            // Field10 = section declaration number). Kept because ProposalService is a
            // partial class and other files may still call it.
            private async Task<string> GetSectionTransGUIDAsync(
                string proposalNo,
                string sectionID,
                string certOrDocNo,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                var sql = @"
            SELECT TOP 1 TransGUID 
            FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo 
            AND SectionID = @SectionID 
            AND Field20 = @CertOrDocNo
            AND Field1 = 'SECTION'
            ORDER BY SubmitOn DESC";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@SectionID", sectionID);
                command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);

                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? Guid.NewGuid().ToString(); // Fallback to new GUID if not found
            }

            private async Task UpdatePolicyDetailsWithCalculationAsync(string proposalNo, decimal totalSumInsured,
                decimal totalPremium, ProRataCalculationResponseDto proRataResponse, CompleteFireCalculationRequestDto request,
                SqlConnection connection, SqlTransaction transaction)
            {
                var sql = @"
            UPDATE PolicyDetails SET 
                SumInsured = @SumInsured,
                GrossPremium = @GrossPremium,
                SumInsuredFrgn = @SumInsuredFrgn,
                GrossPremiumFrgn = @GrossPremiumFrgn,
                ProRataDays = @ProRataDays,
                ProRataPremium = @ProRataPremium,
                NetAmount = @NetAmount,
                TotalRiskValue = @TotalRiskValue,
                ProportionRate = @ProportionRate,
                ExRate = @ExRate,
                ExCurrency = @ExCurrency,
                A1 = @A1,
                A2 = @A2,
                A13 = @A13,
                A14 = @A14,
                Field1 = @Field1,
                Field2 = @Field2,
                Field50 = @Field50,
                ModifiedBy = @ModifiedBy,
                ModifiedOn = @ModifiedOn
            WHERE PolicyNo = @PolicyNo";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@SumInsured", totalSumInsured * (request.ProportionRate / 100));
                command.Parameters.AddWithValue("@GrossPremium", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@SumInsuredFrgn", totalSumInsured * request.ExchangeRate);
                command.Parameters.AddWithValue("@GrossPremiumFrgn", proRataResponse.PremiumDue * request.ExchangeRate);
                command.Parameters.AddWithValue("@ProRataDays", request.CoverDays);
                command.Parameters.AddWithValue("@ProRataPremium", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@NetAmount", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@TotalRiskValue", totalSumInsured);
                command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);
                command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
                command.Parameters.AddWithValue("@ExCurrency", request.Currency);

                // Store aggregate values in A fields
                command.Parameters.AddWithValue("@A1", totalSumInsured); // Total Sum Insured across all sections
                command.Parameters.AddWithValue("@A2", totalPremium); // Total Premium across all sections
                command.Parameters.AddWithValue("@A13", proRataResponse.ProRataPremium); // Pro-rata Premium
                command.Parameters.AddWithValue("@A14", proRataResponse.PremiumDue); // Final Premium Due

                command.Parameters.AddWithValue("@Field1", "CALCULATED");
                command.Parameters.AddWithValue("@Field2", $"Calculated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                command.Parameters.AddWithValue("@Field50", "CALCULATED");
                command.Parameters.AddWithValue("@ModifiedBy", request.CalculatedBy);
                command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);

                await command.ExecuteNonQueryAsync();
            }


            // Updated GetSectionCalculationSummaryAsync method
            public async Task<List<SectionSummaryDto>> GetSectionCalculationSummaryAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sections = new List<SectionSummaryDto>();

                var sql = @"
            SELECT 
                ptd.SectionID,
                ptd.ContentSection,
                ptd.Location,
                ptd.RiskSum,
                ptd.SectionPremium,
                ptd.A1 as SectionSumInsured,
                ptd.A2 as SectionGrossPremium,
                ptd.A11 as SectionNetPremium,
                ptd.A17 as RiskItemCount,
                (ptd.A3 + ptd.A4 + ptd.A5 + ptd.A6 + ptd.A10) as TotalDiscounts,
                (ptd.A7 + ptd.A8 + ptd.A9) as TotalLoadings,
                ptd.Field26,
                ptd.Field7,
                ptd.Field8,
                ptd.SubmitOn,
                ptd.SubmitBy
            FROM PolicyTempDetails ptd
            WHERE ptd.PolicyNo = @PolicyNo
            AND ISNULL(ptd.Tag, '') <> 'PROPOSAL_ADJUSTMENTS'
            AND ISNULL(ptd.Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS')
            ORDER BY ptd.SectionID";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sections.Add(new SectionSummaryDto
                    {
                        SectionID = reader.GetStringOrDefault("SectionID"),
                        SectionName = reader.GetStringOrDefault("ContentSection"),
                        Location = reader.GetStringOrDefault("Location"),
                        RiskItemCount = reader.GetValueOrDefault<int>("RiskItemCount"),
                        SectionSumInsured = reader.GetValueOrDefault<decimal>("SectionSumInsured"),
                        SectionGrossPremium = reader.GetValueOrDefault<decimal>("SectionGrossPremium"),
                        SectionNetPremium = reader.GetValueOrDefault<decimal>("SectionNetPremium"),
                        TotalDiscounts = reader.GetValueOrDefault<decimal>("TotalDiscounts"),
                        TotalLoadings = reader.GetValueOrDefault<decimal>("TotalLoadings"),
                        LastCalculated = reader.GetValueOrDefault<DateTime>("SubmitOn"),
                        CalculatedBy = reader.GetStringOrDefault("SubmitBy"),

                        // NEW: Optional section information fields
                        Field26 = reader.GetStringOrDefault("Field26"),
                        Field7 = reader.GetStringOrDefault("Field7"),
                        Field8 = reader.GetStringOrDefault("Field8")
                    });
                }

                return sections;
            }

            /// <summary>
            /// NEW: Calculate aggregate from multiple sections
            /// </summary>
            public async Task<MultiSectionAggregateResponseDto> CalculateMultiSectionAggregateAsync(
                MultiSectionAggregateRequestDto request)
            {
                try
                {
                    decimal totalSumInsured = 0;
                    decimal totalAggregatePremium = 0;
                    var sectionAggregates = new List<SectionAggregateDetail>();

                    foreach (var section in request.Sections)
                    {
                        // Calculate this section's aggregate
                        var sectionAggregate = new SectionAggregateDetail
                        {
                            SectionID = section.SectionID,
                            SectionName = section.SectionName,
                            SectionSumInsured = section.CalculatedItems.Sum(x => x.ShareValue),
                            SectionAggregatePremium = section.CalculatedItems.Sum(x => x.NetPremiumAfterDiscounts),
                            RiskItemCount = section.CalculatedItems.Count
                        };

                        sectionAggregates.Add(sectionAggregate);
                        totalSumInsured += sectionAggregate.SectionSumInsured;
                        totalAggregatePremium += sectionAggregate.SectionAggregatePremium;
                    }

                    return new MultiSectionAggregateResponseDto
                    {
                        SectionAggregates = sectionAggregates,
                        TotalSumInsured = totalSumInsured,
                        TotalAggregatePremium = totalAggregatePremium,
                        SectionCount = request.Sections.Count,
                        Success = true,
                        Message = $"Successfully aggregated {request.Sections.Count} section(s)"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating multi-section aggregate");
                    return new MultiSectionAggregateResponseDto
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            ///// <summary>
            ///// NEW: Apply proposal-level adjustments (replaces section-level adjustments)
            ///// </summary>
            //public async Task<SectionAdjustmentResponseDto> ApplyProposalAdjustmentsAsync(
            //    ProposalAdjustmentRequestDto request)
            //{
            //    try
            //    {
            //        var response = new SectionAdjustmentResponseDto
            //        {
            //            StartingPremium = request.TotalAggregatePremium,
            //            Success = true
            //        };

            //        decimal workingPremium = request.TotalAggregatePremium;

            //        // Apply discounts in sequence
            //        if (request.SpecialDiscountRate > 0)
            //        {
            //            response.SpecialDiscountAmount = workingPremium * (request.SpecialDiscountRate / 100);
            //            workingPremium -= response.SpecialDiscountAmount;
            //            response.SpecialDiscountNetAmount = workingPremium;
            //        }

            //        if (request.DeductibleDiscountRate > 0)
            //        {
            //            response.DeductibleDiscountAmount = workingPremium * (request.DeductibleDiscountRate / 100);
            //            workingPremium -= response.DeductibleDiscountAmount;
            //            response.DeductibleDiscountNetAmount = workingPremium;
            //        }

            //        if (request.SpreadDiscountRate > 0)
            //        {
            //            response.SpreadDiscountAmount = workingPremium * (request.SpreadDiscountRate / 100);
            //            workingPremium -= response.SpreadDiscountAmount;
            //            response.SpreadDiscountNetAmount = workingPremium;
            //        }

            //        if (request.LtaDiscountRate > 0)
            //        {
            //            response.LtaDiscountAmount = workingPremium * (request.LtaDiscountRate / 100);
            //            workingPremium -= response.LtaDiscountAmount;
            //            response.LtaDiscountNetAmount = workingPremium;
            //        }

            //        if (request.OtherDiscountsRate > 0)
            //        {
            //            response.OtherDiscountsAmount = workingPremium * (request.OtherDiscountsRate / 100);
            //            workingPremium -= response.OtherDiscountsAmount;
            //        }

            //        // Apply loadings in sequence
            //        if (request.TheftLoadingRate > 0)
            //        {
            //            response.TheftLoadingAmount = workingPremium * (request.TheftLoadingRate / 100);
            //            workingPremium += response.TheftLoadingAmount;
            //            response.TheftLoadingNetAmount = workingPremium;
            //        }

            //        if (request.SrccLoadingRate > 0)
            //        {
            //            response.SrccLoadingAmount = workingPremium * (request.SrccLoadingRate / 100);
            //            workingPremium += response.SrccLoadingAmount;
            //            response.SrccLoadingNetAmount = workingPremium;
            //        }

            //        if (request.OtherLoading2Rate > 0)
            //        {
            //            response.OtherLoading2Amount = workingPremium * (request.OtherLoading2Rate / 100);
            //            workingPremium += response.OtherLoading2Amount;
            //            response.OtherLoading2NetAmount = workingPremium;
            //        }

            //        if (request.OtherLoadingsRate > 0)
            //        {
            //            response.OtherLoadingsAmount = workingPremium * (request.OtherLoadingsRate / 100);
            //            workingPremium += response.OtherLoadingsAmount;
            //        }

            //        response.NetPremiumDue = workingPremium;
            //        response.Message = "Proposal-level adjustments applied successfully";

            //        return response;
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.LogError(ex, "Error applying proposal adjustments");
            //        return new SectionAdjustmentResponseDto
            //        {
            //            Success = false,
            //            Message = ex.Message
            //        };
            //    }
            //}

            /// <summary>
            /// NEW: Preview section calculation without saving
            /// </summary>
            public async Task<SectionPreviewResponseDto> PreviewSectionCalculationAsync(
                SectionPreviewRequestDto request)
            {
                try
                {
                    // Step 1: Calculate risk items
                    var itemCalcRequest = new RiskItemCalculationRequestDto
                    {
                        RiskItems = request.Section.RiskItems,
                        ProportionRate = request.ProportionRate
                    };
                    var itemCalcResponse = await CalculateRiskItemsAsync(itemCalcRequest);

                    if (!itemCalcResponse.Success)
                    {
                        return new SectionPreviewResponseDto
                        {
                            Success = false,
                            Message = itemCalcResponse.Message
                        };
                    }
                    // Step 2: Aggregate section -UPDATED to use new totals
                    var aggregateRequest = new SectionAggregateRequestDto
                    {
                        CalculatedItems = itemCalcResponse.CalculatedItems
                    };
                    var aggregateResponse = await CalculateSectionAggregatesAsync(aggregateRequest);

                    return new SectionPreviewResponseDto
                    {
                        SectionID = request.Section.SectionID,
                        SectionName = request.Section.SectionName,
                        CalculatedItems = itemCalcResponse.CalculatedItems,
                        // UPDATED: These now include stock items
                        SectionSumInsured = aggregateResponse.AggregateSumInsured,
                        SectionAggregatePremium = aggregateResponse.AggregatePremium,
                        ProportionRate = request.ProportionRate,
                        ShareSumInsured = aggregateResponse.AggregateSumInsured * (request.ProportionRate / 100),
                        SharePremium = aggregateResponse.AggregatePremium * (request.ProportionRate / 100),
                        Success = true,
                        Message = "Section preview calculated successfully (including stock items)"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error previewing section calculation");
                    return new SectionPreviewResponseDto
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            ///// <summary>
            ///// NEW: Preview complete calculation without saving to database
            ///// </summary>
            //public async Task<CompleteCalculationPreviewDto> PreviewCompleteCalculationAsync(
            //    CompleteCalculationPreviewRequestDto request)
            //{
            //    try
            //    {
            //        var sectionPreviews = new List<SectionPreviewResponseDto>();
            //        decimal totalSumInsured = 0;
            //        decimal totalGrossPremium = 0;

            //        // Step 1 & 2: Calculate each section
            //        foreach (var section in request.Sections)
            //        {
            //            var sectionPreview = await PreviewSectionCalculationAsync(new SectionPreviewRequestDto
            //            {
            //                Section = section,
            //                ProportionRate = request.ProportionRate
            //            });

            //            if (!sectionPreview.Success)
            //            {
            //                return new CompleteCalculationPreviewDto
            //                {
            //                    Success = false,
            //                    Message = $"Section {section.SectionID} calculation failed: {sectionPreview.Message}"
            //                };
            //            }

            //            sectionPreviews.Add(sectionPreview);
            //            totalSumInsured += sectionPreview.SectionSumInsured;
            //            totalGrossPremium += sectionPreview.SectionAggregatePremium;
            //        }

            //        // Step 3: Apply proposal-level adjustments
            //        var adjustmentRequest = new ProposalAdjustmentRequestDto
            //        {
            //            TotalAggregatePremium = totalGrossPremium,
            //            SpecialDiscountRate = request.SpecialDiscountRate,
            //            DeductibleDiscountRate = request.DeductibleDiscountRate,
            //            SpreadDiscountRate = request.SpreadDiscountRate,
            //            LtaDiscountRate = request.LtaDiscountRate,
            //            OtherDiscountsRate = request.OtherDiscountsRate,
            //            TheftLoadingRate = request.TheftLoadingRate,
            //            SrccLoadingRate = request.SrccLoadingRate,
            //            OtherLoading2Rate = request.OtherLoading2Rate,
            //            OtherLoadingsRate = request.OtherLoadingsRate
            //        };
            //        var adjustmentResponse = await ApplyProposalAdjustmentsAsync(adjustmentRequest);

            //        if (!adjustmentResponse.Success)
            //        {
            //            return new CompleteCalculationPreviewDto
            //            {
            //                Success = false,
            //                Message = adjustmentResponse.Message
            //            };
            //        }

            //        // Step 4: Calculate pro-rata
            //        var proRataRequest = new ProRataCalculationRequestDto
            //        {
            //            NetPremiumDue = adjustmentResponse.NetPremiumDue,
            //            CoverDays = request.CoverDays
            //        };
            //        var proRataResponse = await CalculateProRataAsync(proRataRequest);

            //        // Build preview response
            //        return new CompleteCalculationPreviewDto
            //        {
            //            SectionPreviews = sectionPreviews,
            //            TotalSumInsured = totalSumInsured,
            //            TotalGrossPremium = totalGrossPremium,
            //            TotalNetPremium = adjustmentResponse.NetPremiumDue,
            //            ProRataPremium = proRataResponse.ProRataPremium,
            //            FinalPremiumDue = proRataResponse.PremiumDue,
            //            ShareSumInsured = totalSumInsured * (request.ProportionRate / 100),
            //            SharePremium = proRataResponse.PremiumDue * (request.ProportionRate / 100),
            //            ForeignSumInsured = totalSumInsured * request.ExchangeRate,
            //            ForeignPremium = proRataResponse.PremiumDue * request.ExchangeRate,
            //            AdjustmentDetails = adjustmentResponse,
            //            ProRataDetails = proRataResponse,
            //            ProportionRate = request.ProportionRate,
            //            ExchangeRate = request.ExchangeRate,
            //            Currency = request.Currency,
            //            CoverDays = request.CoverDays,
            //            Success = true,
            //            Message = "Complete calculation preview generated successfully"
            //        };
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.LogError(ex, "Error previewing complete calculation");
            //        return new CompleteCalculationPreviewDto
            //        {
            //            Success = false,
            //            Message = ex.Message
            //        };
            //    }
            //}
            // 4. UPDATE UpdateProposalSummaryAsync method - change UPDATE target from Proposals to Policies
            private async Task UpdateProposalSummaryAsync(string proposalNo, decimal totalSumInsured, decimal totalNetPremium,
                ProRataCalculationResponseDto proRataResponse, decimal exchangeRate, SqlConnection connection, SqlTransaction transaction)
            {
                var sql = @"
            UPDATE Policies SET 
                SumInsured = @SumInsured,
                GrossPremium = @GrossPremium,
                SumInsuredFrgn = @SumInsuredFrgn,
                GrossPremiumFrgn = @GrossPremiumFrgn,
                ProRataDays = @ProRataDays,
                ProRataPremium = @ProRataPremium,
                TransSTATUS = @TransSTATUS,
                ModifiedBy = @ModifiedBy,
                ModifiedOn = @ModifiedOn
            WHERE PolicyNo = @ProposalNo";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@SumInsured", totalSumInsured);
                command.Parameters.AddWithValue("@GrossPremium", proRataResponse.PremiumDue);
                command.Parameters.AddWithValue("@SumInsuredFrgn", totalSumInsured * exchangeRate);
                command.Parameters.AddWithValue("@GrossPremiumFrgn", proRataResponse.PremiumDue * exchangeRate);
                command.Parameters.AddWithValue("@ProRataDays", proRataResponse.CoverDays);
                command.Parameters.AddWithValue("@ProRataPremium", proRataResponse.ProRataPremium);
                command.Parameters.AddWithValue("@TransSTATUS", "CALCULATED");
                command.Parameters.AddWithValue("@ModifiedBy", "SYSTEM");
                command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                command.Parameters.AddWithValue("@ProposalNo", proposalNo);

                await command.ExecuteNonQueryAsync();
            }

            private decimal GetTopLocationValueForSection(List<CalculatedRiskItemDto> items)
            {
                return items.GroupBy(x => x.Location ?? "DEFAULT")
                    .Select(g => g.Sum(x => x.TotalSumInsured))
                            .DefaultIfEmpty(0)
                            .Max();
            }


            /// <summary>
            /// Recalculate Fire calculation with section-level adjustments and save to database
            /// </summary>
            public async Task<CompleteCalculationResponseDto> RecalculateFireCalculationAsync(
                RecalculateFireCalculationRequestDto request)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Check if proposal exists
                    var proposalExists = await ProposalExistsAsync(request.ProposalNo);
                    if (!proposalExists)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = request.ProposalNo,
                            Success = false,
                            Message = "Proposal not found",
                            ValidationErrors = { "The specified proposal does not exist" }
                        };
                    }

                    // Delete all previous calculation data before recalculating
                    await DeletePreviousCalculationDataAsync(request.ProposalNo, connection, transaction);

                    var sectionCalculations = new List<SectionCalculationResponseDto>();
                    decimal totalSumInsured = 0;
                    decimal totalGrossPremium = 0; // Aggregate after section-level adjustments

                    // STEP 1 & 2 & 3: Process each section - calculate risk items, aggregate, and apply SECTION-LEVEL adjustments
                    foreach (var section in request.Sections)
                    {
                        // Calculate risk items for this section
                        var itemCalcRequest = new RiskItemCalculationRequestDto
                        {
                            RiskItems = section.RiskItems,
                            ProportionRate = request.ProportionRate
                        };
                        var itemCalcResponse = await CalculateRiskItemsAsync(itemCalcRequest);

                        if (!itemCalcResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} calculation failed: {itemCalcResponse.Message}",
                                ValidationErrors = { itemCalcResponse.Message }
                            };
                        }

                        // Aggregate this section's premium
                        var sectionAggregate = new SectionAggregateResponseDto
                        {
                            AggregateSumInsured = itemCalcResponse.TotalShareValue,
                            AggregatePremium = itemCalcResponse.TotalNetPremiumAfterDiscounts,
                            Success = true
                        };

                        // APPLY SECTION-LEVEL ADJUSTMENTS (7 rates from section)
                        var sectionAdjustmentRequest = new SectionAdjustmentRequestDto
                        {
                            AggregatePremium = sectionAggregate.AggregatePremium,
                            SpecialDiscountRate = section.SpecialDiscountRate,
                            DeductibleDiscountRate = section.DeductibleDiscountRate,
                            SpreadDiscountRate = section.SpreadDiscountRate,
                            LtaDiscountRate = section.LtaDiscountRate,
                            TheftLoadingRate = section.TheftLoadingRate,
                            SrccLoadingRate = section.SrccLoadingRate,
                            OtherLoading2Rate = section.OtherLoading2Rate
                        };

                        var sectionAdjustmentResponse = await ApplySectionAdjustmentsAsync(sectionAdjustmentRequest);

                        if (!sectionAdjustmentResponse.Success)
                        {
                            return new CompleteCalculationResponseDto
                            {
                                ProposalNo = request.ProposalNo,
                                Success = false,
                                Message = $"Section {section.SectionID} adjustments failed: {sectionAdjustmentResponse.Message}",
                                ValidationErrors = { sectionAdjustmentResponse.Message }
                            };
                        }

                        // Store section calculation WITH adjustments
                        var sectionResult = new SectionCalculationResponseDto
                        {
                            SectionID = section.SectionID,
                            SectionName = section.SectionName,
                            CalculatedItems = itemCalcResponse.CalculatedItems,
                            SectionSumInsured = sectionAggregate.AggregateSumInsured,
                            SectionGrossPremium = sectionAggregate.AggregatePremium,
                            SectionNetPremium = sectionAdjustmentResponse.NetPremiumDue,
                            SectionAdjustments = sectionAdjustmentResponse,
                            Success = true
                        };

                        sectionCalculations.Add(sectionResult);

                        // Add to totals (using section net premium after section-level adjustments)
                        totalSumInsured += sectionAggregate.AggregateSumInsured;
                        totalGrossPremium += sectionAdjustmentResponse.NetPremiumDue;

                        // Save section WITH adjustments to database
                        await SaveSectionWithAdjustmentsToPolicyTempDetailsAsync(
                            request.ProposalNo, sectionResult, section,
                            new CompleteFireCalculationRequestDto
                            {
                                ProposalNo = request.ProposalNo,
                                ProportionRate = request.ProportionRate,
                                ExchangeRate = request.ExchangeRate,
                                Currency = request.Currency,
                                CoverDays = request.CoverDays,
                                OtherDiscountsRate = request.OtherDiscountsRate,
                                OtherLoadingsRate = request.OtherLoadingsRate,
                                CalculatedBy = request.RecalculatedBy
                            },
                            connection, transaction);
                    }

                    // STEP 4: Apply PROPOSAL-LEVEL adjustments (ONLY 2 rates) to the aggregate
                    var proposalAdjustmentRequest = new ProposalAdjustmentRequestDto
                    {
                        TotalAggregatePremium = totalGrossPremium,
                        OtherDiscountsRate = request.OtherDiscountsRate,
                        OtherLoadingsRate = request.OtherLoadingsRate
                    };

                    var proposalAdjustmentResponse = await ApplyProposalLevelAdjustmentsAsync(proposalAdjustmentRequest);

                    if (!proposalAdjustmentResponse.Success)
                    {
                        return new CompleteCalculationResponseDto
                        {
                            ProposalNo = request.ProposalNo,
                            Success = false,
                            Message = $"Proposal adjustments failed: {proposalAdjustmentResponse.Message}",
                            ValidationErrors = { proposalAdjustmentResponse.Message }
                        };
                    }

                    decimal totalNetPremium = proposalAdjustmentResponse.NetPremiumDue;

                    // Save proposal-level adjustments
                    await SaveProposalLevelAdjustmentsAsync(request.ProposalNo, proposalAdjustmentResponse,
                        new CompleteFireCalculationRequestDto
                        {
                            ProposalNo = request.ProposalNo,
                            OtherDiscountsRate = request.OtherDiscountsRate,
                            OtherLoadingsRate = request.OtherLoadingsRate,
                            CalculatedBy = request.RecalculatedBy
                        },
                        connection, transaction);

                    // Save adjustment rates to PolicyDetails
                    await SaveAdjustmentRatesToPolicyDetailsAsync(request.ProposalNo,
                        new CompleteFireCalculationRequestDto
                        {
                            ProposalNo = request.ProposalNo,
                            OtherDiscountsRate = request.OtherDiscountsRate,
                            OtherLoadingsRate = request.OtherLoadingsRate,
                            CalculatedBy = request.RecalculatedBy
                        },
                        connection, transaction);

                    // STEP 5: Calculate pro-rata
                    var proRataRequest = new ProRataCalculationRequestDto
                    {
                        NetPremiumDue = totalNetPremium,
                        CoverDays = request.CoverDays
                    };
                    var proRataResponse = await CalculateProRataAsync(proRataRequest);

                    // Update PolicyDetails table with final calculated values
                    await UpdatePolicyDetailsWithCalculationAsync(request.ProposalNo, totalSumInsured,
                        totalGrossPremium, proRataResponse,
                        new CompleteFireCalculationRequestDto
                        {
                            ProposalNo = request.ProposalNo,
                            ProportionRate = request.ProportionRate,
                            ExchangeRate = request.ExchangeRate,
                            Currency = request.Currency,
                            CoverDays = request.CoverDays,
                            CalculatedBy = request.RecalculatedBy
                        },
                        connection, transaction);

                    // Update Proposals table summary
                    await UpdateProposalSummaryAsync(request.ProposalNo, totalSumInsured, totalNetPremium,
                        proRataResponse, request.ExchangeRate, connection, transaction);

                    await transaction.CommitAsync();

                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        SectionCalculations = sectionCalculations,
                        TotalSumInsured = totalSumInsured,
                        TotalPremium = totalGrossPremium,
                        NetPremium = totalNetPremium,
                        ProRataPremium = proRataResponse.ProRataPremium,
                        ShareSumInsured = totalSumInsured * (request.ProportionRate / 100),
                        SharePremium = proRataResponse.PremiumDue * (request.ProportionRate / 100),
                        ForeignSumInsured = totalSumInsured * request.ExchangeRate,
                        ForeignPremium = proRataResponse.PremiumDue * request.ExchangeRate,
                        ProposalLevelAdjustments = proposalAdjustmentResponse,
                        ProRataCalculations = proRataResponse,
                        Success = true,
                        CalculatedOn = DateTime.Now,
                        Message = "Recalculation completed successfully"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error recalculating fire calculation for {ProposalNo}", request.ProposalNo);
                    return new CompleteCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        Success = false,
                        Message = "Recalculation failed",
                        ValidationErrors = { ex.Message }
                    };
                }
            }


            /// <summary>
            /// **NEW METHOD: Delete all previous calculation data before recalculation**
            /// </summary>
            private async Task DeletePreviousCalculationDataAsync(
                string proposalNo,
                SqlConnection connection,
                SqlTransaction transaction)
            {
                // 1. Delete all risk items from PolicyPremDetails
                var deleteRiskItemsSql = @"
            DELETE FROM PolicyPremDetails 
            WHERE PolicyNo = @PolicyNo /*AND Tag = 'RISK_ITEM'*/";

                using (var cmd = new SqlCommand(deleteRiskItemsSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 2. Delete all sections from PolicyTempDetails
                var deleteSectionsSql = @"
            DELETE FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo /*AND Tag = 'SECTION'*/";

                using (var cmd = new SqlCommand(deleteSectionsSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Delete proposal-level adjustments from PolicyTempDetails
                var deleteAdjustmentsSql = @"
            DELETE FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo /*AND Tag = 'PROPOSAL_ADJUSTMENTS'*/";

                using (var cmd = new SqlCommand(deleteAdjustmentsSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    await cmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation("Deleted previous calculation data for proposal {ProposalNo}", proposalNo);
            }


            #region Helper Methods
            // OPTION 3: Use GUID-based approach(eliminates contention)
            private string GenerateProposalNumberGuidBased(string branchId, string riskClass)
            {
                var now = DateTime.Now;
                var shortGuid = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

                return $"{riskClass}{branchId}{now:yy}{shortGuid}";
            }















            private PolicyDetailDto MapPolicyDetailFromReader(SqlDataReader reader)
            {
                return new PolicyDetailDto
                {
                    DetailID = reader.GetStringOrDefault("DetailID"),
                    PolicyNo = reader.GetStringOrDefault("PolicyNo"),
                    CoPolicyNo = reader.GetStringOrDefault("CoPolicyNo"),
                    EntryDate = reader.GetValueOrDefault<DateTime>("EntryDate"),
                    EndorsementNo = reader.GetStringOrDefault("EndorsementNo"),
                    BizOption = reader.GetStringOrDefault("BizOption"),
                    DNCNNo = reader.GetStringOrDefault("DNCNNo"),
                    CertOrDocNo = reader.GetStringOrDefault("CertOrDocNo"),
                    InsuredName = reader.GetStringOrDefault("InsuredName"),
                    StartDate = reader.GetValueOrDefault<DateTime>("StartDate"),
                    EndDate = reader.GetValueOrDefault<DateTime>("EndDate"),
                    LTAStartDate = reader.GetNullableValue<DateTime>("LTAStartDate"),
                    LTAEndDate = reader.GetNullableValue<DateTime>("LTAEndDate"),
                    ExRateID = reader.GetStringOrDefault("ExRateID"),
                    ExRate = reader.GetValueOrDefault<decimal>("ExRate"),
                    ExCurrency = reader.GetStringOrDefault("ExCurrency"),
                    PremiumRate = reader.GetValueOrDefault<decimal>("PremiumRate"),
                    ProportionRate = reader.GetValueOrDefault<decimal>("ProportionRate"),
                    SumInsured = reader.GetValueOrDefault<decimal>("SumInsured"),
                    GrossPremium = reader.GetValueOrDefault<decimal>("GrossPremium"),
                    SumInsuredFrgn = reader.GetValueOrDefault<decimal>("SumInsuredFrgn"),
                    GrossPremiumFrgn = reader.GetValueOrDefault<decimal>("GrossPremiumFrgn"),
                    ProRataDays = reader.GetValueOrDefault<int>("ProRataDays"),
                    ProRataPremium = reader.GetValueOrDefault<decimal>("ProRataPremium"),
                    NetAmount = reader.GetValueOrDefault<decimal>("NetAmount"),
                    Deleted = reader.GetValueOrDefault<int>("Deleted"),
                    Active = reader.GetValueOrDefault<int>("Active"),
                    SubmittedBy = reader.GetStringOrDefault("SubmittedBy"),
                    SubmittedOn = reader.GetValueOrDefault<DateTime>("SubmittedOn"),
                    ModifiedBy = reader.GetStringOrDefault("ModifiedBy"),
                    ModifiedOn = reader.GetNullableValue<DateTime>("ModifiedOn"),
                    TotalRiskValue = reader.GetValueOrDefault<decimal>("TotalRiskValue"),
                    A1 = reader.GetValueOrDefault<decimal>("A1"),
                    A2 = reader.GetValueOrDefault<decimal>("A2"),
                    A3 = reader.GetValueOrDefault<decimal>("A3"),
                    A4 = reader.GetValueOrDefault<decimal>("A4"),
                    A5 = reader.GetValueOrDefault<decimal>("A5"),
                    A6 = reader.GetValueOrDefault<decimal>("A6"),
                    A7 = reader.GetValueOrDefault<decimal>("A7"),
                    A8 = reader.GetValueOrDefault<decimal>("A8"),
                    A9 = reader.GetValueOrDefault<decimal>("A9"),
                    A10 = reader.GetValueOrDefault<decimal>("A10"),
                    A11 = reader.GetValueOrDefault<decimal>("A11"),
                    A12 = reader.GetValueOrDefault<decimal>("A12"),
                    A13 = reader.GetValueOrDefault<decimal>("A13"),
                    A14 = reader.GetValueOrDefault<decimal>("A14"),
                    A15 = reader.GetValueOrDefault<decimal>("A15"),
                    A16 = reader.GetValueOrDefault<decimal>("A16"),
                    A17 = reader.GetValueOrDefault<decimal>("A17"),
                    A18 = reader.GetValueOrDefault<decimal>("A18"),
                    A19 = reader.GetValueOrDefault<decimal>("A19"),
                    A20 = reader.GetValueOrDefault<decimal>("A20"),
                    Field1 = reader.GetStringOrDefault("Field1"),
                    Field2 = reader.GetStringOrDefault("Field2"),
                    Field3 = reader.GetStringOrDefault("Field3"),
                    Field4 = reader.GetStringOrDefault("Field4"),
                    Field5 = reader.GetStringOrDefault("Field5")
                };
            }

            #endregion
            // Helper class for lookup data
            public class LookupData
            {
                public string BranchName { get; set; }
                public string SubRiskName { get; set; }
                public string PartyName { get; set; }
                public string MktStaffName { get; set; }

            }




            // 9. UPDATE MapProposalFromReader to handle the Policy table field mapping
            private ProposalDto MapProposalFromReader(SqlDataReader reader)
            {
                return new ProposalDto
                {
                    ProposalNo = reader["PolicyNo"] as string, // Changed from ProposalNo
                    TransDate = reader["TransDate"] as DateTime?,
                    CoPolicyNo = reader["CoPolicyNo"] as string,
                    CompanyID = reader["CompanyID"] as string,
                    BranchID = reader["BranchID"] as string,
                    Branch = reader["Branch"] as string,
                    BizSource = reader["BizSource"] as string,
                    SourceType = reader["SourceType"] as string,  // NEW FIELD

                    RiskID = reader["RiskID"] as string,
                    SubRiskID = reader["SubRiskID"] as string,
                    SubRisk = reader["SubRisk"] as string,
                    PartyID = reader["PartyID"] as string,
                    Party = reader["Party"] as string,
                    MktStaffID = reader["MktStaffID"] as string,
                    MktStaff = reader["MktStaff"] as string,
                    InsuredID = reader["InsuredID"] as string,
                    InsSurname = reader["InsSurname"] as string,
                    InsFirstname = reader["InsFirstname"] as string,
                    InsOthernames = reader["InsOthernames"] as string,
                    InsAddress = reader["InsAddress"] as string,
                    InsStateID = reader["InsStateID"] as string,
                    InsMobilePhone = reader["InsMobilePhone"] as string,
                    InsLandPhone = reader["InsLandPhone"] as string,
                    InsEmail = reader["InsEmail"] as string,
                    InsFaxNo = reader["InsFaxNo"] as string,
                    InsOccupation = reader["InsOccupation"] as string,
                    IsProposal = reader["isProposal"] as byte?,
                    StartDate = reader["StartDate"] as DateTime?,
                    EndDate = reader["EndDate"] as DateTime?,
                    ExRate = reader["ExRate"] == DBNull.Value ? null : Convert.ToDouble(reader["ExRate"]),
                    ExCurrency = reader["ExCurrency"] as string,
                    PremiumRate = reader["PremiumRate"] == DBNull.Value ? null : Convert.ToDouble(reader["PremiumRate"]),
                    ProportionRate = reader["ProportionRate"] == DBNull.Value ? null : Convert.ToDouble(reader["ProportionRate"]),
                    SumInsured = reader["SumInsured"] == DBNull.Value ? null : Convert.ToDecimal(reader["SumInsured"]),
                    GrossPremium = reader["GrossPremium"] == DBNull.Value ? null : Convert.ToDecimal(reader["GrossPremium"]),
                    SumInsuredFrgn = reader["SumInsuredFrgn"] == DBNull.Value ? null : Convert.ToDecimal(reader["SumInsuredFrgn"]),
                    GrossPremiumFrgn = reader["GrossPremiumFrgn"] == DBNull.Value ? null : Convert.ToDecimal(reader["GrossPremiumFrgn"]),
                    ProRataDays = reader["ProRataDays"] == DBNull.Value ? null : Convert.ToInt32(reader["ProRataDays"]),
                    ProRataPremium = reader["ProRataPremium"] == DBNull.Value ? null : Convert.ToDecimal(reader["ProRataPremium"]),
                    Remarks = reader["Remarks"] as string,
                    Deleted = reader["Deleted"] as byte?,
                    Active = reader["Active"] as byte?,
                    TransSTATUS = reader["TransSTATUS"] as string,
                    SubmittedBy = reader["SubmittedBy"] as string,
                    SubmittedOn = reader["SubmittedOn"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["SubmittedOn"]),
                    ModifiedBy = reader["ModifiedBy"] as string,
                    ModifiedOn = reader["ModifiedOn"] as DateTime?,
                    LeaderID = reader["LeaderID"] as string, // Maps to LeadID in Policy table
                    Leader = reader["Leader"] as string,
                    CommissionRate = null // Not in Policy table, set to null or calculate if needed
                };
            }
            public async Task<CurrentCalculationDto> GetCurrentCalculationAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                try
                {
                    // Get the latest calculation version
                    var latestVersionSql = @"
                        SELECT TOP 1 DetailID, SubmittedBy, SubmittedOn,
                               ProportionRate, ExRate, ExCurrency, ProRataDays,
                               SumInsured, GrossPremium, SumInsuredFrgn, GrossPremiumFrgn,
                               ProRataPremium, NetAmount, TotalRiskValue,
                               A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16
                        FROM PolicyDetails 
                        WHERE PolicyNo = @ProposalNo AND Deleted = 0 
                        ORDER BY SubmittedOn DESC";

                    CurrentCalculationDto result = null;

                    using (var command = new SqlCommand(latestVersionSql, connection))
                    {
                        command.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        using var reader = await command.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            result = new CurrentCalculationDto
                            {
                                ProposalNo = proposalNo,
                                CurrentVersionId = reader.GetStringOrDefault("DetailID"),
                                LastCalculatedOn = reader.GetValueOrDefault<DateTime>("SubmittedOn"),
                                LastCalculatedBy = reader.GetStringOrDefault("SubmittedBy"),
                                ProportionRate = reader.GetValueOrDefault<decimal>("ProportionRate"),
                                ExchangeRate = reader.GetValueOrDefault<decimal>("ExRate"),
                                Currency = reader.GetStringOrDefault("ExCurrency"),
                                CoverDays = reader.GetValueOrDefault<int>("ProRataDays"),
                                TotalSumInsured = reader.GetValueOrDefault<decimal>("TotalRiskValue"),
                                NetPremium = reader.GetValueOrDefault<decimal>("GrossPremium"),
                                ProRataPremium = reader.GetValueOrDefault<decimal>("ProRataPremium"),
                                ShareSumInsured = reader.GetValueOrDefault<decimal>("SumInsured"),
                                SharePremium = reader.GetValueOrDefault<decimal>("NetAmount"),
                                ForeignSumInsured = reader.GetValueOrDefault<decimal>("SumInsuredFrgn"),
                                ForeignPremium = reader.GetValueOrDefault<decimal>("GrossPremiumFrgn"),

                                // Reconstruct section adjustments from A1-A16 fields
                                SectionAdjustments = new SectionAdjustmentCurrentDto
                                {
                                    AggregatePremium = reader.GetValueOrDefault<decimal>("A2"),
                                    SpecialDiscountAmount = reader.GetValueOrDefault<decimal>("A3"),
                                    DeductibleDiscountAmount = reader.GetValueOrDefault<decimal>("A4"),
                                    SpreadDiscountAmount = reader.GetValueOrDefault<decimal>("A5"),
                                    LtaDiscountAmount = reader.GetValueOrDefault<decimal>("A6"),
                                    TheftLoadingAmount = reader.GetValueOrDefault<decimal>("A7"),
                                    SrccLoadingAmount = reader.GetValueOrDefault<decimal>("A8"),
                                    OtherLoading2Amount = reader.GetValueOrDefault<decimal>("A9"),
                                    OtherDiscountsAmount = reader.GetValueOrDefault<decimal>("A10"),
                                    OtherLoadingsAmount = reader.GetValueOrDefault<decimal>("A11"),
                                    NetPremiumDue = reader.GetValueOrDefault<decimal>("A12")
                                }
                            };
                        }
                    }

                    if (result == null) return null;

                    // Get risk items from stored calculation or recreate from business logic
                    // For now, we'll get them from a separate risk items table if it exists
                    // Otherwise, this would need to be reconstructed from the original input
                    result.RiskItems = await GetStoredRiskItemsAsync(proposalNo, result.CurrentVersionId, connection);

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting current calculation for {ProposalNo}", proposalNo);
                    return null;
                }
            }






            private async Task<List<CalculatedRiskItemDto>> GetStoredRiskItemsAsync(string proposalNo, string versionId, SqlConnection connection)
            {
                // This would retrieve risk items if they're stored separately
                // For now, return empty list - in a real system you'd either:
                // 1. Store risk items in a separate table linked to the calculation version
                // 2. Serialize them in Field1-Field5 or other text fields
                // 3. Reconstruct them from the original proposal requirements

                return new List<CalculatedRiskItemDto>();
            }




            /// <summary>
            /// Delete a proposal (soft delete with cascading to related records)
            /// </summary>
            public async Task<ResponseInfo> DeleteProposalAsync(string proposalNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // 1. Check if policy exists and is not already deleted
                    var checkSql = @"
                SELECT COUNT(*) 
                FROM Policies 
                WHERE PolicyNo = @ProposalNo AND Deleted = 0";

                    using (var checkCmd = new SqlCommand(checkSql, connection, transaction))
                    {
                        checkCmd.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        var count = (int)await checkCmd.ExecuteScalarAsync();

                        if (count == 0)
                        {
                            return new ResponseInfo
                            {
                                ErrorCode = 1,
                                ErrorMessage = "Policy not found or already deleted"
                            };
                        }
                    }

                    // 2. Delete from Policies table
                    var deletePolicySql = @"
                DELETE FROM Policies 
                WHERE PolicyNo = @ProposalNo";

                    using (var cmd = new SqlCommand(deletePolicySql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 3. Delete from PolicyDetails
                    var deletePolicyDetailsSql = @"
                DELETE FROM PolicyDetails 
                WHERE PolicyNo = @ProposalNo";

                    using (var cmd = new SqlCommand(deletePolicyDetailsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 4. Delete risk items from PolicyPremDetails
                    var deleteRiskItemsSql = @"
                DELETE FROM PolicyPremDetails 
                WHERE PolicyNo = @ProposalNo";

                    using (var cmd = new SqlCommand(deleteRiskItemsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 5. Delete sections from PolicyTempDetails
                    var deleteSectionsSql = @"
                DELETE FROM PolicyTempDetails 
                WHERE PolicyNo = @ProposalNo";

                    using (var cmd = new SqlCommand(deleteSectionsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ProposalNo", proposalNo);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Policy {ProposalNo} deleted successfully", proposalNo);

                    return new ResponseInfo
                    {
                        ErrorCode = 0,
                        ErrorMessage = "Policy deleted successfully",
                        TotalSuccess = 1
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error deleting policy {ProposalNo}", proposalNo);

                    return new ResponseInfo
                    {
                        ErrorCode = 1,
                        ErrorMessage = "Failed to delete policy",
                        ExtraMessage = ex.Message
                    };
                }
            }
            // ADD THIS NEW METHOD to save adjustment rates to Proposals table
            //    private async Task SaveAdjustmentRatesToProposalAsync(
            //        string proposalNo,
            //        CompleteFireCalculationRequestDto request,
            //        SqlConnection connection,
            //        SqlTransaction transaction)
            //    {
            //        var sql = @"
            //    UPDATE PolicyDetails SET 
            //        A3 = @SpecialDiscountRate,
            //        A4 = @DeductibleDiscountRate,
            //        A5 = @SpreadDiscountRate,
            //        A6 = @LtaDiscountRate,
            //        A7 = @OtherDiscountsRate,
            //        A8 = @TheftLoadingRate,
            //        A9 = @SrccLoadingRate,
            //        A10 = @OtherLoading2Rate,
            //        A11 = @OtherLoadingsRate,
            //        ModifiedBy = @ModifiedBy,
            //        ModifiedOn = @ModifiedOn
            //    WHERE PolicyNo = @ProposalNo";

            //        using var command = new SqlCommand(sql, connection, transaction);

            //        command.Parameters.AddWithValue("@SpecialDiscountRate", request.SpecialDiscountRate);
            //        command.Parameters.AddWithValue("@DeductibleDiscountRate", request.DeductibleDiscountRate);
            //        command.Parameters.AddWithValue("@SpreadDiscountRate", request.SpreadDiscountRate);
            //        command.Parameters.AddWithValue("@LtaDiscountRate", request.LtaDiscountRate);
            //        command.Parameters.AddWithValue("@OtherDiscountsRate", request.OtherDiscountsRate);
            //        command.Parameters.AddWithValue("@TheftLoadingRate", request.TheftLoadingRate);
            //        command.Parameters.AddWithValue("@SrccLoadingRate", request.SrccLoadingRate);
            //        command.Parameters.AddWithValue("@OtherLoading2Rate", request.OtherLoading2Rate);
            //        command.Parameters.AddWithValue("@OtherLoadingsRate", request.OtherLoadingsRate);
            //        command.Parameters.AddWithValue("@ModifiedBy", request.CalculatedBy);
            //        command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
            //        command.Parameters.AddWithValue("@ProposalNo", proposalNo);

            //        await command.ExecuteNonQueryAsync();
            //    }

            // ------------------------------------------------------------
            // PART 1: Dynamic section adjustments
            // ------------------------------------------------------------

            private static FireAdjustmentDto FindFireAdjustment(List<FireAdjustmentDto> adjustments, string name)
            {
                return adjustments?.FirstOrDefault(a =>
                    string.Equals(a.AdjustmentName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
            }

            /// <summary>
            /// New dynamic section-adjustment path — mirrors ApplySectionAdjustmentsAsync's
            /// exact order (loadings first, cascading additive; then discounts,
            /// cascading subtractive) but with per-adjustment name + SumInsured/
            /// Premium basis choice. Does not touch or call ApplySectionAdjustmentsAsync.
            /// </summary>
            public async Task<FireSectionDynamicAdjustmentResponseDto> ApplySectionDynamicAdjustmentsAsync(
                FireSectionDynamicAdjustmentRequestDto request)
            {
                try
                {
                    var workingPremium = request.AggregatePremium;

                    var appliedLoadings = new List<FireAdjustmentDto>();
                    decimal totalLoadingAmount = 0;

                    if (request.Loadings != null && request.Loadings.Any())
                    {
                        int seq = 1;
                        foreach (var loading in request.Loadings.OrderBy(l => l.SequenceOrder))
                        {
                            var baseAmount = loading.AppliedOn == AdjustmentBasis.SumInsured
                                ? request.AggregateSumInsured : workingPremium;
                            var amount = baseAmount * (loading.Rate / 100);

                            appliedLoadings.Add(new FireAdjustmentDto
                            {
                                AdjustmentName = loading.AdjustmentName,
                                AdjustmentType = "LOADING",
                                Rate = loading.Rate,
                                AppliedOn = loading.AppliedOn,
                                BaseAmount = baseAmount,
                                Amount = amount,
                                SequenceOrder = seq++,
                                Formula = loading.AppliedOn == AdjustmentBasis.SumInsured
                                    ? $"{loading.Rate}% of Sum Insured (₦{baseAmount:N2})"
                                    : $"{loading.Rate}% of Current Premium (₦{baseAmount:N2})"
                            });

                            workingPremium += amount;
                            totalLoadingAmount += amount;
                        }
                    }

                    var appliedDiscounts = new List<FireAdjustmentDto>();
                    decimal totalDiscountAmount = 0;

                    if (request.Discounts != null && request.Discounts.Any())
                    {
                        int seq = 1;
                        foreach (var discount in request.Discounts.OrderBy(d => d.SequenceOrder))
                        {
                            var baseAmount = discount.AppliedOn == AdjustmentBasis.SumInsured
                                ? request.AggregateSumInsured : workingPremium;
                            var amount = baseAmount * (discount.Rate / 100);

                            appliedDiscounts.Add(new FireAdjustmentDto
                            {
                                AdjustmentName = discount.AdjustmentName,
                                AdjustmentType = "DISCOUNT",
                                Rate = discount.Rate,
                                AppliedOn = discount.AppliedOn,
                                BaseAmount = baseAmount,
                                Amount = amount,
                                SequenceOrder = seq++,
                                Formula = discount.AppliedOn == AdjustmentBasis.SumInsured
                                    ? $"{discount.Rate}% of Sum Insured (₦{baseAmount:N2})"
                                    : $"{discount.Rate}% of Current Premium (₦{baseAmount:N2})"
                            });

                            workingPremium -= amount;
                            totalDiscountAmount += amount;
                        }
                    }

                    return new FireSectionDynamicAdjustmentResponseDto
                    {
                        StartingPremium = request.AggregatePremium,
                        LoadingsApplied = appliedLoadings,
                        DiscountsApplied = appliedDiscounts,
                        TotalLoadingAmount = totalLoadingAmount,
                        TotalDiscountAmount = totalDiscountAmount,
                        NetPremiumDue = workingPremium,
                        Success = true,
                        Message = $"{appliedLoadings.Count} loading(s), {appliedDiscounts.Count} discount(s) applied"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying fire section dynamic adjustments");
                    return new FireSectionDynamicAdjustmentResponseDto { Success = false, Message = ex.Message };
                }
            }
            /// <summary>
            /// Item-level dynamic adjustments. Same ordering contract as
            /// ApplySectionDynamicAdjustmentsAsync (loadings first, cascading
            /// additive; then discounts, cascading subtractive), but the
            /// SumInsured basis is THIS ITEM's total SI (main + stock) rather
            /// than the section aggregate.
            /// </summary>
            private FireRiskItemDynamicAdjustmentResponseDto ApplyRiskItemDynamicAdjustments(
                FireRiskItemDynamicAdjustmentRequestDto request)
            {
                try
                {
                    var workingPremium = request.ItemGrossPremium;

                    var appliedLoadings = new List<FireAdjustmentDto>();
                    decimal totalLoadingAmount = 0;

                    if (request.Loadings != null && request.Loadings.Any())
                    {
                        int seq = 1;
                        foreach (var loading in request.Loadings.OrderBy(l => l.SequenceOrder))
                        {
                            var baseAmount = loading.AppliedOn == AdjustmentBasis.SumInsured
                                ? request.ItemSumInsured : workingPremium;
                            var amount = baseAmount * (loading.Rate / 100);

                            appliedLoadings.Add(new FireAdjustmentDto
                            {
                                AdjustmentName = loading.AdjustmentName,
                                AdjustmentType = "LOADING",
                                Rate = loading.Rate,
                                AppliedOn = loading.AppliedOn,
                                BaseAmount = baseAmount,
                                Amount = amount,
                                SequenceOrder = seq++,
                                Formula = loading.AppliedOn == AdjustmentBasis.SumInsured
                                    ? $"{loading.Rate}% of Item Sum Insured (₦{baseAmount:N2})"
                                    : $"{loading.Rate}% of Current Item Premium (₦{baseAmount:N2})"
                            });

                            workingPremium += amount;
                            totalLoadingAmount += amount;
                        }
                    }

                    var appliedDiscounts = new List<FireAdjustmentDto>();
                    decimal totalDiscountAmount = 0;

                    if (request.Discounts != null && request.Discounts.Any())
                    {
                        int seq = 1;
                        foreach (var discount in request.Discounts.OrderBy(d => d.SequenceOrder))
                        {
                            var baseAmount = discount.AppliedOn == AdjustmentBasis.SumInsured
                                ? request.ItemSumInsured : workingPremium;
                            var amount = baseAmount * (discount.Rate / 100);

                            appliedDiscounts.Add(new FireAdjustmentDto
                            {
                                AdjustmentName = discount.AdjustmentName,
                                AdjustmentType = "DISCOUNT",
                                Rate = discount.Rate,
                                AppliedOn = discount.AppliedOn,
                                BaseAmount = baseAmount,
                                Amount = amount,
                                SequenceOrder = seq++,
                                Formula = discount.AppliedOn == AdjustmentBasis.SumInsured
                                    ? $"{discount.Rate}% of Item Sum Insured (₦{baseAmount:N2})"
                                    : $"{discount.Rate}% of Current Item Premium (₦{baseAmount:N2})"
                            });

                            workingPremium -= amount;
                            totalDiscountAmount += amount;
                        }
                    }

                    return new FireRiskItemDynamicAdjustmentResponseDto
                    {
                        StartingPremium = request.ItemGrossPremium,
                        LoadingsApplied = appliedLoadings,
                        DiscountsApplied = appliedDiscounts,
                        TotalLoadingAmount = totalLoadingAmount,
                        TotalDiscountAmount = totalDiscountAmount,
                        NetPremiumDue = workingPremium,
                        Success = true,
                        Message = $"{appliedLoadings.Count} loading(s), {appliedDiscounts.Count} discount(s) applied"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying fire risk-item dynamic adjustments");
                    return new FireRiskItemDynamicAdjustmentResponseDto { Success = false, Message = ex.Message };
                }
            }

            /// <summary>Public wrapper so the controller can preview item adjustments without saving.</summary>
            public Task<FireRiskItemDynamicAdjustmentResponseDto> ApplyRiskItemDynamicAdjustmentsAsync(
                FireRiskItemDynamicAdjustmentRequestDto request)
                => Task.FromResult(ApplyRiskItemDynamicAdjustments(request));
            /// <summary>
            /// Back-fills recognized standard names from a dynamic adjustment
            /// result into a SectionAdjustmentResponseDto (matching the shape
            /// ApplySectionAdjustmentsAsync itself returns) AND onto the section's
            /// own rate properties — so SaveSectionWithAdjustmentsToPolicyTempDetailsAsync
            /// and anything else that reads sectionResult.SectionAdjustments or
            /// section.SpecialDiscountRate etc. keeps working unchanged, whether the
            /// fixed or dynamic path computed the numbers. Custom-named adjustments
            /// (not matching one of the 7 standard names) have no fixed-field home
            /// here — they're still fully represented in the dynamic result and
            /// persisted via SaveFireSectionDynamicAdjustmentsWithCertAsync below.
            /// </summary>
            private SectionAdjustmentResponseDto MapDynamicToFixedSectionAdjustments(
                FireSectionDynamicAdjustmentResponseDto dynamicResult,
                FireSectionIncrementalInputDto section)
            {
                var fixedResponse = new SectionAdjustmentResponseDto
                {
                    StartingPremium = dynamicResult.StartingPremium,
                    NetPremiumDue = dynamicResult.NetPremiumDue,
                    Success = true
                };

                // Loadings — cascading running total tracked the same way the
                // fixed method does (NetAmount = premium right after this step).
                var runningAfterLoadings = dynamicResult.StartingPremium;

                var theft = FindFireAdjustment(dynamicResult.LoadingsApplied, "Theft Loading");
                if (theft != null)
                {
                    fixedResponse.TheftLoadingAmount = theft.Amount;
                    runningAfterLoadings += theft.Amount;
                    fixedResponse.TheftLoadingNetAmount = runningAfterLoadings;
                    section.TheftLoadingRate = theft.Rate;
                }

                var srcc = FindFireAdjustment(dynamicResult.LoadingsApplied, "SRCC Loading");
                if (srcc != null)
                {
                    fixedResponse.SrccLoadingAmount = srcc.Amount;
                    runningAfterLoadings += srcc.Amount;
                    fixedResponse.SrccLoadingNetAmount = runningAfterLoadings;
                    section.SrccLoadingRate = srcc.Rate;
                }

                var otherLoading2 = FindFireAdjustment(dynamicResult.LoadingsApplied, "Other Loading 2");
                if (otherLoading2 != null)
                {
                    fixedResponse.OtherLoading2Amount = otherLoading2.Amount;
                    runningAfterLoadings += otherLoading2.Amount;
                    fixedResponse.OtherLoading2NetAmount = runningAfterLoadings;
                    section.OtherLoading2Rate = otherLoading2.Rate;
                }

                // Discounts — cascading from wherever loadings left off.
                var runningAfterDiscounts = runningAfterLoadings;

                var special = FindFireAdjustment(dynamicResult.DiscountsApplied, "Special Discount");
                if (special != null)
                {
                    fixedResponse.SpecialDiscountAmount = special.Amount;
                    runningAfterDiscounts -= special.Amount;
                    fixedResponse.SpecialDiscountNetAmount = runningAfterDiscounts;
                    section.SpecialDiscountRate = special.Rate;
                }

                var deductible = FindFireAdjustment(dynamicResult.DiscountsApplied, "Deductible Discount");
                if (deductible != null)
                {
                    fixedResponse.DeductibleDiscountAmount = deductible.Amount;
                    runningAfterDiscounts -= deductible.Amount;
                    fixedResponse.DeductibleDiscountNetAmount = runningAfterDiscounts;
                    section.DeductibleDiscountRate = deductible.Rate;
                }

                var spread = FindFireAdjustment(dynamicResult.DiscountsApplied, "Spread Discount");
                if (spread != null)
                {
                    fixedResponse.SpreadDiscountAmount = spread.Amount;
                    runningAfterDiscounts -= spread.Amount;
                    fixedResponse.SpreadDiscountNetAmount = runningAfterDiscounts;
                    section.SpreadDiscountRate = spread.Rate;
                }

                var lta = FindFireAdjustment(dynamicResult.DiscountsApplied, "LTA Discount");
                if (lta != null)
                {
                    fixedResponse.LtaDiscountAmount = lta.Amount;
                    runningAfterDiscounts -= lta.Amount;
                    fixedResponse.LtaDiscountNetAmount = runningAfterDiscounts;
                    section.LtaDiscountRate = lta.Rate;
                }

                return fixedResponse;
            }

            /// <summary>
            /// Overload of the above for FireSectionDto — the type used inside the
            /// existing CompleteFireCalculationAsync's section loop. Same back-fill
            /// logic, duplicated rather than shared via a common base/interface
            /// since FireSectionDto isn't declared partial and I'm not modifying it
            /// beyond the two new list properties already requested.
            /// </summary>
            private SectionAdjustmentResponseDto MapDynamicToFixedSectionAdjustments(
                FireSectionDynamicAdjustmentResponseDto dynamicResult,
                FireSectionDto section)
            {
                var fixedResponse = new SectionAdjustmentResponseDto
                {
                    StartingPremium = dynamicResult.StartingPremium,
                    NetPremiumDue = dynamicResult.NetPremiumDue,
                    Success = true
                };

                var runningAfterLoadings = dynamicResult.StartingPremium;

                var theft = FindFireAdjustment(dynamicResult.LoadingsApplied, "Theft Loading");
                if (theft != null)
                {
                    fixedResponse.TheftLoadingAmount = theft.Amount;
                    runningAfterLoadings += theft.Amount;
                    fixedResponse.TheftLoadingNetAmount = runningAfterLoadings;
                    section.TheftLoadingRate = theft.Rate;
                }

                var srcc = FindFireAdjustment(dynamicResult.LoadingsApplied, "SRCC Loading");
                if (srcc != null)
                {
                    fixedResponse.SrccLoadingAmount = srcc.Amount;
                    runningAfterLoadings += srcc.Amount;
                    fixedResponse.SrccLoadingNetAmount = runningAfterLoadings;
                    section.SrccLoadingRate = srcc.Rate;
                }

                var otherLoading2 = FindFireAdjustment(dynamicResult.LoadingsApplied, "Other Loading 2");
                if (otherLoading2 != null)
                {
                    fixedResponse.OtherLoading2Amount = otherLoading2.Amount;
                    runningAfterLoadings += otherLoading2.Amount;
                    fixedResponse.OtherLoading2NetAmount = runningAfterLoadings;
                    section.OtherLoading2Rate = otherLoading2.Rate;
                }

                var runningAfterDiscounts = runningAfterLoadings;

                var special = FindFireAdjustment(dynamicResult.DiscountsApplied, "Special Discount");
                if (special != null)
                {
                    fixedResponse.SpecialDiscountAmount = special.Amount;
                    runningAfterDiscounts -= special.Amount;
                    fixedResponse.SpecialDiscountNetAmount = runningAfterDiscounts;
                    section.SpecialDiscountRate = special.Rate;
                }

                var deductible = FindFireAdjustment(dynamicResult.DiscountsApplied, "Deductible Discount");
                if (deductible != null)
                {
                    fixedResponse.DeductibleDiscountAmount = deductible.Amount;
                    runningAfterDiscounts -= deductible.Amount;
                    fixedResponse.DeductibleDiscountNetAmount = runningAfterDiscounts;
                    section.DeductibleDiscountRate = deductible.Rate;
                }

                var spread = FindFireAdjustment(dynamicResult.DiscountsApplied, "Spread Discount");
                if (spread != null)
                {
                    fixedResponse.SpreadDiscountAmount = spread.Amount;
                    runningAfterDiscounts -= spread.Amount;
                    fixedResponse.SpreadDiscountNetAmount = runningAfterDiscounts;
                    section.SpreadDiscountRate = spread.Rate;
                }

                var lta = FindFireAdjustment(dynamicResult.DiscountsApplied, "LTA Discount");
                if (lta != null)
                {
                    fixedResponse.LtaDiscountAmount = lta.Amount;
                    runningAfterDiscounts -= lta.Amount;
                    fixedResponse.LtaDiscountNetAmount = runningAfterDiscounts;
                    section.LtaDiscountRate = lta.Rate;
                }

                return fixedResponse;
            }

            /// <summary>
            /// Persists the FULL dynamic adjustment breakdown (any name, standard
            /// or custom) as PolicyPremDetails rows — the authoritative source,
            /// same pattern as Hull/Cargo/Motor's adjustment persistence. The
            /// fixed-column mirror above is for backward compatibility only.
            /// </summary>
            private async Task SaveFireSectionDynamicAdjustmentsWithCertAsync(
                string proposalNo, string certOrDocNo, string sectionDeclarationNo,
                List<FireAdjustmentDto> adjustments, string adjustmentType,
                SqlConnection connection, SqlTransaction transaction)
            {
                foreach (var adjustment in adjustments)
                {
                    var sql = @"
            INSERT INTO PolicyPremDetails (
                PolicyNo, ENDTNum, Section, ContentDesc, SMICode,
                Field1, Field2, Field3, Field4, Field5, Field10,
                A1, A2, A3, A4,
                Tag, TransGUID, SubmitBy, SubmitOn
            ) VALUES (
                @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode,
                @Field1, @Field2, @Field3, @Field4, @Field5, @Field10,
                @A1, @A2, @A3, @A4,
                @Tag, @TransGUID, @SubmitBy, @SubmitOn
            )";

                    using var command = new SqlCommand(sql, connection, transaction);
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@ENDTNum", sectionDeclarationNo);
                    command.Parameters.AddWithValue("@Section", "FIRE SECTION");
                    command.Parameters.AddWithValue("@ContentDesc", adjustment.AdjustmentName);
                    command.Parameters.AddWithValue("@SMICode", adjustmentType);
                    command.Parameters.AddWithValue("@Field1", adjustment.AdjustmentName);
                    command.Parameters.AddWithValue("@Field2", adjustmentType);
                    command.Parameters.AddWithValue("@Field3", adjustment.AppliedOn.ToString());
                    command.Parameters.AddWithValue("@Field4", adjustment.Formula ?? "");
                    command.Parameters.AddWithValue("@Field5", adjustment.SequenceOrder.ToString());
                    command.Parameters.AddWithValue("@Field10", certOrDocNo);
                    command.Parameters.AddWithValue("@A1", adjustment.Rate);
                    command.Parameters.AddWithValue("@A2", adjustment.Amount);
                    command.Parameters.AddWithValue("@A3", adjustment.BaseAmount);
                    command.Parameters.AddWithValue("@A4", adjustment.SequenceOrder);
                    command.Parameters.AddWithValue("@Tag", $"FIRE_SECTION_{adjustmentType}");
                    command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
                    command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
            /// <summary>
            /// Persists one risk item's dynamic adjustment breakdown as
            /// PolicyPremDetails rows — same pattern as the section-level
            /// SaveFireSectionDynamicAdjustmentsWithCertAsync, but linked to the
            /// parent ITEM through ENDTNum = the item's CERTNO.
            ///   Field10 = certOrDocNo         -> swept by DeleteCalculationDataByCertOrDocNoAsync,
            ///                                    and keeps these rows out of the item read
            ///                                    (which filters Field10 = sectionDeclarationNo)
            ///   Field11 = sectionDeclarationNo -> section-scoped delete + single-query read-back
            /// </summary>
            private async Task SaveFireRiskItemDynamicAdjustmentsAsync(
                string policyNo, string certOrDocNo, string sectionDeclarationNo,
                string itemCertNo, string sectionId,
                List<FireAdjustmentDto> adjustments, string adjustmentType,
                SqlConnection connection, SqlTransaction transaction)
            {
                if (adjustments == null || !adjustments.Any()) return;

                var sql = @"
            INSERT INTO PolicyPremDetails (
                PolicyNo, ENDTNum, Section, ContentDesc, SMICode,
                Field1, Field2, Field3, Field4, Field5, Field10, Field11,
                A1, A2, A3, A4,
                Tag, TransGUID, SubmitBy, SubmitOn
            ) VALUES (
                @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode,
                @Field1, @Field2, @Field3, @Field4, @Field5, @Field10, @Field11,
                @A1, @A2, @A3, @A4,
                @Tag, @TransGUID, @SubmitBy, @SubmitOn
            )";

                foreach (var adjustment in adjustments)
                {
                    using var command = new SqlCommand(sql, connection, transaction);
                    command.Parameters.AddWithValue("@PolicyNo", policyNo);
                    command.Parameters.AddWithValue("@ENDTNum", itemCertNo);          // parent item link
                    command.Parameters.AddWithValue("@Section", sectionId ?? "");
                    command.Parameters.AddWithValue("@ContentDesc", adjustment.AdjustmentName);
                    command.Parameters.AddWithValue("@SMICode", adjustmentType);
                    command.Parameters.AddWithValue("@Field1", adjustment.AdjustmentName);
                    command.Parameters.AddWithValue("@Field2", adjustmentType);
                    command.Parameters.AddWithValue("@Field3", adjustment.AppliedOn.ToString());
                    command.Parameters.AddWithValue("@Field4", adjustment.Formula ?? "");
                    command.Parameters.AddWithValue("@Field5", adjustment.SequenceOrder.ToString());
                    command.Parameters.AddWithValue("@Field10", certOrDocNo ?? "");
                    command.Parameters.AddWithValue("@Field11", sectionDeclarationNo ?? "");
                    command.Parameters.AddWithValue("@A1", adjustment.Rate);
                    command.Parameters.AddWithValue("@A2", adjustment.Amount);
                    command.Parameters.AddWithValue("@A3", adjustment.BaseAmount);
                    command.Parameters.AddWithValue("@A4", adjustment.SequenceOrder);
                    command.Parameters.AddWithValue("@Tag", $"FIRE_ITEM_{adjustmentType}");
                    command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
                    command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
            // ------------------------------------------------------------
            // PART 2: Incremental Section add/edit/remove
            // ------------------------------------------------------------

            /// <summary>
            /// Declaration-level context an incremental section add/edit doesn't
            /// resupply: CoverDays/ProportionRate/ExRate/ExCurrency and the
            /// existing proposal-level 2-rate box (OtherDiscountsRate/
            /// OtherLoadingsRate, read from PolicyDetails.A10/A11 — same slots
            /// SaveAdjustmentRatesToPolicyDetailsAsync writes to).
            /// </summary>
            private async Task<(int CoverDays, decimal ProportionRate, decimal ExRate, string ExCurrency,
                decimal OtherDiscountsRate, decimal OtherLoadingsRate)> GetFireDeclarationContextAsync(
                string proposalNo, SqlConnection connection, SqlTransaction transaction)
            {
                int coverDays = 365;
                decimal proportionRate = 100;
                decimal exRate = 1;
                string exCurrency = "NAIRA";
                decimal otherDiscountsRate = 0;
                decimal otherLoadingsRate = 0;

                var sql = @"
        SELECT ProRataDays, ProportionRate, ExRate, ExCurrency, A10, A11
        FROM PolicyDetails
        WHERE PolicyNo = @PolicyNo AND Deleted = 0";
                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var days = reader.GetValueOrDefault<int>("ProRataDays");
                    coverDays = days > 0 ? days : 365;
                    var rate = reader.GetValueOrDefault<decimal>("ProportionRate");
                    proportionRate = rate > 0 ? rate : 100;
                    var fx = reader.GetValueOrDefault<decimal>("ExRate");
                    exRate = fx > 0 ? fx : 1;
                    var currency = reader.GetStringOrDefault("ExCurrency");
                    exCurrency = string.IsNullOrEmpty(currency) ? "NAIRA" : currency;
                    otherDiscountsRate = reader.GetValueOrDefault<decimal>("A10");
                    otherLoadingsRate = reader.GetValueOrDefault<decimal>("A11");
                }

                return (coverDays, proportionRate, exRate, exCurrency, otherDiscountsRate, otherLoadingsRate);
            }

            /// <summary>
            /// Reads custom-named dynamic adjustments (beyond the 7 standard ones)
            /// for a section from PolicyPremDetails — the rows
            /// SaveFireSectionDynamicAdjustmentsWithCertAsync writes. Skips any
            /// name already present in alreadySurfaced (the standard-name entries
            /// the caller already built from the fixed A3-A9/A12-A16 columns), so
            /// nothing gets double-counted between the two sources.
            /// </summary>
            private async Task<List<AdjustmentDetailDto>> GetFireSectionCustomAdjustmentsAsync(
                string proposalNo, string sectionDeclarationNo, string adjustmentType,
                List<AdjustmentDetailDto> alreadySurfaced, SqlConnection connection)
            {
                var results = new List<AdjustmentDetailDto>();
                if (string.IsNullOrEmpty(sectionDeclarationNo)) return results;

                var sql = @"
        SELECT ContentDesc, A1, A2, A4, Field4
        FROM PolicyPremDetails
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @SectionDeclarationNo
          AND Tag = @Tag AND SMICode = @AdjustmentType
        ORDER BY A4";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@SectionDeclarationNo", sectionDeclarationNo);
                command.Parameters.AddWithValue("@Tag", $"FIRE_SECTION_{adjustmentType}");
                command.Parameters.AddWithValue("@AdjustmentType", adjustmentType);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetStringOrDefault("ContentDesc");
                    if (alreadySurfaced.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
                        continue; // already surfaced from the fixed columns — skip to avoid double-counting

                    results.Add(new AdjustmentDetailDto
                    {
                        Name = name,
                        Type = adjustmentType,
                        Rate = reader.GetValueOrDefault<decimal>("A1"),
                        Amount = reader.GetValueOrDefault<decimal>("A2"),
                        SequenceOrder = reader.GetValueOrDefault<int>("A4"),
                        Formula = reader.GetStringOrDefault("Field4")
                    });
                }

                return results;
            }

            /// <summary>
            /// Looks up the ENDTNum of the most recently saved section row matching
            /// this SectionID — used right after SaveSectionWithAdjustmentsToPolicyTempDetailsAsync
            /// to correctly link dynamic adjustment rows to the real section, since
            /// that method generates its own declaration number internally and
            /// doesn't return it.
            /// </summary>
            private async Task<string> GetMostRecentSectionEndtNumAsync(
                string proposalNo, string sectionID, SqlConnection connection, SqlTransaction transaction)
            {
                var sql = @"
        SELECT TOP 1 ENDTNum
        FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo AND SectionID = @SectionID AND Tag = 'SECTION'
        ORDER BY SubmitOn DESC";

                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@SectionID", sectionID);
                var result = await cmd.ExecuteScalarAsync();
                return result as string;
            }

            /// <summary>
            /// Add one Section (with its own risk-item grid) to the proposal's
            /// Fire declaration, persist it, and recompute+save the declaration's
            /// totals. Incremental — works against whatever's currently persisted,
            /// NOT a resubmission of the full section list. Do not mix with the
            /// existing bulk /calculate/fire/complete endpoint in the same editing
            /// session without resubmitting the complete section list there first.
            /// </summary>
            public async Task<FireDeclarationRecomputeResponseDto> AddFireSectionAsync(
                string proposalNo, FireSectionIncrementalInputDto section)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    var persistResult = await PersistOneFireSectionAsync(proposalNo, section, connection, transaction);
                    if (!persistResult.Success)
                    {
                        return new FireDeclarationRecomputeResponseDto
                        {
                            ProposalNo = proposalNo,
                            Success = false,
                            Message = persistResult.Message
                        };
                    }

                    var updated = await RecomputeAndSaveFireAggregateAsync(proposalNo, connection, transaction);

                    await transaction.CommitAsync();

                    updated.Message = $"Section {section.SectionID} added; declaration totals recalculated.";
                    return updated;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error adding fire section for {ProposalNo}", proposalNo);
                    return new FireDeclarationRecomputeResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            /// <summary>
            /// Edit an existing Section's fields/items/adjustments. Note this
            /// behaves slightly differently from Hull/Cargo/Motor's item edit:
            /// those preserve the same CertificateNo across an edit, hard-
            /// deleting and re-inserting under the SAME identifier. Fire's
            /// existing SaveSectionWithAdjustmentsToPolicyTempDetailsAsync always
            /// mints a FRESH declaration number internally (via
            /// NextDeclarationNoAsync) and has no way to be told to reuse an
            /// existing one — I chose not to duplicate/fork that method just to
            /// add this, so an edit here deletes the old section entirely (row +
            /// items + adjustments) and re-persists it as a brand new section row
            /// with a NEW declaration number. The response's Message reports the
            /// new number — if your frontend needs to track "this same section"
            /// across edits, it needs to pick up that new number, not assume the
            /// old sectionDeclarationNo still refers to anything.
            /// </summary>
            public async Task<FireDeclarationRecomputeResponseDto> UpdateFireSectionAsync(
                string proposalNo, string sectionDeclarationNo, FireSectionIncrementalInputDto section)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    var deleteSectionSql = @"
        DELETE FROM PolicyPremDetails
WHERE PolicyNo = @PolicyNo
  AND (ENDTNum = @SectionDeclarationNo      -- section-level adjustment rows
    OR Field10 = @SectionDeclarationNo      -- risk item rows
    OR Field11 = @SectionDeclarationNo)     -- item-level adjustment rows";
                    using (var cmd = new SqlCommand(deleteSectionSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                        cmd.Parameters.AddWithValue("@SectionDeclarationNo", sectionDeclarationNo);
                        var rows = await cmd.ExecuteNonQueryAsync();
                        if (rows == 0)
                        {
                            return new FireDeclarationRecomputeResponseDto
                            {
                                ProposalNo = proposalNo,
                                Success = false,
                                Message = $"Section {sectionDeclarationNo} not found for policy {proposalNo}"
                            };
                        }
                    }

                    var deleteItemsSql = @"
        DELETE FROM PolicyPremDetails
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @SectionDeclarationNo";
                    using (var cmd = new SqlCommand(deleteItemsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                        cmd.Parameters.AddWithValue("@SectionDeclarationNo", sectionDeclarationNo);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    var persistResult = await PersistOneFireSectionAsync(proposalNo, section, connection, transaction);
                    if (!persistResult.Success)
                    {
                        return new FireDeclarationRecomputeResponseDto
                        {
                            ProposalNo = proposalNo,
                            Success = false,
                            Message = persistResult.Message
                        };
                    }

                    var updated = await RecomputeAndSaveFireAggregateAsync(proposalNo, connection, transaction);

                    await transaction.CommitAsync();

                    updated.Message = $"Section {sectionDeclarationNo} updated (new section number: {persistResult.NewSectionDeclarationNo}); declaration totals recalculated.";
                    return updated;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating fire section {SectionDeclarationNo} for {ProposalNo}", sectionDeclarationNo, proposalNo);
                    return new FireDeclarationRecomputeResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            /// <summary>
            /// Core section-persist logic shared by AddFireSectionAsync and
            /// UpdateFireSectionAsync — calculates items, applies adjustments
            /// (dynamic or fixed depending on what's supplied), persists via the
            /// existing SaveSectionWithAdjustmentsToPolicyTempDetailsAsync, and
            /// persists the full dynamic breakdown if the dynamic path was used.
            /// Does NOT open/commit a transaction or recompute the declaration —
            /// callers do that themselves.
            /// </summary>
            private async Task<(bool Success, string Message, string NewSectionDeclarationNo)> PersistOneFireSectionAsync(
                string proposalNo, FireSectionIncrementalInputDto section,
                SqlConnection connection, SqlTransaction transaction)
            {
                var context = await GetFireDeclarationContextAsync(proposalNo, connection, transaction);

                var itemCalcResponse = await CalculateRiskItemsAsync(new RiskItemCalculationRequestDto
                {
                    RiskItems = section.RiskItems,
                    ProportionRate = context.ProportionRate
                });

                if (!itemCalcResponse.Success)
                {
                    return (false, $"Section {section.SectionID} item calculation failed: {itemCalcResponse.Message}", null);
                }

                var aggregateSumInsured = itemCalcResponse.TotalShareValue;
                var aggregatePremium = itemCalcResponse.TotalNetPremiumAfterDiscounts;

                SectionAdjustmentResponseDto sectionAdjustments;
                FireSectionDynamicAdjustmentResponseDto dynamicResult = null;
                if ((section.Discounts != null && section.Discounts.Any()) ||
                    (section.Loadings != null && section.Loadings.Any()))
                {
                    var dynamicResultLocal = await ApplySectionDynamicAdjustmentsAsync(new FireSectionDynamicAdjustmentRequestDto
                    {
                        AggregateSumInsured = aggregateSumInsured,
                        AggregatePremium = aggregatePremium,
                        Discounts = section.Discounts,
                        Loadings = section.Loadings
                    });
                    if (!dynamicResultLocal.Success)
                    {
                        return (false, $"Section {section.SectionID} dynamic adjustments failed: {dynamicResultLocal.Message}", null);
                    }
                    dynamicResult = dynamicResultLocal;
                    sectionAdjustments = MapDynamicToFixedSectionAdjustments(dynamicResult, section);
                }
                else
                {
                    // No dynamic adjustments supplied — use the existing fixed-rate path.
                    sectionAdjustments = await ApplySectionAdjustmentsAsync(new SectionAdjustmentRequestDto
                    {
                        AggregatePremium = aggregatePremium,
                        SpecialDiscountRate = section.SpecialDiscountRate,
                        DeductibleDiscountRate = section.DeductibleDiscountRate,
                        SpreadDiscountRate = section.SpreadDiscountRate,
                        LtaDiscountRate = section.LtaDiscountRate,
                        TheftLoadingRate = section.TheftLoadingRate,
                        SrccLoadingRate = section.SrccLoadingRate,
                        OtherLoading2Rate = section.OtherLoading2Rate
                    });
                    if (!sectionAdjustments.Success)
                    {
                        return (false, $"Section {section.SectionID} adjustments failed: {sectionAdjustments.Message}", null);
                    }
                }

                var sectionResult = new SectionCalculationResponseDto
                {
                    SectionID = section.SectionID,
                    SectionName = section.SectionName,
                    CalculatedItems = itemCalcResponse.CalculatedItems,
                    SectionSumInsured = aggregateSumInsured,
                    SectionGrossPremium = aggregatePremium,
                    SectionNetPremium = sectionAdjustments.NetPremiumDue,
                    SectionAdjustments = sectionAdjustments,
                    Success = true
                };

                // Reuse the existing persist path exactly as the bulk flow does —
                // it reads rates off `section` and amounts off `sectionResult.SectionAdjustments`,
                // both of which are now correctly populated whichever path computed them.
                var shimRequest = new CompleteFireCalculationRequestDto
                {
                    ProposalNo = proposalNo,
                    CoverDays = context.CoverDays,
                    ProportionRate = context.ProportionRate,
                    CalculatedBy = "SYSTEM"
                };

                // FireSectionIncrementalInputDto and FireSectionDto carry the same
                // rate/name/location fields the save path reads — construct the
                // real FireSectionDto it expects.
                var fireSectionDto = new FireSectionDto
                {
                    SectionID = section.SectionID,
                    SectionName = section.SectionName,
                    Location = section.Location,
                    RiskItems = section.RiskItems,
                    SpecialDiscountRate = section.SpecialDiscountRate,
                    DeductibleDiscountRate = section.DeductibleDiscountRate,
                    SpreadDiscountRate = section.SpreadDiscountRate,
                    LtaDiscountRate = section.LtaDiscountRate,
                    TheftLoadingRate = section.TheftLoadingRate,
                    SrccLoadingRate = section.SrccLoadingRate,
                    OtherLoading2Rate = section.OtherLoading2Rate,
                    Field26 = section.Field26,
                    Field7 = section.Field7,
                    Field8 = section.Field8
                };

                await SaveSectionWithAdjustmentsToPolicyTempDetailsAsync(
                    proposalNo, sectionResult, fireSectionDto, shimRequest, connection, transaction);

                // The bulk path's save method internally re-derives certOrDocNo/
                // branchId/sectionDeclarationNo and calls SaveSectionRiskItemsToPolicyPremDetailsAsync
                // itself — nothing further needed here for item persistence.

                // Persist the FULL dynamic breakdown (custom names beyond the 7
                // standard ones) — only meaningful if the dynamic path was used.
                // Look up the section's ACTUAL persisted ENDTNum (rather than
                // minting a second, unrelated declaration number) so these rows
                // correctly link to the real section row just saved above.
                string newSectionDeclarationNo = null;
                if (dynamicResult != null)
                {
                    var certOrDocNo = await GetOrGenerateCertOrDocNoAsync(proposalNo, connection, transaction);
                    newSectionDeclarationNo = await GetMostRecentSectionEndtNumAsync(
                        proposalNo, section.SectionID, connection, transaction);

                    if (!string.IsNullOrEmpty(newSectionDeclarationNo))
                    {
                        await SaveFireSectionDynamicAdjustmentsWithCertAsync(
                            proposalNo, certOrDocNo, newSectionDeclarationNo,
                            dynamicResult.DiscountsApplied, "DISCOUNT", connection, transaction);
                        await SaveFireSectionDynamicAdjustmentsWithCertAsync(
                            proposalNo, certOrDocNo, newSectionDeclarationNo,
                            dynamicResult.LoadingsApplied, "LOADING", connection, transaction);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not find the just-saved section row for {ProposalNo}/{SectionID} to link dynamic adjustments to — they were not persisted.",
                            proposalNo, section.SectionID);
                    }
                }
                else
                {
                    newSectionDeclarationNo = await GetMostRecentSectionEndtNumAsync(
                        proposalNo, section.SectionID, connection, transaction);
                }

                return (true, "Section persisted", newSectionDeclarationNo);
            }

            /// <summary>
            /// Remove one Section (hard delete — PolicyTempDetails/PolicyPremDetails
            /// have no Deleted column, same as every other module) and recompute+
            /// save the declaration's totals.
            /// </summary>
            public async Task<FireDeclarationRecomputeResponseDto> RemoveFireSectionAsync(
                string proposalNo, string sectionDeclarationNo)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    var deleteSectionSql = @"
        DELETE FROM PolicyPremDetails
WHERE PolicyNo = @PolicyNo
  AND (ENDTNum = @SectionDeclarationNo      -- section-level adjustment rows
    OR Field10 = @SectionDeclarationNo      -- risk item rows
    OR Field11 = @SectionDeclarationNo)     -- item-level adjustment rows";
                    using (var cmd = new SqlCommand(deleteSectionSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                        cmd.Parameters.AddWithValue("@SectionDeclarationNo", sectionDeclarationNo);
                        var rows = await cmd.ExecuteNonQueryAsync();
                        if (rows == 0)
                        {
                            return new FireDeclarationRecomputeResponseDto
                            {
                                ProposalNo = proposalNo,
                                Success = false,
                                Message = $"Section {sectionDeclarationNo} not found for policy {proposalNo}"
                            };
                        }
                    }

                    var deleteItemsSql = @"
        DELETE FROM PolicyPremDetails
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @SectionDeclarationNo";
                    using (var cmd = new SqlCommand(deleteItemsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                        cmd.Parameters.AddWithValue("@SectionDeclarationNo", sectionDeclarationNo);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    var updated = await RecomputeAndSaveFireAggregateAsync(proposalNo, connection, transaction);

                    await transaction.CommitAsync();

                    updated.Message = $"Section {sectionDeclarationNo} removed; declaration totals recalculated.";
                    return updated;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error removing fire section {SectionDeclarationNo} for {ProposalNo}", sectionDeclarationNo, proposalNo);
                    return new FireDeclarationRecomputeResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = ex.Message
                    };
                }
            }

            /// <summary>
            /// Shared by AddFireSectionAsync/RemoveFireSectionAsync: re-sums all
            /// currently-active sections for this proposal (RiskSum/SectionPremium
            /// on their PolicyTempDetails rows, Tag='SECTION'), re-applies the
            /// existing proposal-level 2-rate box (read fresh from the header, not
            /// touched by this feature per your earlier confirmation for Motor —
            /// same reasoning applies here), redoes pro-rata, and updates
            /// PolicyDetails/Policies via narrow methods that don't require a full
            /// CompleteFireCalculationRequestDto.
            /// </summary>
            private async Task<FireDeclarationRecomputeResponseDto> RecomputeAndSaveFireAggregateAsync(
                string proposalNo, SqlConnection connection, SqlTransaction transaction)
            {
                decimal totalSumInsured = 0;
                decimal totalGrossPremium = 0;

                var sectionSql = @"
        SELECT RiskSum, SectionPremium
        FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo AND Tag = 'SECTION'";
                using (var cmd = new SqlCommand(sectionSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        totalSumInsured += reader.GetValueOrDefault<decimal>("RiskSum");
                        totalGrossPremium += reader.GetValueOrDefault<decimal>("SectionPremium");
                    }
                }

                var context = await GetFireDeclarationContextAsync(proposalNo, connection, transaction);

                var proposalAdjustmentResponse = await ApplyProposalLevelAdjustmentsAsync(new ProposalAdjustmentRequestDto
                {
                    TotalAggregatePremium = totalGrossPremium,
                    OtherDiscountsRate = context.OtherDiscountsRate,
                    OtherLoadingsRate = context.OtherLoadingsRate
                });

                var totalNetPremium = proposalAdjustmentResponse.NetPremiumDue;

                var proRataResponse = await CalculateProRataAsync(new ProRataCalculationRequestDto
                {
                    NetPremiumDue = totalNetPremium,
                    CoverDays = context.CoverDays
                });

                await UpdatePolicyDetailsForFireRecomputeAsync(
                    proposalNo, totalSumInsured, totalGrossPremium, proRataResponse, context, connection, transaction);

                await UpdateProposalSummaryAsync(
                    proposalNo, totalSumInsured, totalNetPremium, proRataResponse, context.ExRate, connection, transaction);

                return new FireDeclarationRecomputeResponseDto
                {
                    ProposalNo = proposalNo,
                    TotalSumInsured = totalSumInsured,
                    TotalGrossPremium = totalGrossPremium,
                    TotalNetPremium = totalNetPremium,
                    ShareSumInsured = totalSumInsured * (context.ProportionRate / 100),
                    SharePremium = proRataResponse.PremiumDue * (context.ProportionRate / 100),
                    ForeignSumInsured = totalSumInsured * context.ExRate,
                    ForeignPremium = proRataResponse.PremiumDue * context.ExRate,
                    Success = true,
                    CalculatedOn = DateTime.Now
                };
            }

            /// <summary>
            /// Narrower PolicyDetails update used ONLY by the incremental recompute
            /// path — deliberately NOT a reuse of the existing
            /// UpdatePolicyDetailsWithCalculationAsync, since that method requires
            /// a full CompleteFireCalculationRequestDto and resets fields
            /// (Field1/Field2/Field50) built specifically for the bulk flow. Same
            /// reasoning as UpdatePolicyDetailsForMotorRecomputeAsync.
            /// </summary>
            private async Task UpdatePolicyDetailsForFireRecomputeAsync(
                string proposalNo, decimal totalSumInsured, decimal totalGrossPremium,
                ProRataCalculationResponseDto proRataResponse,
                (int CoverDays, decimal ProportionRate, decimal ExRate, string ExCurrency, decimal OtherDiscountsRate, decimal OtherLoadingsRate) context,
                SqlConnection connection, SqlTransaction transaction)
            {
                var sql = @"
        UPDATE PolicyDetails SET
            SumInsured = @SumInsured,
            GrossPremium = @GrossPremium,
            SumInsuredFrgn = @SumInsuredFrgn,
            GrossPremiumFrgn = @GrossPremiumFrgn,
            ProRataDays = @ProRataDays,
            ProRataPremium = @ProRataPremium,
            NetAmount = @NetAmount,
            TotalRiskValue = @TotalRiskValue,
            ModifiedOn = @ModifiedOn
        WHERE PolicyNo = @PolicyNo";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@SumInsured", ToStoredAmount(totalSumInsured * (context.ProportionRate / 100), context.ExRate));
                command.Parameters.AddWithValue("@GrossPremium", ToStoredAmount(totalGrossPremium, context.ExRate));
                command.Parameters.AddWithValue("@SumInsuredFrgn", totalSumInsured * context.ExRate);
                command.Parameters.AddWithValue("@GrossPremiumFrgn", proRataResponse.PremiumDue * context.ExRate);
                command.Parameters.AddWithValue("@ProRataDays", context.CoverDays);
                command.Parameters.AddWithValue("@ProRataPremium", ToStoredAmount(proRataResponse.ProRataPremium, context.ExRate));
                command.Parameters.AddWithValue("@NetAmount", ToStoredAmount(proRataResponse.PremiumDue * (context.ProportionRate / 100), context.ExRate));
                command.Parameters.AddWithValue("@TotalRiskValue", ToStoredAmount(totalSumInsured, context.ExRate));
                command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);

                await command.ExecuteNonQueryAsync();
            }

        }



    } 