using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    public partial class ProposalService
    {

        /// <summary>
        /// List the sections of a source calculation that are available to renew
        /// (Field1='SECTION' and not yet expired).
        /// </summary>
        public async Task<List<RenewableSectionDto>> GetRenewableSectionsAsync(string proposalNo, string detailID)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Resolve source CertOrDocNo from the header
            string sourceCert = null;
            var certSql = @"SELECT CertOrDocNo FROM PolicyDetails
                    WHERE PolicyNo = @PolicyNo AND DetailID = @DetailID AND Deleted = 0";
            using (var cmd = new SqlCommand(certSql, connection))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@DetailID", detailID);
                sourceCert = await cmd.ExecuteScalarAsync() as string;
            }

            var result = new List<RenewableSectionDto>();
            if (string.IsNullOrEmpty(sourceCert)) return result;

            var sql = @"
        SELECT SectionID, ContentSection, Location, A1, A11, A17, TransGUID
        FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo
        AND Field20 = @Cert
        AND ISNULL(Tag, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'EXPIRED')  -- expired = already renewed
        AND ISNULL(Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS')
        -- LEGACY: legacy-app sections carry a cert number in Field1 and Tag = 'NEW',
        -- so we EXCLUDE non-section rows instead of requiring the 'SECTION' marker.
        ORDER BY SubmitOn, SectionID";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@Cert", sourceCert);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new RenewableSectionDto
                {
                    SelectionToken = reader.GetStringOrDefault("TransGUID"),
                    SectionID = reader.GetStringOrDefault("SectionID"),
                    SectionName = reader.GetStringOrDefault("ContentSection"),
                    Location = reader.GetStringOrDefault("Location"),
                    SectionSumInsured = reader.GetValueOrDefault<decimal>("A1"),
                    SectionNetPremium = reader.GetValueOrDefault<decimal>("A11"),
                    RiskItemCount = (int)reader.GetValueOrDefault<decimal>("A17")
                });
            }

            return result;
        }

        /// <summary>
        /// Renew selected sections of a source calculation into a NEW calculation (same policy).
        /// New header: BizOption='RENEWAL', Field50='UNCOMPLETED', Active=1, Field3=sourceCert.
        /// Renewed sections + their risk items are re-keyed under a new CertOrDocNo (sections Tag='RENEWAL').
        /// Source header is expired (BizOption='EXPIRED', Active=0) and the renewed source sections get Tag='EXPIRED'.
        /// </summary>
        public async Task<RenewCalculationResponseDto> CreateRenewalFromCalculationAsync(
            string proposalNo, string sourceDetailID, RenewCalculationSectionsRequestDto request)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Source header
                var sourceHeader = await GetPolicyDetailByDetailIDAsync(proposalNo, sourceDetailID, connection, transaction);
                if (sourceHeader == null)
                {
                    return new RenewCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = "Source calculation not found",
                        ValidationErrors = { $"DetailID {sourceDetailID} not found for policy {proposalNo}" }
                    };
                }
                var sourceCert = sourceHeader.CertOrDocNo;

                // 2. Source sections (active only)
                var sourceSections = new List<(string TransGUID, decimal SumInsured, decimal NetPremium, string DeclarationNo)>();
                var secSql = @"
            SELECT TransGUID, A1, A11, ENDTNum
            FROM PolicyTempDetails
            WHERE PolicyNo = @PolicyNo AND Field20 = @Cert
            AND ISNULL(Tag, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'EXPIRED')
            AND ISNULL(Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS')";
                using (var cmd = new SqlCommand(secSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    cmd.Parameters.AddWithValue("@Cert", sourceCert);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        sourceSections.Add((
                            r.GetStringOrDefault("TransGUID"),
                            r.GetValueOrDefault<decimal>("A1"),
                            r.GetValueOrDefault<decimal>("A11"),
                            r.GetStringOrDefault("ENDTNum")));  // LEGACY: section declaration number
                    }
                }

                if (!sourceSections.Any())
                {
                    return new RenewCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = "No renewable sections found on the source calculation",
                        ValidationErrors = { $"No active sections under CertOrDocNo {sourceCert}" }
                    };
                }

                // 3. Resolve selection (empty = all)
                var selected = (request.SelectedSectionTokens != null && request.SelectedSectionTokens.Any())
                    ? sourceSections.Where(s => request.SelectedSectionTokens.Contains(s.TransGUID)).ToList()
                    : sourceSections;

                if (!selected.Any())
                {
                    return new RenewCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = "None of the selected sections exist on the source calculation",
                        ValidationErrors = { "SelectedSectionTokens did not match any source section" }
                    };
                }

                // 4. New cert for the renewal calculation
                // LEGACY: DECLARATION_NO auto number (branch + subrisk + endorse type)
                var proposal = await GetProposalAsync(proposalNo);
                var newCert = await NextCalcCertAsync(proposal.BranchID, proposal.SubRiskID, MapEndorseType("Renewal"));

                var now = DateTime.Now;
                decimal totalSumInsured = 0;
                decimal totalGrossPremium = 0;  // sum of section net premiums (after section-level adj)

                // 5. Re-key each selected section + its risk items
                foreach (var sec in selected)
                {
                    var newSectionTransGUID = Guid.NewGuid().ToString();

                    // LEGACY: every renewed section gets a fresh declaration number
                    // (PROPOSAL auto number) and every copied risk item gets a fresh
                    // CERTNO — the same re-keying the legacy renewal loop performs.
                    var newDeclarationNo = await NextDeclarationNoAsync(proposal.BranchID);

                    await ReKeyRenewalSectionAsync(
                        proposalNo, sourceCert, newCert, sec.TransGUID, newSectionTransGUID,
                        sec.DeclarationNo, newDeclarationNo, proposal.BranchID,
                        request.RenewedBy, now, connection, transaction);

                    totalSumInsured += sec.SumInsured;
                    totalGrossPremium += sec.NetPremium;
                }

                // 6. Proposal-level adjustments — recomputed for the selected subset, using source rates
                var proposalAdjResponse = await ApplyProposalLevelAdjustmentsAsync(new ProposalAdjustmentRequestDto
                {
                    TotalAggregatePremium = totalGrossPremium,
                    OtherDiscountsRate = sourceHeader.A10,   // header A10 = OtherDiscountsRate
                    OtherLoadingsRate = sourceHeader.A11     // header A11 = OtherLoadingsRate
                });

                await SaveRenewalProposalAdjustmentsAsync(
                    proposalNo, newCert, proposalAdjResponse, request.RenewedBy, now, connection, transaction);

                // 7. Pro-rata using source cover days
                var coverDays = sourceHeader.ProRataDays > 0 ? sourceHeader.ProRataDays : 365;
                var proRata = await CalculateProRataAsync(new ProRataCalculationRequestDto
                {
                    NetPremiumDue = proposalAdjResponse.NetPremiumDue,
                    CoverDays = coverDays
                });

                // 8. New header (RENEWAL / UNCOMPLETED)
                await CreateRenewalCalcHeaderAsync(
                    proposalNo, newCert, sourceCert, sourceHeader,
                    totalSumInsured, totalGrossPremium, proRata, coverDays,
                    request, now, connection, transaction);

                // 9. Expire source: header + the renewed source sections
                await ExpireSourceCalculationAsync(
                    sourceDetailID, selected.Select(s => s.TransGUID).ToList(),
                    request.RenewedBy, now, connection, transaction);

                await transaction.CommitAsync();

                return new RenewCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    SourceDetailID = sourceDetailID,
                    SourceCertOrDocNo = sourceCert,
                    NewCertOrDocNo = newCert,
                    RenewedSectionCount = selected.Count,
                    TotalSumInsured = totalSumInsured,
                    TotalNetPremium = proposalAdjResponse.NetPremiumDue,
                    ProRataPremium = proRata.ProRataPremium,
                    FinalPremiumDue = proRata.PremiumDue,
                    Success = true,
                    Message = $"Renewed {selected.Count} section(s) into new calculation {newCert}. Source {sourceCert} expired.",
                    NextStep = "Edit the renewed calculation, then update/recalculate to finalize (Field50 -> COMPLETED)."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error renewing calculation {DetailID} for {ProposalNo}", sourceDetailID, proposalNo);
                return new RenewCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    SourceDetailID = sourceDetailID,
                    Success = false,
                    Message = "Renewal failed",
                    ValidationErrors = { ex.Message }
                };
            }
        }

        /// <summary>
        /// Copy one source section row + its risk items into the new cert.
        /// Section: Field1 stays 'SECTION', Tag -> 'RENEWAL', new TransGUID + Field20.
        /// Risk items: re-pointed to the section's new TransGUID (Field1) and new cert (Field10).
        /// </summary>
        private async Task ReKeyRenewalSectionAsync(
            string proposalNo, string sourceCert, string newCert,
            string sourceSectionTransGUID, string newSectionTransGUID,
            string sourceDeclarationNo, string newDeclarationNo, string branchId,
            string renewedBy, DateTime now, SqlConnection connection, SqlTransaction transaction)
        {
            // Copy the section row verbatim, overriding key columns.
            // LEGACY: ENDTNum -> fresh declaration number (PROPOSAL auto number),
            //         Field20 -> new calc cert (section -> calculation link).
            // No Field1 filter: TransGUID is unique and legacy-app sections carry a
            // cert number in Field1 rather than the 'SECTION' marker.
            var sectionSql = @"
INSERT INTO PolicyTempDetails (
    PolicyNo, ENDTNum, SectionID, ContentSection, Location,
    Field1, Field2, Field3, Field4, Field5, Field20, Field11, Field12,
    Field26, Field7, Field8,
    RiskSum, OtherSum, SectionPremium, TStartDate, TEndDate,
    A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16, A17, A18, A19, A20,
    Tag, TransGUID, SubmitBy, SubmitOn
)
SELECT
    PolicyNo, @NewDeclarationNo, SectionID, ContentSection, Location,
    Field1, Field2, Field3, Field4, Field5, @NewCert, Field11, Field12,
    Field26, Field7, Field8,
    RiskSum, OtherSum, SectionPremium, TStartDate, TEndDate,
    A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16, A17, A18, A19, A20,
    'RENEWAL', @NewTransGUID, @SubmitBy, @SubmitOn
FROM PolicyTempDetails
WHERE PolicyNo = @PolicyNo AND TransGUID = @SourceTransGUID";

            using (var cmd = new SqlCommand(sectionSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@SourceTransGUID", sourceSectionTransGUID);
                cmd.Parameters.AddWithValue("@NewDeclarationNo", newDeclarationNo);
                cmd.Parameters.AddWithValue("@NewCert", newCert);
                cmd.Parameters.AddWithValue("@NewTransGUID", newSectionTransGUID);
                cmd.Parameters.AddWithValue("@SubmitBy", renewedBy);
                cmd.Parameters.AddWithValue("@SubmitOn", now);
                await cmd.ExecuteNonQueryAsync();
            }

            // ---------------------------------------------------------------
            // Find the source risk items. Two linkage generations exist:
            //   - LEGACY linkage (legacy app AND the current save path):
            //       item.Field10 = source section's declaration number
            //   - pre-legacy API rows (section ENDTNum was empty):
            //       item.Field1 = source section TransGUID AND item.Field10 = source cert
            // ---------------------------------------------------------------
            var findSql = string.IsNullOrWhiteSpace(sourceDeclarationNo)
                ? @"SELECT TransGUID FROM PolicyPremDetails
                    WHERE PolicyNo = @PolicyNo
                    AND Field1 = @SourceSectionTransGUID AND Field10 = @SourceCert"
                : @"SELECT TransGUID FROM PolicyPremDetails
                    WHERE PolicyNo = @PolicyNo
                    AND (Field10 = @SourceDeclarationNo
                         OR (Field1 = @SourceSectionTransGUID AND Field10 = @SourceCert))";

            var sourceItemGuids = new List<string>();
            using (var cmd = new SqlCommand(findSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@SourceSectionTransGUID", sourceSectionTransGUID);
                cmd.Parameters.AddWithValue("@SourceCert", sourceCert);
                cmd.Parameters.AddWithValue("@SourceDeclarationNo", sourceDeclarationNo ?? "");
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    sourceItemGuids.Add(r.GetStringOrDefault("TransGUID"));
            }

            // Copy each item individually so EVERY renewed item gets a fresh CERTNO —
            // exactly like the legacy loop that resets txtCertNo to [AUTO] per row.
            // LEGACY overrides: ENDTNum -> new CERTNO, Field1 -> SectionID (also
            // normalizes pre-legacy rows), Field2 -> new calc cert,
            // Field10 -> new section declaration number.
            var copyItemSql = @"
INSERT INTO PolicyPremDetails (
    PolicyNo, ENDTNum, Section, ContentDesc, SMICode, SMIDetail, Location,
    Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9, Field10,
    A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16,
    Tag, TransGUID, SubmitBy, SubmitOn
)
SELECT
    PolicyNo, @NewItemCertNo, Section, ContentDesc, SMICode, SMIDetail, Location,
    Section, @NewCert, Field3, Field4, Field5, Field6, Field7, Field8, Field9, @NewDeclarationNo,
    A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16,
    Tag, LOWER(CONVERT(varchar(36), NEWID())), @SubmitBy, @SubmitOn
FROM PolicyPremDetails
WHERE PolicyNo = @PolicyNo AND TransGUID = @RowGuid";

            foreach (var rowGuid in sourceItemGuids)
            {
                // LEGACY: fresh CERTNO auto number per risk item
                var newItemCertNo = await NextItemCertNoAsync(branchId);

                using var cmd = new SqlCommand(copyItemSql, connection, transaction);
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@RowGuid", rowGuid);
                cmd.Parameters.AddWithValue("@NewItemCertNo", newItemCertNo);
                cmd.Parameters.AddWithValue("@NewCert", newCert);
                cmd.Parameters.AddWithValue("@NewDeclarationNo", newDeclarationNo);
                cmd.Parameters.AddWithValue("@SubmitBy", renewedBy);
                cmd.Parameters.AddWithValue("@SubmitOn", now);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>Fresh proposal-level adjustments row for the renewed subset.</summary>
        private async Task SaveRenewalProposalAdjustmentsAsync(
            string proposalNo, string newCert, ProposalLevelAdjustmentResponseDto adjustments,
            string renewedBy, DateTime now, SqlConnection connection, SqlTransaction transaction)
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
            command.Parameters.AddWithValue("@Field20", newCert);
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
            command.Parameters.AddWithValue("@SubmitBy", renewedBy);
            command.Parameters.AddWithValue("@SubmitOn", now);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>New PolicyDetails header for the renewal calc. Field3 = source cert for traceability.</summary>
        private async Task CreateRenewalCalcHeaderAsync(
            string proposalNo, string newCert, string sourceCert, PolicyDetailDto source,
            decimal totalSumInsured, decimal totalPremium, ProRataCalculationResponseDto proRata,
            int coverDays, RenewCalculationSectionsRequestDto request, DateTime now,
            SqlConnection connection, SqlTransaction transaction)
        {
            var fullName = await GetInsuredFullNameAsync(null, connection, transaction, proposalNo);

            var sql = @"
INSERT INTO PolicyDetails (
    PolicyNo, CertOrDocNo, EntryDate, BizOption, InsuredName,
    StartDate, EndDate, ExRate, ExCurrency, ProportionRate,
    SumInsured, GrossPremium, SumInsuredFrgn, GrossPremiumFrgn,
    ProRataDays, ProRataPremium, NetAmount, TotalRiskValue,
    A1, A2, A10, A11, A13, A14,
    Field1, Field2, Field3, Field50,
    SubmittedBy, SubmittedOn, Active, Deleted
) VALUES (
    @PolicyNo, @CertOrDocNo, @EntryDate, @BizOption, @InsuredName,
    @StartDate, @EndDate, @ExRate, @ExCurrency, @ProportionRate,
    @SumInsured, @GrossPremium, @SumInsuredFrgn, @GrossPremiumFrgn,
    @ProRataDays, @ProRataPremium, @NetAmount, @TotalRiskValue,
    @A1, @A2, @A10, @A11, @A13, @A14,
    @Field1, @Field2, @Field3, @Field50,
    @SubmittedBy, @SubmittedOn, 1, 0
)";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@CertOrDocNo", newCert);
            command.Parameters.AddWithValue("@EntryDate", now);
            command.Parameters.AddWithValue("@BizOption", "RENEWAL");
            command.Parameters.AddWithValue("@InsuredName", fullName);
            command.Parameters.AddWithValue("@StartDate", source.StartDate);
            command.Parameters.AddWithValue("@EndDate", source.EndDate);
            command.Parameters.AddWithValue("@ExRate", source.ExRate);
            command.Parameters.AddWithValue("@ExCurrency", source.ExCurrency ?? "NAIRA");
            command.Parameters.AddWithValue("@ProportionRate", source.ProportionRate);
            command.Parameters.AddWithValue("@SumInsured", totalSumInsured * (source.ProportionRate / 100));
            command.Parameters.AddWithValue("@GrossPremium", proRata.PremiumDue);
            command.Parameters.AddWithValue("@SumInsuredFrgn", totalSumInsured * source.ExRate);
            command.Parameters.AddWithValue("@GrossPremiumFrgn", proRata.PremiumDue * source.ExRate);
            command.Parameters.AddWithValue("@ProRataDays", coverDays);
            command.Parameters.AddWithValue("@ProRataPremium", proRata.ProRataPremium);
            command.Parameters.AddWithValue("@NetAmount", proRata.PremiumDue);
            command.Parameters.AddWithValue("@TotalRiskValue", totalSumInsured);
            command.Parameters.AddWithValue("@A1", totalSumInsured);
            command.Parameters.AddWithValue("@A2", totalPremium);
            command.Parameters.AddWithValue("@A10", source.A10);   // carry proposal discount rate
            command.Parameters.AddWithValue("@A11", source.A11);   // carry proposal loading rate
            command.Parameters.AddWithValue("@A13", proRata.ProRataPremium);
            command.Parameters.AddWithValue("@A14", proRata.PremiumDue);
            command.Parameters.AddWithValue("@Field1", string.IsNullOrEmpty(request.Remarks)
                ? $"RENEWAL of {sourceCert}" : request.Remarks);
            command.Parameters.AddWithValue("@Field2", $"Renewed on {now:yyyy-MM-dd HH:mm:ss}");
            command.Parameters.AddWithValue("@Field3", sourceCert);     // traceability: source cert
            command.Parameters.AddWithValue("@Field50", "UNCOMPLETED"); // status
            command.Parameters.AddWithValue("@SubmittedBy", request.RenewedBy);
            command.Parameters.AddWithValue("@SubmittedOn", now);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>Expire the source header and the renewed source sections.</summary>
        private async Task ExpireSourceCalculationAsync(
            string sourceDetailID, List<string> renewedSectionTransGUIDs,
            string modifiedBy, DateTime now, SqlConnection connection, SqlTransaction transaction)
        {
            var headerSql = @"
        UPDATE PolicyDetails
        SET BizOption = 'EXPIRED', ModifiedBy = @ModifiedBy, ModifiedOn = @ModifiedOn
        WHERE DetailID = @DetailID";
            using (var cmd = new SqlCommand(headerSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@DetailID", sourceDetailID);
                cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
                cmd.Parameters.AddWithValue("@ModifiedOn", now);
                await cmd.ExecuteNonQueryAsync();
            }

            var sectionSql = @"UPDATE PolicyTempDetails SET Tag = 'EXPIRED' WHERE TransGUID = @TransGUID";
            foreach (var guid in renewedSectionTransGUIDs)
            {
                using var cmd = new SqlCommand(sectionSql, connection, transaction);
                cmd.Parameters.AddWithValue("@TransGUID", guid);
                await cmd.ExecuteNonQueryAsync();
            }
        }


        /// <summary>
        /// List vehicles of a source motor calculation that can be renewed
        /// (SectionID='MOTOR' and currently active — Tag='MOTOR_VEHICLE').
        /// </summary>
        public async Task<List<RenewableVehicleDto>> GetRenewableVehiclesAsync(string proposalNo, string detailID)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string sourceCert = null;
            var certSql = @"SELECT CertOrDocNo FROM PolicyDetails
                    WHERE PolicyNo = @PolicyNo AND DetailID = @DetailID AND Deleted = 0";
            using (var cmd = new SqlCommand(certSql, connection))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@DetailID", detailID);
                sourceCert = await cmd.ExecuteScalarAsync() as string;
            }

            var result = new List<RenewableVehicleDto>();
            if (string.IsNullOrEmpty(sourceCert)) return result;

            var sql = @"
        SELECT ENDTNum, ContentSection, Field1, Field2, A1, SectionPremium
        FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo
        AND (Field20 = @Cert OR SectionID = @Cert)
        AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'
        AND ISNULL(Tag, '') <> 'EXPIRED'   -- already-renewed source vehicles are not renewable again
        -- LEGACY/mobile: mobile rows carry SectionID = cover type and Tag = 'NEW',
        -- legacy-page rows carry SectionID = declaration no. Requiring
        -- SectionID='MOTOR' AND Tag='MOTOR_VEHICLE' made those unrenewable — and
        -- also blocked renewing a renewal (its vehicles carry Tag='RENEWAL').
        ORDER BY ENDTNum";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@Cert", sourceCert);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var certNo = reader.GetStringOrDefault("ENDTNum");
                result.Add(new RenewableVehicleDto
                {
                    SelectionToken = certNo,
                    CertificateNo = certNo,
                    VehicleRegNo = reader.GetStringOrDefault("Field1"),
                    VehicleType = reader.GetStringOrDefault("Field2"),
                    VehicleUser = reader.GetStringOrDefault("ContentSection"),
                    VehicleValue = reader.GetValueOrDefault<decimal>("A1"),
                    NetPremium = reader.GetValueOrDefault<decimal>("SectionPremium")
                });
            }

            return result;
        }

        /// <summary>
        /// Renew selected vehicles of a source motor calculation into a NEW calculation (same policy).
        /// New header: BizOption='RENEWAL', Field50='UNCOMPLETED', Active=1, Field3=sourceCert.
        /// Renewed vehicles + their adjustments are re-keyed under a new CertOrDocNo with fresh
        /// CertificateNos (vehicles Tag='RENEWAL'). Source header is expired (BizOption='EXPIRED',
        /// Active=0) and the renewed source vehicles get Tag='EXPIRED'.
        /// </summary>
        public async Task<RenewMotorCalculationResponseDto> CreateMotorRenewalFromCalculationAsync(
            string proposalNo, string sourceDetailID, RenewMotorCalculationRequestDto request)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Source header
                var source = await GetPolicyDetailByDetailIDAsync(proposalNo, sourceDetailID, connection, transaction);
                if (source == null)
                {
                    return new RenewMotorCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = "Source calculation not found",
                        ValidationErrors = { $"DetailID {sourceDetailID} not found for policy {proposalNo}" }
                    };
                }
                var sourceCert = source.CertOrDocNo;

                // 2. Source vehicles (active only)
                var sourceVehicles = new List<(string CertNo, decimal Value, decimal NetPremium)>();
                var vSql = @"
            SELECT ENDTNum, A1, SectionPremium
            FROM PolicyTempDetails
            WHERE PolicyNo = @PolicyNo
            AND (Field20 = @Cert OR SectionID = @Cert)
            AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'
            AND ISNULL(Tag, '') <> 'EXPIRED'";
                using (var cmd = new SqlCommand(vSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    cmd.Parameters.AddWithValue("@Cert", sourceCert);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        sourceVehicles.Add((
                            r.GetStringOrDefault("ENDTNum"),
                            r.GetValueOrDefault<decimal>("A1"),
                            r.GetValueOrDefault<decimal>("SectionPremium")));
                    }
                }

                if (!sourceVehicles.Any())
                {
                    return new RenewMotorCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = "No renewable vehicles found on the source calculation",
                        ValidationErrors = { $"No active vehicles under CertOrDocNo {sourceCert}" }
                    };
                }

                // 3. Resolve selection (empty = all)
                var selected = (request.SelectedVehicleTokens != null && request.SelectedVehicleTokens.Any())
                    ? sourceVehicles.Where(v => request.SelectedVehicleTokens.Contains(v.CertNo)).ToList()
                    : sourceVehicles;

                if (!selected.Any())
                {
                    return new RenewMotorCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = "None of the selected vehicles exist on the source calculation",
                        ValidationErrors = { "SelectedVehicleTokens did not match any source vehicle" }
                    };
                }

                // 4. New cert for the renewal calculation
                // LEGACY: DECLARATION_NO auto number (branch + subrisk + endorse type)
                var proposal = await GetProposalAsync(proposalNo);
                var newCert = await NextCalcCertAsync(proposal.BranchID, proposal.SubRiskID, MapEndorseType("Renewal"));

                var now = DateTime.Now;
                decimal totalVehicleValue = 0;
                decimal totalNetPremium = 0;

                // 5. Re-key each selected vehicle + its adjustments under a fresh CertificateNo
                foreach (var veh in selected)
                {
                    // LEGACY: every renewed vehicle gets a fresh CERTNO auto number,
                    // exactly like the legacy motor module issues one certificate per
                    // vehicle — no more fabricated '{PolicyNo}/CERT/000001' strings.
                    var newCertNo = await NextItemCertNoAsync(proposal.BranchID);
                    await ReKeyRenewalVehicleAsync(
                        proposalNo, sourceCert, newCert, veh.CertNo, newCertNo,
                        request.RenewedBy, now, connection, transaction);

                    totalVehicleValue += veh.Value;
                    totalNetPremium += veh.NetPremium;
                }

                // 6. Proposal-level adjustments — recomputed for the selected subset, using source rates
                var proposalAdj = await ApplyMotorProposalAdjustmentsAsync(new MotorStep6RequestDto
                {
                    AggregatePremium = totalNetPremium,
                    OtherDiscountRate = source.A10,   // header A10 = OtherDiscountRate
                    OtherLoadingRate = source.A11     // header A11 = OtherLoadingRate
                });

                await SaveMotorProposalAdjustmentsWithCertAsync(
                    proposalNo, newCert, proposalAdj, connection, transaction);

                // 7. Pro-rata / proportion / fx using source parameters
                var coverDays = source.ProRataDays > 0 ? source.ProRataDays : 365;
                var final = await CalculateMotorFinalAsync(new MotorStep7RequestDto
                {
                    NetPremium = proposalAdj.NetPremiumDue,
                    TotalVehicleValue = totalVehicleValue,
                    CoverDays = coverDays,
                    ProportionRate = source.ProportionRate,
                    ExchangeRate = source.ExRate,
                    Currency = source.ExCurrency ?? "NAIRA"
                });

                // 8. New header (RENEWAL / UNCOMPLETED)
                await CreateMotorRenewalHeaderAsync(
                    proposalNo, newCert, sourceCert, source,
                    totalVehicleValue, totalNetPremium, coverDays, proposalAdj, final,
                    request, now, connection, transaction);

                // 9. Expire source: header + the renewed source vehicles
                await ExpireSourceMotorCalculationAsync(
                    sourceDetailID, sourceCert, selected.Select(v => v.CertNo).ToList(),
                    proposalNo, request.RenewedBy, now, connection, transaction);

                await transaction.CommitAsync();

                return new RenewMotorCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    SourceDetailID = sourceDetailID,
                    SourceCertOrDocNo = sourceCert,
                    NewCertOrDocNo = newCert,
                    RenewedVehicleCount = selected.Count,
                    TotalVehicleValue = totalVehicleValue,
                    TotalNetPremium = proposalAdj.NetPremiumDue,
                    ProRataPremium = final.ProRataPremium,
                    FinalPremiumDue = final.FinalPremiumDue,
                    Success = true,
                    Message = $"Renewed {selected.Count} vehicle(s) into new calculation {newCert}. Source {sourceCert} expired.",
                    NextStep = "Edit the renewed calculation, then update/recalculate to finalize (Field50 -> COMPLETED)."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error renewing motor calculation {DetailID} for {ProposalNo}", sourceDetailID, proposalNo);
                return new RenewMotorCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    SourceDetailID = sourceDetailID,
                    Success = false,
                    Message = "Renewal failed",
                    ValidationErrors = { ex.Message }
                };
            }
        }

        /// <summary>
        /// Copy one source vehicle row + its discount/loading rows into the new cert under a fresh CertificateNo.
        /// Vehicle: SectionID stays 'MOTOR', Tag -> 'RENEWAL', new ENDTNum + Field20 + TransGUID.
        /// Adjustments: re-pointed to the new CertificateNo (ENDTNum) and new cert (Field10).
        /// </summary>
        private async Task ReKeyRenewalVehicleAsync(
            string proposalNo, string sourceCert, string newCert,
            string sourceCertNo, string newCertNo,
            string renewedBy, DateTime now, SqlConnection connection, SqlTransaction transaction)
        {
            var vehicleSql = @"
INSERT INTO PolicyTempDetails (
    PolicyNo, ENDTNum, SectionID, ContentSection, Field20,
    Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9,
    Field17, Field18, Field23, Field24,
    RiskSum, SectionPremium, OtherSum,
    A1, A2, A3, A12, A13, A17, A18,
    Tag, TransGUID, SubmitBy, SubmitOn
)
SELECT
    PolicyNo, @NewCertNo,
    CASE WHEN SectionID = @SrcCert THEN 'MOTOR' ELSE SectionID END,  -- don't carry the OLD cert into the copy
    ContentSection, @NewCert,
    Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9,
    Field17, Field18, Field23, Field24,
    RiskSum, SectionPremium, OtherSum,
    A1, A2, A3, A12, A13, A17, A18,
    'RENEWAL', @NewTransGUID, @SubmitBy, @SubmitOn
FROM PolicyTempDetails
WHERE PolicyNo = @PolicyNo AND ENDTNum = @SrcCertNo
AND (Field20 = @SrcCert OR SectionID = @SrcCert)
AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'";

            using (var cmd = new SqlCommand(vehicleSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@SrcCertNo", sourceCertNo);
                cmd.Parameters.AddWithValue("@SrcCert", sourceCert);
                cmd.Parameters.AddWithValue("@NewCertNo", newCertNo);
                cmd.Parameters.AddWithValue("@NewCert", newCert);
                cmd.Parameters.AddWithValue("@NewTransGUID", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@SubmitBy", renewedBy);
                cmd.Parameters.AddWithValue("@SubmitOn", now);
                await cmd.ExecuteNonQueryAsync();
            }

            // Discounts + loadings keep their Tag; re-point ENDTNum -> newCertNo and Field10 -> newCert.
            var adjSql = @"
INSERT INTO PolicyPremDetails (
    PolicyNo, ENDTNum, Section, ContentDesc, SMICode,
    Field1, Field2, Field3, Field4, Field5, Field10,
    A1, A2, A3, A4,
    Tag, TransGUID, SubmitBy, SubmitOn
)
SELECT
    PolicyNo, @NewCertNo, Section, ContentDesc, SMICode,
    Field1, Field2, Field3, Field4, Field5, @NewCert,
    A1, A2, A3, A4,
    Tag, LOWER(CONVERT(varchar(36), NEWID())), @SubmitBy, @SubmitOn
FROM PolicyPremDetails
WHERE PolicyNo = @PolicyNo AND Field10 = @SrcCert AND ENDTNum = @SrcCertNo
AND Tag IN ('MOTOR_DISCOUNT', 'MOTOR_LOADING')";

            using (var cmd = new SqlCommand(adjSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@SrcCert", sourceCert);
                cmd.Parameters.AddWithValue("@SrcCertNo", sourceCertNo);
                cmd.Parameters.AddWithValue("@NewCertNo", newCertNo);
                cmd.Parameters.AddWithValue("@NewCert", newCert);
                cmd.Parameters.AddWithValue("@SubmitBy", renewedBy);
                cmd.Parameters.AddWithValue("@SubmitOn", now);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>New PolicyDetails header for the renewal calc. Field3 = source cert for traceability.</summary>
        private async Task CreateMotorRenewalHeaderAsync(
            string proposalNo, string newCert, string sourceCert, PolicyDetailDto source,
            decimal totalVehicleValue, decimal totalNetPremium, int coverDays,
            MotorProposalAdjustmentsResponseDto proposalAdj, MotorFinalCalculationResponseDto final,
            RenewMotorCalculationRequestDto request, DateTime now,
            SqlConnection connection, SqlTransaction transaction)
        {
            var fullName = await GetInsuredFullNameAsync(null, connection, transaction, proposalNo);

            var sql = @"
INSERT INTO PolicyDetails (
    PolicyNo, CertOrDocNo, EntryDate, BizOption, InsuredName,
    StartDate, EndDate, ExRate, ExCurrency, ProportionRate,
    SumInsured, GrossPremium, SumInsuredFrgn, GrossPremiumFrgn,
    ProRataDays, ProRataPremium, NetAmount, TotalRiskValue,
    A1, A2, A10, A11, A13, A14,
    Field1, Field2, Field3, Field50,
    SubmittedBy, SubmittedOn, Active, Deleted
) VALUES (
    @PolicyNo, @CertOrDocNo, @EntryDate, @BizOption, @InsuredName,
    @StartDate, @EndDate, @ExRate, @ExCurrency, @ProportionRate,
    @SumInsured, @GrossPremium, @SumInsuredFrgn, @GrossPremiumFrgn,
    @ProRataDays, @ProRataPremium, @NetAmount, @TotalRiskValue,
    @A1, @A2, @A10, @A11, @A13, @A14,
    @Field1, @Field2, @Field3, @Field50,
    @SubmittedBy, @SubmittedOn, 1, 0
)";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@CertOrDocNo", newCert);
            command.Parameters.AddWithValue("@EntryDate", now);
            command.Parameters.AddWithValue("@BizOption", "RENEWAL");
            command.Parameters.AddWithValue("@InsuredName", fullName);
            command.Parameters.AddWithValue("@StartDate", source.StartDate);
            command.Parameters.AddWithValue("@EndDate", source.EndDate);
            command.Parameters.AddWithValue("@ExRate", source.ExRate);
            command.Parameters.AddWithValue("@ExCurrency", source.ExCurrency ?? "NAIRA");
            command.Parameters.AddWithValue("@ProportionRate", source.ProportionRate);
            command.Parameters.AddWithValue("@SumInsured", final.ShareSumInsured);
            command.Parameters.AddWithValue("@GrossPremium", totalNetPremium);
            command.Parameters.AddWithValue("@SumInsuredFrgn", final.ForeignSumInsured);
            command.Parameters.AddWithValue("@GrossPremiumFrgn", final.ForeignPremium);
            command.Parameters.AddWithValue("@ProRataDays", coverDays);
            command.Parameters.AddWithValue("@ProRataPremium", final.ProRataPremium);
            command.Parameters.AddWithValue("@NetAmount", final.ProRataPremium);
            command.Parameters.AddWithValue("@TotalRiskValue", totalVehicleValue);
            command.Parameters.AddWithValue("@A1", totalVehicleValue);
            command.Parameters.AddWithValue("@A2", totalNetPremium);
            command.Parameters.AddWithValue("@A10", source.A10);   // carry OtherDiscountRate
            command.Parameters.AddWithValue("@A11", source.A11);   // carry OtherLoadingRate
            command.Parameters.AddWithValue("@A13", final.ProRataPremium);
            command.Parameters.AddWithValue("@A14", final.FinalPremiumDue);
            command.Parameters.AddWithValue("@Field1", string.IsNullOrEmpty(request.Remarks)
                ? $"RENEWAL of {sourceCert}" : request.Remarks);
            command.Parameters.AddWithValue("@Field2", $"Renewed on {now:yyyy-MM-dd HH:mm:ss}");
            command.Parameters.AddWithValue("@Field3", sourceCert);     // traceability: source cert
            command.Parameters.AddWithValue("@Field50", "UNCOMPLETED"); // status
            command.Parameters.AddWithValue("@SubmittedBy", request.RenewedBy);
            command.Parameters.AddWithValue("@SubmittedOn", now);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>Expire the source header and the renewed source vehicles.</summary>
        private async Task ExpireSourceMotorCalculationAsync(
            string sourceDetailID, string sourceCert, List<string> renewedCertNos,
            string proposalNo, string modifiedBy, DateTime now,
            SqlConnection connection, SqlTransaction transaction)
        {
            var headerSql = @"
        UPDATE PolicyDetails
        SET BizOption = 'EXPIRED', Active = 0, ModifiedBy = @ModifiedBy, ModifiedOn = @ModifiedOn
        WHERE DetailID = @DetailID";
            using (var cmd = new SqlCommand(headerSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@DetailID", sourceDetailID);
                cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
                cmd.Parameters.AddWithValue("@ModifiedOn", now);
                await cmd.ExecuteNonQueryAsync();
            }

            var vehSql = @"
        UPDATE PolicyTempDetails SET Tag = 'EXPIRED'
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertNo
        AND (Field20 = @SrcCert OR SectionID = @SrcCert)";
            foreach (var certNo in renewedCertNos)
            {
                using var cmd = new SqlCommand(vehSql, connection, transaction);
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@SrcCert", sourceCert);
                cmd.Parameters.AddWithValue("@CertNo", certNo);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
