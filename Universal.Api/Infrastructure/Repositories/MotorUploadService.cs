using GibsLifesMicroWebApi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    // ============================================================
    // MOTOR — BULK VEHICLE APPEND (backs the file upload path)
    //
    // AddMotorVehicleAsync opens its own connection/transaction and recomputes
    // the declaration totals per vehicle — correct for one vehicle from a form,
    // wasteful and non-atomic for 200 rows from a spreadsheet (a failure on row
    // 150 would leave rows 1-149 committed). This does the same work for a
    // list: one transaction, one recompute at the end, all-or-nothing.
    //
    // Everything else is identical to the single-vehicle path — same
    // CalculateSingleVehicleCompleteAsync, same CERTNO issuance at save time,
    // same SaveMotorVehicleWithCertAsync/SaveMotorVehicleAdjustmentsWithCertAsync,
    // same RecomputeAndSaveMotorAggregateAsync. File-sourced vehicles are not a
    // separate species of vehicle.
    // ============================================================

    public partial class ProposalService
    {
        /// <summary>
        /// Append many vehicles to the proposal's Motor declaration in a single
        /// transaction, then recompute+save the declaration's totals ONCE
        /// (Motor's Other Discount/Loading rates re-applied against the new
        /// combined total, proportion+FX redone, PolicyDetails/Policies header
        /// updated). Incremental — works against whatever is currently
        /// persisted, NOT a resubmission of the full vehicle list.
        ///
        /// The returned response carries CalculatedVehicles populated with the
        /// vehicles this call added, each holding its REAL issued CertificateNo
        /// and full step breakdown — what a caller needs to let the user review
        /// and then edit individual vehicles via UpdateMotorVehicleAsync.
        /// </summary>
        public async Task<MotorCalculationResponseDto> AddMotorVehiclesAsync(
            string proposalNo, List<MotorRiskItemInputDto> vehicles)
        {
            if (vehicles == null || !vehicles.Any())
            {
                return new MotorCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    Success = false,
                    Message = "At least one vehicle is required"
                };
            }

            if (!await ProposalExistsAsync(proposalNo))
            {
                return new MotorCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    Success = false,
                    Message = $"Proposal {proposalNo} not found"
                };
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var certOrDocNo = await GetOrGenerateCertOrDocNoAsync(proposalNo, connection, transaction);
                var branchId = await GetBranchIdAsync(proposalNo, connection, transaction);
                var context = await GetMotorDeclarationContextAsync(proposalNo, certOrDocNo, connection, transaction);

                // Declaration-level context (cover days in particular) comes from
                // the stored header, exactly as AddMotorVehicleAsync does — an
                // uploaded file describes vehicles, not declaration terms. On a
                // brand-new proposal with nothing on the header yet, this falls
                // back to 365 days / 100% / rate 1 / NAIRA.
                var contextRequest = new MotorCalculationRequestDto
                {
                    ProposalNo = proposalNo,
                    CoverDays = context.CoverDays
                };

                var seq = await GetMaxMotorVehicleSequenceAsync(proposalNo, connection, transaction);
                var calculatedVehicles = new List<CalculatedMotorRiskItemDto>();

                foreach (var vehicle in vehicles)
                {
                    var calculated = await CalculateSingleVehicleCompleteAsync(vehicle, contextRequest, ++seq);

                    // Real CERTNO auto number issued here at save time, same as
                    // every other motor save path — previews never burn numbers.
                    if (string.IsNullOrWhiteSpace(calculated.CertificateNo) ||
                        calculated.CertificateNo.Contains("/CERT/"))
                    {
                        calculated.CertificateNo = await NextItemCertNoAsync(branchId);
                    }

                    await SaveMotorVehicleWithCertAsync(
                        proposalNo, certOrDocNo, calculated, connection, transaction);
                    await SaveMotorVehicleAdjustmentsWithCertAsync(
                        proposalNo, certOrDocNo, calculated.CertificateNo,
                        calculated.Step2_Discounts.DiscountsApplied, "DISCOUNT", connection, transaction);
                    await SaveMotorVehicleAdjustmentsWithCertAsync(
                        proposalNo, certOrDocNo, calculated.CertificateNo,
                        calculated.Step3_Loadings.LoadingsApplied, "LOADING", connection, transaction);

                    calculatedVehicles.Add(calculated);
                }

                var updated = await RecomputeAndSaveMotorAggregateAsync(
                    proposalNo, certOrDocNo, connection, transaction);

                await transaction.CommitAsync();

                // RecomputeAndSaveMotorAggregateAsync deliberately doesn't re-read
                // individual vehicles (it only re-sums RiskSum/SectionPremium), so
                // attach the ones just calculated — they're the authoritative
                // record of what this call added, certificate numbers included.
                updated.CalculatedVehicles = calculatedVehicles;

                var certNos = calculatedVehicles.Select(v => v.CertificateNo).ToList();
                updated.Message =
                    $"{certNos.Count} vehicle(s) added ({string.Join(", ", certNos.Take(5))}" +
                    (certNos.Count > 5 ? ", …" : "") +
                    "); declaration totals recalculated.";

                _logger.LogInformation(
                    "Bulk-added {Count} motor vehicle(s) to {ProposalNo} / {CertOrDocNo}",
                    certNos.Count, proposalNo, certOrDocNo);

                return updated;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error bulk-adding {Count} motor vehicle(s) for {ProposalNo}",
                    vehicles.Count, proposalNo);
                return new MotorCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    Success = false,
                    Message = ex.Message
                };
            }
        }
    }
}
