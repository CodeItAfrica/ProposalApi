using Castle.Core.Logging;
using Gibs.Domain.Entities;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Http;
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
        /// STEP 1: Calculate basic premium for a vehicle
        /// </summary>
        public async Task<MotorBasicPremiumResponseDto> CalculateMotorBasicPremiumAsync(
            MotorStep1RequestDto request)
        {
            try
            {
                // Check if it's Third Party or has flat premium
                var flatPremium = await GetTPDFlatPremiumAsync(
                    request.VehicleType,
                    request.Usage,
                    request.CoverType);

                if (flatPremium > 0)
                {
                    // Third Party or flat premium
                    return new MotorBasicPremiumResponseDto
                    {
                        CoverType = request.CoverType,
                        IsThirdPartyOrFlat = true,
                        VehicleValue = request.VehicleValue,
                        PremiumRate = 0,
                        BasicPremium = flatPremium,
                        CalculationFormula = $"Flat Premium for {request.CoverType}",
                        Success = true,
                        Message = "Basic premium calculated (flat rate)"
                    };
                }
                else
                {
                    // Comprehensive - calculate based on vehicle value and rate
                    var basicPremium = request.VehicleValue * (request.PremiumRate / 100);

                    return new MotorBasicPremiumResponseDto
                    {
                        CoverType = request.CoverType,
                        IsThirdPartyOrFlat = false,
                        VehicleValue = request.VehicleValue,
                        PremiumRate = request.PremiumRate,
                        BasicPremium = basicPremium,
                        CalculationFormula = $"{request.VehicleValue:N2} × {request.PremiumRate}% = {basicPremium:N2}",
                        Success = true,
                        Message = "Basic premium calculated"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating basic premium");
                return new MotorBasicPremiumResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// STEP 2: Apply discounts (BEFORE loadings)
        /// </summary>
        public async Task<MotorDiscountsResponseDto> ApplyMotorDiscountsAsync(
            MotorStep2RequestDto request)
        {
            try
            {
                var workingPremium = request.BasicPremium;
                var appliedDiscounts = new List<MotorAdjustmentDto>();
                decimal totalDiscountAmount = 0;

                if (request.Discounts != null && request.Discounts.Any())
                {
                    // Order discounts by sequence
                    var orderedDiscounts = request.Discounts
                        .OrderBy(d => d.SequenceOrder)
                        .ToList();

                    int sequence = 1;
                    foreach (var discount in orderedDiscounts)
                    {
                        decimal baseAmount;
                        decimal discountAmount;
                        string formula;

                        // Determine base amount based on application basis
                        if (discount.AppliedOn == AdjustmentBasis.SumInsured)
                        {
                            baseAmount = request.VehicleValue;
                            discountAmount = baseAmount * (discount.Rate / 100);
                            formula = $"{discount.Rate}% of Sum Insured (₦{baseAmount:N2})";
                        }
                        else // Premium
                        {
                            baseAmount = workingPremium;
                            discountAmount = baseAmount * (discount.Rate / 100);
                            formula = $"{discount.Rate}% of Current Premium (₦{baseAmount:N2})";
                        }

                        var appliedDiscount = new MotorAdjustmentDto
                        {
                            AdjustmentName = discount.AdjustmentName,
                            AdjustmentType = "DISCOUNT",
                            Rate = discount.Rate,
                            AppliedOn = discount.AppliedOn,
                            BaseAmount = baseAmount,
                            Amount = discountAmount,
                            SequenceOrder = sequence++,
                            Formula = formula
                        };

                        workingPremium -= discountAmount;
                        totalDiscountAmount += discountAmount;
                        appliedDiscounts.Add(appliedDiscount);
                    }
                }

                return new MotorDiscountsResponseDto
                {
                    BasicPremium = request.BasicPremium,
                    VehicleValue = request.VehicleValue,
                    DiscountsApplied = appliedDiscounts,
                    TotalDiscountAmount = totalDiscountAmount,
                    PremiumAfterDiscounts = workingPremium,
                    Success = true,
                    Message = $"{appliedDiscounts.Count} discount(s) applied"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying discounts");
                return new MotorDiscountsResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// STEP 3: Apply loadings (AFTER discounts)
        /// </summary>
        public async Task<MotorLoadingsResponseDto> ApplyMotorLoadingsAsync(
            MotorStep3RequestDto request)
        {
            try
            {
                var workingPremium = request.PremiumAfterDiscounts;
                var appliedLoadings = new List<MotorAdjustmentDto>();
                decimal totalLoadingAmount = 0;

                if (request.Loadings != null && request.Loadings.Any())
                {
                    // Order loadings by sequence
                    var orderedLoadings = request.Loadings
                        .OrderBy(l => l.SequenceOrder)
                        .ToList();

                    int sequence = 1;
                    foreach (var loading in orderedLoadings)
                    {
                        decimal baseAmount;
                        decimal loadingAmount;
                        string formula;

                        // Determine base amount based on application basis
                        if (loading.AppliedOn == AdjustmentBasis.SumInsured)
                        {
                            baseAmount = request.VehicleValue;
                            loadingAmount = baseAmount * (loading.Rate / 100);
                            formula = $"{loading.Rate}% of Sum Insured (₦{baseAmount:N2})";
                        }
                        else // Premium
                        {
                            baseAmount = workingPremium;
                            loadingAmount = baseAmount * (loading.Rate / 100);
                            formula = $"{loading.Rate}% of Current Premium (₦{baseAmount:N2})";
                        }

                        var appliedLoading = new MotorAdjustmentDto
                        {
                            AdjustmentName = loading.AdjustmentName,
                            AdjustmentType = "LOADING",
                            Rate = loading.Rate,
                            AppliedOn = loading.AppliedOn,
                            BaseAmount = baseAmount,
                            Amount = loadingAmount,
                            SequenceOrder = sequence++,
                            Formula = formula
                        };

                        workingPremium += loadingAmount;
                        totalLoadingAmount += loadingAmount;
                        appliedLoadings.Add(appliedLoading);
                    }
                }

                return new MotorLoadingsResponseDto
                {
                    PremiumBeforeLoadings = request.PremiumAfterDiscounts,
                    VehicleValue = request.VehicleValue,
                    LoadingsApplied = appliedLoadings,
                    TotalLoadingAmount = totalLoadingAmount,
                    GrossPremium = workingPremium,
                    Success = true,
                    Message = $"{appliedLoadings.Count} loading(s) applied"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying loadings");
                return new MotorLoadingsResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// STEP 4: Add additional costs to get final premium
        /// </summary>
        public async Task<MotorFinalPremiumResponseDto> CalculateMotorFinalPremiumAsync(
            MotorStep4RequestDto request)
        {
            try
            {
                var netPremium = request.GrossPremium + request.TrackingCost + request.RescueCost;

                return new MotorFinalPremiumResponseDto
                {
                    GrossPremium = request.GrossPremium,
                    TrackingCost = request.TrackingCost,
                    RescueCost = request.RescueCost,
                    NetPremium = netPremium,
                    Success = true,
                    Message = "Final premium calculated"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating final premium");
                return new MotorFinalPremiumResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// STEP 5: Aggregate all vehicles
        /// </summary>
        public async Task<MotorAggregateVehiclesResponseDto> AggregateVehiclesAsync(
            MotorStep5RequestDto request)
        {
            try
            {
                decimal totalValue = 0;
                decimal totalPremium = 0;

                foreach (var vehicle in request.Vehicles)
                {
                    totalValue += vehicle.VehicleValue;
                    totalPremium += vehicle.NetPremium;
                }

                return new MotorAggregateVehiclesResponseDto
                {
                    TotalVehicleValue = totalValue,
                    TotalNetPremium = totalPremium,
                    VehicleCount = request.Vehicles.Count,
                    VehicleSummaries = request.Vehicles,
                    Success = true,
                    Message = $"Successfully aggregated {request.Vehicles.Count} vehicle(s)"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating vehicles");
                return new MotorAggregateVehiclesResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// STEP 6: Apply proposal-level adjustments (other discount and other loading)
        /// </summary>
        public async Task<MotorProposalAdjustmentsResponseDto> ApplyMotorProposalAdjustmentsAsync(
            MotorStep6RequestDto request)
        {
            try
            {
                var workingPremium = request.AggregatePremium;

                // Apply other discount first
                var discountAmount = workingPremium * (request.OtherDiscountRate / 100);
                var premiumAfterDiscount = workingPremium - discountAmount;

                // Then apply other loading
                var loadingAmount = premiumAfterDiscount * (request.OtherLoadingRate / 100);
                var netPremium = premiumAfterDiscount + loadingAmount;

                return new MotorProposalAdjustmentsResponseDto
                {
                    AggregatePremium = request.AggregatePremium,
                    OtherDiscountRate = request.OtherDiscountRate,
                    OtherDiscountAmount = discountAmount,
                    OtherDiscountFormula = $"{request.OtherDiscountRate}% of ₦{workingPremium:N2} = ₦{discountAmount:N2}",
                    PremiumAfterDiscount = premiumAfterDiscount,
                    OtherLoadingRate = request.OtherLoadingRate,
                    OtherLoadingAmount = loadingAmount,
                    OtherLoadingFormula = $"{request.OtherLoadingRate}% of ₦{premiumAfterDiscount:N2} = ₦{loadingAmount:N2}",
                    NetPremiumDue = netPremium,
                    Success = true,
                    Message = "Proposal-level adjustments applied successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying proposal adjustments");
                return new MotorProposalAdjustmentsResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// STEP 7: Calculate pro-rata and apply proportion rate
        /// </summary>
        public async Task<MotorFinalCalculationResponseDto> CalculateMotorFinalAsync(
            MotorStep7RequestDto request)
        {
            try
            {
                // Calculate pro-rata
                var proRataPremium = request.NetPremium * request.CoverDays / 365;
                var proRataFormula = $"₦{request.NetPremium:N2} × {request.CoverDays}/365 = ₦{proRataPremium:N2}";

                // Apply proportion rate
                var shareSumInsured = request.TotalVehicleValue * (request.ProportionRate / 100);
                var sharePremium = proRataPremium * (request.ProportionRate / 100);

                // Apply exchange rate
                var foreignSumInsured = shareSumInsured * request.ExchangeRate;
                var foreignPremium = sharePremium * request.ExchangeRate;

                return new MotorFinalCalculationResponseDto
                {
                    NetPremium = request.NetPremium,
                    TotalVehicleValue = request.TotalVehicleValue,
                    CoverDays = request.CoverDays,
                    ProRataPremium = proRataPremium,
                    ProRataFormula = proRataFormula,
                    ProportionRate = request.ProportionRate,
                    ShareSumInsured = shareSumInsured,
                    SharePremium = sharePremium,
                    ExchangeRate = request.ExchangeRate,
                    Currency = request.Currency,
                    ForeignSumInsured = foreignSumInsured,
                    ForeignPremium = foreignPremium,
                    FinalPremiumDue = sharePremium,
                    Success = true,
                    Message = "Final calculation completed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating final premium");
                return new MotorFinalCalculationResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<MotorCalculationResponseDto> CalculateMotorRiskItemsAsync(
     MotorCalculationRequestDto request)
        {
            try
            {
                var response = new MotorCalculationResponseDto
                {
                    ProposalNo = request.ProposalNo,
                    Success = true,
                    CalculatedOn = DateTime.Now
                };

                decimal totalVehicleValue = 0;
                decimal totalGrossPremium = 0;
                decimal totalNetPremium = 0;

                int seq = await GetMaxMotorVehicleSequenceAsync(request.ProposalNo);

                foreach (var vehicle in request.Vehicles)
                {
                    var calculated = await CalculateSingleVehicleCompleteAsync(vehicle, request, ++seq);
                    response.CalculatedVehicles.Add(calculated);

                    totalVehicleValue += calculated.VehicleValue;
                    totalGrossPremium += calculated.GrossPremium;
                    totalNetPremium += calculated.NetPremium;
                }

                response.TotalVehicleValue = totalVehicleValue;
                response.TotalGrossPremium = totalGrossPremium;
                response.TotalNetPremium = totalNetPremium;
                response.ShareSumInsured = totalVehicleValue * (request.ProportionRate / 100);
                response.SharePremium = totalNetPremium * (request.ProportionRate / 100);

                response.ProRataPremium = request.CoverDays < 365
                    ? response.SharePremium * request.CoverDays / 365
                    : response.SharePremium;

                response.ForeignSumInsured = response.ShareSumInsured * request.ExchangeRate;
                response.ForeignPremium = response.ProRataPremium * request.ExchangeRate;

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating motor risk items");
                return new MotorCalculationResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
        public async Task<MotorCalculationResponseDto> CalculateMotorCompleteAsync(
      MotorCalculationRequestDto request,
      SqlConnection connection = null,
      SqlTransaction transaction = null)
        {
            try
            {
                var response = new MotorCalculationResponseDto
                {
                    ProposalNo = request.ProposalNo,
                    Success = true,
                    CalculatedOn = DateTime.Now
                };

                decimal totalVehicleValue = 0;
                decimal totalNetPremium = 0;

                // Seed sequence ONCE, then increment per vehicle (no per-vehicle DB read)
                int seq = await GetMaxMotorVehicleSequenceAsync(
                    request.ProposalNo, connection, transaction);

                foreach (var vehicle in request.Vehicles)
                {
                    var calculated = await CalculateSingleVehicleCompleteAsync(vehicle, request, ++seq);
                    response.CalculatedVehicles.Add(calculated);

                    totalVehicleValue += calculated.VehicleValue;
                    totalNetPremium += calculated.NetPremium;
                }

                response.TotalVehicleValue = totalVehicleValue;
                response.TotalGrossPremium = totalNetPremium; // see note below — semantics unchanged here
                response.TotalNetPremium = totalNetPremium;

                // Proposal-level adjustments
                var proposalAdjustmentRequest = new MotorStep6RequestDto
                {
                    AggregatePremium = totalNetPremium,
                    OtherDiscountRate = request.OtherDiscountRate,
                    OtherLoadingRate = request.OtherLoadingRate
                };

                var proposalAdjustments = await ApplyMotorProposalAdjustmentsAsync(proposalAdjustmentRequest);
                if (!proposalAdjustments.Success)
                    throw new Exception($"Proposal adjustments failed: {proposalAdjustments.Message}");

                response.ProposalAdjustments = proposalAdjustments;
                response.NetPremiumAfterProposalAdjustments = proposalAdjustments.NetPremiumDue;

                var finalRequest = new MotorStep7RequestDto
                {
                    NetPremium = proposalAdjustments.NetPremiumDue,
                    TotalVehicleValue = totalVehicleValue,
                    CoverDays = request.CoverDays,
                    ProportionRate = request.ProportionRate,
                    ExchangeRate = request.ExchangeRate,
                    Currency = request.Currency
                };

                var finalCalc = await CalculateMotorFinalAsync(finalRequest);
                if (!finalCalc.Success)
                    throw new Exception($"Final calculation failed: {finalCalc.Message}");

                response.ProRataPremium = finalCalc.ProRataPremium;
                response.ShareSumInsured = finalCalc.ShareSumInsured;
                response.SharePremium = finalCalc.SharePremium;
                response.FinalPremiumDue = finalCalc.FinalPremiumDue;
                response.ForeignSumInsured = finalCalc.ForeignSumInsured;
                response.ForeignPremium = finalCalc.ForeignPremium;

                response.Message = "Motor calculation completed successfully";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating motor risk items");
                return new MotorCalculationResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }


        /// <summary>
        /// Complete calculation for a single vehicle (all steps combined).
        /// CertificateNo is built from the caller-supplied sequence to guarantee
        /// per-request uniqueness across multiple vehicles.
        /// </summary>
        private async Task<CalculatedMotorRiskItemDto> CalculateSingleVehicleCompleteAsync(
            MotorRiskItemInputDto vehicle,
            MotorCalculationRequestDto request,
            int certificateSequence)
        {
            // Step 1: Basic premium
            var step1Request = new MotorStep1RequestDto
            {
                VehicleType = vehicle.VehicleType,
                VehicleValue = vehicle.VehicleValue,
                PremiumRate = vehicle.PremiumRate,
                CoverType = vehicle.CoverType,
                Usage = vehicle.Usage
            };
            var step1 = await CalculateMotorBasicPremiumAsync(step1Request);
            if (!step1.Success)
                throw new Exception($"Step 1 failed: {step1.Message}");

            // Step 2: Apply discounts
            var step2Request = new MotorStep2RequestDto
            {
                BasicPremium = step1.BasicPremium,
                VehicleValue = vehicle.VehicleValue,
                Discounts = vehicle.Discounts
            };
            var step2 = await ApplyMotorDiscountsAsync(step2Request);
            if (!step2.Success)
                throw new Exception($"Step 2 failed: {step2.Message}");

            // Step 3: Apply loadings
            var step3Request = new MotorStep3RequestDto
            {
                PremiumAfterDiscounts = step2.PremiumAfterDiscounts,
                VehicleValue = vehicle.VehicleValue,
                Loadings = vehicle.Loadings
            };
            var step3 = await ApplyMotorLoadingsAsync(step3Request);
            if (!step3.Success)
                throw new Exception($"Step 3 failed: {step3.Message}");

            // Step 4: Final premium
            var step4Request = new MotorStep4RequestDto
            {
                GrossPremium = step3.GrossPremium,
                TrackingCost = vehicle.TrackingCost,
                RescueCost = vehicle.RescueCost
            };
            var step4 = await CalculateMotorFinalPremiumAsync(step4Request);
            if (!step4.Success)
                throw new Exception($"Step 4 failed: {step4.Message}");

            // Share values (proportion applied later at proposal level)
            var shareValue = vehicle.VehicleValue;
            var sharePremium = step4.NetPremium;

            // PREVIEW PLACEHOLDER ONLY — collision-free, no DB round-trip.
            // The real per-vehicle certificate (CERTNO auto number, like the legacy
            // motor module) is issued at save time in PersistMotorCalculationWithCertAsync,
            // which replaces any '/CERT/' placeholder. Previews never burn numbers.
            var certificateNo = $"{request.ProposalNo}/CERT/{certificateSequence:D6}";

            return new CalculatedMotorRiskItemDto
            {
                ItemNo = vehicle.ItemNo,
                VehicleRegNo = vehicle.VehicleRegNo,
                VehicleUser = vehicle.VehicleUser,
                VehicleType = vehicle.VehicleType,
                VehicleMake = vehicle.VehicleMake,
                VehicleModel = vehicle.VehicleModel,
                ChassisNo = vehicle.ChassisNo,
                EngineNo = vehicle.EngineNo,
                Color = vehicle.Color,
                ModelYear = vehicle.ModelYear,
                CoverType = vehicle.CoverType,
                Usage = vehicle.Usage,
                VehicleValue = vehicle.VehicleValue,
                PremiumRate = vehicle.PremiumRate,
                State = vehicle.State,
                SeatCapacity = vehicle.SeatCapacity,
                WaxCode = vehicle.WaxCode,
                Location = vehicle.Location,
                StartDate = vehicle.StartDate,
                EndDate = vehicle.EndDate,
                TrackingCost = vehicle.TrackingCost,
                RescueCost = vehicle.RescueCost,
                Discounts = vehicle.Discounts,
                Loadings = vehicle.Loadings,

                CertificateNo = certificateNo,
                Step1_BasicPremium = step1,
                Step2_Discounts = step2,
                Step3_Loadings = step3,
                Step4_FinalPremium = step4,

                ShareValue = shareValue,
                SharePremium = sharePremium
            };
        }
        /// <summary>
        /// Get TPD flat premium from database
        /// </summary>
        private async Task<decimal> GetTPDFlatPremiumAsync(string vehicleType, string usage, string coverType)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
    SELECT FlatPremium 
    FROM TPD_Premium 
    WHERE VehType = @VehicleType 
    AND VehUsage = @Usage 
    AND CoverType = @CoverType";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@VehicleType", vehicleType);
            command.Parameters.AddWithValue("@Usage", usage);
            command.Parameters.AddWithValue("@CoverType", coverType);

            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToDecimal(result);
        }

        /// <summary>
        /// Updated SaveMotorCalculationAsync with option to delete existing data first
        /// </summary>
        public async Task<ResponseInfo> SaveMotorCalculationAsync(
            MotorCalculationResponseDto calculation,
            bool deleteExisting = false)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Delete existing calculations if requested (for recalculation)
                if (deleteExisting)
                {
                    await DeleteExistingMotorCalculationsAsync(
                        calculation.ProposalNo, connection, transaction);
                }

                // 1. Update PolicyDetails with summary
                await UpdatePolicyDetailsWithMotorCalculationAsync(
                    calculation, connection, transaction);

                var certOrDocNo = await GetOrGenerateCertOrDocNoAsync(
    calculation.ProposalNo, connection, transaction);

                await PersistMotorCalculationWithCertAsync(calculation, certOrDocNo, connection, transaction);
                // 6. Update Proposals summary
                await UpdateProposalWithMotorTotalsAsync(
                    calculation, connection, transaction);

                await transaction.CommitAsync();

                return new ResponseInfo
                {
                    ErrorCode = 0,
                    ErrorMessage = deleteExisting
                        ? "Motor calculation recalculated and saved successfully"
                        : "Motor calculation saved successfully",
                    TotalSuccess = 1
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error saving motor calculation");
                return new ResponseInfo
                {
                    ErrorCode = 1,
                    ErrorMessage = "Failed to save motor calculation",
                    ExtraMessage = ex.Message
                };
            }
        }


        private async Task SaveVehicleAdjustmentsAsync(
            string proposalNo,
            string certificateNo,
            List<MotorAdjustmentDto> adjustments,
            string adjustmentType,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            foreach (var adjustment in adjustments)
            {
                var sql = @"
                INSERT INTO PolicyPremDetails (
                    PolicyNo, ENDTNum, Section, ContentDesc, SMICode,
                    Field1, Field2, Field3, Field4, Field5,
                    A1, A2, A3, A4,
                    Tag, TransGUID, SubmitBy, SubmitOn
                ) VALUES (
                    @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode,
                    @Field1, @Field2, @Field3, @Field4, @Field5,
                    @A1, @A2, @A3, @A4,
                    @Tag, @TransGUID, @SubmitBy, @SubmitOn
                )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@ENDTNum", certificateNo);
                command.Parameters.AddWithValue("@Section", "MOTOR");
                command.Parameters.AddWithValue("@ContentDesc", adjustment.AdjustmentName);
                command.Parameters.AddWithValue("@SMICode", adjustmentType);

                command.Parameters.AddWithValue("@Field1", adjustment.AdjustmentName);
                command.Parameters.AddWithValue("@Field2", adjustmentType);
                command.Parameters.AddWithValue("@Field3", adjustment.AppliedOn.ToString());
                command.Parameters.AddWithValue("@Field4", adjustment.Formula);
                command.Parameters.AddWithValue("@Field5", adjustment.SequenceOrder.ToString());

                command.Parameters.AddWithValue("@A1", adjustment.Rate);
                command.Parameters.AddWithValue("@A2", adjustment.Amount);
                command.Parameters.AddWithValue("@A3", adjustment.BaseAmount);
                command.Parameters.AddWithValue("@A4", adjustment.SequenceOrder);

                command.Parameters.AddWithValue("@Tag", $"MOTOR_{adjustmentType}");
                command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task SaveProposalLevelAdjustmentsAsync(
            string proposalNo,
            MotorProposalAdjustmentsResponseDto adjustments,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            // Save Other Discount
            if (adjustments.OtherDiscountRate > 0)
            {
                var sql = @"
                INSERT INTO PolicyPremDetails (
                    PolicyNo, ENDTNum, Section, ContentDesc, SMICode,
                    Field1, Field2, Field3,
                    A1, A2, A3,
                    Tag, TransGUID, SubmitBy, SubmitOn
                ) VALUES (
                    @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode,
                    @Field1, @Field2, @Field3,
                    @A1, @A2, @A3,
                    @Tag, @TransGUID, @SubmitBy, @SubmitOn
                )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@ENDTNum", "PROPOSAL");
                command.Parameters.AddWithValue("@Section", "MOTOR");
                command.Parameters.AddWithValue("@ContentDesc", "OTHER DISCOUNT");
                command.Parameters.AddWithValue("@SMICode", "PROPOSAL_DISCOUNT");

                command.Parameters.AddWithValue("@Field1", "OTHER DISCOUNT");
                command.Parameters.AddWithValue("@Field2", adjustments.OtherDiscountFormula);
                command.Parameters.AddWithValue("@Field3", "Premium");

                command.Parameters.AddWithValue("@A1", adjustments.OtherDiscountRate);
                command.Parameters.AddWithValue("@A2", adjustments.OtherDiscountAmount);
                command.Parameters.AddWithValue("@A3", adjustments.AggregatePremium);

                command.Parameters.AddWithValue("@Tag", "MOTOR_PROPOSAL_ADJUSTMENT");
                command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }

            // Save Other Loading
            if (adjustments.OtherLoadingRate > 0)
            {
                var sql = @"
                INSERT INTO PolicyPremDetails (
                    PolicyNo, ENDTNum, Section, ContentDesc, SMICode,
                    Field1, Field2, Field3,
                    A1, A2, A3,
                    Tag, TransGUID, SubmitBy, SubmitOn
                ) VALUES (
                    @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode,
                    @Field1, @Field2, @Field3,
                    @A1, @A2, @A3,
                    @Tag, @TransGUID, @SubmitBy, @SubmitOn
                )";

                using var command = new SqlCommand(sql, connection, transaction);

                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@ENDTNum", "PROPOSAL");
                command.Parameters.AddWithValue("@Section", "MOTOR");
                command.Parameters.AddWithValue("@ContentDesc", "OTHER LOADING");
                command.Parameters.AddWithValue("@SMICode", "PROPOSAL_LOADING");

                command.Parameters.AddWithValue("@Field1", "OTHER LOADING");
                command.Parameters.AddWithValue("@Field2", adjustments.OtherLoadingFormula);
                command.Parameters.AddWithValue("@Field3", "Premium");

                command.Parameters.AddWithValue("@A1", adjustments.OtherLoadingRate);
                command.Parameters.AddWithValue("@A2", adjustments.OtherLoadingAmount);
                command.Parameters.AddWithValue("@A3", adjustments.PremiumAfterDiscount);

                command.Parameters.AddWithValue("@Tag", "MOTOR_PROPOSAL_ADJUSTMENT");
                command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }
        }
        private async Task SaveVehicleToPolicyTempDetailsAsync(
            string proposalNo,
            CalculatedMotorRiskItemDto vehicle,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            var sql = @"
            INSERT INTO PolicyTempDetails (
                PolicyNo, ENDTNum, SectionID, ContentSection,
                Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9,
                Field17, Field18, Field23, Field24,
                RiskSum, SectionPremium, OtherSum,
                A1, A2, A3, A12, A13, A17, A18,
                Tag, TransGUID, SubmitBy, SubmitOn
            ) VALUES (
                @PolicyNo, @ENDTNum, @SectionID, @ContentSection,
                @Field1, @Field2, @Field3, @Field4, @Field5, @Field6, @Field7, @Field8, @Field9,
                @Field17, @Field18, @Field23, @Field24,
                @RiskSum, @SectionPremium, @OtherSum,
                @A1, @A2, @A3, @A12, @A13, @A17, @A18,
                @Tag, @TransGUID, @SubmitBy, @SubmitOn
            )";

            using var command = new SqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@ENDTNum", vehicle.CertificateNo);
            command.Parameters.AddWithValue("@SectionID", "MOTOR");
            command.Parameters.AddWithValue("@ContentSection", vehicle.VehicleUser ?? "");

            command.Parameters.AddWithValue("@Field1", vehicle.VehicleRegNo ?? "");
            command.Parameters.AddWithValue("@Field2", vehicle.VehicleType ?? "");
            command.Parameters.AddWithValue("@Field3", vehicle.State ?? "");
            command.Parameters.AddWithValue("@Field4", vehicle.Color ?? "");
            command.Parameters.AddWithValue("@Field5", vehicle.ModelYear.ToString());
            command.Parameters.AddWithValue("@Field6", vehicle.SeatCapacity.ToString());
            command.Parameters.AddWithValue("@Field7", vehicle.EngineNo ?? "");
            command.Parameters.AddWithValue("@Field8", vehicle.ChassisNo ?? "");
            command.Parameters.AddWithValue("@Field9", vehicle.Usage ?? "");
            command.Parameters.AddWithValue("@Field17", vehicle.CoverType ?? "");
            command.Parameters.AddWithValue("@Field18", vehicle.WaxCode ?? "");
            command.Parameters.AddWithValue("@Field23", vehicle.VehicleMake ?? "");
            command.Parameters.AddWithValue("@Field24", vehicle.VehicleModel ?? "");

            command.Parameters.AddWithValue("@RiskSum", vehicle.ShareValue);
            command.Parameters.AddWithValue("@SectionPremium", vehicle.NetPremium);
            command.Parameters.AddWithValue("@OtherSum", vehicle.VehicleValue);

            command.Parameters.AddWithValue("@A1", vehicle.VehicleValue);
            command.Parameters.AddWithValue("@A2", vehicle.TrackingCost);
            command.Parameters.AddWithValue("@A3", vehicle.RescueCost);
            command.Parameters.AddWithValue("@A12", vehicle.TotalDiscountsAmount);
            command.Parameters.AddWithValue("@A13", vehicle.TotalLoadingsAmount);
            command.Parameters.AddWithValue("@A17", vehicle.PremiumRate);
            command.Parameters.AddWithValue("@A18", vehicle.BasicPremium);

            command.Parameters.AddWithValue("@Tag", "MOTOR_VEHICLE");
            command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
            command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

            await command.ExecuteNonQueryAsync();
        }
        private async Task UpdatePolicyDetailsWithMotorCalculationAsync(
            MotorCalculationResponseDto calculation,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            var sql = @"
            UPDATE PolicyDetails SET 
                BizOption = 'CALCULATED',
                SumInsured = @SumInsured,
                GrossPremium = @GrossPremium,
                NetAmount = @NetAmount,
                ProRataPremium = @ProRataPremium,
                TotalRiskValue = @TotalRiskValue,
                SumInsuredFrgn = @SumInsuredFrgn,
                GrossPremiumFrgn = @GrossPremiumFrgn,
                ModifiedBy = 'SYSTEM',
                ModifiedOn = @ModifiedOn
            WHERE PolicyNo = @PolicyNo AND BizOption IN ('PROPOSAL', 'CALCULATED')";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@SumInsured", calculation.ShareSumInsured);
            command.Parameters.AddWithValue("@GrossPremium", calculation.TotalGrossPremium);
            command.Parameters.AddWithValue("@NetAmount", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@ProRataPremium", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@TotalRiskValue", calculation.TotalVehicleValue);
            command.Parameters.AddWithValue("@SumInsuredFrgn", calculation.ForeignSumInsured);
            command.Parameters.AddWithValue("@GrossPremiumFrgn", calculation.ForeignPremium);
            command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
            command.Parameters.AddWithValue("@PolicyNo", calculation.ProposalNo);

            await command.ExecuteNonQueryAsync();
        }

        private async Task UpdateProposalWithMotorTotalsAsync(
       MotorCalculationResponseDto calculation,
       SqlConnection connection,
       SqlTransaction transaction)
        {
            var sql = @"
        UPDATE Policies SET 
            SumInsured = @SumInsured,
            GrossPremium = @GrossPremium,
            SumInsuredFrgn = @SumInsuredFrgn,
            GrossPremiumFrgn = @GrossPremiumFrgn,
            ProRataPremium = @ProRataPremium,
            TransSTATUS = 'CALCULATED',
            ModifiedBy = 'SYSTEM',
            ModifiedOn = @ModifiedOn
        WHERE PolicyNo = @ProposalNo";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@SumInsured", calculation.TotalVehicleValue);
            command.Parameters.AddWithValue("@GrossPremium", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@SumInsuredFrgn", calculation.ForeignSumInsured);
            command.Parameters.AddWithValue("@GrossPremiumFrgn", calculation.ForeignPremium);
            command.Parameters.AddWithValue("@ProRataPremium", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
            command.Parameters.AddWithValue("@ProposalNo", calculation.ProposalNo);

            await command.ExecuteNonQueryAsync();
        }


        private async Task SaveSingleAdjustmentAsync(
    string proposalNo,
    string certificateNo,
    string adjustmentName,
    string adjustmentType,
    decimal rate,
    decimal amount,
    int sequence,
    SqlConnection connection,
    SqlTransaction transaction)
        {
            var sql = @"
    INSERT INTO PolicyPremDetails (
        PolicyNo, ENDTNum, Section, ContentDesc, SMICode,
        Field1, Field2, Field3, Field4,
        A1, A2, A3,
        Tag, TransGUID, SubmitBy, SubmitOn
    ) VALUES (
        @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode,
        @Field1, @Field2, @Field3, @Field4,
        @A1, @A2, @A3,
        @Tag, @TransGUID, @SubmitBy, @SubmitOn
    )";

            using var command = new SqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@ENDTNum", certificateNo);
            command.Parameters.AddWithValue("@Section", "MOTOR");
            command.Parameters.AddWithValue("@ContentDesc", adjustmentName);
            command.Parameters.AddWithValue("@SMICode", adjustmentType);

            command.Parameters.AddWithValue("@Field1", adjustmentName);
            command.Parameters.AddWithValue("@Field2", adjustmentType);
            command.Parameters.AddWithValue("@Field3", sequence.ToString());
            command.Parameters.AddWithValue("@Field4", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            command.Parameters.AddWithValue("@A1", rate);
            command.Parameters.AddWithValue("@A2", amount);
            command.Parameters.AddWithValue("@A3", sequence);

            command.Parameters.AddWithValue("@Tag", $"MOTOR_{adjustmentType}");
            command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
            command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Get complete calculation breakdown showing all inputs and results
        /// </summary>
        public async Task<MotorCalculationBreakdownDto> GetMotorCalculationBreakdownAsync(string proposalNo)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                // Get calculation summary from PolicyDetails
                var calculationSql = @"
                SELECT TOP 1 * FROM PolicyDetails 
                WHERE PolicyNo = @PolicyNo  AND BizOption IN ('CALCULATED', 'RECALCULATION', 'SUMMARY')
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

                // Get vehicles with calculations
                var vehicles = await GetCalculatedVehiclesBreakdownAsync(proposalNo, connection);

                if (!vehicles.Any())
                {
                    return null;
                }

                // Get proposal-level adjustments
                var proposalAdjustments = await GetProposalLevelAdjustmentsBreakdownAsync(proposalNo, connection);

                // Calculate aggregations
                var vehicleAggregation = new VehicleAggregationDto
                {
                    TotalVehicles = vehicles.Count,
                    TotalVehicleValue = vehicles.Sum(v => v.VehicleValue),
                    TotalNetPremium = vehicles.Sum(v => v.Step4_FinalPremium.ResultingAmount),
                    VehicleSummaries = vehicles.Select(v => new VehicleSummaryDto
                    {
                        ItemNo = v.ItemNo,
                        VehicleRegNo = v.VehicleRegNo,
                        VehicleValue = v.VehicleValue,
                        NetPremium = v.Step4_FinalPremium.ResultingAmount
                    }).ToList()
                };

                return new MotorCalculationBreakdownDto
                {
                    ProposalNo = proposalNo,
                    CalculatedOn = calculation.SubmittedOn,
                    CalculatedBy = calculation.SubmittedBy ?? "Unknown",

                    Inputs = new MotorCalculationInputsDto
                    {
                        ProportionRate = calculation.ProportionRate,
                        ExchangeRate = calculation.ExRate,
                        Currency = calculation.ExCurrency ?? "NAIRA",
                        CoverDays = calculation.ProRataDays > 0 ? calculation.ProRataDays : 365,
                        VehicleCount = vehicles.Count,
                        OtherDiscountRate = proposalAdjustments?.OtherDiscountRate ?? 0,
                        OtherLoadingRate = proposalAdjustments?.OtherLoadingRate ?? 0
                    },

                    VehicleCalculations = vehicles,
                    VehicleAggregation = vehicleAggregation,
                    ProposalAdjustments = proposalAdjustments,

                    FinalResults = new MotorCalculationResultsDto
                    {
                        TotalVehicleValue = calculation.TotalRiskValue,
                        TotalNetPremium = vehicleAggregation.TotalNetPremium,
                        NetPremiumAfterProposalAdjustments = proposalAdjustments?.NetPremiumDue ?? vehicleAggregation.TotalNetPremium,
                        ProRataPremium = calculation.ProRataPremium,
                        ShareSumInsured = calculation.SumInsured,
                        SharePremium = calculation.NetAmount,
                        ForeignSumInsured = calculation.SumInsuredFrgn,
                        ForeignPremium = calculation.GrossPremiumFrgn,
                        ForeignCurrency = calculation.ExCurrency,
                        FinalPremiumDue = calculation.NetAmount
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting motor calculation breakdown for {ProposalNo}", proposalNo);
                throw;
            }
        }

        private async Task<List<VehicleCalculationBreakdownDto>> GetCalculatedVehiclesBreakdownAsync(
            string proposalNo, SqlConnection connection)
        {
            var vehicles = new List<VehicleCalculationBreakdownDto>();

            // Get vehicles from PolicyTempDetails
            var vehicleSql = @"
        SELECT ENDTNum, ContentSection, SectionID, Field20,
               Field1, Field2, Field3, Field4, Field5, Field6,
               Field7, Field8, Field9, Field17, Field18, Field23, Field24,
               A1, A2, A3, A17, A18
        FROM PolicyTempDetails 
        WHERE PolicyNo = @PolicyNo
        AND (Field20 = @CertOrDocNo OR SectionID = @CertOrDocNo)
        AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'
        -- Field20 = cert matches this API's rows, the legacy page AND the mobile
        -- backend (verified row: SectionID = cover type e.g. 'COMPREHENSIVE',
        -- Field20 = cert). SectionID = cert catches writers that only set SectionID.
        -- NO SectionID shape guard: it was excluding real vehicle rows.
        ORDER BY ENDTNum";


            using var vehicleCommand = new SqlCommand(vehicleSql, connection);
            vehicleCommand.Parameters.AddWithValue("@PolicyNo", proposalNo);

            int itemNo = 1;
            using var reader = await vehicleCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var certificateNo = reader.GetStringOrDefault("ENDTNum");
                var vehicleValue = reader.GetValueOrDefault<decimal>("A1");
                var premiumRate = reader.GetValueOrDefault<decimal>("A17");
                var basicPremium = reader.GetValueOrDefault<decimal>("A18");
                var totalDiscounts = reader.GetValueOrDefault<decimal>("A12");
                var totalLoadings = reader.GetValueOrDefault<decimal>("A13");
                var trackingCost = reader.GetValueOrDefault<decimal>("A2");
                var rescueCost = reader.GetValueOrDefault<decimal>("A3");

                var vehicle = new VehicleCalculationBreakdownDto
                {
                    ItemNo = itemNo++,
                    CertificateNo = reader.GetStringOrDefault("ENDTNum"),
                    VehicleUser = reader.GetStringOrDefault("ContentSection"),
                    VehicleRegNo = reader.GetStringOrDefault("Field1"),
                    VehicleType = reader.GetStringOrDefault("Field2"),
                    State = reader.GetStringOrDefault("Field3"),
                    Color = reader.GetStringOrDefault("Field4"),
                    ModelYear = int.TryParse(reader.GetStringOrDefault("Field5"), out var yr) ? yr : 0,
                    SeatCapacity = int.TryParse(reader.GetStringOrDefault("Field6"), out var sc) ? sc : 0,
                    EngineNo = reader.GetStringOrDefault("Field7"),
                    ChassisNo = reader.GetStringOrDefault("Field8"),
                    Usage = reader.GetStringOrDefault("Field9"),
                    // Mobile rows keep the cover type in SectionID ('COMPREHENSIVE' etc.)
                    CoverType = reader.GetStringOrDefault("Field17") is { Length: > 0 } ct ? ct
                        : (reader.GetStringOrDefault("SectionID") is { Length: > 0 } sid
                           && sid != "MOTOR"
                           && sid != reader.GetStringOrDefault("Field20") ? sid : ""),
                    WaxCode = reader.GetStringOrDefault("Field18"),
                    VehicleMake = reader.GetStringOrDefault("Field23"),
                    VehicleModel = reader.GetStringOrDefault("Field24"),
                    VehicleValue = vehicleValue,
                    PremiumRate = premiumRate,

                    // Step 1: Basic Premium
                    Step1_BasicPremium = new CalculationStepDto
                    {
                        StepName = "Basic Premium",
                        StartingAmount = vehicleValue,
                        ResultingAmount = basicPremium,
                        Formula = $"₦{vehicleValue:N2} × {premiumRate}% = ₦{basicPremium:N2}",
                        Adjustments = new List<AdjustmentDetailDto>()
                    }
                };

                vehicles.Add(vehicle);
            }
            reader.Close();

            // Get discounts, loadings, and build complete breakdown for each vehicle
            foreach (var vehicle in vehicles)
            {
                // Get discounts
                var discounts = await GetVehicleAdjustmentsAsync(
                    proposalNo, vehicle.CertificateNo, "DISCOUNT", connection);

                var premiumAfterDiscounts = vehicle.Step1_BasicPremium.ResultingAmount - discounts.Sum(d => d.Amount);

                vehicle.Step2_AfterDiscounts = new CalculationStepDto
                {
                    StepName = "After Discounts",
                    StartingAmount = vehicle.Step1_BasicPremium.ResultingAmount,
                    Adjustments = discounts,
                    TotalAdjustment = -discounts.Sum(d => d.Amount),
                    ResultingAmount = premiumAfterDiscounts,
                    Formula = $"₦{vehicle.Step1_BasicPremium.ResultingAmount:N2} - ₦{discounts.Sum(d => d.Amount):N2} = ₦{premiumAfterDiscounts:N2}"
                };

                // Get loadings
                var loadings = await GetVehicleAdjustmentsAsync(
                    proposalNo, vehicle.CertificateNo, "LOADING", connection);

                var grossPremium = premiumAfterDiscounts + loadings.Sum(l => l.Amount);

                vehicle.Step3_AfterLoadings = new CalculationStepDto
                {
                    StepName = "After Loadings",
                    StartingAmount = premiumAfterDiscounts,
                    Adjustments = loadings,
                    TotalAdjustment = loadings.Sum(l => l.Amount),
                    ResultingAmount = grossPremium,
                    Formula = $"₦{premiumAfterDiscounts:N2} + ₦{loadings.Sum(l => l.Amount):N2} = ₦{grossPremium:N2}"
                };

                // Get additional costs from database
                var costsSql = @"
                SELECT A2, A3 FROM PolicyTempDetails 
                WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo
                AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'";

                decimal trackingCost = 0;
                decimal rescueCost = 0;

                using (var command = new SqlCommand(costsSql, connection))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@CertificateNo", vehicle.CertificateNo);
                    using var costReader = await command.ExecuteReaderAsync();
                    if (await costReader.ReadAsync())
                    {
                        trackingCost = costReader.GetValueOrDefault<decimal>("A2");
                        rescueCost = costReader.GetValueOrDefault<decimal>("A3");
                    }
                }

                var netPremium = grossPremium + trackingCost + rescueCost;

                var additionalCosts = new List<AdjustmentDetailDto>();
                if (trackingCost > 0)
                {
                    additionalCosts.Add(new AdjustmentDetailDto
                    {
                        Name = "Tracking Cost",
                        Type = "COST",
                        Amount = trackingCost,
                        Formula = $"₦{trackingCost:N2}"
                    });
                }
                if (rescueCost > 0)
                {
                    additionalCosts.Add(new AdjustmentDetailDto
                    {
                        Name = "Rescue Cost",
                        Type = "COST",
                        Amount = rescueCost,
                        Formula = $"₦{rescueCost:N2}"
                    });
                }

                vehicle.Step4_FinalPremium = new CalculationStepDto
                {
                    StepName = "Final Premium",
                    StartingAmount = grossPremium,
                    Adjustments = additionalCosts,
                    TotalAdjustment = trackingCost + rescueCost,
                    ResultingAmount = netPremium,
                    Formula = $"₦{grossPremium:N2} + ₦{trackingCost + rescueCost:N2} = ₦{netPremium:N2}"
                };
            }

            return vehicles;
        }

        private async Task<ProposalAdjustmentBreakdownDto> GetProposalLevelAdjustmentsBreakdownAsync(
            string proposalNo, SqlConnection connection)
        {
            var sql = @"
            SELECT ContentDesc, SMICode, Field1, Field2, A1, A2, A3
            FROM PolicyPremDetails
            WHERE PolicyNo = @PolicyNo AND ENDTNum = 'PROPOSAL'
            AND Tag = 'MOTOR_PROPOSAL_ADJUSTMENT'
            ORDER BY SMICode";

            decimal otherDiscountRate = 0;
            decimal otherDiscountAmount = 0;
            string otherDiscountFormula = "";
            decimal aggregatePremium = 0;

            decimal otherLoadingRate = 0;
            decimal otherLoadingAmount = 0;
            string otherLoadingFormula = "";
            decimal premiumAfterDiscount = 0;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var smiCode = reader.GetStringOrDefault("SMICode");
                if (smiCode == "PROPOSAL_DISCOUNT")
                {
                    otherDiscountRate = reader.GetValueOrDefault<decimal>("A1");
                    otherDiscountAmount = reader.GetValueOrDefault<decimal>("A2");
                    aggregatePremium = reader.GetValueOrDefault<decimal>("A3");
                    otherDiscountFormula = reader.GetStringOrDefault("Field2");
                }
                else if (smiCode == "PROPOSAL_LOADING")
                {
                    otherLoadingRate = reader.GetValueOrDefault<decimal>("A1");
                    otherLoadingAmount = reader.GetValueOrDefault<decimal>("A2");
                    premiumAfterDiscount = reader.GetValueOrDefault<decimal>("A3");
                    otherLoadingFormula = reader.GetStringOrDefault("Field2");
                }
            }

            // If no proposal adjustments found, return null
            if (otherDiscountRate == 0 && otherLoadingRate == 0)
            {
                return null;
            }

            // Calculate net premium
            if (aggregatePremium == 0)
            {
                aggregatePremium = premiumAfterDiscount + otherDiscountAmount;
            }
            if (premiumAfterDiscount == 0)
            {
                premiumAfterDiscount = aggregatePremium - otherDiscountAmount;
            }

            var netPremiumDue = premiumAfterDiscount + otherLoadingAmount;

            return new ProposalAdjustmentBreakdownDto
            {
                AggregatePremium = aggregatePremium,
                OtherDiscountRate = otherDiscountRate,
                OtherDiscountAmount = otherDiscountAmount,
                OtherDiscountFormula = otherDiscountFormula,
                PremiumAfterDiscount = premiumAfterDiscount,
                OtherLoadingRate = otherLoadingRate,
                OtherLoadingAmount = otherLoadingAmount,
                OtherLoadingFormula = otherLoadingFormula,
                NetPremiumDue = netPremiumDue
            };
        }

        private async Task<List<AdjustmentDetailDto>> GetVehicleAdjustmentsAsync(
            string proposalNo, string certificateNo, string adjustmentType, SqlConnection connection)
        {
            var adjustments = new List<AdjustmentDetailDto>();

            var sql = @"
            SELECT ContentDesc, SMICode, Field1, Field2, Field3, Field4, A1, A2, A3, A4
            FROM PolicyPremDetails
            WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo 
            AND Tag = @Tag AND SMICode = @AdjustmentType
            ORDER BY A4";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@CertificateNo", certificateNo);
            command.Parameters.AddWithValue("@Tag", $"MOTOR_{adjustmentType}");
            command.Parameters.AddWithValue("@AdjustmentType", adjustmentType);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                adjustments.Add(new AdjustmentDetailDto
                {
                    Name = reader.GetStringOrDefault("ContentDesc"),
                    Type = adjustmentType,
                    Rate = reader.GetValueOrDefault<decimal>("A1"),
                    Amount = reader.GetValueOrDefault<decimal>("A2"),
                    SequenceOrder = reader.GetValueOrDefault<int>("A4"),
                    Formula = reader.GetStringOrDefault("Field4")
                });
            }

            return adjustments;
        }


        /// <summary>
        /// Delete a motor vehicle from calculation
        /// </summary>
        public async Task<ResponseInfo> DeleteMotorVehicleAsync(string proposalNo, string certificateNo)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Delete vehicle from PolicyTempDetails
                var deleteVehicleSql = @"
        UPDATE PolicyTempDetails SET Deleted = 1
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo
        AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'";

                using (var command = new SqlCommand(deleteVehicleSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@CertificateNo", certificateNo);
                    await command.ExecuteNonQueryAsync();
                }

                // Delete associated discounts and loadings
                var deleteAdjustmentsSql = @"
        UPDATE PolicyPremDetails SET Deleted = 1
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo 
        AND Tag IN ('MOTOR_DISCOUNT', 'MOTOR_LOADING')";

                using (var command = new SqlCommand(deleteAdjustmentsSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@CertificateNo", certificateNo);
                    await command.ExecuteNonQueryAsync();
                }

                // Recalculate totals
                await RecalculateMotorTotalsAsync(proposalNo, connection, transaction);

                await transaction.CommitAsync();

                return new ResponseInfo
                {
                    ErrorCode = 0,
                    ErrorMessage = "Vehicle deleted successfully",
                    TotalSuccess = 1
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting motor vehicle");
                return new ResponseInfo
                {
                    ErrorCode = 1,
                    ErrorMessage = "Failed to delete vehicle",
                    ExtraMessage = ex.Message
                };
            }
        }

        ///// <summary>
        ///// Add a single vehicle to existing motor calculation
        ///// </summary>
        //public async Task<CalculatedMotorRiskItemDto> AddMotorVehicleAsync(
        //    string proposalNo, AddMotorVehicleRequestDto request)
        //{
        //    using var connection = new SqlConnection(_connectionString);
        //    await connection.OpenAsync();
        //    using var transaction = connection.BeginTransaction();

        //    try
        //    {
        //        // Get proposal details for calculation parameters
        //        var proposal = await GetProposalAsync(proposalNo);
        //        if (proposal == null)
        //        {
        //            return null;
        //        }

        //        // Calculate the single vehicle
        //        var calculationRequest = new MotorCalculationRequestDto
        //        {
        //            ProposalNo = proposalNo,
        //            Vehicles = new List<MotorRiskItemInputDto> { request.Vehicle },
        //            ProportionRate = request.ProportionRate
        //        };

        //        var calculated = await CalculateSingleVehicleAsync(request.Vehicle, calculationRequest);

        //        //// Save to database
        //        //await SaveVehicleToPolicyTempDetailsAsync(proposalNo, calculated, connection, transaction);
        //        //await SaveVehicleDiscountsAsync(proposalNo, calculated, connection, transaction);
        //        //await SaveVehicleLoadingsAsync(proposalNo, calculated, connection, transaction);

        //        // Recalculate totals
        //        await RecalculateMotorTotalsAsync(proposalNo, connection, transaction);

        //        await transaction.CommitAsync();

        //        return calculated;
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, "Error adding motor vehicle");
        //        return null;
        //    }
        //}

        ///// <summary>
        ///// Update a specific vehicle
        ///// </summary>
        //public async Task<CalculatedMotorRiskItemDto> UpdateMotorVehicleAsync(
        //    string proposalNo, string certificateNo, MotorRiskItemInputDto request)
        //{
        //    using var connection = new SqlConnection(_connectionString);
        //    await connection.OpenAsync();
        //    using var transaction = connection.BeginTransaction();

        //    try
        //    {
        //        // Delete existing vehicle
        //        await DeleteMotorVehicleAsync(proposalNo, certificateNo);

        //        // Add updated vehicle
        //        var addRequest = new AddMotorVehicleRequestDto
        //        {
        //            Vehicle = request,
        //            ProportionRate = 100 // Get from proposal if needed
        //        };

        //        var result = await AddMotorVehicleAsync(proposalNo, addRequest);

        //        await transaction.CommitAsync();

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, "Error updating motor vehicle");
        //        return null;
        //    }
        //}

        /// <summary>
        /// Recalculate motor totals after adding/removing vehicles
        /// </summary>
        private async Task RecalculateMotorTotalsAsync(
       string proposalNo, SqlConnection connection, SqlTransaction transaction)
        {
            // Get all active vehicles
            var vehiclesSql = @"
        SELECT OtherSum, SectionPremium
        FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo AND SectionID = 'MOTOR'";

            decimal totalValue = 0;
            decimal totalPremium = 0;

            using (var command = new SqlCommand(vehiclesSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalValue += reader.GetValueOrDefault<decimal>("OtherSum");
                    totalPremium += reader.GetValueOrDefault<decimal>("SectionPremium");
                }
            }

            // Update PolicyDetails
            var updatePolicySql = @"
        UPDATE PolicyDetails SET 
            SumInsured = @SumInsured,
            GrossPremium = @GrossPremium,
            NetAmount = @NetAmount,
            TotalRiskValue = @TotalRiskValue,
            ModifiedOn = @ModifiedOn
        WHERE PolicyNo = @PolicyNo AND BizOption IN ('CALCULATED', 'PROPOSAL')";

            using (var command = new SqlCommand(updatePolicySql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SumInsured", totalValue);
                command.Parameters.AddWithValue("@GrossPremium", totalPremium);
                command.Parameters.AddWithValue("@NetAmount", totalPremium);
                command.Parameters.AddWithValue("@TotalRiskValue", totalValue);
                command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                await command.ExecuteNonQueryAsync();
            }

            // Update Policies (changed from Proposals)
            var updatePolicySummarySql = @"
        UPDATE Policies SET 
            SumInsured = @SumInsured,
            GrossPremium = @GrossPremium,
            ModifiedOn = @ModifiedOn
        WHERE PolicyNo = @ProposalNo";

            using (var command = new SqlCommand(updatePolicySummarySql, connection, transaction))
            {
                command.Parameters.AddWithValue("@SumInsured", totalValue);
                command.Parameters.AddWithValue("@GrossPremium", totalPremium);
                command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                command.Parameters.AddWithValue("@ProposalNo", proposalNo);
                await command.ExecuteNonQueryAsync();
            }
        }


        // ============================================================
        // MOTOR MULTI-CALCULATION (CertOrDocNo-scoped) — mirrors Fire
        // ============================================================

        /// <summary>
        /// Create an ADDITIONAL motor calculation. Generates a new CertOrDocNo and
        /// INSERTS a new PolicyDetails row, leaving prior calculations intact.
        /// </summary>
        public async Task<MotorCalculationResponseDto> CreateAdditionalMotorCalculationAsync(
            MotorCalculationRequestDto request)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                if (!await ProposalExistsAsync(request.ProposalNo))
                {
                    return new MotorCalculationResponseDto
                    {
                        ProposalNo = request.ProposalNo,
                        Success = false,
                        Message = "Proposal not found"
                    };
                }

                var proposal = await GetProposalAsync(request.ProposalNo);
                // LEGACY: DECLARATION_NO auto number (branch + subrisk + endorse type),
                // as in the legacy motor InsertIntoPolicyDetails.
                var certOrDocNo = await NextCalcCertAsync(
                    proposal.BranchID, proposal.SubRiskID, MapEndorseType("Additional"));

                var calculation = await CalculateMotorCompleteAsync(request, connection, transaction);
                if (!calculation.Success)
                {
                    await transaction.RollbackAsync();
                    return calculation;
                }

                await PersistMotorCalculationWithCertAsync(calculation, certOrDocNo, connection, transaction);

                await CreateNewMotorPolicyDetailsRecordAsync(
                    request.ProposalNo, certOrDocNo, calculation, request, connection, transaction);

                await transaction.CommitAsync();

                calculation.CertOrDocNo = certOrDocNo;
                calculation.Message = "Additional motor calculation created successfully";
                return calculation;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating additional motor calculation for {ProposalNo}", request.ProposalNo);
                return new MotorCalculationResponseDto
                {
                    ProposalNo = request.ProposalNo,
                    Success = false,
                    Message = "Calculation failed"
                };
            }
        }

        /// <summary>
        /// Update/recalculate a specific motor calculation by DetailID.
        /// Deletes only that calculation's data (scoped by CertOrDocNo) and rewrites it.
        /// </summary>
        public async Task<MotorCalculationResponseDto> UpdateMotorCalculationAsync(
            UpdateMotorCalculationRequestDto request)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                string proposalNo = null;
                string certOrDocNo = null;

                var lookupSql = @"
            SELECT PolicyNo, CertOrDocNo 
            FROM PolicyDetails 
            WHERE DetailID = @DetailID AND Deleted = 0";

                using (var cmd = new SqlCommand(lookupSql, connection, transaction))
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
                    return new MotorCalculationResponseDto
                    {
                        Success = false,
                        Message = $"Motor calculation {request.DetailID} not found"
                    };
                }

                await DeleteMotorCalculationDataByCertOrDocNoAsync(proposalNo, certOrDocNo, connection, transaction);

                var calcRequest = new MotorCalculationRequestDto
                {
                    ProposalNo = proposalNo,
                    Vehicles = request.Vehicles,
                    OtherDiscountRate = request.OtherDiscountRate,
                    OtherLoadingRate = request.OtherLoadingRate,
                    ProportionRate = request.ProportionRate,
                    ExchangeRate = request.ExchangeRate,
                    Currency = request.Currency,
                    CoverDays = request.CoverDays,
                    CalculatedBy = request.RecalculatedBy
                };

                var calculation = await CalculateMotorCompleteAsync(calcRequest, connection, transaction);
                if (!calculation.Success)
                {
                    await transaction.RollbackAsync();
                    return calculation;
                }

                await PersistMotorCalculationWithCertAsync(calculation, certOrDocNo, connection, transaction);

                await UpdateExistingMotorPolicyDetailsRecordAsync(
                    request.DetailID, calculation, calcRequest, request.Remarks, connection, transaction);

                await transaction.CommitAsync();

                calculation.CertOrDocNo = certOrDocNo;
                calculation.Message = "Motor calculation updated successfully";
                return calculation;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating motor calculation {DetailID}", request.DetailID);
                return new MotorCalculationResponseDto { Success = false, Message = "Update failed" };
            }
        }

        /// <summary>
        /// List all motor calculations (PolicyDetails rows) for a policy.
        /// </summary>
        public async Task<List<MotorCalculationListItemDto>> GetAllMotorCalculationsAsync(string proposalNo)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var calculations = new List<MotorCalculationListItemDto>();

            var sql = @"
        SELECT 
            pd.DetailID, pd.PolicyNo, pd.CertOrDocNo,
            pd.SubmittedOn AS CalculatedOn, pd.SubmittedBy AS CalculatedBy,
            pd.BizOption AS CalculationType,
            pd.TotalRiskValue AS TotalSumInsured,
            pd.GrossPremium AS TotalPremium,
            pd.GrossPremium AS NetPremium,
            pd.ProRataPremium,
            pd.NetAmount AS FinalPremiumDue,
            pd.Active, pd.Deleted, pd.Field1 AS Remarks,
            (SELECT COUNT(*) 
             FROM PolicyTempDetails 
             WHERE PolicyNo = pd.PolicyNo 
               AND (Field20 = pd.CertOrDocNo OR SectionID = pd.CertOrDocNo)
               AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS') AS VehicleCount
        FROM PolicyDetails pd
        WHERE pd.PolicyNo = @ProposalNo
        ORDER BY pd.SubmittedOn DESC";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ProposalNo", proposalNo);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new MotorCalculationListItemDto
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
                    VehicleCount = reader.GetValueOrDefault<int>("VehicleCount"),
                    Remarks = reader.GetStringOrDefault("Remarks")
                };

                item.Status = reader.GetValueOrDefault<int>("Deleted") == 1 ? "DELETED"
                            : reader.GetValueOrDefault<int>("Active") == 1 ? "ACTIVE"
                            : "SUPERSEDED";

                calculations.Add(item);
            }

            return calculations;
        }
        private async Task PersistMotorCalculationWithCertAsync(
            MotorCalculationResponseDto calculation,
            string certOrDocNo,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            // LEGACY: vehicle certificates are CERTNO auto numbers keyed by branch,
            // exactly like getNextAutoNumber("AUTO", CERTNO, P.BranchID) in the
            // legacy motor module. Resolve the branch once for the whole batch.
            var branchId = await GetBranchIdAsync(calculation.ProposalNo, connection, transaction);

            foreach (var vehicle in calculation.CalculatedVehicles)
            {
                // LEGACY: every vehicle gets its own CERTNO. The '{PolicyNo}/CERT/000001'
                // value produced at calculation time is only a preview placeholder — the
                // real auto number is issued HERE at save time, so previews never burn
                // numbers. A caller-supplied cert that is NOT a placeholder is kept:
                // the legacy screen also allows manually entered certificate numbers
                // (CdbVehNumType <> "AUTO"), and updates re-use the existing cert.
                if (string.IsNullOrWhiteSpace(vehicle.CertificateNo) ||
                    vehicle.CertificateNo.Contains("/CERT/"))
                {
                    vehicle.CertificateNo = await NextItemCertNoAsync(branchId);
                }

                await SaveMotorVehicleWithCertAsync(
                    calculation.ProposalNo, certOrDocNo, vehicle, connection, transaction);

                await SaveMotorVehicleAdjustmentsWithCertAsync(
                    calculation.ProposalNo, certOrDocNo, vehicle.CertificateNo,
                    vehicle.Step2_Discounts.DiscountsApplied, "DISCOUNT", connection, transaction);

                await SaveMotorVehicleAdjustmentsWithCertAsync(
                    calculation.ProposalNo, certOrDocNo, vehicle.CertificateNo,
                    vehicle.Step3_Loadings.LoadingsApplied, "LOADING", connection, transaction);
            }

            if (calculation.ProposalAdjustments != null)
            {
                await SaveMotorProposalAdjustmentsWithCertAsync(
                    calculation.ProposalNo, certOrDocNo, calculation.ProposalAdjustments,
                    connection, transaction);
            }
        }

        private async Task SaveMotorVehicleWithCertAsync(
            string proposalNo,
            string certOrDocNo,
            CalculatedMotorRiskItemDto vehicle,
            SqlConnection connection,
            SqlTransaction transaction,
            string rowTag = "MOTOR_VEHICLE")
        {
            var sql = @"
        INSERT INTO PolicyTempDetails (
            PolicyNo, ENDTNum, SectionID, ContentSection, Field20,
            Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9,
            Field17, Field18, Field23, Field24,
            RiskSum, SectionPremium, OtherSum,
            A1, A2, A3, A12, A13, A17, A18,
            Tag, TransGUID, SubmitBy, SubmitOn
        ) VALUES (
            @PolicyNo, @ENDTNum, @SectionID, @ContentSection, @Field20,
            @Field1, @Field2, @Field3, @Field4, @Field5, @Field6, @Field7, @Field8, @Field9,
            @Field17, @Field18, @Field23, @Field24,
            @RiskSum, @SectionPremium, @OtherSum,
            @A1, @A2, @A3, @A12, @A13, @A17, @A18,
            @Tag, @TransGUID, @SubmitBy, @SubmitOn
        )";

            using var command = new SqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@ENDTNum", vehicle.CertificateNo);
            command.Parameters.AddWithValue("@SectionID", "MOTOR");
            command.Parameters.AddWithValue("@ContentSection", vehicle.VehicleUser ?? "");
            command.Parameters.AddWithValue("@Field20", certOrDocNo);   // LINK TO CALCULATION

            command.Parameters.AddWithValue("@Field1", vehicle.VehicleRegNo ?? "");
            command.Parameters.AddWithValue("@Field2", vehicle.VehicleType ?? "");
            command.Parameters.AddWithValue("@Field3", vehicle.State ?? "");
            command.Parameters.AddWithValue("@Field4", vehicle.Color ?? "");
            command.Parameters.AddWithValue("@Field5", vehicle.ModelYear.ToString());
            command.Parameters.AddWithValue("@Field6", vehicle.SeatCapacity.ToString());
            command.Parameters.AddWithValue("@Field7", vehicle.EngineNo ?? "");
            command.Parameters.AddWithValue("@Field8", vehicle.ChassisNo ?? "");
            command.Parameters.AddWithValue("@Field9", vehicle.Usage ?? "");
            command.Parameters.AddWithValue("@Field17", vehicle.CoverType ?? "");
            command.Parameters.AddWithValue("@Field18", vehicle.WaxCode ?? "");
            command.Parameters.AddWithValue("@Field23", vehicle.VehicleMake ?? "");
            command.Parameters.AddWithValue("@Field24", vehicle.VehicleModel ?? "");

            command.Parameters.AddWithValue("@RiskSum", vehicle.ShareValue);
            command.Parameters.AddWithValue("@SectionPremium", vehicle.NetPremium);
            command.Parameters.AddWithValue("@OtherSum", vehicle.VehicleValue);

            command.Parameters.AddWithValue("@A1", vehicle.VehicleValue);
            command.Parameters.AddWithValue("@A2", vehicle.TrackingCost);
            command.Parameters.AddWithValue("@A3", vehicle.RescueCost);
            command.Parameters.AddWithValue("@A12", vehicle.TotalDiscountsAmount);
            command.Parameters.AddWithValue("@A13", vehicle.TotalLoadingsAmount);
            command.Parameters.AddWithValue("@A17", vehicle.PremiumRate);
            command.Parameters.AddWithValue("@A18", vehicle.BasicPremium);

            command.Parameters.AddWithValue("@Tag", string.IsNullOrWhiteSpace(rowTag) ? "MOTOR_VEHICLE" : rowTag);
            command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
            command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

            await command.ExecuteNonQueryAsync();
        }

        private async Task SaveMotorVehicleAdjustmentsWithCertAsync(
            string proposalNo,
            string certOrDocNo,
            string certificateNo,
            List<MotorAdjustmentDto> adjustments,
            string adjustmentType,
            SqlConnection connection,
            SqlTransaction transaction)
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
                command.Parameters.AddWithValue("@ENDTNum", certificateNo);
                command.Parameters.AddWithValue("@Section", "MOTOR");
                command.Parameters.AddWithValue("@ContentDesc", adjustment.AdjustmentName);
                command.Parameters.AddWithValue("@SMICode", adjustmentType);

                command.Parameters.AddWithValue("@Field1", adjustment.AdjustmentName);
                command.Parameters.AddWithValue("@Field2", adjustmentType);
                command.Parameters.AddWithValue("@Field3", adjustment.AppliedOn.ToString());
                command.Parameters.AddWithValue("@Field4", adjustment.Formula ?? "");
                command.Parameters.AddWithValue("@Field5", adjustment.SequenceOrder.ToString());
                command.Parameters.AddWithValue("@Field10", certOrDocNo);   // LINK TO CALCULATION

                command.Parameters.AddWithValue("@A1", adjustment.Rate);
                command.Parameters.AddWithValue("@A2", adjustment.Amount);
                command.Parameters.AddWithValue("@A3", adjustment.BaseAmount);
                command.Parameters.AddWithValue("@A4", adjustment.SequenceOrder);

                command.Parameters.AddWithValue("@Tag", $"MOTOR_{adjustmentType}");
                command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task SaveMotorProposalAdjustmentsWithCertAsync(
            string proposalNo,
            string certOrDocNo,
            MotorProposalAdjustmentsResponseDto adjustments,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            async Task InsertRow(string desc, string smiCode, string formula,
                                 decimal rate, decimal amount, decimal baseAmount)
            {
                var sql = @"
            INSERT INTO PolicyPremDetails (
                PolicyNo, ENDTNum, Section, ContentDesc, SMICode,
                Field1, Field2, Field3, Field10,
                A1, A2, A3,
                Tag, TransGUID, SubmitBy, SubmitOn
            ) VALUES (
                @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode,
                @Field1, @Field2, @Field3, @Field10,
                @A1, @A2, @A3,
                @Tag, @TransGUID, @SubmitBy, @SubmitOn
            )";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                command.Parameters.AddWithValue("@ENDTNum", "PROPOSAL");
                command.Parameters.AddWithValue("@Section", "MOTOR");
                command.Parameters.AddWithValue("@ContentDesc", desc);
                command.Parameters.AddWithValue("@SMICode", smiCode);
                command.Parameters.AddWithValue("@Field1", desc);
                command.Parameters.AddWithValue("@Field2", formula ?? "");
                command.Parameters.AddWithValue("@Field3", "Premium");
                command.Parameters.AddWithValue("@Field10", certOrDocNo);   // LINK TO CALCULATION
                command.Parameters.AddWithValue("@A1", rate);
                command.Parameters.AddWithValue("@A2", amount);
                command.Parameters.AddWithValue("@A3", baseAmount);
                command.Parameters.AddWithValue("@Tag", "MOTOR_PROPOSAL_ADJUSTMENT");
                command.Parameters.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@SubmitBy", "SYSTEM");
                command.Parameters.AddWithValue("@SubmitOn", DateTime.Now);
                await command.ExecuteNonQueryAsync();
            }

            if (adjustments.OtherDiscountRate > 0)
                await InsertRow("OTHER DISCOUNT", "PROPOSAL_DISCOUNT", adjustments.OtherDiscountFormula,
                    adjustments.OtherDiscountRate, adjustments.OtherDiscountAmount, adjustments.AggregatePremium);

            if (adjustments.OtherLoadingRate > 0)
                await InsertRow("OTHER LOADING", "PROPOSAL_LOADING", adjustments.OtherLoadingFormula,
                    adjustments.OtherLoadingRate, adjustments.OtherLoadingAmount, adjustments.PremiumAfterDiscount);
        }

        private async Task CreateNewMotorPolicyDetailsRecordAsync(
            string proposalNo,
            string certOrDocNo,
            MotorCalculationResponseDto calculation,
            MotorCalculationRequestDto request,
            SqlConnection connection,
            SqlTransaction transaction)
        {
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
            command.Parameters.AddWithValue("@BizOption", "Additional");
            command.Parameters.AddWithValue("@InsuredName", fullName);
            command.Parameters.AddWithValue("@StartDate", DateTime.Now);
            command.Parameters.AddWithValue("@EndDate", DateTime.Now.AddDays(request.CoverDays));
            command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
            command.Parameters.AddWithValue("@ExCurrency", request.Currency);
            command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);

            command.Parameters.AddWithValue("@SumInsured", calculation.ShareSumInsured);
            command.Parameters.AddWithValue("@GrossPremium", calculation.TotalGrossPremium);
            command.Parameters.AddWithValue("@SumInsuredFrgn", calculation.ForeignSumInsured);
            command.Parameters.AddWithValue("@GrossPremiumFrgn", calculation.ForeignPremium);
            command.Parameters.AddWithValue("@ProRataDays", request.CoverDays);
            command.Parameters.AddWithValue("@ProRataPremium", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@NetAmount", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@TotalRiskValue", calculation.TotalVehicleValue);

            command.Parameters.AddWithValue("@A1", calculation.TotalVehicleValue);
            command.Parameters.AddWithValue("@A2", calculation.TotalGrossPremium);
            command.Parameters.AddWithValue("@A10", request.OtherDiscountRate);
            command.Parameters.AddWithValue("@A11", request.OtherLoadingRate);
            command.Parameters.AddWithValue("@A13", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@A14", calculation.FinalPremiumDue);

            command.Parameters.AddWithValue("@Field1", "ADDITIONAL_MOTOR_CALCULATION");
            command.Parameters.AddWithValue("@Field2", $"Calculated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            command.Parameters.AddWithValue("@Field50", "MOTOR");
            command.Parameters.AddWithValue("@SubmittedBy", request.CalculatedBy);
            command.Parameters.AddWithValue("@SubmittedOn", DateTime.Now);

            await command.ExecuteNonQueryAsync();
        }

        private async Task UpdateExistingMotorPolicyDetailsRecordAsync(
            string detailID,
            MotorCalculationResponseDto calculation,
            MotorCalculationRequestDto request,
            string remarks,
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
            A1 = @A1, A2 = @A2, A10 = @A10, A11 = @A11, A13 = @A13, A14 = @A14,
            Field1 = @Field1, Field2 = @Field2,
            ModifiedBy = @ModifiedBy, ModifiedOn = @ModifiedOn
        WHERE DetailID = @DetailID";

            using var command = new SqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("@SumInsured", calculation.ShareSumInsured);
            command.Parameters.AddWithValue("@GrossPremium", calculation.TotalGrossPremium);
            command.Parameters.AddWithValue("@SumInsuredFrgn", calculation.ForeignSumInsured);
            command.Parameters.AddWithValue("@GrossPremiumFrgn", calculation.ForeignPremium);
            command.Parameters.AddWithValue("@ProRataDays", request.CoverDays);
            command.Parameters.AddWithValue("@ProRataPremium", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@NetAmount", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@TotalRiskValue", calculation.TotalVehicleValue);
            command.Parameters.AddWithValue("@ProportionRate", request.ProportionRate);
            command.Parameters.AddWithValue("@ExRate", request.ExchangeRate);
            command.Parameters.AddWithValue("@ExCurrency", request.Currency);
            command.Parameters.AddWithValue("@A1", calculation.TotalVehicleValue);
            command.Parameters.AddWithValue("@A2", calculation.TotalGrossPremium);
            command.Parameters.AddWithValue("@A10", request.OtherDiscountRate);
            command.Parameters.AddWithValue("@A11", request.OtherLoadingRate);
            command.Parameters.AddWithValue("@A13", calculation.ProRataPremium);
            command.Parameters.AddWithValue("@A14", calculation.FinalPremiumDue);
            command.Parameters.AddWithValue("@Field1", remarks ?? "RECALCULATED");
            command.Parameters.AddWithValue("@Field2", $"Recalculated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            command.Parameters.AddWithValue("@ModifiedBy", request.CalculatedBy);
            command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
            command.Parameters.AddWithValue("@DetailID", detailID);

            await command.ExecuteNonQueryAsync();
        }

        private async Task DeleteMotorCalculationDataByCertOrDocNoAsync(
            string proposalNo,
            string certOrDocNo,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            // Vehicles live in PolicyTempDetails (has Field20)
            var deleteVehiclesSql = @"
        DELETE FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo
        AND (Field20 = @CertOrDocNo OR SectionID = @CertOrDocNo)
        AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'";
            using (var cmd = new SqlCommand(deleteVehiclesSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                await cmd.ExecuteNonQueryAsync();
            }

            // Vehicle + proposal adjustments live in PolicyPremDetails (use Field10 — no Field20 there)
            var deleteAdjustmentsSql = @"
        DELETE FROM PolicyPremDetails
        WHERE PolicyNo = @PolicyNo AND Field10 = @CertOrDocNo
          AND Tag IN ('MOTOR_DISCOUNT', 'MOTOR_LOADING', 'MOTOR_PROPOSAL_ADJUSTMENT')";
            using (var cmd = new SqlCommand(deleteAdjustmentsSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                await cmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Deleted motor calculation data for CertOrDocNo {CertOrDocNo}", certOrDocNo);
        }

        /// <summary>
        /// Breakdown for ONE motor calculation, scoped by its CertOrDocNo (via DetailID).
        /// </summary>
        public async Task<MotorCalculationBreakdownDto> GetMotorCalculationBreakdownByDetailIDAsync(
            string proposalNo, string detailID)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var calcSql = @"
        SELECT * FROM PolicyDetails 
        WHERE PolicyNo = @ProposalNo AND DetailID = @DetailID AND Deleted = 0";

            PolicyDetailDto calculation = null;
            using (var command = new SqlCommand(calcSql, connection))
            {
                command.Parameters.AddWithValue("@ProposalNo", proposalNo);
                command.Parameters.AddWithValue("@DetailID", detailID);
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    calculation = MapPolicyDetailFromReader(reader);
            }

            if (calculation == null) return null;

            var vehicles = await GetCalculatedVehiclesBreakdownByCertAsync(
                proposalNo, calculation.CertOrDocNo, connection);

            // LEGACY: a calculation with no matching vehicle rows is still a valid
            // calculation. Returning null here made the API answer "calculation not
            // found" for records that clearly exist (e.g. rows written by another
            // client whose layout the vehicle query doesn't match yet).
            if (!vehicles.Any())
            {
                _logger.LogInformation(
                    "No vehicle rows found for {ProposalNo} / {CertOrDocNo} — returning breakdown with empty vehicles",
                    proposalNo, calculation.CertOrDocNo);
            }

            var proposalAdjustments = await GetMotorProposalAdjustmentsBreakdownByCertAsync(
                proposalNo, calculation.CertOrDocNo, connection);

            var vehicleAggregation = new VehicleAggregationDto
            {
                TotalVehicles = vehicles.Count,
                TotalVehicleValue = vehicles.Sum(v => v.VehicleValue),
                TotalNetPremium = vehicles.Sum(v => v.Step4_FinalPremium.ResultingAmount),
                VehicleSummaries = vehicles.Select(v => new VehicleSummaryDto
                {
                    ItemNo = v.ItemNo,
                    VehicleRegNo = v.VehicleRegNo,
                    VehicleValue = v.VehicleValue,
                    NetPremium = v.Step4_FinalPremium.ResultingAmount
                }).ToList()
            };

            return new MotorCalculationBreakdownDto
            {
                ProposalNo = proposalNo,
                CalculatedOn = calculation.SubmittedOn,
                CalculatedBy = calculation.SubmittedBy ?? "Unknown",
                Inputs = new MotorCalculationInputsDto
                {
                    ProportionRate = calculation.ProportionRate,
                    ExchangeRate = calculation.ExRate,
                    Currency = calculation.ExCurrency ?? "NAIRA",
                    CoverDays = calculation.ProRataDays > 0 ? calculation.ProRataDays : 365,
                    VehicleCount = vehicles.Count,
                    OtherDiscountRate = proposalAdjustments?.OtherDiscountRate ?? 0,
                    OtherLoadingRate = proposalAdjustments?.OtherLoadingRate ?? 0
                },
                VehicleCalculations = vehicles,
                VehicleAggregation = vehicleAggregation,
                ProposalAdjustments = proposalAdjustments,
                FinalResults = new MotorCalculationResultsDto
                {
                    TotalVehicleValue = calculation.TotalRiskValue,
                    TotalNetPremium = vehicleAggregation.TotalNetPremium,
                    NetPremiumAfterProposalAdjustments = proposalAdjustments?.NetPremiumDue ?? vehicleAggregation.TotalNetPremium,
                    ProRataPremium = calculation.ProRataPremium,
                    ShareSumInsured = calculation.SumInsured,
                    SharePremium = calculation.NetAmount,
                    ForeignSumInsured = calculation.SumInsuredFrgn,
                    ForeignPremium = calculation.GrossPremiumFrgn,
                    ForeignCurrency = calculation.ExCurrency,
                    FinalPremiumDue = calculation.NetAmount
                }
            };
        }

        private async Task<List<VehicleCalculationBreakdownDto>> GetCalculatedVehiclesBreakdownByCertAsync(
            string proposalNo, string certOrDocNo, SqlConnection connection)
        {
            var vehicles = new List<VehicleCalculationBreakdownDto>();

            var vehicleSql = @"
        SELECT ENDTNum, ContentSection, SectionID, Field20,
               Field1, Field2, Field3, Field4, Field5, Field6,
               Field7, Field8, Field9, Field17, Field18, Field23, Field24,
               A1, A2, A3, A17, A18
        FROM PolicyTempDetails 
        WHERE PolicyNo = @PolicyNo
        AND (Field20 = @CertOrDocNo OR SectionID = @CertOrDocNo)
        AND ISNULL(Field1, '') <> 'PROPOSAL_ADJUSTMENTS'
        -- Field20 = cert matches this API's rows, the legacy page AND the mobile
        -- backend (verified row: SectionID = cover type e.g. 'COMPREHENSIVE',
        -- Field20 = cert). SectionID = cert catches writers that only set SectionID.
        -- NO SectionID shape guard: it was excluding real vehicle rows.
        ORDER BY ENDTNum";

            // FALLBACK: vehicle rows saved by earlier builds of this API carry NO
            // Field20 link at all (the old SaveVehicleToPolicyTempDetailsAsync never
            // wrote it), so a cert-scoped query can't see them. If the scoped query
            // finds nothing, retry across the whole policy. Logged so the linkage
            // gap stays visible; remove once historical rows are backfilled.
            var fallbackSql = @"
        SELECT ENDTNum, ContentSection,
               Field1, Field2, Field3, Field4, Field5, Field6,
               Field7, Field8, Field9, Field17, Field18, Field23, Field24,
               A1, A2, A3, A17, A18
        FROM PolicyTempDetails 
        WHERE PolicyNo = @PolicyNo
        AND (SectionID = 'MOTOR' OR SectionID = Field20 OR ISNULL(Tag, '') = 'MOTOR_VEHICLE')
        ORDER BY ENDTNum";

            foreach (var (sql, useCertFilter) in new[] { (vehicleSql, true), (fallbackSql, false) })
            {
                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    if (useCertFilter) cmd.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);

                    int itemNo = 1;
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var vehicleValue = reader.GetValueOrDefault<decimal>("A1");
                        var premiumRate = reader.GetValueOrDefault<decimal>("A17");
                        var basicPremium = reader.GetValueOrDefault<decimal>("A18");

                        vehicles.Add(new VehicleCalculationBreakdownDto
                        {
                            ItemNo = itemNo++,
                            CertificateNo = reader.GetStringOrDefault("ENDTNum"),
                            VehicleUser = reader.GetStringOrDefault("ContentSection"),
                            VehicleRegNo = reader.GetStringOrDefault("Field1"),
                            VehicleType = reader.GetStringOrDefault("Field2"),
                            State = reader.GetStringOrDefault("Field3"),
                            Color = reader.GetStringOrDefault("Field4"),
                            ModelYear = int.TryParse(reader.GetStringOrDefault("Field5"), out var yr) ? yr : 0,
                            SeatCapacity = int.TryParse(reader.GetStringOrDefault("Field6"), out var sc) ? sc : 0,
                            EngineNo = reader.GetStringOrDefault("Field7"),
                            ChassisNo = reader.GetStringOrDefault("Field8"),
                            Usage = reader.GetStringOrDefault("Field9"),
                            // Mobile rows keep the cover type in SectionID ('COMPREHENSIVE' etc.)
                            CoverType = reader.GetStringOrDefault("Field17") is { Length: > 0 } ct ? ct
                                : (reader.GetStringOrDefault("SectionID") is { Length: > 0 } sid
                                   && sid != "MOTOR"
                                   && sid != reader.GetStringOrDefault("Field20") ? sid : ""),
                            WaxCode = reader.GetStringOrDefault("Field18"),
                            VehicleMake = reader.GetStringOrDefault("Field23"),
                            VehicleModel = reader.GetStringOrDefault("Field24"),
                            VehicleValue = vehicleValue,
                            PremiumRate = premiumRate,
                            Step1_BasicPremium = new CalculationStepDto
                            {
                                StepName = "Basic Premium",
                                StartingAmount = vehicleValue,
                                ResultingAmount = basicPremium,
                                Formula = $"₦{vehicleValue:N2} × {premiumRate}% = ₦{basicPremium:N2}",
                                Adjustments = new List<AdjustmentDetailDto>()
                            },
                            // stash tracking/rescue temporarily for step 4
                            ShareValue = reader.GetValueOrDefault<decimal>("A2"),   // tracking
                            SharePremium = reader.GetValueOrDefault<decimal>("A3")  // rescue
                        });
                    }
                }

                if (vehicles.Count > 0) break;

                if (useCertFilter)
                {
                    _logger.LogWarning(
                        "No vehicle rows matched CertOrDocNo {CertOrDocNo} for {ProposalNo} — retrying without the calculation link (rows saved by earlier builds have no Field20)",
                        certOrDocNo, proposalNo);
                }
            }

            foreach (var vehicle in vehicles)
            {
                var discounts = await GetVehicleAdjustmentsByCertAsync(
                    proposalNo, certOrDocNo, vehicle.CertificateNo, "DISCOUNT", connection);
                var premiumAfterDiscounts = vehicle.Step1_BasicPremium.ResultingAmount - discounts.Sum(d => d.Amount);

                vehicle.Step2_AfterDiscounts = new CalculationStepDto
                {
                    StepName = "After Discounts",
                    StartingAmount = vehicle.Step1_BasicPremium.ResultingAmount,
                    Adjustments = discounts,
                    TotalAdjustment = -discounts.Sum(d => d.Amount),
                    ResultingAmount = premiumAfterDiscounts,
                    Formula = $"₦{vehicle.Step1_BasicPremium.ResultingAmount:N2} - ₦{discounts.Sum(d => d.Amount):N2} = ₦{premiumAfterDiscounts:N2}"
                };

                var loadings = await GetVehicleAdjustmentsByCertAsync(
                    proposalNo, certOrDocNo, vehicle.CertificateNo, "LOADING", connection);
                var grossPremium = premiumAfterDiscounts + loadings.Sum(l => l.Amount);

                vehicle.Step3_AfterLoadings = new CalculationStepDto
                {
                    StepName = "After Loadings",
                    StartingAmount = premiumAfterDiscounts,
                    Adjustments = loadings,
                    TotalAdjustment = loadings.Sum(l => l.Amount),
                    ResultingAmount = grossPremium,
                    Formula = $"₦{premiumAfterDiscounts:N2} + ₦{loadings.Sum(l => l.Amount):N2} = ₦{grossPremium:N2}"
                };

                var trackingCost = vehicle.ShareValue;   // recovered above
                var rescueCost = vehicle.SharePremium;
                var netPremium = grossPremium + trackingCost + rescueCost;

                var additionalCosts = new List<AdjustmentDetailDto>();
                if (trackingCost > 0)
                    additionalCosts.Add(new AdjustmentDetailDto { Name = "Tracking Cost", Type = "COST", Amount = trackingCost, Formula = $"₦{trackingCost:N2}" });
                if (rescueCost > 0)
                    additionalCosts.Add(new AdjustmentDetailDto { Name = "Rescue Cost", Type = "COST", Amount = rescueCost, Formula = $"₦{rescueCost:N2}" });

                vehicle.Step4_FinalPremium = new CalculationStepDto
                {
                    StepName = "Final Premium",
                    StartingAmount = grossPremium,
                    Adjustments = additionalCosts,
                    TotalAdjustment = trackingCost + rescueCost,
                    ResultingAmount = netPremium,
                    Formula = $"₦{grossPremium:N2} + ₦{trackingCost + rescueCost:N2} = ₦{netPremium:N2}"
                };

                vehicle.ShareValue = 0;     // reset the temp stash
                vehicle.SharePremium = 0;
            }

            return vehicles;
        }

        private async Task<List<AdjustmentDetailDto>> GetVehicleAdjustmentsByCertAsync(
            string proposalNo, string certOrDocNo, string certificateNo, string adjustmentType, SqlConnection connection)
        {
            var adjustments = new List<AdjustmentDetailDto>();

            var sql = @"
        SELECT ContentDesc, A1, A2, A4, Field4
        FROM PolicyPremDetails
        WHERE PolicyNo = @PolicyNo AND Field10 = @CertOrDocNo
          AND ENDTNum = @CertificateNo AND Tag = @Tag AND SMICode = @AdjustmentType
        ORDER BY A4";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
            command.Parameters.AddWithValue("@CertificateNo", certificateNo);
            command.Parameters.AddWithValue("@Tag", $"MOTOR_{adjustmentType}");
            command.Parameters.AddWithValue("@AdjustmentType", adjustmentType);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                adjustments.Add(new AdjustmentDetailDto
                {
                    Name = reader.GetStringOrDefault("ContentDesc"),
                    Type = adjustmentType,
                    Rate = reader.GetValueOrDefault<decimal>("A1"),
                    Amount = reader.GetValueOrDefault<decimal>("A2"),
                    SequenceOrder = reader.GetValueOrDefault<int>("A4"),
                    Formula = reader.GetStringOrDefault("Field4")
                });
            }

            return adjustments;
        }

        private async Task<ProposalAdjustmentBreakdownDto> GetMotorProposalAdjustmentsBreakdownByCertAsync(
            string proposalNo, string certOrDocNo, SqlConnection connection)
        {
            var sql = @"
        SELECT SMICode, Field2, A1, A2, A3
        FROM PolicyPremDetails
        WHERE PolicyNo = @PolicyNo AND Field10 = @CertOrDocNo
          AND ENDTNum = 'PROPOSAL' AND Tag = 'MOTOR_PROPOSAL_ADJUSTMENT'
        ORDER BY SMICode";

            decimal otherDiscountRate = 0, otherDiscountAmount = 0, aggregatePremium = 0;
            decimal otherLoadingRate = 0, otherLoadingAmount = 0, premiumAfterDiscount = 0;
            string otherDiscountFormula = "", otherLoadingFormula = "";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);
            command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var smiCode = reader.GetStringOrDefault("SMICode");
                if (smiCode == "PROPOSAL_DISCOUNT")
                {
                    otherDiscountRate = reader.GetValueOrDefault<decimal>("A1");
                    otherDiscountAmount = reader.GetValueOrDefault<decimal>("A2");
                    aggregatePremium = reader.GetValueOrDefault<decimal>("A3");
                    otherDiscountFormula = reader.GetStringOrDefault("Field2");
                }
                else if (smiCode == "PROPOSAL_LOADING")
                {
                    otherLoadingRate = reader.GetValueOrDefault<decimal>("A1");
                    otherLoadingAmount = reader.GetValueOrDefault<decimal>("A2");
                    premiumAfterDiscount = reader.GetValueOrDefault<decimal>("A3");
                    otherLoadingFormula = reader.GetStringOrDefault("Field2");
                }
            }

            if (otherDiscountRate == 0 && otherLoadingRate == 0) return null;

            if (aggregatePremium == 0) aggregatePremium = premiumAfterDiscount + otherDiscountAmount;
            if (premiumAfterDiscount == 0) premiumAfterDiscount = aggregatePremium - otherDiscountAmount;

            return new ProposalAdjustmentBreakdownDto
            {
                AggregatePremium = aggregatePremium,
                OtherDiscountRate = otherDiscountRate,
                OtherDiscountAmount = otherDiscountAmount,
                OtherDiscountFormula = otherDiscountFormula,
                PremiumAfterDiscount = premiumAfterDiscount,
                OtherLoadingRate = otherLoadingRate,
                OtherLoadingAmount = otherLoadingAmount,
                OtherLoadingFormula = otherLoadingFormula,
                NetPremiumDue = premiumAfterDiscount + otherLoadingAmount
            };
        }

        /// <summary>
        /// Returns the highest existing motor certificate sequence for a policy.
        /// MAX-based (not COUNT) so deletes/gaps never cause a reused number.
        /// Pass the active connection/transaction to read uncommitted state safely.
        /// </summary>
        // NOTE: only used to seed PREVIEW placeholder certs at calculation time now;
        // real CERTNO auto numbers are issued at save time.
        private async Task<int> GetMaxMotorVehicleSequenceAsync(
            string proposalNo,
            SqlConnection connection = null,
            SqlTransaction transaction = null)
        {
            const string sql = @"
        SELECT ISNULL(MAX(
            CASE WHEN ISNUMERIC(RIGHT(ENDTNum, 6)) = 1
                 THEN CAST(RIGHT(ENDTNum, 6) AS INT)
                 ELSE 0 END), 0)
        FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo AND SectionID = 'MOTOR'";

            async Task<int> RunAsync(SqlConnection conn, SqlTransaction tx)
            {
                using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }

            if (connection != null)
                return await RunAsync(connection, transaction);

            using var ownConnection = new SqlConnection(_connectionString);
            await ownConnection.OpenAsync();
            return await RunAsync(ownConnection, null);
        }
        //    /// <summary>
        //    /// Get motor vehicles for a proposal
        //    /// </summary>
        //    public async Task<List<CalculatedMotorRiskItemDto>> GetMotorVehiclesAsync(string proposalNo)
        //    {
        //        using var connection = new SqlConnection(_connectionString);
        //        await connection.OpenAsync();

        //        var vehicles = new List<CalculatedMotorRiskItemDto>();

        //        // Get vehicles from PolicyTempDetails
        //        var sql = @"
        //SELECT ENDTNum, ContentSection, Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9,
        //       Field17, Field18, Field23, Field24,
        //       RiskSum, SectionPremium, OtherSum,
        //       A1, A2, A3, A6, A7, A12, A13, A14, A15, A16, A17, A18
        //FROM PolicyTempDetails 
        //WHERE PolicyNo = @PolicyNo AND Tag = 'MOTOR_VEHICLE'
        //ORDER BY ENDTNum";

        //        using var command = new SqlCommand(sql, connection);
        //        command.Parameters.AddWithValue("@PolicyNo", proposalNo);

        //        using var reader = await command.ExecuteReaderAsync();
        //        while (await reader.ReadAsync())
        //        {
        //            var vehicle = new CalculatedMotorRiskItemDto
        //            {
        //                CertificateNo = reader.GetStringOrDefault("ENDTNum"),
        //                VehicleUser = reader.GetStringOrDefault("ContentSection"),
        //                VehicleRegNo = reader.GetStringOrDefault("Field1"),
        //                VehicleType = reader.GetStringOrDefault("Field2"),
        //                State = reader.GetStringOrDefault("Field3"),
        //                Color = reader.GetStringOrDefault("Field4"),
        //                ModelYear = int.TryParse(reader.GetStringOrDefault("Field5"), out var year) ? year : 0,
        //                SeatCapacity = int.TryParse(reader.GetStringOrDefault("Field6"), out var seats) ? seats : 0,
        //                EngineNo = reader.GetStringOrDefault("Field7"),
        //                ChassisNo = reader.GetStringOrDefault("Field8"),
        //                CoverType = reader.GetStringOrDefault("Field17"),
        //                WaxCode = reader.GetStringOrDefault("Field18"),
        //                VehicleMake = reader.GetStringOrDefault("Field23"),
        //                VehicleModel = reader.GetStringOrDefault("Field24"),

        //                VehicleValue = reader.GetValueOrDefault<decimal>("A1"),
        //                TrackingCost = reader.GetValueOrDefault<decimal>("A2"),
        //                RescueCost = reader.GetValueOrDefault<decimal>("A3"),
        //                SRCCRate = reader.GetValueOrDefault<decimal>("A6"),
        //                SRCCAmount = reader.GetValueOrDefault<decimal>("A7"),
        //                PremiumRate = reader.GetValueOrDefault<decimal>("A17"),
        //                BasicPremium = reader.GetValueOrDefault<decimal>("A18"),

        //                ShareValue = reader.GetValueOrDefault<decimal>("RiskSum"),
        //                NetPremium = reader.GetValueOrDefault<decimal>("SectionPremium")
        //            };

        //            vehicles.Add(vehicle);
        //        }
        //        reader.Close();

        //        // Get discounts and loadings for each vehicle
        //        foreach (var vehicle in vehicles)
        //        {
        //            var loadings = await GetVehicleAdjustmentsAsync(proposalNo, vehicle.CertificateNo, "LOADING", connection);
        //            vehicle.AppliedLoadings = loadings.Select(l => new MotorAdjustmentDto
        //            {
        //                AdjustmentName = l.Name,
        //                AdjustmentType = "LOADING",
        //                Rate = l.Rate,
        //                Amount = l.Amount,
        //                SequenceOrder = l.SequenceOrder
        //            }).ToList();

        //            var discounts = await GetVehicleAdjustmentsAsync(proposalNo, vehicle.CertificateNo, "DISCOUNT", connection);
        //            vehicle.AppliedDiscounts = discounts.Select(d => new MotorAdjustmentDto
        //            {
        //                AdjustmentName = d.Name,
        //                AdjustmentType = "DISCOUNT",
        //                Rate = d.Rate,
        //                Amount = d.Amount,
        //                SequenceOrder = d.SequenceOrder
        //            }).ToList();

        //            vehicle.TotalLoadingsAmount = vehicle.AppliedLoadings.Sum(l => l.Amount);
        //            vehicle.TotalDiscountsAmount = vehicle.AppliedDiscounts.Sum(d => d.Amount);
        //        }

        //        return vehicles;
        //    }


        // NOTE: superseded — vehicle certificates are now real CERTNO auto numbers
        // issued in PersistMotorCalculationWithCertAsync at save time. Kept because
        // ProposalService is a partial class and other files may still call it.
        private async Task<string> GenerateCertificateNumberAsync(string proposalNo)
        {
            // Implementation similar to VB.NET's certificate number generation
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
            SELECT COUNT(*) 
            FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo AND SectionID = 'MOTOR'";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PolicyNo", proposalNo);

            var count = (int)await command.ExecuteScalarAsync();
            return $"{proposalNo}/CERT/{count + 1:D6}";
        }

        /// <summary>
        /// Process Excel upload for bulk motor entry (like the VB.NET UploadDataExcel_CIP)
        /// </summary>
        public async Task<MotorCalculationResponseDto> ProcessMotorExcelUploadAsync(
            MotorExcelUploadDto request)
        {
            try
            {
                // Read Excel file
                var vehicles = await ReadMotorExcelFileAsync(request.ExcelFile);

                // Calculate
                var calculationRequest = new MotorCalculationRequestDto
                {
                    ProposalNo = request.ProposalNo,
                    Vehicles = vehicles,
                    ProportionRate = request.ProportionRate
                };

                var result = await CalculateMotorRiskItemsAsync(calculationRequest);

                if (result.Success)
                {
                    // Save to database
                    await SaveMotorCalculationAsync(result);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing motor Excel upload");
                return new MotorCalculationResponseDto
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Delete all existing motor calculations for a proposal
        /// </summary>
        private async Task DeleteExistingMotorCalculationsAsync(
            string proposalNo,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            try
            {
                // 1. Delete all vehicles from PolicyTempDetails
                var deleteVehiclesSql = @"
            DELETE FROM PolicyTempDetails 
            WHERE PolicyNo = @PolicyNo AND SectionID = 'MOTOR'";

                using (var command = new SqlCommand(deleteVehiclesSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    await command.ExecuteNonQueryAsync();
                }

                // 2. Delete all vehicle-level adjustments (discounts and loadings)
                var deleteAdjustmentsSql = @"
            DELETE FROM PolicyPremDetails 
            WHERE PolicyNo = @PolicyNo 
            AND Tag IN ('MOTOR_DISCOUNT', 'MOTOR_LOADING')";

                using (var command = new SqlCommand(deleteAdjustmentsSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    await command.ExecuteNonQueryAsync();
                }

                // 3. Delete proposal-level adjustments
                var deleteProposalAdjustmentsSql = @"
            DELETE FROM PolicyPremDetails 
            WHERE PolicyNo = @PolicyNo 
            AND ENDTNum = 'PROPOSAL'
            AND Tag = 'MOTOR_PROPOSAL_ADJUSTMENT'";

                using (var command = new SqlCommand(deleteProposalAdjustmentsSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    await command.ExecuteNonQueryAsync();
                }

                // 4. Reset PolicyDetails calculation fields
                var resetPolicyDetailsSql = @"
            UPDATE PolicyDetails SET 
                BizOption = 'PROPOSAL',
                SumInsured = 0,
                GrossPremium = 0,
                NetAmount = 0,
                ProRataPremium = 0,
                TotalRiskValue = 0,
                SumInsuredFrgn = 0,
                GrossPremiumFrgn = 0,
                ModifiedBy = 'SYSTEM',
                ModifiedOn = @ModifiedOn
            WHERE PolicyNo = @PolicyNo AND BizOption IN ('CALCULATED', 'RECALCULATION')";

                using (var command = new SqlCommand(resetPolicyDetailsSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    await command.ExecuteNonQueryAsync();
                }

                _logger.LogInformation("Successfully deleted existing motor calculations for proposal {ProposalNo}", proposalNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting existing motor calculations for proposal {ProposalNo}", proposalNo);
                throw;
            }
        }


        private async Task<List<MotorRiskItemInputDto>> ReadMotorExcelFileAsync(IFormFile file)
        {
            var vehicles = new List<MotorRiskItemInputDto>();

            // Use a library like EPPlus or ClosedXML to read Excel
            // This is a simplified example
            using var stream = file.OpenReadStream();
            // Implementation depends on your Excel library choice

            return vehicles;
        }
        /// <summary>
        /// Pulls declaration-level context an incremental add/edit doesn't
        /// resupply: CoverDays/ProportionRate/ExRate/ExCurrency, plus Motor's
        /// EXISTING OtherDiscountRate/OtherLoadingRate (stored at A10/A11 on
        /// the PolicyDetails header, per SaveMotorCalculationAsync's own
        /// convention, referenced earlier in this conversation).
        /// </summary>
        private async Task<(int CoverDays, decimal ProportionRate, decimal ExRate, string ExCurrency,
            decimal OtherDiscountRate, decimal OtherLoadingRate)> GetMotorDeclarationContextAsync(
            string proposalNo, string certOrDocNo, SqlConnection connection, SqlTransaction transaction)
        {
            int coverDays = 365;
            decimal proportionRate = 100;
            decimal exRate = 1;
            string exCurrency = "NAIRA";
            decimal otherDiscountRate = 0;
            decimal otherLoadingRate = 0;

            var headerSql = @"
        SELECT ProRataDays, ProportionRate, ExRate, ExCurrency, A10, A11
        FROM PolicyDetails
        WHERE PolicyNo = @PolicyNo AND CertOrDocNo = @CertOrDocNo AND Deleted = 0";
            using (var cmd = new SqlCommand(headerSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
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
                    // NOTE: A10/A11 as OtherDiscountRate/OtherLoadingRate — carried
                    // over from how SaveMotorCalculationAsync was described earlier
                    // in this conversation. Verify against your actual header
                    // column usage if the recomputed "other" amounts look wrong.
                    otherDiscountRate = reader.GetValueOrDefault<decimal>("A10");
                    otherLoadingRate = reader.GetValueOrDefault<decimal>("A11");
                }
            }

            return (coverDays, proportionRate, exRate, exCurrency, otherDiscountRate, otherLoadingRate);
        }

        /// <summary>
        /// Add one vehicle to the proposal's Motor declaration, persist it, and
        /// recompute+save the declaration's totals (Motor's EXISTING Other
        /// Discount/Loading 2-rate box re-applied against the new combined
        /// total, final pro-rata+proportion+FX step redone, PolicyDetails/
        /// Policies header updated). Returns the updated declaration-level state.
        ///
        /// Incremental — works against whatever's currently persisted, NOT a
        /// resubmission of the full vehicle list. Do not mix with Motor's
        /// existing bulk calculate-and-save endpoint in the same editing
        /// session without resubmitting the complete vehicle list there first
        /// (same caveat as Hull/Cargo's equivalent).
        /// </summary>
        public async Task<MotorCalculationResponseDto> AddMotorVehicleAsync(
            string proposalNo, MotorRiskItemInputDto vehicle)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var certOrDocNo = await GetOrGenerateCertOrDocNoAsync(proposalNo, connection, transaction);
                var branchId = await GetBranchIdAsync(proposalNo, connection, transaction);
                var context = await GetMotorDeclarationContextAsync(proposalNo, certOrDocNo, connection, transaction);

                var seq = await GetMaxMotorVehicleSequenceAsync(proposalNo, connection, transaction);
                var calculated = await CalculateSingleVehicleCompleteAsync(
                    vehicle,
                    new MotorCalculationRequestDto
                    {
                        ProposalNo = proposalNo,
                        CoverDays = context.CoverDays
                    },
                    seq + 1);

                if (string.IsNullOrWhiteSpace(calculated.CertificateNo) || calculated.CertificateNo.Contains("/CERT/"))
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

                var updated = await RecomputeAndSaveMotorAggregateAsync(proposalNo, certOrDocNo, connection, transaction);

                await transaction.CommitAsync();

                updated.Message = $"Vehicle {calculated.CertificateNo} added; declaration totals recalculated.";
                return updated;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error adding motor vehicle for {ProposalNo}", proposalNo);
                return new MotorCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Edit an existing vehicle's fields/rates in place. Keeps the
        /// vehicle's existing CertificateNo — only its own values change.
        /// Recalculates the vehicle fresh, replaces its stored row and
        /// adjustment rows (hard delete + re-insert within this transaction,
        /// not a soft delete), then runs the same declaration-level recompute
        /// as AddMotorVehicleAsync/RemoveMotorVehicleAsync.
        /// </summary>
        public async Task<MotorCalculationResponseDto> UpdateMotorVehicleAsync(
            string proposalNo, string certificateNo, MotorRiskItemInputDto vehicle)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                string certOrDocNo = null;
                var lookupSql = @"
            SELECT Field20 FROM PolicyTempDetails
            WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo AND SectionID = 'MOTOR'";
                using (var cmd = new SqlCommand(lookupSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    cmd.Parameters.AddWithValue("@CertificateNo", certificateNo);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                        certOrDocNo = reader.GetStringOrDefault("Field20");
                }

                if (string.IsNullOrEmpty(certOrDocNo))
                {
                    return new MotorCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = $"Vehicle {certificateNo} not found for policy {proposalNo}"
                    };
                }

                var context = await GetMotorDeclarationContextAsync(proposalNo, certOrDocNo, connection, transaction);

                var calculated = await CalculateSingleVehicleCompleteAsync(
                    vehicle,
                    new MotorCalculationRequestDto
                    {
                        ProposalNo = proposalNo,
                        CoverDays = context.CoverDays
                    },
                    0); // sequence unused: certificate number overridden below
                calculated.CertificateNo = certificateNo;

                await DeleteMotorVehicleRowAsync(proposalNo, certificateNo, connection, transaction);

                await SaveMotorVehicleWithCertAsync(
                    proposalNo, certOrDocNo, calculated, connection, transaction);
                await SaveMotorVehicleAdjustmentsWithCertAsync(
                    proposalNo, certOrDocNo, calculated.CertificateNo,
                    calculated.Step2_Discounts.DiscountsApplied, "DISCOUNT", connection, transaction);
                await SaveMotorVehicleAdjustmentsWithCertAsync(
                    proposalNo, certOrDocNo, calculated.CertificateNo,
                    calculated.Step3_Loadings.LoadingsApplied, "LOADING", connection, transaction);

                var updated = await RecomputeAndSaveMotorAggregateAsync(proposalNo, certOrDocNo, connection, transaction);

                await transaction.CommitAsync();

                updated.Message = $"Vehicle {certificateNo} updated; declaration totals recalculated.";
                return updated;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating motor vehicle {CertificateNo} for {ProposalNo}", certificateNo, proposalNo);
                return new MotorCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Hard-deletes a vehicle row + its discount/loading rows — used by
        /// UpdateMotorVehicleAsync immediately before re-inserting the edited
        /// version under the same CertificateNo. Functionally the same kind of
        /// hard delete RemoveMotorVehicleAsync now uses (neither PolicyTempDetails
        /// nor PolicyPremDetails has a Deleted column, per confirmation) — this one
        /// exists as its own method because it's mid-transaction replace-then-
        /// reinsert, not a standalone removal.
        /// </summary>
        private async Task DeleteMotorVehicleRowAsync(
            string proposalNo, string certificateNo, SqlConnection connection, SqlTransaction transaction)
        {
            var deleteVehicleSql = @"
        DELETE FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo AND SectionID = 'MOTOR'";
            using (var cmd = new SqlCommand(deleteVehicleSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@CertificateNo", certificateNo);
                await cmd.ExecuteNonQueryAsync();
            }

            var deleteAdjSql = @"
        DELETE FROM PolicyPremDetails
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo
        AND Tag IN ('MOTOR_DISCOUNT', 'MOTOR_LOADING')";
            using (var cmd = new SqlCommand(deleteAdjSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@CertificateNo", certificateNo);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Remove one vehicle (hard delete — PolicyTempDetails/PolicyPremDetails
        /// have no Deleted column) and recompute+save the declaration's totals —
        /// same shared recompute as AddMotorVehicleAsync.
        /// </summary>
        public async Task<MotorCalculationResponseDto> RemoveMotorVehicleAsync(string proposalNo, string certificateNo)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                string certOrDocNo = null;
                var lookupSql = @"
            SELECT Field20 FROM PolicyTempDetails
            WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo AND SectionID = 'MOTOR'";
                using (var cmd = new SqlCommand(lookupSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    cmd.Parameters.AddWithValue("@CertificateNo", certificateNo);
                    certOrDocNo = await cmd.ExecuteScalarAsync() as string;
                }

                if (string.IsNullOrEmpty(certOrDocNo))
                {
                    return new MotorCalculationResponseDto
                    {
                        ProposalNo = proposalNo,
                        Success = false,
                        Message = $"Vehicle {certificateNo} not found for policy {proposalNo}"
                    };
                }

                var deleteVehicleSql = @"
        DELETE FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo AND SectionID = 'MOTOR'";
                using (var command = new SqlCommand(deleteVehicleSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@CertificateNo", certificateNo);
                    await command.ExecuteNonQueryAsync();
                }

                var deleteAdjustmentsSql = @"
        DELETE FROM PolicyPremDetails
        WHERE PolicyNo = @PolicyNo AND ENDTNum = @CertificateNo
        AND Tag IN ('MOTOR_DISCOUNT', 'MOTOR_LOADING')";
                using (var command = new SqlCommand(deleteAdjustmentsSql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@PolicyNo", proposalNo);
                    command.Parameters.AddWithValue("@CertificateNo", certificateNo);
                    await command.ExecuteNonQueryAsync();
                }

                var updated = await RecomputeAndSaveMotorAggregateAsync(proposalNo, certOrDocNo, connection, transaction);

                await transaction.CommitAsync();

                updated.Message = $"Vehicle {certificateNo} removed; declaration totals recalculated.";
                return updated;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error removing motor vehicle {CertificateNo} for {ProposalNo}", certificateNo, proposalNo);
                return new MotorCalculationResponseDto
                {
                    ProposalNo = proposalNo,
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Shared by AddMotorVehicleAsync/UpdateMotorVehicleAsync/RemoveMotorVehicleAsync:
        /// re-sums whatever vehicles are currently active for the declaration,
        /// re-applies Motor's EXISTING OtherDiscountRate/OtherLoadingRate (read
        /// fresh from the header, not trusted from any stale prior calculation)
        /// against the new totals, redoes the final pro-rata+proportion+FX step,
        /// and updates the PolicyDetails/Policies header. Does not touch or
        /// re-fetch individual vehicles' own step breakdowns.
        ///
        /// LOWER CONFIDENCE (see file header): the vehicle-sum query below uses
        /// RiskSum/SectionPremium + SectionID='MOTOR', the same pattern
        /// confirmed for Hull and Cargo — but not independently re-verified
        /// against Motor's actual schema. If the recomputed totals look wrong
        /// after a real test, this query is the first place to check.
        /// </summary>
        private async Task<MotorCalculationResponseDto> RecomputeAndSaveMotorAggregateAsync(
            string proposalNo, string certOrDocNo, SqlConnection connection, SqlTransaction transaction)
        {
            decimal totalVehicleValue = 0;
            decimal totalNetPremium = 0;

            var vehicleSql = @"
        SELECT RiskSum, SectionPremium
        FROM PolicyTempDetails
        WHERE PolicyNo = @PolicyNo AND Field20 = @CertOrDocNo AND SectionID = 'MOTOR'
        AND ISNULL(Tag, '') <> 'EXPIRED'";
            using (var cmd = new SqlCommand(vehicleSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PolicyNo", proposalNo);
                cmd.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalVehicleValue += reader.GetValueOrDefault<decimal>("RiskSum");
                    totalNetPremium += reader.GetValueOrDefault<decimal>("SectionPremium");
                }
            }

            var context = await GetMotorDeclarationContextAsync(proposalNo, certOrDocNo, connection, transaction);

            // Re-apply Motor's EXISTING fixed 2-rate Other Discount/Loading box
            // against the freshly-summed total — cascading, discount before
            // loading, matching the established discounts-before-loadings
            // convention used throughout this codebase (Motor/Hull/Cargo).
            var afterOtherDiscount = totalNetPremium - (totalNetPremium * (context.OtherDiscountRate / 100));
            var adjustedTotalNetPremium = afterOtherDiscount + (afterOtherDiscount * (context.OtherLoadingRate / 100));

            // Final step: pro-rata is already baked into each vehicle's own
            // SectionPremium (unaffected by an add/remove elsewhere); only
            // proportion + FX apply here, against the adjusted total.
            var shareSumInsured = totalVehicleValue * (context.ProportionRate / 100);
            var sharePremium = adjustedTotalNetPremium * (context.ProportionRate / 100);
            var foreignSumInsured = shareSumInsured * context.ExRate;
            var foreignPremium = sharePremium * context.ExRate;

            var response = new MotorCalculationResponseDto
            {
                ProposalNo = proposalNo,
                CertOrDocNo = certOrDocNo,
                TotalVehicleValue = totalVehicleValue,
                TotalNetPremium = totalNetPremium,
                ShareSumInsured = shareSumInsured,
                SharePremium = sharePremium,
                FinalPremiumDue = sharePremium,
                ForeignSumInsured = foreignSumInsured,
                ForeignPremium = foreignPremium,
                Success = true,
                CalculatedOn = DateTime.Now
            };

            // CertOrDocNo is already set on response above, so the header update
            // reads it from there rather than taking it as a separate parameter.
            await UpdatePolicyDetailsForMotorRecomputeAsync(response, connection, transaction);
            await UpdateProposalForMotorRecomputeAsync(response, connection, transaction);

            return response;
        }

        /// <summary>
        /// Narrower Policies-table update used ONLY by the incremental recompute
        /// path — deliberately NOT a reuse of your existing
        /// UpdateProposalWithMotorTotalsAsync, for the same two reasons as
        /// UpdatePolicyDetailsForMotorRecomputeAsync above: (1) it always sets
        /// TransSTATUS = 'CALCULATED', which should only happen on the main
        /// /complete calculate-and-save flow, not on every incremental add/
        /// edit/remove; (2) it reads calculation.ProRataPremium, a property
        /// this recompute's response object never sets (it uses SharePremium
        /// instead) — reusing it as-is would have silently zeroed out
        /// GrossPremium/ProRataPremium on Policies on every incremental change,
        /// the same class of bug already found and fixed on the PolicyDetails
        /// side. Your existing method is left completely untouched.
        /// </summary>
        private async Task UpdateProposalForMotorRecomputeAsync(
            MotorCalculationResponseDto calculation,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            // EXCHANGE RATE: this updates the proposal's own header row — use its saved rate.
            var exchangeContext = await GetSavedExchangeContextAsync(calculation.ProposalNo, connection, transaction);
            var sql = @"
        UPDATE Policies SET
            SumInsured = @SumInsured,
            GrossPremium = @GrossPremium,
            SumInsuredFrgn = @SumInsuredFrgn,
            GrossPremiumFrgn = @GrossPremiumFrgn,
            ProRataPremium = @ProRataPremium,
            ModifiedBy = 'SYSTEM',
            ModifiedOn = @ModifiedOn
        WHERE PolicyNo = @ProposalNo";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@SumInsured", ToStoredAmount(calculation.TotalVehicleValue, exchangeContext.ExRate));
            command.Parameters.AddWithValue("@GrossPremium", ToStoredAmount(calculation.SharePremium, exchangeContext.ExRate));
            command.Parameters.AddWithValue("@SumInsuredFrgn", calculation.ForeignSumInsured);
            command.Parameters.AddWithValue("@GrossPremiumFrgn", calculation.ForeignPremium);
            command.Parameters.AddWithValue("@ProRataPremium", ToStoredAmount(calculation.SharePremium, exchangeContext.ExRate));
            command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
            command.Parameters.AddWithValue("@ProposalNo", calculation.ProposalNo);

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Narrower update used ONLY by the incremental recompute path
        /// (RecomputeAndSaveMotorAggregateAsync above) — deliberately NOT a
        /// reuse of your existing UpdatePolicyDetailsWithMotorCalculationAsync,
        /// for two reasons found by inspecting it directly: (1) it always sets
        /// BizOption = 'CALCULATED', which would wrongly reset the policy's
        /// lifecycle state on every add/edit/remove; (2) it reads
        /// calculation.TotalGrossPremium/ProRataPremium, properties this
        /// recompute's response object never sets (it uses TotalNetPremium/
        /// SharePremium instead) — reusing it as-is would have silently
        /// zeroed out GrossPremium/NetAmount on every incremental change.
        /// This mirrors the original's WHERE clause (PolicyNo + BizOption IN
        /// ('PROPOSAL','CALCULATED'), matching Motor's established single-
        /// declaration-per-policy pattern — no CertOrDocNo filter, since the
        /// original doesn't use one either) with only those two things fixed.
        /// Your existing method is left completely untouched.
        /// </summary>
        private async Task UpdatePolicyDetailsForMotorRecomputeAsync(
            MotorCalculationResponseDto calculation,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            // EXCHANGE RATE: this updates an EXISTING proposal — use its saved rate.
            var exchangeContext = await GetSavedExchangeContextAsync(calculation.ProposalNo, connection, transaction);
            var sql = @"
            UPDATE PolicyDetails SET
                SumInsured = @SumInsured,
                GrossPremium = @GrossPremium,
                NetAmount = @NetAmount,
                ProRataPremium = @ProRataPremium,
                TotalRiskValue = @TotalRiskValue,
                SumInsuredFrgn = @SumInsuredFrgn,
                GrossPremiumFrgn = @GrossPremiumFrgn,
                ModifiedBy = 'SYSTEM',
                ModifiedOn = @ModifiedOn
            WHERE PolicyNo = @PolicyNo AND BizOption IN ('PROPOSAL', 'CALCULATED')";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@SumInsured", ToStoredAmount(calculation.ShareSumInsured, exchangeContext.ExRate));
            command.Parameters.AddWithValue("@GrossPremium", ToStoredAmount(calculation.TotalNetPremium, exchangeContext.ExRate));
            command.Parameters.AddWithValue("@NetAmount", ToStoredAmount(calculation.SharePremium, exchangeContext.ExRate));
            command.Parameters.AddWithValue("@ProRataPremium", ToStoredAmount(calculation.SharePremium, exchangeContext.ExRate));
            command.Parameters.AddWithValue("@TotalRiskValue", ToStoredAmount(calculation.TotalVehicleValue, exchangeContext.ExRate));
            command.Parameters.AddWithValue("@SumInsuredFrgn", calculation.ForeignSumInsured);
            command.Parameters.AddWithValue("@GrossPremiumFrgn", calculation.ForeignPremium);
            command.Parameters.AddWithValue("@ModifiedOn", DateTime.Now);
            command.Parameters.AddWithValue("@PolicyNo", calculation.ProposalNo);

            await command.ExecuteNonQueryAsync();
        }
    }
}