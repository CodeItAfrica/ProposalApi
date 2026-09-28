using GibsLifesMicroWebApi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    // -------------------------------------------------------------------------------------------
    // Motor endorsements. Same six types as fire, against one existing record (a PolicyDetails row
    // = one motor calculation). The unit is a VEHICLE, where fire had a section:
    //
    //   ADDITIONAL  record and vehicles untouched; new vehicles priced and pro-rated from the
    //               effective date to the policy's expiry, tagged ADDITIONAL.
    //   REVERSAL    record EXPIRED; its vehicles keep their tags but are stamped with the
    //               reversal's number; every vehicle copied with the original's figures, tagged
    //               REVERSAL; description "Cancellation Endorsement". DELETE / DELETED are the same.
    //   RETURN      record untouched; ticked vehicles EXPIRED and stamped with the RETURN's number;
    //               ticked vehicles copied, tagged RETURN; description "Deletion of Risk or Cover".
    //   REDO        record and ticked vehicles EXPIRED; ticked vehicles copied at their original
    //               figures (no repricing), tagged NEW — the screen calls a REDO "NEW".
    //   EXTENSION   record and vehicles untouched; all vehicles copied at full premium, tagged
    //               EXTENSION; the new record runs to the new end date.
    //   REDUCTION   record and vehicles untouched; all vehicles copied at full figures, tagged REDUCTION.
    //
    // Every new record is saved UNCOMPLETED (Field50). The Policies header is not touched.
    //
    //   record            = PolicyDetails      BizOption = type or EXPIRED, Field4 = description
    //   vehicle           = PolicyTempDetails  Field20 = the record's CertOrDocNo,
    //                                          ENDTNum = the vehicle's certificate number
    //   vehicle adjustment= PolicyPremDetails  ENDTNum = parent vehicle's cert no, Field10 = record cert,
    //                                          Tag = MOTOR_DISCOUNT / MOTOR_LOADING
    //   proposal adjustment=PolicyPremDetails  ENDTNum = 'PROPOSAL', Field10 = record cert
    //
    // WHERE MOTOR DIFFERS FROM FIRE, and why the code looks different in these three places:
    //
    // 1. The vehicle read matches on (Field20 = cert OR SectionID = cert) because SectionID is
    //    overloaded across writers — the literal "MOTOR", a cover type, or the cert. Loading uses the
    //    module's own predicate so an endorsement sees exactly the vehicles the screens see.
    // 2. That read filters neither Tag nor Deleted, so Tag = 'EXPIRED' alone does NOT take a vehicle
    //    off cover. PATCH-motor-vehicle-reads.md adds the Tag guard; without it a RETURN marks the
    //    vehicle but every screen still shows it as insured.
    // 3. Motor writes BizOption = "Additional" for an ordinary additional CALCULATION, which collides
    //    with the ADDITIONAL endorsement code. Nothing here treats a record as an endorsement on
    //    BizOption alone — EndorsementNo must also be set. See IsMotorEndorsementRecord.
    //
    // Tags go on the vehicle row only, never on MOTOR_DISCOUNT / MOTOR_LOADING /
    // MOTOR_PROPOSAL_ADJUSTMENT rows, whose Tag the breakdown reads filter on.
    //
    // The shared pieces from the fire work are reused rather than duplicated: EndorsementTypes,
    // FireEndorsementRows (schema-driven row copy, identifier quoting, column-width checks),
    // FireEndorsementRuleException, AcquireFireEndorsementLockAsync, NextFireEndorsementNumberAsync
    // and SectionStampColumn.
    // -------------------------------------------------------------------------------------------
    public partial class ProposalService
    {
        private const string MotorVehicleTag = "MOTOR_VEHICLE";
        private const string MotorSectionId = "MOTOR";
        private const string MotorProposalAdjustmentTag = "MOTOR_PROPOSAL_ADJUSTMENT";

        private static readonly string[] MotorVehicleAdjustmentTags = { "MOTOR_DISCOUNT", "MOTOR_LOADING" };

        /// <summary>Rows on a vehicle's table that are not vehicles.</summary>
        private const string MotorNotAVehiclePredicate = "ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'";

        // Record columns a motor REVERSAL carries over unchanged from the record it reverses.
        private static readonly string[] MotorReversalMirroredColumns =
        {
            "ExRate", "ExCurrency", "ProportionRate",
            "SumInsured", "GrossPremium", "SumInsuredFrgn", "GrossPremiumFrgn",
            "ProRataDays", "ProRataPremium", "NetAmount", "TotalRiskValue",
            "A1", "A2", "A10", "A11", "A13", "A14"
        };

        /// <summary>
        /// True only when the record is genuinely an endorsement. BizOption is not enough on its own:
        /// the ordinary additional-calculation path writes BizOption = "Additional", which matches the
        /// ADDITIONAL endorsement code.
        /// </summary>
        private static bool IsMotorEndorsementRecord(string bizOption, string endorsementNo) =>
            !string.IsNullOrWhiteSpace(endorsementNo) && EndorsementTypes.IsEndorsement(bizOption);

        #region Motor endorsements — public API

        /// <summary>Creates a motor endorsement of any type against one record of the policy.</summary>
        public async Task<MotorEndorsementResponseDto> CreateMotorEndorsementAsync(MotorEndorsementRequestDto request)
        {
            var response = new MotorEndorsementResponseDto
            {
                PolicyNo = request?.PolicyNo,
                SourceDetailID = request?.SourceDetailID,
                Status = EndorsementPendingStatus
            };

            if (request == null)
                return FailMotorEndorsement(response, "Request body is required");

            if (!EndorsementTypes.TryNormalize(request.EndorsementType, out var type))
                return FailMotorEndorsement(response,
                    $"Unknown endorsement type '{request.EndorsementType}'. Use one of " +
                    $"{string.Join(", ", EndorsementTypes.GetAll())} (DELETE/DELETED = REVERSAL, NEW = REDO).");

            response.EndorsementType = type;

            var errors = ValidateMotorEndorsementRequest(type, request, response.Warnings);
            if (errors.Count > 0)
                return FailMotorEndorsement(response, "Invalid endorsement request", errors);

            var policy = await GetProposalAsync(request.PolicyNo);
            if (policy == null)
                return FailMotorEndorsement(response, $"Policy {request.PolicyNo} not found");
            if (string.IsNullOrWhiteSpace(policy.BranchID))
                return FailMotorEndorsement(response,
                    $"Policy {request.PolicyNo} has no BranchID — vehicle certificate numbers are issued per branch.");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                await AcquireFireEndorsementLockAsync(request.PolicyNo, connection, transaction);

                var source = await LoadFireEndorsementSourceAsync(request.PolicyNo, request.SourceDetailID, connection, transaction)
                    ?? throw new FireEndorsementRuleException(
                        $"Record {request.SourceDetailID} not found on policy {request.PolicyNo}.");

                if (source.IsExpired)
                    throw new FireEndorsementRuleException(
                        $"Record {source.DetailID} is already EXPIRED. Endorse the record that replaced it instead.");

                response.SourceCertOrDocNo = source.CertOrDocNo;

                var ctx = new MotorEndorsementContext
                {
                    Type = type,
                    RowTag = EndorsementTypes.RowTagFor(type),
                    Request = request,
                    Policy = policy,
                    Source = source,
                    CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? "SYSTEM" : request.CreatedBy.Trim(),
                    Now = DateTime.Now,
                    Connection = connection,
                    Transaction = transaction
                };

                switch (type)
                {
                    case EndorsementTypes.Additional:
                        await EndorseMotorAdditionalAsync(ctx, response);
                        break;
                    case EndorsementTypes.Reversal:
                        await EndorseMotorReversalAsync(ctx, response);
                        break;
                    case EndorsementTypes.Return:
                        await EndorseMotorReturnAsync(ctx, response);
                        break;
                    case EndorsementTypes.Redo:
                        await EndorseMotorRedoAsync(ctx, response);
                        break;
                    case EndorsementTypes.Extension:
                    case EndorsementTypes.Reduction:
                        await EndorseMotorWithFullCopyAsync(ctx, response);
                        break;
                }

                await transaction.CommitAsync();

                response.EndorsementNo = ctx.EndorsementNo;
                response.CertOrDocNo = ctx.CertOrDocNo;
                response.Success = true;
                response.Message = $"{type} endorsement {ctx.EndorsementNo} saved as {EndorsementPendingStatus}.";
                return response;
            }
            catch (FireEndorsementRuleException rule)
            {
                RollbackFireEndorsement(transaction);
                return FailMotorEndorsement(response, rule.Message);
            }
            catch (Exception ex)
            {
                RollbackFireEndorsement(transaction);
                _logger.LogError(ex, "Error creating {Type} motor endorsement on {PolicyNo}, record {DetailID}",
                    type, request.PolicyNo, request.SourceDetailID);
                return FailMotorEndorsement(response, $"Failed to create {type} endorsement", new List<string> { ex.Message });
            }
        }

        /// <summary>
        /// Vehicles of one record, for the RETURN / REDO tick list. Null when the record doesn't exist.
        /// </summary>
        public async Task<List<EndorsableMotorVehicleDto>> GetEndorsableMotorVehiclesAsync(string policyNo, string detailId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string certOrDocNo;
            using (var command = new SqlCommand(
                "SELECT CertOrDocNo FROM PolicyDetails WHERE PolicyNo = @PolicyNo AND DetailID = @DetailID AND Deleted = 0",
                connection))
            {
                command.Parameters.AddWithValue("@PolicyNo", policyNo ?? string.Empty);
                command.Parameters.AddWithValue("@DetailID", detailId ?? string.Empty);
                certOrDocNo = FireEndorsementRows.Text(await command.ExecuteScalarAsync());
            }

            if (string.IsNullOrWhiteSpace(certOrDocNo)) return null;

            var vehicles = await LoadMotorEndorsementVehiclesAsync(policyNo, certOrDocNo, false, connection, null);

            // Adjustment rows per vehicle certificate number.
            var adjustmentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var command = new SqlCommand($@"
SELECT ENDTNum, COUNT(*) AS Adjustments
FROM PolicyPremDetails
WHERE PolicyNo = @PolicyNo AND Field10 = @CertOrDocNo
  AND Tag IN ('{string.Join("', '", MotorVehicleAdjustmentTags)}')
GROUP BY ENDTNum", connection))
            {
                command.Parameters.AddWithValue("@PolicyNo", policyNo);
                command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var certificateNo = FireEndorsementRows.Text(reader.GetValue(0));
                    if (!string.IsNullOrEmpty(certificateNo))
                        adjustmentCounts[certificateNo] = (int)FireEndorsementRows.Number(reader.GetValue(1));
                }
            }

            return vehicles.Select(vehicle => new EndorsableMotorVehicleDto
            {
                SelectionToken = vehicle.SelectionToken,
                CertificateNo = vehicle.CertificateNo,
                VehicleRegNo = vehicle.RegNo,
                VehicleMake = FireEndorsementRows.Text(vehicle.Values, "Field23"),
                VehicleModel = FireEndorsementRows.Text(vehicle.Values, "Field24"),
                VehicleType = FireEndorsementRows.Text(vehicle.Values, "Field2"),
                CoverType = FireEndorsementRows.Text(vehicle.Values, "Field17"),
                Usage = FireEndorsementRows.Text(vehicle.Values, "Field9"),
                VehicleUser = FireEndorsementRows.Text(vehicle.Values, "ContentSection"),
                Tag = vehicle.Tag,
                IsExpired = vehicle.IsExpired,
                StampedBy = vehicle.Stamp,
                VehicleValue = FireEndorsementRows.Number(vehicle.Values, "A1"),
                SumInsured = vehicle.SumInsured,
                NetPremium = vehicle.NetPremium,
                AdjustmentCount = !string.IsNullOrEmpty(vehicle.CertificateNo) &&
                                  adjustmentCounts.TryGetValue(vehicle.CertificateNo, out var count) ? count : 0
            }).ToList();
        }

        /// <summary>Endorsement records on a motor policy, newest first.</summary>
        public async Task<List<MotorEndorsementListItemDto>> GetMotorEndorsementsAsync(string policyNo)
        {
            const string sql = @"
SELECT DetailID, EndorsementNo, CertOrDocNo, BizOption, Field4, Field50, Field1,
       EntryDate, StartDate, EndDate, TotalRiskValue, NetAmount, SubmittedBy
FROM PolicyDetails
WHERE PolicyNo = @PolicyNo AND Deleted = 0 AND ISNULL(EndorsementNo, '') <> ''
ORDER BY SubmittedOn DESC";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PolicyNo", policyNo ?? string.Empty);

            var endorsements = new List<MotorEndorsementListItemDto>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                endorsements.Add(new MotorEndorsementListItemDto
                {
                    DetailID = FireEndorsementRows.Text(row, "DetailID"),
                    EndorsementNo = FireEndorsementRows.Text(row, "EndorsementNo"),
                    CertOrDocNo = FireEndorsementRows.Text(row, "CertOrDocNo"),
                    EndorsementType = FireEndorsementRows.Text(row, "BizOption"),
                    Description = FireEndorsementRows.Text(row, "Field4"),
                    Status = FireEndorsementRows.Text(row, "Field50"),
                    Remarks = FireEndorsementRows.Text(row, "Field1"),
                    EntryDate = FireEndorsementRows.Date(row, "EntryDate"),
                    StartDate = FireEndorsementRows.Date(row, "StartDate"),
                    EndDate = FireEndorsementRows.Date(row, "EndDate"),
                    TotalVehicleValue = FireEndorsementRows.Number(row, "TotalRiskValue"),
                    PremiumDue = FireEndorsementRows.Number(row, "NetAmount"),
                    CreatedBy = FireEndorsementRows.Text(row, "SubmittedBy")
                });
            }

            return endorsements;
        }

        #endregion

        #region Motor endorsements — one handler per type

        /// <summary>
        /// ADDITIONAL: record and vehicles untouched. The new vehicles are priced like any motor
        /// calculation and pro-rated from the effective date to the policy's expiry.
        /// </summary>
        private async Task EndorseMotorAdditionalAsync(MotorEndorsementContext ctx, MotorEndorsementResponseDto response)
        {
            var request = ctx.Request;

            var expiry = (ctx.Policy.EndDate ?? ctx.Source.EndDate)?.Date
                ?? throw new FireEndorsementRuleException("The policy has no expiry date to pro-rate the new vehicles to.");

            var effective = (request.EffectiveDate ?? ctx.Now).Date;
            if (ctx.Policy.StartDate.HasValue && effective < ctx.Policy.StartDate.Value.Date)
                throw new FireEndorsementRuleException(
                    $"Effective date {effective:yyyy-MM-dd} is before the policy starts ({ctx.Policy.StartDate.Value:yyyy-MM-dd}).");

            var coverDays = (expiry - effective).Days;
            if (coverDays <= 0)
                throw new FireEndorsementRuleException(
                    $"Effective date {effective:yyyy-MM-dd} must be before the policy's expiry ({expiry:yyyy-MM-dd}).");

            var calculationRequest = new MotorCalculationRequestDto
            {
                ProposalNo = ctx.PolicyNo,
                Vehicles = request.Vehicles,
                CoverDays = coverDays,
                ProportionRate = FireEndorsementRows.PositiveOr(request.ProportionRate, ctx.Source.ProportionRate, 100m),
                ExchangeRate = FireEndorsementRows.PositiveOr(request.ExchangeRate, ctx.Source.ExRate, 1m),
                Currency = FireEndorsementRows.FirstText(request.Currency, ctx.Source.ExCurrency, "NAIRA"),
                OtherDiscountRate = request.OtherDiscountRate ?? ctx.Source.OtherDiscountsRate,
                OtherLoadingRate = request.OtherLoadingRate ?? ctx.Source.OtherLoadingsRate,
                CalculatedBy = ctx.CreatedBy
            };

            // Price everything before issuing a number, so a bad vehicle fails cleanly.
            var calculation = await CalculateMotorCompleteAsync(calculationRequest, ctx.Connection, ctx.Transaction);
            if (calculation == null || !calculation.Success)
                throw new FireEndorsementRuleException($"Motor calculation failed: {calculation?.Message ?? "no result"}");

            await IssueMotorEndorsementNumbersAsync(ctx);

            var branchId = FireEndorsementRows.FirstText(ctx.Policy.BranchID);
            foreach (var vehicle in calculation.CalculatedVehicles ?? new List<CalculatedMotorRiskItemDto>())
            {
                // LEGACY (PersistMotorCalculationWithCertAsync): the real CERTNO is issued at save
                // time so previews never burn numbers, but a manually entered cert is kept.
                if (string.IsNullOrWhiteSpace(vehicle.CertificateNo) || vehicle.CertificateNo.Contains("/CERT/"))
                    vehicle.CertificateNo = await NextItemCertNoAsync(branchId);
                response.NewCertificateNos.Add(vehicle.CertificateNo);

                // rowTag is the added parameter from PATCH-motor-vehicle-reads.md: the endorsement's
                // vehicles carry its tag, not MOTOR_VEHICLE.
                await SaveMotorVehicleWithCertAsync(ctx.PolicyNo, ctx.CertOrDocNo, vehicle,
                    ctx.Connection, ctx.Transaction, ctx.RowTag);

                // Same source as PersistMotorCalculationWithCertAsync: the applied adjustments live on
                // the step results, not on the vehicle itself.
                if (vehicle.Step2_Discounts?.DiscountsApplied?.Count > 0)
                    await SaveMotorVehicleAdjustmentsWithCertAsync(ctx.PolicyNo, ctx.CertOrDocNo, vehicle.CertificateNo,
                        vehicle.Step2_Discounts.DiscountsApplied, "DISCOUNT", ctx.Connection, ctx.Transaction);
                if (vehicle.Step3_Loadings?.LoadingsApplied?.Count > 0)
                    await SaveMotorVehicleAdjustmentsWithCertAsync(ctx.PolicyNo, ctx.CertOrDocNo, vehicle.CertificateNo,
                        vehicle.Step3_Loadings.LoadingsApplied, "LOADING", ctx.Connection, ctx.Transaction);
            }

            if (calculation.ProposalAdjustments != null)
                await SaveMotorProposalAdjustmentsWithCertAsync(ctx.PolicyNo, ctx.CertOrDocNo,
                    calculation.ProposalAdjustments, ctx.Connection, ctx.Transaction);

            var figures = MotorRecordFigures(
                calculation.TotalVehicleValue, calculation.ShareSumInsured, calculation.TotalGrossPremium,
                calculation.ForeignSumInsured, calculation.ForeignPremium,
                calculation.ProRataPremium, calculation.FinalPremiumDue, coverDays,
                calculationRequest.ProportionRate, calculationRequest.ExchangeRate, calculationRequest.Currency,
                calculationRequest.OtherDiscountRate, calculationRequest.OtherLoadingRate);

            var description = FireEndorsementRows.NullIfBlank(request.Description);
            response.NewDetailID = await InsertMotorEndorsementRecordAsync(ctx, effective, expiry, description, figures);
            response.Description = description;
            response.StartDate = effective;
            response.EndDate = expiry;
            response.CoverDays = coverDays;
            response.VehicleCount = response.NewCertificateNos.Count;
            response.TotalVehicleValue = calculation.TotalVehicleValue;
            response.TotalSumInsured = calculation.ShareSumInsured;
            response.TotalGrossPremium = calculation.TotalGrossPremium;
            response.ProRataPremium = calculation.ProRataPremium;
            response.PremiumDue = calculation.FinalPremiumDue;
        }

        /// <summary>
        /// REVERSAL: record EXPIRED; its vehicles keep their tags but carry the reversal's number;
        /// every vehicle copied, tagged REVERSAL, with the original's figures.
        /// </summary>
        private async Task EndorseMotorReversalAsync(MotorEndorsementContext ctx, MotorEndorsementResponseDto response)
        {
            var vehicles = await LoadMotorEndorsementVehiclesAsync(ctx.PolicyNo, ctx.Source.CertOrDocNo, true, ctx.Connection, ctx.Transaction);
            var onCover = vehicles.Where(v => !v.IsExpired).ToList();
            var alreadyOff = vehicles.Where(v => v.IsExpired).ToList();

            if (vehicles.Count > 0 && onCover.Count == 0)
                throw new FireEndorsementRuleException(
                    "Every vehicle on this record was already taken off cover by an earlier endorsement — there is nothing left to reverse.");

            await IssueMotorEndorsementNumbersAsync(ctx);

            await ExpireFireEndorsementSourceAsync(ctx.AsFireContext());
            foreach (var vehicle in onCover)
            {
                await MarkOriginalMotorVehicleAsync(ctx, vehicle, expire: false, stamp: true);
                response.StampedVehicleTokens.Add(vehicle.SelectionToken);
            }

            var copies = new List<CopiedMotorVehicle>();
            foreach (var vehicle in onCover)
            {
                copies.Add(await CopyMotorVehicleAsync(ctx, vehicle, null));
            }
            response.NewCertificateNos.AddRange(copies.Select(c => c.CertificateNo));

            Dictionary<string, object> figures;
            if (alreadyOff.Count == 0)
            {
                var copiedPremium = await CopyMotorProposalAdjustmentRowsAsync(ctx);
                figures = MotorReversalMirroredColumns.ToDictionary(c => c, c => ctx.Source.Value(c), StringComparer.OrdinalIgnoreCase);

                response.TotalVehicleValue = ctx.Source.Number("TotalRiskValue");
                response.TotalSumInsured = ctx.Source.Number("SumInsured");
                response.TotalGrossPremium = copiedPremium ?? ctx.Source.Number("A2");
                response.ProRataPremium = ctx.Source.Number("ProRataPremium");
                response.PremiumDue = ctx.Source.Number("NetAmount");
                response.CoverDays = ctx.Source.ProRataDays;
            }
            else
            {
                foreach (var vehicle in alreadyOff)
                {
                    response.Warnings.Add(
                        $"Vehicle {vehicle.Label} was already taken off cover by {DescribeMotorStamp(vehicle)} and was not reversed again.");
                }

                figures = await PriceCopiedMotorVehiclesAsync(ctx, copies, ctx.Source.PricingDays, response);
            }

            response.NewDetailID = await InsertMotorEndorsementRecordAsync(
                ctx, ctx.Source.StartDate, ctx.Source.EndDate, ReversalEndorsementDescription, figures);
            response.Description = ReversalEndorsementDescription;
            response.SourceRecordExpired = true;
            response.StartDate = ctx.Source.StartDate;
            response.EndDate = ctx.Source.EndDate;
            response.VehicleCount = copies.Count;
        }

        /// <summary>
        /// RETURN: record untouched; ticked vehicles EXPIRED and stamped with the RETURN's number;
        /// ticked vehicles copied, tagged RETURN.
        /// </summary>
        private async Task EndorseMotorReturnAsync(MotorEndorsementContext ctx, MotorEndorsementResponseDto response)
        {
            var vehicles = await LoadMotorEndorsementVehiclesAsync(ctx.PolicyNo, ctx.Source.CertOrDocNo, true, ctx.Connection, ctx.Transaction);
            var ticked = ResolveTickedMotorVehicles(vehicles, ctx.Request.SelectedVehicleTokens);

            await IssueMotorEndorsementNumbersAsync(ctx);

            foreach (var vehicle in ticked)
            {
                await MarkOriginalMotorVehicleAsync(ctx, vehicle, expire: true, stamp: true);
                response.ExpiredVehicleTokens.Add(vehicle.SelectionToken);
                response.StampedVehicleTokens.Add(vehicle.SelectionToken);
            }

            var copies = new List<CopiedMotorVehicle>();
            foreach (var vehicle in ticked)
            {
                copies.Add(await CopyMotorVehicleAsync(ctx, vehicle, null));
            }
            response.NewCertificateNos.AddRange(copies.Select(c => c.CertificateNo));

            var figures = await PriceCopiedMotorVehiclesAsync(ctx, copies, ctx.Source.PricingDays, response);

            response.NewDetailID = await InsertMotorEndorsementRecordAsync(
                ctx, ctx.Source.StartDate, ctx.Source.EndDate, ReturnEndorsementDescription, figures);
            response.Description = ReturnEndorsementDescription;
            response.StartDate = ctx.Source.StartDate;
            response.EndDate = ctx.Source.EndDate;
            response.VehicleCount = copies.Count;
        }

        /// <summary>
        /// REDO (screen: NEW): record and ticked vehicles EXPIRED; ticked vehicles copied, tagged NEW,
        /// with their original figures. Nothing is repriced.
        /// </summary>
        private async Task EndorseMotorRedoAsync(MotorEndorsementContext ctx, MotorEndorsementResponseDto response)
        {
            var vehicles = await LoadMotorEndorsementVehiclesAsync(ctx.PolicyNo, ctx.Source.CertOrDocNo, true, ctx.Connection, ctx.Transaction);
            var ticked = ResolveTickedMotorVehicles(vehicles, ctx.Request.SelectedVehicleTokens);

            await IssueMotorEndorsementNumbersAsync(ctx);

            await ExpireFireEndorsementSourceAsync(ctx.AsFireContext());
            foreach (var vehicle in ticked)
            {
                await MarkOriginalMotorVehicleAsync(ctx, vehicle, expire: true, stamp: false);
                response.ExpiredVehicleTokens.Add(vehicle.SelectionToken);
            }

            var copies = new List<CopiedMotorVehicle>();
            foreach (var vehicle in ticked)
            {
                copies.Add(await CopyMotorVehicleAsync(ctx, vehicle, null));
            }
            response.NewCertificateNos.AddRange(copies.Select(c => c.CertificateNo));

            var figures = await PriceCopiedMotorVehiclesAsync(ctx, copies, ctx.Source.PricingDays, response);

            var description = FireEndorsementRows.NullIfBlank(ctx.Request.Description);
            response.NewDetailID = await InsertMotorEndorsementRecordAsync(
                ctx, ctx.Source.StartDate, ctx.Source.EndDate, description, figures);
            response.Description = description;
            response.SourceRecordExpired = true;
            response.StartDate = ctx.Source.StartDate;
            response.EndDate = ctx.Source.EndDate;
            response.VehicleCount = copies.Count;
        }

        /// <summary>
        /// EXTENSION and REDUCTION: record and vehicles untouched; every vehicle on cover copied as it
        /// stands at full premium (no pro-rata). An EXTENSION's record runs to the new end date.
        /// </summary>
        private async Task EndorseMotorWithFullCopyAsync(MotorEndorsementContext ctx, MotorEndorsementResponseDto response)
        {
            var isExtension = ctx.Type == EndorsementTypes.Extension;
            DateTime? newEndDate = null;

            if (isExtension)
            {
                newEndDate = ctx.Request.NewEndDate.Value.Date;
                var currentEnd = (ctx.Source.EndDate ?? ctx.Policy.EndDate)?.Date;
                if (currentEnd.HasValue && newEndDate.Value <= currentEnd.Value)
                    throw new FireEndorsementRuleException(
                        $"New end date {newEndDate.Value:yyyy-MM-dd} must be after the record's current end date {currentEnd.Value:yyyy-MM-dd}.");
            }

            var vehicles = await LoadMotorEndorsementVehiclesAsync(ctx.PolicyNo, ctx.Source.CertOrDocNo, false, ctx.Connection, ctx.Transaction);
            var onCover = vehicles.Where(v => !v.IsExpired).ToList();
            if (onCover.Count == 0)
                throw new FireEndorsementRuleException("This record has no vehicles on cover to copy.");

            foreach (var vehicle in vehicles.Where(v => v.IsExpired))
            {
                response.Warnings.Add(
                    $"Vehicle {vehicle.Label} is already off cover ({DescribeMotorStamp(vehicle)}) and was not copied.");
            }

            await IssueMotorEndorsementNumbersAsync(ctx);

            var copies = new List<CopiedMotorVehicle>();
            foreach (var vehicle in onCover)
            {
                copies.Add(await CopyMotorVehicleAsync(ctx, vehicle, newEndDate));
            }
            response.NewCertificateNos.AddRange(copies.Select(c => c.CertificateNo));

            // Full premium: 365 is the module's own "no pro-rata" basis.
            var figures = await PriceCopiedMotorVehiclesAsync(ctx, copies, 365, response);

            var description = FireEndorsementRows.NullIfBlank(ctx.Request.Description);
            var endDate = isExtension ? newEndDate : ctx.Source.EndDate;
            response.NewDetailID = await InsertMotorEndorsementRecordAsync(ctx, ctx.Source.StartDate, endDate, description, figures);
            response.Description = description;
            response.StartDate = ctx.Source.StartDate;
            response.EndDate = endDate;
            response.VehicleCount = copies.Count;
        }

        #endregion

        #region Motor endorsements — copying vehicles

        /// <summary>
        /// Copies one original vehicle into the endorsement: the vehicle row and its MOTOR_DISCOUNT /
        /// MOTOR_LOADING rows. Every column comes across as stored; only the links (record cert, the
        /// vehicle's own certificate number), the tag and the audit columns change. An EXTENSION passes
        /// its new end date for TEndDate when the table carries one.
        /// </summary>
        private async Task<CopiedMotorVehicle> CopyMotorVehicleAsync(
            MotorEndorsementContext ctx, MotorEndorsementVehicle source, DateTime? newEndDate)
        {
            var connection = ctx.Connection;
            var transaction = ctx.Transaction;
            var vehicleShape = await FireEndorsementRows.GetShapeAsync(FireSectionsTable, connection, transaction);
            var adjustmentShape = await FireEndorsementRows.GetShapeAsync(FireItemsTable, connection, transaction);

            // LEGACY: a vehicle's certificate is a CERTNO auto number keyed by branch.
            var certificateNo = await NextItemCertNoAsync(ctx.Policy.BranchID);
            var token = Guid.NewGuid().ToString();

            var vehicle = new Dictionary<string, object>(source.Values, StringComparer.OrdinalIgnoreCase)
            {
                ["Field20"] = ctx.CertOrDocNo,
                ["ENDTNum"] = certificateNo,
                ["Tag"] = ctx.RowTag,
                ["TransGUID"] = token,
                ["SubmitBy"] = ctx.CreatedBy,
                ["SubmitOn"] = ctx.Now
            };
            vehicle.Remove(SectionStampColumn); // a copy starts unstamped

            // Some writers put the record's cert in SectionID instead of Field20. Where the original did
            // that, the copy has to point at the new cert or the copy would still read as the original's.
            if (string.Equals(source.SectionId, ctx.Source.CertOrDocNo, StringComparison.OrdinalIgnoreCase))
                vehicle["SectionID"] = ctx.CertOrDocNo;

            if (newEndDate.HasValue && vehicleShape.Writable.Contains("TEndDate"))
                vehicle["TEndDate"] = newEndDate.Value;

            await FireEndorsementRows.InsertAsync(FireSectionsTable, vehicleShape.OnlyKnown(vehicle), connection, transaction);

            // Vehicle adjustments hang off the vehicle's certificate number.
            var adjustmentCount = 0;
            if (!string.IsNullOrEmpty(source.CertificateNo))
            {
                var adjustments = await FireEndorsementRows.ReadAsync(adjustmentShape,
                    $@"PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo AND Field10 = @SourceCert
  AND Tag IN ('{string.Join("', '", MotorVehicleAdjustmentTags)}')",
                    null, false, connection, transaction,
                    ("@PolicyNo", ctx.PolicyNo), ("@CertificateNo", source.CertificateNo),
                    ("@SourceCert", ctx.Source.CertOrDocNo));

                foreach (var (values, _) in adjustments)
                {
                    var copy = new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase)
                    {
                        ["ENDTNum"] = certificateNo,
                        ["Field10"] = ctx.CertOrDocNo,
                        ["TransGUID"] = Guid.NewGuid().ToString(),
                        ["SubmitBy"] = ctx.CreatedBy,
                        ["SubmitOn"] = ctx.Now
                    };
                    await FireEndorsementRows.InsertAsync(FireItemsTable, adjustmentShape.OnlyKnown(copy), connection, transaction);
                    adjustmentCount++;
                }
            }

            return new CopiedMotorVehicle
            {
                Token = token,
                CertificateNo = certificateNo,
                AdjustmentCount = adjustmentCount,
                VehicleValue = FireEndorsementRows.Number(source.Values, "A1"),
                SumInsured = source.SumInsured,
                NetPremium = source.NetPremium
            };
        }

        /// <summary>REVERSAL: copies the record's proposal-level adjustment rows. Returns the first row's net premium.</summary>
        private static async Task<decimal?> CopyMotorProposalAdjustmentRowsAsync(MotorEndorsementContext ctx)
        {
            var shape = await FireEndorsementRows.GetShapeAsync(FireItemsTable, ctx.Connection, ctx.Transaction);
            var rows = await FireEndorsementRows.ReadAsync(shape,
                "PolicyNo = @PolicyNo AND Field10 = @CertOrDocNo AND Tag = @Tag",
                "SubmitOn", false, ctx.Connection, ctx.Transaction,
                ("@PolicyNo", ctx.PolicyNo), ("@CertOrDocNo", ctx.Source.CertOrDocNo),
                ("@Tag", MotorProposalAdjustmentTag));

            decimal? netPremium = null;
            foreach (var (values, _) in rows)
            {
                var copy = new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase)
                {
                    ["Field10"] = ctx.CertOrDocNo,
                    ["TransGUID"] = Guid.NewGuid().ToString(),
                    ["SubmitBy"] = ctx.CreatedBy,
                    ["SubmitOn"] = ctx.Now
                };
                await FireEndorsementRows.InsertAsync(FireItemsTable, shape.OnlyKnown(copy), ctx.Connection, ctx.Transaction);
                netPremium ??= FireEndorsementRows.Number(values, "A2");
            }

            return netPremium;
        }

        /// <summary>
        /// Totals copied vehicles on the endorsed record's basis — its other discount/loading rates and
        /// the given cover days. Nothing is repriced: vehicle figures are summed as stored.
        /// </summary>
        private async Task<Dictionary<string, object>> PriceCopiedMotorVehiclesAsync(
            MotorEndorsementContext ctx, List<CopiedMotorVehicle> copies, int coverDays, MotorEndorsementResponseDto response)
        {
            var source = ctx.Source;
            var totalVehicleValue = copies.Sum(c => c.VehicleValue);
            var totalSumInsured = copies.Sum(c => c.SumInsured);
            var grossPremium = copies.Sum(c => c.NetPremium);

            var adjustments = await ApplyMotorProposalAdjustmentsAsync(new MotorStep6RequestDto
            {
                AggregatePremium = grossPremium,
                OtherDiscountRate = source.OtherDiscountsRate,
                OtherLoadingRate = source.OtherLoadingsRate
            });
            if (adjustments == null || !adjustments.Success)
                throw new FireEndorsementRuleException(
                    $"Proposal-level adjustments failed: {adjustments?.Message ?? "no result"}");

            await SaveMotorProposalAdjustmentsWithCertAsync(ctx.PolicyNo, ctx.CertOrDocNo, adjustments, ctx.Connection, ctx.Transaction);

            // Same pro-rata basis the module uses: 365 days is full premium.
            var netPremium = adjustments.NetPremiumDue;
            var proRata = coverDays >= 365 || coverDays <= 0
                ? netPremium
                : Math.Round(netPremium * coverDays / 365m, 2);

            var exchangeRate = FireEndorsementRows.PositiveOr(null, source.ExRate, 1m);

            response.TotalVehicleValue = totalVehicleValue;
            response.TotalSumInsured = totalSumInsured;
            response.TotalGrossPremium = grossPremium;
            response.ProRataPremium = proRata;
            response.PremiumDue = proRata;
            response.CoverDays = coverDays;

            return MotorRecordFigures(
                totalVehicleValue, totalSumInsured, grossPremium,
                totalSumInsured * exchangeRate, proRata * exchangeRate,
                proRata, proRata, coverDays,
                FireEndorsementRows.PositiveOr(null, source.ProportionRate, 100m),
                exchangeRate,
                FireEndorsementRows.FirstText(source.ExCurrency, "NAIRA"),
                source.OtherDiscountsRate, source.OtherLoadingsRate);
        }

        /// <summary>Record figures, matching CreateNewMotorPolicyDetailsRecordAsync's column mapping.</summary>
        private static Dictionary<string, object> MotorRecordFigures(
            decimal totalVehicleValue, decimal shareSumInsured, decimal grossPremium,
            decimal foreignSumInsured, decimal foreignPremium,
            decimal proRataPremium, decimal finalPremiumDue, int coverDays,
            decimal proportionRate, decimal exchangeRate, string currency,
            decimal otherDiscountRate, decimal otherLoadingRate)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["ExRate"] = exchangeRate,
                ["ExCurrency"] = currency,
                ["ProportionRate"] = proportionRate,
                ["SumInsured"] = shareSumInsured,
                ["GrossPremium"] = grossPremium,
                ["SumInsuredFrgn"] = foreignSumInsured,
                ["GrossPremiumFrgn"] = foreignPremium,
                ["ProRataDays"] = coverDays,
                ["ProRataPremium"] = proRataPremium,
                ["NetAmount"] = proRataPremium,
                ["TotalRiskValue"] = totalVehicleValue,
                ["A1"] = totalVehicleValue,
                ["A2"] = grossPremium,
                ["A10"] = otherDiscountRate,
                ["A11"] = otherLoadingRate,
                ["A13"] = proRataPremium,
                ["A14"] = finalPremiumDue
            };
        }

        /// <summary>Inserts the endorsement's PolicyDetails record (UNCOMPLETED) and returns its DetailID.</summary>
        private async Task<string> InsertMotorEndorsementRecordAsync(
            MotorEndorsementContext ctx, DateTime? startDate, DateTime? endDate, string description, Dictionary<string, object> figures)
        {
            var insuredName = ctx.Source.InsuredName;
            if (string.IsNullOrWhiteSpace(insuredName))
                insuredName = await GetInsuredFullNameAsync(null, ctx.Connection, ctx.Transaction, ctx.PolicyNo);

            var record = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["PolicyNo"] = ctx.PolicyNo,
                ["EndorsementNo"] = ctx.EndorsementNo,
                ["CertOrDocNo"] = ctx.CertOrDocNo,
                ["EntryDate"] = ctx.Now,
                ["BizOption"] = ctx.Type,
                ["InsuredName"] = insuredName,
                ["StartDate"] = startDate,
                ["EndDate"] = endDate,
                ["Field1"] = FireEndorsementRows.NullIfBlank(ctx.Request.Remarks) ?? $"ENDORSEMENT_{ctx.Type}",
                ["Field2"] = $"{ctx.Type} of {ctx.Source.DetailID}",
                ["Field4"] = description,

                // The ordinary motor path writes Field50 = "MOTOR", but nothing in the module reads that
                // column, and the endorsement's pending status has to live somewhere the fire completion
                // flow already understands. BizOption + EndorsementNo identify a motor endorsement.
                ["Field50"] = EndorsementPendingStatus,

                ["SubmittedBy"] = ctx.CreatedBy,
                ["SubmittedOn"] = ctx.Now,
                ["Active"] = 1,
                ["Deleted"] = 0
            };
            foreach (var figure in figures)
            {
                record[figure.Key] = figure.Value;
            }

            await FireEndorsementRows.InsertAsync("PolicyDetails", record, ctx.Connection, ctx.Transaction);

            using var command = new SqlCommand(@"
SELECT TOP 1 DetailID FROM PolicyDetails
WHERE PolicyNo = @PolicyNo AND CertOrDocNo = @CertOrDocNo AND EndorsementNo = @EndorsementNo
ORDER BY SubmittedOn DESC", ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue("@PolicyNo", ctx.PolicyNo);
            command.Parameters.AddWithValue("@CertOrDocNo", ctx.CertOrDocNo);
            command.Parameters.AddWithValue("@EndorsementNo", ctx.EndorsementNo);
            return FireEndorsementRows.Text(await command.ExecuteScalarAsync());
        }

        #endregion

        #region Motor endorsements — loading, validation, marking

        /// <summary>
        /// Vehicles of a record. Uses the module's own predicate — (Field20 = cert OR SectionID = cert),
        /// excluding the proposal-adjustment row — so an endorsement sees exactly what the screens see.
        /// </summary>
        private static async Task<List<MotorEndorsementVehicle>> LoadMotorEndorsementVehiclesAsync(
            string policyNo, string certOrDocNo, bool forUpdate, SqlConnection connection, SqlTransaction transaction)
        {
            var shape = await FireEndorsementRows.GetShapeAsync(FireSectionsTable, connection, transaction);
            var rows = await FireEndorsementRows.ReadAsync(shape,
                $@"PolicyNo = @PolicyNo
  AND (Field20 = @CertOrDocNo OR SectionID = @CertOrDocNo)
  AND {MotorNotAVehiclePredicate}
  AND ISNULL(Tag, '') <> '{MotorProposalAdjustmentTag}'",
                "ENDTNum", forUpdate, connection, transaction,
                ("@PolicyNo", policyNo), ("@CertOrDocNo", certOrDocNo));

            return rows.Select(row => new MotorEndorsementVehicle(row.Values, row.Key)).ToList();
        }

        private static List<string> ValidateMotorEndorsementRequest(
            string type, MotorEndorsementRequestDto request, List<string> warnings)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.PolicyNo)) errors.Add("PolicyNo is required");
            if (string.IsNullOrWhiteSpace(request.SourceDetailID)) errors.Add("SourceDetailID is required");

            var vehicles = request.Vehicles ?? new List<MotorRiskItemInputDto>();
            var tokens = (request.SelectedVehicleTokens ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            switch (type)
            {
                case EndorsementTypes.Additional:
                    if (vehicles.Count == 0)
                        errors.Add("ADDITIONAL needs at least one vehicle in Vehicles");
                    for (var i = 0; i < vehicles.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(vehicles[i]?.VehicleRegNo))
                            errors.Add($"Vehicles[{i}]: VehicleRegNo is required");
                    }
                    if (tokens.Count > 0)
                        warnings.Add("SelectedVehicleTokens is ignored for ADDITIONAL — it leaves the original vehicles alone.");
                    break;

                case EndorsementTypes.Return:
                case EndorsementTypes.Redo:
                    if (tokens.Count == 0)
                        errors.Add($"{type} needs at least one ticked vehicle in SelectedVehicleTokens");
                    if (vehicles.Count > 0)
                        warnings.Add($"Vehicles is ignored for {type} — the ticked vehicles are copied at their original figures.");
                    break;

                case EndorsementTypes.Extension:
                    if (!request.NewEndDate.HasValue)
                        errors.Add("EXTENSION needs NewEndDate");
                    goto case EndorsementTypes.Reduction;

                case EndorsementTypes.Reversal:
                case EndorsementTypes.Reduction:
                    if (vehicles.Count > 0)
                        warnings.Add($"Vehicles is ignored for {type} — every vehicle on cover is copied at its original figures.");
                    if (tokens.Count > 0)
                        warnings.Add($"SelectedVehicleTokens is ignored for {type} — it applies to every vehicle on cover.");
                    break;
            }

            if (type != EndorsementTypes.Extension && request.NewEndDate.HasValue)
                warnings.Add($"NewEndDate is ignored for {type} — only EXTENSION moves the end date.");
            if (type != EndorsementTypes.Additional && request.EffectiveDate.HasValue)
                warnings.Add($"EffectiveDate is ignored for {type} — the endorsement keeps the record's own period.");
            if (type == EndorsementTypes.Reversal && !string.IsNullOrWhiteSpace(request.Description))
                warnings.Add($"Description is ignored for REVERSAL — it is always \"{ReversalEndorsementDescription}\".");
            if (type == EndorsementTypes.Return && !string.IsNullOrWhiteSpace(request.Description))
                warnings.Add($"Description is ignored for RETURN — it is always \"{ReturnEndorsementDescription}\".");

            return errors;
        }

        /// <summary>Matches the ticked tokens onto in-force vehicles. Throws on anything unmatched, expired or duplicated.</summary>
        private static List<MotorEndorsementVehicle> ResolveTickedMotorVehicles(
            List<MotorEndorsementVehicle> vehicles, List<string> tokens)
        {
            var wanted = (tokens ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var ticked = new List<MotorEndorsementVehicle>();
            var missing = new List<string>();
            var offCover = new List<string>();

            foreach (var token in wanted)
            {
                var matches = vehicles.Where(v =>
                    string.Equals(v.SelectionToken, token, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(v.CertificateNo, token, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(v.RegNo, token, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matches.Count == 0) { missing.Add(token); continue; }
                if (matches.Count > 1)
                    throw new FireEndorsementRuleException(
                        $"Token '{token}' matches {matches.Count} vehicles on this record. Use the SelectionToken from " +
                        "GET endorsement/motor/vehicles, which is unique per vehicle.");

                var vehicle = matches[0];
                if (vehicle.IsExpired) { offCover.Add(vehicle.Label); continue; }
                ticked.Add(vehicle);
            }

            if (missing.Count > 0)
                throw new FireEndorsementRuleException(
                    $"These vehicles are not on this record: {string.Join(", ", missing)}. " +
                    "Tokens come from GET endorsement/motor/vehicles for the same DetailID.");

            if (offCover.Count > 0)
                throw new FireEndorsementRuleException(
                    $"These vehicles are already off cover from an earlier endorsement and can't be endorsed again: {string.Join(", ", offCover)}.");

            if (ticked.Count == 0)
                throw new FireEndorsementRuleException("No vehicles were ticked.");

            return ticked;
        }

        /// <summary>
        /// Marks an original vehicle. expire sets its Tag to EXPIRED; stamp writes the endorsement number
        /// into the stamp column while the Tag is left as it is.
        ///
        /// NOTE: Tag = EXPIRED only takes the vehicle off cover once the vehicle reads filter it —
        /// see PATCH-motor-vehicle-reads.md. Unlike fire, motor's reads do not filter Tag today.
        /// </summary>
        private static async Task MarkOriginalMotorVehicleAsync(
            MotorEndorsementContext ctx, MotorEndorsementVehicle vehicle, bool expire, bool stamp)
        {
            if (!expire && !stamp) return;

            var shape = await FireEndorsementRows.GetShapeAsync(FireSectionsTable, ctx.Connection, ctx.Transaction);
            if (stamp) FireEndorsementRows.RequireFits(shape, SectionStampColumn, ctx.EndorsementNo);

            var sets = new List<string>();
            if (expire) sets.Add("[Tag] = @Expired");
            if (stamp) sets.Add($"{FireEndorsementRows.Q(SectionStampColumn)} = @Stamp");
            sets.Add("[SubmitBy] = @SubmitBy");
            sets.Add("[SubmitOn] = @SubmitOn");

            using var command = new SqlCommand(
                $"UPDATE {FireEndorsementRows.Q(FireSectionsTable)} SET {string.Join(", ", sets)} " +
                $"WHERE [PolicyNo] = @PolicyNo AND {vehicle.KeyPredicate}",
                ctx.Connection, ctx.Transaction);

            if (expire) command.Parameters.AddWithValue("@Expired", EndorsementTypes.Expired);
            if (stamp) command.Parameters.AddWithValue("@Stamp", ctx.EndorsementNo);
            command.Parameters.AddWithValue("@SubmitBy", ctx.CreatedBy);
            command.Parameters.AddWithValue("@SubmitOn", ctx.Now);
            command.Parameters.AddWithValue("@PolicyNo", ctx.PolicyNo);
            command.Parameters.AddWithValue("@KeyValue", vehicle.KeyValue);

            if (await command.ExecuteNonQueryAsync() == 0)
                throw new FireEndorsementRuleException(
                    $"Could not update vehicle {vehicle.Label} — it may have been changed by someone else.");
        }

        /// <summary>Issues the endorsement number and the record's cert number, in that order.</summary>
        private async Task IssueMotorEndorsementNumbersAsync(MotorEndorsementContext ctx)
        {
            ctx.EndorsementNo = await NextFireEndorsementNumberAsync(ctx.PolicyNo, ctx.Connection, ctx.Transaction);

            var subRiskId = FireEndorsementRows.FirstText(ctx.Policy.SubRiskID, ctx.Source.SubRiskID);
            if (string.IsNullOrWhiteSpace(subRiskId))
                throw new FireEndorsementRuleException(
                    $"Policy {ctx.PolicyNo} has no SubRiskID — the endorsement's certificate number can't be generated.");

            ctx.CertOrDocNo = await NextCalcCertAsync(ctx.Policy.BranchID, subRiskId, MapEndorseType(ctx.Type));
        }

        private static string DescribeMotorStamp(MotorEndorsementVehicle vehicle) =>
            string.IsNullOrWhiteSpace(vehicle.Stamp) ? "an earlier endorsement" : $"endorsement {vehicle.Stamp}";

        private static MotorEndorsementResponseDto FailMotorEndorsement(
            MotorEndorsementResponseDto response, string message, List<string> errors = null)
        {
            response.Success = false;
            response.Message = message;
            response.Status = null;
            if (errors != null) response.ValidationErrors.AddRange(errors);
            return response;
        }

        #endregion

        #region Motor endorsements — working types

        /// <summary>Everything one motor endorsement needs while it runs.</summary>
        private class MotorEndorsementContext
        {
            public string Type { get; set; }

            /// <summary>Tag written on the vehicle rows this endorsement creates (NEW for a REDO).</summary>
            public string RowTag { get; set; }

            public MotorEndorsementRequestDto Request { get; set; }
            public ProposalDto Policy { get; set; }
            public FireEndorsementSource Source { get; set; }
            public string CreatedBy { get; set; }
            public DateTime Now { get; set; }
            public SqlConnection Connection { get; set; }
            public SqlTransaction Transaction { get; set; }

            public string EndorsementNo { get; set; }
            public string CertOrDocNo { get; set; }

            public string PolicyNo => Request?.PolicyNo;

            /// <summary>
            /// Expiring the record is identical for both modules, so this reuses the fire routine
            /// rather than a copy of it.
            /// </summary>
            public FireEndorsementContext AsFireContext() => new()
            {
                Type = Type,
                RowTag = RowTag,
                Request = new FireEndorsementRequestDto { PolicyNo = PolicyNo },
                Policy = Policy,
                Source = Source,
                CreatedBy = CreatedBy,
                Now = Now,
                Connection = Connection,
                Transaction = Transaction,
                EndorsementNo = EndorsementNo,
                CertOrDocNo = CertOrDocNo
            };
        }

        /// <summary>One vehicle row of the record being endorsed.</summary>
        private class MotorEndorsementVehicle
        {
            public MotorEndorsementVehicle(Dictionary<string, object> values, object key)
            {
                Values = values;
                KeyValue = key;
            }

            public Dictionary<string, object> Values { get; }
            public object KeyValue { get; }

            public string KeyPredicate => FireEndorsementRows.KeyPredicateFor(FireSectionsTable, Values);

            /// <summary>PolicyTempDetails.ENDTNum — the vehicle's certificate number.</summary>
            public string CertificateNo => FireEndorsementRows.Text(Values, "ENDTNum");

            public string TransGuid => FireEndorsementRows.Text(Values, "TransGUID");
            public string SectionId => FireEndorsementRows.Text(Values, "SectionID");
            public string RegNo => FireEndorsementRows.Text(Values, "Field1");
            public string Tag => FireEndorsementRows.Text(Values, "Tag");
            public string Stamp => FireEndorsementRows.Text(Values, SectionStampColumn);

            /// <summary>RiskSum — the vehicle's proportional sum insured.</summary>
            public decimal SumInsured => FireEndorsementRows.Number(Values, "RiskSum");

            /// <summary>SectionPremium — the vehicle's net premium.</summary>
            public decimal NetPremium => FireEndorsementRows.Number(Values, "SectionPremium");

            public string SelectionToken => FireEndorsementRows.FirstText(TransGuid, CertificateNo);

            public bool IsExpired => string.Equals(Tag, EndorsementTypes.Expired, StringComparison.OrdinalIgnoreCase);

            public string Label
            {
                get
                {
                    var name = FireEndorsementRows.FirstText(RegNo, "(no reg. no.)");
                    return string.IsNullOrEmpty(CertificateNo) ? name : $"{name} [{CertificateNo}]";
                }
            }
        }

        private class CopiedMotorVehicle
        {
            public string Token { get; set; }
            public string CertificateNo { get; set; }
            public int AdjustmentCount { get; set; }
            public decimal VehicleValue { get; set; }
            public decimal SumInsured { get; set; }
            public decimal NetPremium { get; set; }
        }

        #endregion
    }
}
