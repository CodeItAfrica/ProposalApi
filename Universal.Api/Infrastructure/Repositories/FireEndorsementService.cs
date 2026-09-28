using Gibs.Application.Services;
using Gibs.Domain.Entities;
using Gibs.Infrastructure;
using GibsLifesMicroWebApi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    // -------------------------------------------------------------------------------------------
    // Fire endorsements. CreateFireEndorsementAsync handles every type against one existing record
    // (a PolicyDetails row = one fire calculation):
    //
    //   ADDITIONAL  record and sections untouched; new sections priced and pro-rated from the
    //               effective date to the policy's expiry, tagged ADDITIONAL.
    //   REVERSAL    record EXPIRED; its sections keep their tags but are stamped with the reversal's
    //               number; every section copied with the original's figures, tagged REVERSAL;
    //               description "Cancellation Endorsement". DELETE / DELETED are the same type.
    //   RETURN      record untouched; ticked sections EXPIRED and stamped with the RETURN's number;
    //               ticked sections copied, tagged RETURN; description "Deletion of Risk or Cover".
    //   REDO        record and ticked sections EXPIRED; ticked sections copied at their original
    //               figures (no repricing), tagged NEW — the screen calls a REDO "NEW".
    //   EXTENSION   record and sections untouched; all sections copied at full premium, tagged
    //               EXTENSION; the new record runs to the new end date.
    //   REDUCTION   record and sections untouched; all sections copied at full figures, tagged REDUCTION.
    //
    // Every new record is saved UNCOMPLETED (Field50). The Policies header is not touched.
    //
    //   record  = PolicyDetails      BizOption = type or EXPIRED, Field4 = description
    //   section = PolicyTempDetails  Field20 = the record's CertOrDocNo, ENDTNum = declaration no.
    //   item    = PolicyPremDetails  Field10 = the section's declaration no., ENDTNum = item CERTNO
    //
    // Tags go on section and risk-item rows only — never on PROPOSAL_ADJUSTMENTS or FIRE_SECTION_* /
    // FIRE_ITEM_* rows, whose Tag the breakdown reads filter on. Sections an earlier endorsement
    // already EXPIRED are off cover: they can't be ticked, and REVERSAL / EXTENSION / REDUCTION
    // leave them out (with a warning).
    // -------------------------------------------------------------------------------------------
    public partial class ProposalService
    {
        // PolicyDetails.Field50 for a record that hasn't been completed yet.
        private const string EndorsementPendingStatus = "UNCOMPLETED";

        private const string ReversalEndorsementDescription = "Cancellation Endorsement";
        private const string ReturnEndorsementDescription = "Deletion of Risk or Cover";

        // PolicyTempDetails column that receives the endorsement number when an endorsement touches an
        // original section (REVERSAL: every section it reverses; RETURN: the ticked ones). Nothing else
        // in the fire module writes it. Change it here if the legacy screen reads another column.
        private const string SectionStampColumn = "Field21";

        private const string FireSectionsTable = "PolicyTempDetails";
        private const string FireItemsTable = "PolicyPremDetails";

        // Record columns a REVERSAL carries over unchanged from the record it reverses.
        private static readonly string[] ReversalMirroredColumns =
        {
            "ExRate", "ExCurrency", "ProportionRate",
            "SumInsured", "GrossPremium", "SumInsuredFrgn", "GrossPremiumFrgn",
            "ProRataDays", "ProRataPremium", "NetAmount", "TotalRiskValue",
            "A1", "A2", "A10", "A11", "A13", "A14"
        };

        #region Fire endorsements — public API

        /// <summary>Creates a fire endorsement of any type against one record of the policy.</summary>
        public async Task<FireEndorsementResponseDto> CreateFireEndorsementAsync(FireEndorsementRequestDto request)
        {
            var response = new FireEndorsementResponseDto
            {
                PolicyNo = request?.PolicyNo,
                SourceDetailID = request?.SourceDetailID,
                Status = EndorsementPendingStatus
            };

            if (request == null)
                return FailFireEndorsement(response, "Request body is required");

            if (!EndorsementTypes.TryNormalize(request.EndorsementType, out var type))
                return FailFireEndorsement(response,
                    $"Unknown endorsement type '{request.EndorsementType}'. Use one of " +
                    $"{string.Join(", ", EndorsementTypes.GetAll())} (DELETE/DELETED = REVERSAL, NEW = REDO).");

            response.EndorsementType = type;

            var errors = ValidateFireEndorsementRequest(type, request, response.Warnings);
            if (errors.Count > 0)
                return FailFireEndorsement(response, "Invalid endorsement request", errors);

            var policy = await GetProposalAsync(request.PolicyNo);
            if (policy == null)
                return FailFireEndorsement(response, $"Policy {request.PolicyNo} not found");
            if (string.IsNullOrWhiteSpace(policy.BranchID))
                return FailFireEndorsement(response,
                    $"Policy {request.PolicyNo} has no BranchID — section and item numbers are issued per branch.");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // One endorsement per policy at a time: numbering, expiring and stamping all read
                // rows and then write them back.
                await AcquireFireEndorsementLockAsync(request.PolicyNo, connection, transaction);

                var source = await LoadFireEndorsementSourceAsync(request.PolicyNo, request.SourceDetailID, connection, transaction)
                    ?? throw new FireEndorsementRuleException(
                        $"Record {request.SourceDetailID} not found on policy {request.PolicyNo}.");

                if (source.IsExpired)
                    throw new FireEndorsementRuleException(
                        $"Record {source.DetailID} is already EXPIRED. Endorse the record that replaced it instead.");

                response.SourceCertOrDocNo = source.CertOrDocNo;

                var ctx = new FireEndorsementContext
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
                        await EndorseAdditionalAsync(ctx, response);
                        break;
                    case EndorsementTypes.Reversal:
                        await EndorseReversalAsync(ctx, response);
                        break;
                    case EndorsementTypes.Return:
                        await EndorseReturnAsync(ctx, response);
                        break;
                    case EndorsementTypes.Redo:
                        await EndorseRedoAsync(ctx, response);
                        break;
                    case EndorsementTypes.Extension:
                    case EndorsementTypes.Reduction:
                        await EndorseWithFullCopyAsync(ctx, response);
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
                return FailFireEndorsement(response, rule.Message);
            }
            catch (Exception ex)
            {
                RollbackFireEndorsement(transaction);
                _logger.LogError(ex, "Error creating {Type} fire endorsement on {PolicyNo}, record {DetailID}",
                    type, request.PolicyNo, request.SourceDetailID);
                return FailFireEndorsement(response, $"Failed to create {type} endorsement", new List<string> { ex.Message });
            }
        }

        /// <summary>
        /// Sections of one record, for the RETURN / REDO tick list. Null when the record doesn't exist.
        /// </summary>
        public async Task<List<EndorsableFireSectionDto>> GetEndorsableFireSectionsAsync(string policyNo, string detailId)
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

            var sections = await LoadFireEndorsementSectionsAsync(policyNo, certOrDocNo, false, connection, null);

            // Risk items per section declaration number (items carry the record's CertOrDocNo in Field2).
            var itemCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var command = new SqlCommand(@"
SELECT Field10, COUNT(*) AS Items
FROM PolicyPremDetails
WHERE PolicyNo = @PolicyNo AND Field2 = @CertOrDocNo AND ISNULL(Field10, '') <> ''
GROUP BY Field10", connection))
            {
                command.Parameters.AddWithValue("@PolicyNo", policyNo);
                command.Parameters.AddWithValue("@CertOrDocNo", certOrDocNo);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var declarationNo = FireEndorsementRows.Text(reader.GetValue(0));
                    if (!string.IsNullOrEmpty(declarationNo))
                        itemCounts[declarationNo] = (int)FireEndorsementRows.Number(reader.GetValue(1));
                }
            }

            return sections.Select(section => new EndorsableFireSectionDto
            {
                SelectionToken = section.SelectionToken,
                SectionDeclarationNo = section.DeclarationNo,
                SectionID = section.SectionID,
                SectionName = section.SectionName,
                Location = section.Location,
                Tag = section.Tag,
                IsExpired = section.IsExpired,
                StampedBy = section.Stamp,
                SectionSumInsured = section.SumInsured,
                SectionNetPremium = section.NetPremium,
                RiskItemCount = !string.IsNullOrEmpty(section.DeclarationNo) && itemCounts.TryGetValue(section.DeclarationNo, out var count)
                    ? count
                    : (int)FireEndorsementRows.Number(section.Values, "A17")
            }).ToList();
        }

        /// <summary>Endorsement records on a policy (any record with an EndorsementNo), newest first.</summary>
        public async Task<List<FireEndorsementListItemDto>> GetFireEndorsementsAsync(string policyNo)
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

            var endorsements = new List<FireEndorsementListItemDto>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                endorsements.Add(new FireEndorsementListItemDto
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
                    TotalSumInsured = FireEndorsementRows.Number(row, "TotalRiskValue"),
                    PremiumDue = FireEndorsementRows.Number(row, "NetAmount"),
                    CreatedBy = FireEndorsementRows.Text(row, "SubmittedBy")
                });
            }

            return endorsements;
        }

        #endregion

        #region Fire endorsements — one handler per type

        /// <summary>
        /// ADDITIONAL: record and sections untouched. The new sections are priced like any fire
        /// calculation and pro-rated from the effective date to the policy's expiry.
        /// </summary>
        private async Task EndorseAdditionalAsync(FireEndorsementContext ctx, FireEndorsementResponseDto response)
        {
            var request = ctx.Request;

            var expiry = (ctx.Policy.EndDate ?? ctx.Source.EndDate)?.Date
                ?? throw new FireEndorsementRuleException("The policy has no expiry date to pro-rate the new sections to.");

            var effective = (request.EffectiveDate ?? ctx.Now).Date;
            if (ctx.Policy.StartDate.HasValue && effective < ctx.Policy.StartDate.Value.Date)
                throw new FireEndorsementRuleException(
                    $"Effective date {effective:yyyy-MM-dd} is before the policy starts ({ctx.Policy.StartDate.Value:yyyy-MM-dd}).");

            // Same day count the module uses everywhere for ProRataDays: EndDate - StartDate.
            var coverDays = (expiry - effective).Days;
            if (coverDays <= 0)
                throw new FireEndorsementRuleException(
                    $"Effective date {effective:yyyy-MM-dd} must be before the policy's expiry ({expiry:yyyy-MM-dd}).");

            var proportionRate = FireEndorsementRows.PositiveOr(request.ProportionRate, ctx.Source.ProportionRate, 100m);
            var exchangeRate = FireEndorsementRows.PositiveOr(request.ExchangeRate, ctx.Source.ExRate, 1m);
            var currency = FireEndorsementRows.FirstText(request.Currency, ctx.Source.ExCurrency, "NAIRA");
            var otherDiscountsRate = request.OtherDiscountsRate ?? ctx.Source.OtherDiscountsRate;
            var otherLoadingsRate = request.OtherLoadingsRate ?? ctx.Source.OtherLoadingsRate;

            // Price everything first so a bad section fails before any number is issued.
            var priced = new List<PricedEndorsementSection>();
            foreach (var section in request.Sections)
            {
                priced.Add(await PriceEndorsementSectionAsync(section, proportionRate));
            }

            var totalSumInsured = priced.Sum(p => p.Result.SectionSumInsured);
            var totalPremium = priced.Sum(p => p.Result.SectionNetPremium);
            var (adjustments, proRata) = await PriceEndorsementRecordAsync(totalPremium, otherDiscountsRate, otherLoadingsRate, coverDays);

            await IssueEndorsementNumbersAsync(ctx);

            var itemCount = 0;
            foreach (var section in priced)
            {
                itemCount += await InsertPricedEndorsementSectionAsync(ctx, section, proportionRate, effective, expiry);
            }

            await InsertEndorsementAdjustmentsRowAsync(ctx, adjustments);

            var figures = ComputedRecordFigures(totalSumInsured, totalPremium, proRata, coverDays,
                proportionRate, exchangeRate, currency, otherDiscountsRate, otherLoadingsRate);

            var description = FireEndorsementRows.NullIfBlank(request.Description);
            response.NewDetailID = await InsertFireEndorsementRecordAsync(ctx, effective, expiry, description, figures);
            response.Description = description;
            response.StartDate = effective;
            response.EndDate = expiry;
            response.SectionCount = priced.Count;
            response.RiskItemCount = itemCount;
            FillEndorsementTotals(response, totalSumInsured, totalPremium, adjustments.NetPremiumDue, proRata, coverDays);
        }

        /// <summary>
        /// REVERSAL: record EXPIRED; its sections keep their tags but carry the reversal's number;
        /// every section copied, tagged REVERSAL, with the original's figures.
        /// </summary>
        private async Task EndorseReversalAsync(FireEndorsementContext ctx, FireEndorsementResponseDto response)
        {
            var sections = await LoadFireEndorsementSectionsAsync(ctx.PolicyNo, ctx.Source.CertOrDocNo, true, ctx.Connection, ctx.Transaction);
            var onCover = sections.Where(s => !s.IsExpired).ToList();
            var alreadyOff = sections.Where(s => s.IsExpired).ToList();

            if (sections.Count > 0 && onCover.Count == 0)
                throw new FireEndorsementRuleException(
                    "Every section on this record was already taken off cover by an earlier endorsement — there is nothing left to reverse.");

            await IssueEndorsementNumbersAsync(ctx);

            await ExpireFireEndorsementSourceAsync(ctx);
            foreach (var section in onCover)
            {
                await MarkOriginalFireSectionAsync(ctx, section, expire: false, stamp: true);
                response.StampedSectionTokens.Add(section.SelectionToken);
            }

            var copies = new List<CopiedFireSection>();
            foreach (var section in onCover)
            {
                copies.Add(await CopyFireSectionAsync(ctx, section, null));
            }

            Dictionary<string, object> figures;
            if (alreadyOff.Count == 0)
            {
                // The usual case: the reversal carries the original record's figures exactly,
                // proposal-level adjustment row included.
                var copiedNetPremium = await CopyFireProposalAdjustmentRowsAsync(ctx);
                figures = ReversalMirroredColumns.ToDictionary(c => c, c => ctx.Source.Value(c), StringComparer.OrdinalIgnoreCase);

                response.TotalSumInsured = ctx.Source.Number("TotalRiskValue");
                response.TotalPremium = ctx.Source.Number("A2");
                response.NetPremium = copiedNetPremium ?? ctx.Source.Number("NetAmount");
                response.ProRataPremium = ctx.Source.Number("ProRataPremium");
                response.PremiumDue = ctx.Source.Number("NetAmount");
                response.CoverDays = ctx.Source.ProRataDays;
            }
            else
            {
                // Some sections were already returned (and credited) by an earlier endorsement:
                // reverse only what is still on cover, on the original's pricing basis.
                foreach (var section in alreadyOff)
                {
                    response.Warnings.Add(
                        $"Section {section.Label} was already taken off cover by {DescribeStamp(section)} and was not reversed again.");
                }

                figures = await PriceCopiedFireSectionsAsync(ctx, copies, ctx.Source.PricingDays, response);
            }

            response.NewDetailID = await InsertFireEndorsementRecordAsync(
                ctx, ctx.Source.StartDate, ctx.Source.EndDate, ReversalEndorsementDescription, figures);
            response.Description = ReversalEndorsementDescription;
            response.SourceRecordExpired = true;
            response.StartDate = ctx.Source.StartDate;
            response.EndDate = ctx.Source.EndDate;
            response.SectionCount = copies.Count;
            response.RiskItemCount = copies.Sum(c => c.ItemCount);
        }

        /// <summary>
        /// RETURN: record untouched; ticked sections EXPIRED and stamped with the RETURN's number;
        /// ticked sections copied, tagged RETURN.
        /// </summary>
        private async Task EndorseReturnAsync(FireEndorsementContext ctx, FireEndorsementResponseDto response)
        {
            var sections = await LoadFireEndorsementSectionsAsync(ctx.PolicyNo, ctx.Source.CertOrDocNo, true, ctx.Connection, ctx.Transaction);
            var ticked = ResolveTickedFireSections(sections, ctx.Request.SelectedSectionTokens);

            await IssueEndorsementNumbersAsync(ctx);

            foreach (var section in ticked)
            {
                await MarkOriginalFireSectionAsync(ctx, section, expire: true, stamp: true);
                response.ExpiredSectionTokens.Add(section.SelectionToken);
                response.StampedSectionTokens.Add(section.SelectionToken);
            }

            var copies = new List<CopiedFireSection>();
            foreach (var section in ticked)
            {
                copies.Add(await CopyFireSectionAsync(ctx, section, null));
            }

            // Copies keep their figures; the record is totalled on the original's basis
            // (same other discount/loading rates and cover days), so ticking every section
            // reproduces the original record's figures.
            var figures = await PriceCopiedFireSectionsAsync(ctx, copies, ctx.Source.PricingDays, response);

            response.NewDetailID = await InsertFireEndorsementRecordAsync(
                ctx, ctx.Source.StartDate, ctx.Source.EndDate, ReturnEndorsementDescription, figures);
            response.Description = ReturnEndorsementDescription;
            response.StartDate = ctx.Source.StartDate;
            response.EndDate = ctx.Source.EndDate;
            response.SectionCount = copies.Count;
            response.RiskItemCount = copies.Sum(c => c.ItemCount);
        }

        /// <summary>
        /// REDO (screen: NEW): record and ticked sections EXPIRED; ticked sections copied, tagged NEW,
        /// with their original figures. Nothing is repriced — the copies keep their stored figures and
        /// the record is only totalled, on the original's basis.
        /// </summary>
        private async Task EndorseRedoAsync(FireEndorsementContext ctx, FireEndorsementResponseDto response)
        {
            var sections = await LoadFireEndorsementSectionsAsync(ctx.PolicyNo, ctx.Source.CertOrDocNo, true, ctx.Connection, ctx.Transaction);
            var ticked = ResolveTickedFireSections(sections, ctx.Request.SelectedSectionTokens);

            await IssueEndorsementNumbersAsync(ctx);

            await ExpireFireEndorsementSourceAsync(ctx);
            foreach (var section in ticked)
            {
                await MarkOriginalFireSectionAsync(ctx, section, expire: true, stamp: false);
                response.ExpiredSectionTokens.Add(section.SelectionToken);
            }

            var copies = new List<CopiedFireSection>();
            foreach (var section in ticked)
            {
                copies.Add(await CopyFireSectionAsync(ctx, section, null));
            }

            var figures = await PriceCopiedFireSectionsAsync(ctx, copies, ctx.Source.PricingDays, response);

            var description = FireEndorsementRows.NullIfBlank(ctx.Request.Description);
            response.NewDetailID = await InsertFireEndorsementRecordAsync(
                ctx, ctx.Source.StartDate, ctx.Source.EndDate, description, figures);
            response.Description = description;
            response.SourceRecordExpired = true;
            response.StartDate = ctx.Source.StartDate;
            response.EndDate = ctx.Source.EndDate;
            response.SectionCount = copies.Count;
            response.RiskItemCount = copies.Sum(c => c.ItemCount);
        }

        /// <summary>
        /// EXTENSION and REDUCTION: record and sections untouched; every section on cover copied as it
        /// stands at full premium (no pro-rata). An EXTENSION's record and copied sections run to the
        /// new end date.
        /// </summary>
        private async Task EndorseWithFullCopyAsync(FireEndorsementContext ctx, FireEndorsementResponseDto response)
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

            var sections = await LoadFireEndorsementSectionsAsync(ctx.PolicyNo, ctx.Source.CertOrDocNo, false, ctx.Connection, ctx.Transaction);
            var onCover = sections.Where(s => !s.IsExpired).ToList();
            if (onCover.Count == 0)
                throw new FireEndorsementRuleException("This record has no sections on cover to copy.");

            foreach (var section in sections.Where(s => s.IsExpired))
            {
                response.Warnings.Add(
                    $"Section {section.Label} is already off cover ({DescribeStamp(section)}) and was not copied.");
            }

            await IssueEndorsementNumbersAsync(ctx);

            var copies = new List<CopiedFireSection>();
            foreach (var section in onCover)
            {
                copies.Add(await CopyFireSectionAsync(ctx, section, newEndDate));
            }

            // Full premium: 365 is the module's "no pro-rata" basis (CalculateProRataAsync).
            var figures = await PriceCopiedFireSectionsAsync(ctx, copies, 365, response);

            var description = FireEndorsementRows.NullIfBlank(ctx.Request.Description);
            var endDate = isExtension ? newEndDate : ctx.Source.EndDate;
            response.NewDetailID = await InsertFireEndorsementRecordAsync(ctx, ctx.Source.StartDate, endDate, description, figures);
            response.Description = description;
            response.StartDate = ctx.Source.StartDate;
            response.EndDate = endDate;
            response.SectionCount = copies.Count;
            response.RiskItemCount = copies.Sum(c => c.ItemCount);
        }

        #endregion

        #region Fire endorsements — pricing and saving new rows

        /// <summary>
        /// Prices one ADDITIONAL section the way CompleteFireCalculationAsync does: named (dynamic)
        /// section adjustments when supplied, otherwise the seven fixed section rates.
        /// </summary>
        private async Task<PricedEndorsementSection> PriceEndorsementSectionAsync(FireSectionDto section, decimal proportionRate)
        {
            if (section == null)
                throw new FireEndorsementRuleException("A section in the request is empty.");

            var riskItems = section.RiskItems ?? new List<CleanRiskItemInputDto>();
            foreach (var item in riskItems.Where(i => string.IsNullOrEmpty(i.SectionID)))
            {
                item.SectionID = section.SectionID;
            }

            var items = await CalculateRiskItemsAsync(new RiskItemCalculationRequestDto
            {
                RiskItems = riskItems,
                ProportionRate = proportionRate
            });
            if (!items.Success)
                throw new FireEndorsementRuleException($"Section {section.SectionID} calculation failed: {items.Message}");

            var aggregateSumInsured = items.TotalShareValue;
            var aggregatePremium = items.TotalNetPremiumAfterDiscounts;

            SectionAdjustmentResponseDto adjustments;
            FireSectionDynamicAdjustmentResponseDto dynamic = null;

            if ((section.Discounts?.Count ?? 0) > 0 || (section.Loadings?.Count ?? 0) > 0)
            {
                dynamic = await ApplySectionDynamicAdjustmentsAsync(new FireSectionDynamicAdjustmentRequestDto
                {
                    AggregateSumInsured = aggregateSumInsured,
                    AggregatePremium = aggregatePremium,
                    Discounts = section.Discounts ?? new List<FireAdjustmentDto>(),
                    Loadings = section.Loadings ?? new List<FireAdjustmentDto>()
                });
                if (!dynamic.Success)
                    throw new FireEndorsementRuleException($"Section {section.SectionID} adjustments failed: {dynamic.Message}");

                // Back-fills the fixed rate columns from recognised names, as the bulk save path does.
                adjustments = MapDynamicToFixedSectionAdjustments(dynamic, section);
            }
            else
            {
                adjustments = await ApplySectionAdjustmentsAsync(new SectionAdjustmentRequestDto
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
                if (!adjustments.Success)
                    throw new FireEndorsementRuleException($"Section {section.SectionID} adjustments failed: {adjustments.Message}");
            }

            return new PricedEndorsementSection
            {
                Input = section,
                Dynamic = dynamic,
                Result = new SectionCalculationResponseDto
                {
                    SectionID = section.SectionID,
                    SectionName = section.SectionName,
                    CalculatedItems = items.CalculatedItems ?? new List<CalculatedRiskItemDto>(),
                    SectionSumInsured = aggregateSumInsured,
                    SectionGrossPremium = aggregatePremium,
                    SectionNetPremium = adjustments.NetPremiumDue,
                    SectionAdjustments = adjustments,
                    Success = true
                }
            };
        }

        /// <summary>Proposal-level (other discount/loading) adjustments, then pro-rata.</summary>
        private async Task<(ProposalLevelAdjustmentResponseDto Adjustments, ProRataCalculationResponseDto ProRata)>
            PriceEndorsementRecordAsync(decimal totalPremium, decimal otherDiscountsRate, decimal otherLoadingsRate, int coverDays)
        {
            var adjustments = await ApplyProposalLevelAdjustmentsAsync(new ProposalAdjustmentRequestDto
            {
                TotalAggregatePremium = totalPremium,
                OtherDiscountsRate = otherDiscountsRate,
                OtherLoadingsRate = otherLoadingsRate
            });
            if (!adjustments.Success)
                throw new FireEndorsementRuleException($"Proposal-level adjustments failed: {adjustments.Message}");

            var proRata = await CalculateProRataAsync(new ProRataCalculationRequestDto
            {
                NetPremiumDue = adjustments.NetPremiumDue,
                CoverDays = coverDays
            });
            if (!proRata.Success)
                throw new FireEndorsementRuleException($"Pro-rata calculation failed: {proRata.Message}");

            return (adjustments, proRata);
        }

        /// <summary>
        /// Totals copied sections on the endorsed record's basis — its other discount/loading rates and
        /// the given cover days. Nothing is repriced: section figures are summed as stored. Saves the
        /// proposal-level adjustment row and returns the record figures.
        /// </summary>
        private async Task<Dictionary<string, object>> PriceCopiedFireSectionsAsync(
            FireEndorsementContext ctx, List<CopiedFireSection> copies, int coverDays, FireEndorsementResponseDto response)
        {
            var source = ctx.Source;
            var totalSumInsured = copies.Sum(c => c.SumInsured);
            var totalPremium = copies.Sum(c => c.NetPremium);

            var (adjustments, proRata) = await PriceEndorsementRecordAsync(
                totalPremium, source.OtherDiscountsRate, source.OtherLoadingsRate, coverDays);

            await InsertEndorsementAdjustmentsRowAsync(ctx, adjustments);
            FillEndorsementTotals(response, totalSumInsured, totalPremium, adjustments.NetPremiumDue, proRata, coverDays);

            return ComputedRecordFigures(totalSumInsured, totalPremium, proRata, coverDays,
                FireEndorsementRows.PositiveOr(null, source.ProportionRate, 100m),
                FireEndorsementRows.PositiveOr(null, source.ExRate, 1m),
                FireEndorsementRows.FirstText(source.ExCurrency, "NAIRA"),
                source.OtherDiscountsRate, source.OtherLoadingsRate);
        }

        /// <summary>Record figures — same formulas as CreateNewPolicyDetailsRecordAsync / UpdateExistingPolicyDetailsRecordAsync.</summary>
        private static Dictionary<string, object> ComputedRecordFigures(
            decimal totalSumInsured, decimal totalPremium, ProRataCalculationResponseDto proRata, int coverDays,
            decimal proportionRate, decimal exchangeRate, string currency, decimal otherDiscountsRate, decimal otherLoadingsRate)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["ExRate"] = exchangeRate,
                ["ExCurrency"] = currency,
                ["ProportionRate"] = proportionRate,
                ["SumInsured"] = totalSumInsured * (proportionRate / 100),
                ["GrossPremium"] = proRata.PremiumDue,
                ["SumInsuredFrgn"] = totalSumInsured * exchangeRate,
                ["GrossPremiumFrgn"] = proRata.PremiumDue * exchangeRate,
                ["ProRataDays"] = coverDays,
                ["ProRataPremium"] = proRata.ProRataPremium,
                ["NetAmount"] = proRata.PremiumDue,
                ["TotalRiskValue"] = totalSumInsured,
                ["A1"] = totalSumInsured,
                ["A2"] = totalPremium,
                ["A10"] = otherDiscountsRate,
                ["A11"] = otherLoadingsRate,
                ["A13"] = proRata.ProRataPremium,
                ["A14"] = proRata.PremiumDue
            };
        }

        private static void FillEndorsementTotals(FireEndorsementResponseDto response, decimal totalSumInsured,
            decimal totalPremium, decimal netPremium, ProRataCalculationResponseDto proRata, int coverDays)
        {
            response.TotalSumInsured = totalSumInsured;
            response.TotalPremium = totalPremium;
            response.NetPremium = netPremium;
            response.ProRataPremium = proRata.ProRataPremium;
            response.PremiumDue = proRata.PremiumDue;
            response.CoverDays = coverDays;
        }

        /// <summary>
        /// Saves one priced ADDITIONAL section like the normal fire save path (section row, risk items,
        /// item and section adjustment rows) with the endorsement's tag and cover period.
        /// Returns the number of risk items saved.
        /// </summary>
        private async Task<int> InsertPricedEndorsementSectionAsync(
            FireEndorsementContext ctx, PricedEndorsementSection priced, decimal proportionRate, DateTime coverFrom, DateTime coverTo)
        {
            var section = priced.Input;
            var result = priced.Result;
            var connection = ctx.Connection;
            var transaction = ctx.Transaction;

            // LEGACY (AddItemSectionTempGridDetails): every section gets its own PROPOSAL auto number in ENDTNum.
            var declarationNo = await NextDeclarationNoAsync(ctx.Policy.BranchID);

            const string sectionSql = @"
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

            using (var command = new SqlCommand(sectionSql, connection, transaction))
            {
                var p = command.Parameters;
                p.AddWithValue("@PolicyNo", ctx.PolicyNo);
                p.AddWithValue("@ENDTNum", declarationNo);
                p.AddWithValue("@SectionID", FireEndorsementRows.Db(result.SectionID));
                p.AddWithValue("@ContentSection", FireEndorsementRows.Db(result.SectionName));
                p.AddWithValue("@Location", result.CalculatedItems.FirstOrDefault()?.Location ?? section.Location ?? string.Empty);

                p.AddWithValue("@Field1", "SECTION");
                p.AddWithValue("@Field2", FireEndorsementRows.Db(result.SectionName));
                p.AddWithValue("@Field3", $"Items: {result.CalculatedItems.Count}");
                p.AddWithValue("@Field4", ctx.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                p.AddWithValue("@Field5", $"ENDORSEMENT_{ctx.Type}");
                p.AddWithValue("@Field20", ctx.CertOrDocNo);
                p.AddWithValue("@Field11", section.SrccLoadingRate.ToString("F2"));
                p.AddWithValue("@Field12", section.OtherLoading2Rate.ToString("F2"));
                p.AddWithValue("@Field26", FireEndorsementRows.Db(section.Field26));
                p.AddWithValue("@Field7", FireEndorsementRows.Db(section.Field7));
                p.AddWithValue("@Field8", FireEndorsementRows.Db(section.Field8));

                p.AddWithValue("@RiskSum", result.SectionSumInsured);
                p.AddWithValue("@OtherSum", 0);
                p.AddWithValue("@SectionPremium", result.SectionNetPremium);
                p.AddWithValue("@TStartDate", coverFrom);
                p.AddWithValue("@TEndDate", coverTo);

                p.AddWithValue("@A1", result.SectionSumInsured);
                p.AddWithValue("@A2", result.SectionGrossPremium);
                p.AddWithValue("@A3", result.SectionAdjustments?.SpecialDiscountAmount ?? 0);
                p.AddWithValue("@A4", result.SectionAdjustments?.DeductibleDiscountAmount ?? 0);
                p.AddWithValue("@A5", result.SectionAdjustments?.SpreadDiscountAmount ?? 0);
                p.AddWithValue("@A6", result.SectionAdjustments?.LtaDiscountAmount ?? 0);
                p.AddWithValue("@A7", result.SectionAdjustments?.TheftLoadingAmount ?? 0);
                p.AddWithValue("@A8", result.SectionAdjustments?.SrccLoadingAmount ?? 0);
                p.AddWithValue("@A9", result.SectionAdjustments?.OtherLoading2Amount ?? 0);
                p.AddWithValue("@A10", 0);
                p.AddWithValue("@A11", result.SectionNetPremium);
                p.AddWithValue("@A12", section.SpecialDiscountRate);
                p.AddWithValue("@A13", section.DeductibleDiscountRate);
                p.AddWithValue("@A14", section.SpreadDiscountRate);
                p.AddWithValue("@A15", section.LtaDiscountRate);
                p.AddWithValue("@A16", section.TheftLoadingRate);
                p.AddWithValue("@A17", result.CalculatedItems.Count);
                p.AddWithValue("@A18", GetTopLocationValueForSection(result.CalculatedItems));
                p.AddWithValue("@A19", result.SectionSumInsured * (proportionRate / 100));
                p.AddWithValue("@A20", result.SectionNetPremium * (proportionRate / 100));

                p.AddWithValue("@Tag", ctx.RowTag);
                p.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                p.AddWithValue("@SubmitBy", ctx.CreatedBy);
                p.AddWithValue("@SubmitOn", ctx.Now);

                await command.ExecuteNonQueryAsync();
            }

            // Items link to the section through Field10 = declaration number (AddItemToTempGridDetails).
            // A17 = stock discount rate, which the breakdown reads back.
            const string itemSql = @"
INSERT INTO PolicyPremDetails (
    PolicyNo, ENDTNum, Section, ContentDesc, SMICode, SMIDetail, Location,
    Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9, Field10,
    A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15, A16, A17,
    Tag, TransGUID, SubmitBy, SubmitOn
) VALUES (
    @PolicyNo, @ENDTNum, @Section, @ContentDesc, @SMICode, @SMIDetail, @Location,
    @Field1, @Field2, @Field3, @Field4, @Field5, @Field6, @Field7, @Field8, @Field9, @Field10,
    @A1, @A2, @A3, @A4, @A5, @A6, @A7, @A8, @A9, @A10, @A11, @A12, @A13, @A14, @A15, @A16, @A17,
    @Tag, @TransGUID, @SubmitBy, @SubmitOn
)";

            foreach (var riskItem in result.CalculatedItems)
            {
                // LEGACY: a fresh CERTNO auto number for every risk item row.
                var itemCertNo = await NextItemCertNoAsync(ctx.Policy.BranchID);

                using (var command = new SqlCommand(itemSql, connection, transaction))
                {
                    var p = command.Parameters;
                    p.AddWithValue("@PolicyNo", ctx.PolicyNo);
                    p.AddWithValue("@ENDTNum", itemCertNo);
                    p.AddWithValue("@Section", FireEndorsementRows.Db(result.SectionID));
                    p.AddWithValue("@ContentDesc", riskItem.ItemDescription ?? string.Empty);
                    p.AddWithValue("@SMICode", riskItem.SMICode ?? string.Empty);
                    p.AddWithValue("@SMIDetail", riskItem.SMIDetail ?? riskItem.ItemDescription ?? string.Empty);
                    p.AddWithValue("@Location", riskItem.Location ?? string.Empty);

                    p.AddWithValue("@Field1", FireEndorsementRows.Db(result.SectionID));
                    p.AddWithValue("@Field2", ctx.CertOrDocNo);
                    p.AddWithValue("@Field3", FireEndorsementRows.Db(result.SectionID));
                    p.AddWithValue("@Field4", "RISK_ITEM");
                    p.AddWithValue("@Field5", riskItem.Location ?? string.Empty);
                    p.AddWithValue("@Field6", riskItem.StockItem?.StockCode ?? string.Empty);
                    p.AddWithValue("@Field7", riskItem.StockItem?.StockDescription ?? string.Empty);
                    p.AddWithValue("@Field8", riskItem.StockItem?.StockSumInsured.ToString("F2") ?? "0");
                    p.AddWithValue("@Field9", riskItem.StockItem?.StockRate.ToString("F2") ?? "0");
                    p.AddWithValue("@Field10", declarationNo);

                    p.AddWithValue("@A1", riskItem.ActualValue);
                    p.AddWithValue("@A2", riskItem.ItemRate);
                    p.AddWithValue("@A3", riskItem.ActualPremium);
                    p.AddWithValue("@A4", riskItem.ShareValue);
                    p.AddWithValue("@A5", riskItem.PremiumValue);
                    p.AddWithValue("@A6", riskItem.MultiplyRate);
                    p.AddWithValue("@A7", riskItem.StockSumInsured);
                    p.AddWithValue("@A8", riskItem.StockGrossPremium);
                    p.AddWithValue("@A9", riskItem.StockDiscountAmount);
                    p.AddWithValue("@A10", riskItem.StockNetPremium);
                    p.AddWithValue("@A11", riskItem.TotalSumInsured);
                    p.AddWithValue("@A12", riskItem.TotalGrossPremium);
                    p.AddWithValue("@A13", riskItem.FeaDiscountRate);
                    p.AddWithValue("@A14", riskItem.FeaDiscountAmount);
                    p.AddWithValue("@A15", riskItem.NetPremiumAfterDiscounts);
                    p.AddWithValue("@A16", riskItem.ItemNo);
                    p.AddWithValue("@A17", riskItem.StockItem?.StockDiscountRate ?? 0);

                    p.AddWithValue("@Tag", ctx.RowTag);
                    p.AddWithValue("@TransGUID", Guid.NewGuid().ToString());
                    p.AddWithValue("@SubmitBy", ctx.CreatedBy);
                    p.AddWithValue("@SubmitOn", ctx.Now);

                    await command.ExecuteNonQueryAsync();
                }

                await SaveFireRiskItemDynamicAdjustmentsAsync(ctx.PolicyNo, ctx.CertOrDocNo, declarationNo, itemCertNo,
                    result.SectionID, riskItem.AppliedLoadings, "LOADING", connection, transaction);
                await SaveFireRiskItemDynamicAdjustmentsAsync(ctx.PolicyNo, ctx.CertOrDocNo, declarationNo, itemCertNo,
                    result.SectionID, riskItem.AppliedDiscounts, "DISCOUNT", connection, transaction);
            }

            if (priced.Dynamic != null)
            {
                await SaveFireSectionDynamicAdjustmentsWithCertAsync(ctx.PolicyNo, ctx.CertOrDocNo, declarationNo,
                    priced.Dynamic.DiscountsApplied ?? new List<FireAdjustmentDto>(), "DISCOUNT", connection, transaction);
                await SaveFireSectionDynamicAdjustmentsWithCertAsync(ctx.PolicyNo, ctx.CertOrDocNo, declarationNo,
                    priced.Dynamic.LoadingsApplied ?? new List<FireAdjustmentDto>(), "LOADING", connection, transaction);
            }

            return result.CalculatedItems.Count;
        }

        /// <summary>
        /// The record's proposal-level adjustment row. Goes through InsertAsync so every string is
        /// width-checked against the live schema rather than failing as an unattributed SQL truncation.
        /// </summary>
        private static async Task InsertEndorsementAdjustmentsRowAsync(FireEndorsementContext ctx, ProposalLevelAdjustmentResponseDto adjustments)
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["PolicyNo"] = ctx.PolicyNo,
                ["ENDTNum"] = ctx.EndorsementNo,
                ["SectionID"] = "PROPOSAL_ADJUSTMENTS",
                ["ContentSection"] = "Endorsement Proposal Level Adjustments",
                ["Field20"] = ctx.CertOrDocNo,
                ["Field1"] = "ENDORSEMENT_PROPOSAL_ADJUSTMENTS",
                ["Field18"] = adjustments.StartingPremium.ToString(CultureInfo.InvariantCulture),
                ["Field28"] = adjustments.OtherDiscountsAmount.ToString(CultureInfo.InvariantCulture),
                ["Field29"] = adjustments.OtherLoadingsAmount.ToString(CultureInfo.InvariantCulture),
                ["A1"] = adjustments.StartingPremium,
                ["A2"] = adjustments.NetPremiumDue,
                ["A10"] = adjustments.OtherDiscountsAmount,
                ["A11"] = adjustments.OtherLoadingsAmount,
                ["Tag"] = "PROPOSAL_ADJUSTMENTS",
                ["TransGUID"] = Guid.NewGuid().ToString(),
                ["SubmitBy"] = ctx.CreatedBy,
                ["SubmitOn"] = ctx.Now
            };

            var shape = await FireEndorsementRows.GetShapeAsync(FireSectionsTable, ctx.Connection, ctx.Transaction);
            await FireEndorsementRows.InsertAsync(FireSectionsTable, shape.OnlyKnown(row), ctx.Connection, ctx.Transaction);
        }

        /// <summary>Inserts the endorsement's PolicyDetails record (UNCOMPLETED) and returns its DetailID.</summary>
        private async Task<string> InsertFireEndorsementRecordAsync(
            FireEndorsementContext ctx, DateTime? startDate, DateTime? endDate, string description, Dictionary<string, object> figures)
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
                // Short on purpose: the renewal path puts ~38 chars in this column, so its width is
                // modest. The endorsement's own EndorsementNo/CertOrDocNo carry the detail.
                ["Field2"] = $"{ctx.Type} of {ctx.Source.DetailID}",
                ["Field4"] = description,
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

        #region Fire endorsements — copying original sections

        /// <summary>
        /// Copies one original section into the endorsement: the section row, its risk items (each with a
        /// fresh CERTNO), their item-level adjustment rows and the section-level adjustment rows. Every column
        /// comes across exactly as stored; only the links (cert, declaration no., item CERTNO), the tag and
        /// the audit columns change. An EXTENSION passes its new end date for TEndDate.
        /// </summary>
        private async Task<CopiedFireSection> CopyFireSectionAsync(
            FireEndorsementContext ctx, FireEndorsementSection source, DateTime? newSectionEndDate)
        {
            var connection = ctx.Connection;
            var transaction = ctx.Transaction;
            var sectionShape = await FireEndorsementRows.GetShapeAsync(FireSectionsTable, connection, transaction);
            var itemShape = await FireEndorsementRows.GetShapeAsync(FireItemsTable, connection, transaction);

            var declarationNo = await NextDeclarationNoAsync(ctx.Policy.BranchID);
            var token = Guid.NewGuid().ToString();

            var section = new Dictionary<string, object>(source.Values, StringComparer.OrdinalIgnoreCase)
            {
                ["Field20"] = ctx.CertOrDocNo,
                ["ENDTNum"] = declarationNo,
                ["Tag"] = ctx.RowTag,
                ["TransGUID"] = token,
                ["SubmitBy"] = ctx.CreatedBy,
                ["SubmitOn"] = ctx.Now
            };
            section.Remove(SectionStampColumn); // a copy starts unstamped
            if (newSectionEndDate.HasValue) section["TEndDate"] = newSectionEndDate.Value;
            await FireEndorsementRows.InsertAsync(FireSectionsTable, sectionShape.OnlyKnown(section), connection, transaction);

            var items = await LoadFireSectionItemsAsync(ctx.PolicyNo, source, itemShape, connection, transaction);
            var itemAdjustments = await LoadFireItemAdjustmentsAsync(ctx.PolicyNo, source.DeclarationNo, itemShape, connection, transaction);

            foreach (var (itemValues, _) in items)
            {
                var sourceItemCertNo = FireEndorsementRows.Text(itemValues, "ENDTNum");
                var itemCertNo = await NextItemCertNoAsync(ctx.Policy.BranchID);

                var item = new Dictionary<string, object>(itemValues, StringComparer.OrdinalIgnoreCase)
                {
                    ["ENDTNum"] = itemCertNo,
                    ["Field2"] = ctx.CertOrDocNo,
                    ["Field10"] = declarationNo,
                    ["Tag"] = ctx.RowTag,
                    ["TransGUID"] = Guid.NewGuid().ToString(),
                    ["SubmitBy"] = ctx.CreatedBy,
                    ["SubmitOn"] = ctx.Now
                };
                if (string.IsNullOrEmpty(source.DeclarationNo))
                {
                    // Rows saved before the legacy numbering pointed at the section's TransGUID through
                    // Field1; the copy uses the Field10 link like everything else.
                    item["Field1"] = source.SectionID;
                }
                await FireEndorsementRows.InsertAsync(FireItemsTable, itemShape.OnlyKnown(item), connection, transaction);

                if (!string.IsNullOrEmpty(sourceItemCertNo) && itemAdjustments.TryGetValue(sourceItemCertNo, out var adjustmentRows))
                {
                    foreach (var adjustment in adjustmentRows)
                    {
                        var adjustmentCopy = new Dictionary<string, object>(adjustment, StringComparer.OrdinalIgnoreCase)
                        {
                            ["ENDTNum"] = itemCertNo,
                            ["Field10"] = ctx.CertOrDocNo,
                            ["Field11"] = declarationNo,
                            ["TransGUID"] = Guid.NewGuid().ToString(),
                            ["SubmitBy"] = ctx.CreatedBy,
                            ["SubmitOn"] = ctx.Now
                        };
                        await FireEndorsementRows.InsertAsync(FireItemsTable, itemShape.OnlyKnown(adjustmentCopy), connection, transaction);
                    }
                }
            }

            // Section-level named adjustments hang off the section's declaration number.
            if (!string.IsNullOrEmpty(source.DeclarationNo))
            {
                var sectionAdjustments = await FireEndorsementRows.ReadAsync(itemShape,
                    "PolicyNo = @PolicyNo AND ENDTNum = @DeclarationNo AND Tag IN ('FIRE_SECTION_DISCOUNT', 'FIRE_SECTION_LOADING')",
                    null, false, connection, transaction,
                    ("@PolicyNo", ctx.PolicyNo), ("@DeclarationNo", source.DeclarationNo));

                foreach (var (adjustment, _) in sectionAdjustments)
                {
                    var adjustmentCopy = new Dictionary<string, object>(adjustment, StringComparer.OrdinalIgnoreCase)
                    {
                        ["ENDTNum"] = declarationNo,
                        ["Field10"] = ctx.CertOrDocNo,
                        ["TransGUID"] = Guid.NewGuid().ToString(),
                        ["SubmitBy"] = ctx.CreatedBy,
                        ["SubmitOn"] = ctx.Now
                    };
                    await FireEndorsementRows.InsertAsync(FireItemsTable, itemShape.OnlyKnown(adjustmentCopy), connection, transaction);
                }
            }

            return new CopiedFireSection
            {
                Token = token,
                DeclarationNo = declarationNo,
                ItemCount = items.Count,
                SumInsured = source.SumInsured,
                NetPremium = source.NetPremium
            };
        }

        /// <summary>
        /// Risk items of a section: Field10 = declaration number, or the pre-numbering TransGUID link.
        ///
        /// The adjustment-tag exclusion is load-bearing. PolicyPremDetails holds three kinds of row and
        /// Field10 means something different on each: on a risk item it is the section's declaration
        /// number, but on a FIRE_ITEM_* or FIRE_SECTION_* adjustment row it is the record's cert. Those
        /// come from different auto-number series (PROPOSAL vs DECLARATION_NO), so they do not normally
        /// collide — but the series are configured per branch, and if one ever did, adjustment rows would
        /// be copied as risk items and their rate/amount read as item figures. Tag is the reliable
        /// discriminator, so exclude by tag rather than trusting the numbering.
        ///
        /// Excluded by tag, not filtered to a whitelist: a copy's risk items carry the endorsement's tag
        /// (ADDITIONAL, REVERSAL, RETURN, NEW, EXTENSION, REDUCTION) rather than RISK_ITEM, so a positive
        /// list would silently miss rows the next endorsement type adds.
        /// </summary>
        private static async Task<List<(Dictionary<string, object> Values, object Key)>> LoadFireSectionItemsAsync(
            string policyNo, FireEndorsementSection section, FireEndorsementRows.TableShape itemShape,
            SqlConnection connection, SqlTransaction transaction)
        {
            if (!string.IsNullOrEmpty(section.DeclarationNo))
            {
                return await FireEndorsementRows.ReadAsync(itemShape,
                    @"PolicyNo = @PolicyNo AND Field10 = @DeclarationNo
  AND ISNULL(Tag, '') NOT IN ('FIRE_ITEM_DISCOUNT', 'FIRE_ITEM_LOADING',
                              'FIRE_SECTION_DISCOUNT', 'FIRE_SECTION_LOADING')",
                    "A16", false, connection, transaction,
                    ("@PolicyNo", policyNo), ("@DeclarationNo", section.DeclarationNo));
            }

            if (!string.IsNullOrEmpty(section.TransGuid))
            {
                return await FireEndorsementRows.ReadAsync(itemShape,
                    "PolicyNo = @PolicyNo AND Field1 = @SectionGuid AND Tag = 'RISK_ITEM'", "A16", false, connection, transaction,
                    ("@PolicyNo", policyNo), ("@SectionGuid", section.TransGuid));
            }

            return new List<(Dictionary<string, object> Values, object Key)>();
        }

        /// <summary>Item-level adjustment rows of a section, grouped by the parent item's CERTNO.</summary>
        private static async Task<Dictionary<string, List<Dictionary<string, object>>>> LoadFireItemAdjustmentsAsync(
            string policyNo, string declarationNo, FireEndorsementRows.TableShape itemShape,
            SqlConnection connection, SqlTransaction transaction)
        {
            var byItem = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(declarationNo)) return byItem;

            var rows = await FireEndorsementRows.ReadAsync(itemShape,
                "PolicyNo = @PolicyNo AND Field11 = @DeclarationNo AND Tag IN ('FIRE_ITEM_DISCOUNT', 'FIRE_ITEM_LOADING')",
                null, false, connection, transaction,
                ("@PolicyNo", policyNo), ("@DeclarationNo", declarationNo));

            foreach (var (values, _) in rows)
            {
                var itemCertNo = FireEndorsementRows.Text(values, "ENDTNum");
                if (string.IsNullOrEmpty(itemCertNo)) continue;

                if (!byItem.TryGetValue(itemCertNo, out var list))
                {
                    list = new List<Dictionary<string, object>>();
                    byItem[itemCertNo] = list;
                }
                list.Add(values);
            }

            return byItem;
        }

        /// <summary>REVERSAL: copies the original record's proposal-level adjustment row(s). Returns the first row's net premium.</summary>
        private static async Task<decimal?> CopyFireProposalAdjustmentRowsAsync(FireEndorsementContext ctx)
        {
            var shape = await FireEndorsementRows.GetShapeAsync(FireSectionsTable, ctx.Connection, ctx.Transaction);
            var rows = await FireEndorsementRows.ReadAsync(shape,
                @"PolicyNo = @PolicyNo AND Field20 = @CertOrDocNo
  AND (Tag = 'PROPOSAL_ADJUSTMENTS' OR ISNULL(Field1, '') IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS'))",
                "SubmitOn", false, ctx.Connection, ctx.Transaction,
                ("@PolicyNo", ctx.PolicyNo), ("@CertOrDocNo", ctx.Source.CertOrDocNo));

            decimal? netPremium = null;
            foreach (var (values, _) in rows)
            {
                var copy = new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase)
                {
                    ["Field20"] = ctx.CertOrDocNo,
                    ["ENDTNum"] = ctx.EndorsementNo,
                    ["TransGUID"] = Guid.NewGuid().ToString(),
                    ["SubmitBy"] = ctx.CreatedBy,
                    ["SubmitOn"] = ctx.Now
                };
                await FireEndorsementRows.InsertAsync(FireSectionsTable, shape.OnlyKnown(copy), ctx.Connection, ctx.Transaction);
                netPremium ??= FireEndorsementRows.Number(values, "A2");
            }

            return netPremium;
        }

        #endregion

        #region Fire endorsements — validation, locking, numbering

        private static List<string> ValidateFireEndorsementRequest(
            string type, FireEndorsementRequestDto request, List<string> warnings)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.PolicyNo)) errors.Add("PolicyNo is required");
            if (string.IsNullOrWhiteSpace(request.SourceDetailID)) errors.Add("SourceDetailID is required");

            var sections = request.Sections ?? new List<FireSectionDto>();
            var tokens = (request.SelectedSectionTokens ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            switch (type)
            {
                case EndorsementTypes.Additional:
                    if (sections.Count == 0)
                        errors.Add("ADDITIONAL needs at least one section in Sections");
                    for (var i = 0; i < sections.Count; i++)
                    {
                        var section = sections[i];
                        if (string.IsNullOrWhiteSpace(section?.SectionID))
                            errors.Add($"Sections[{i}]: SectionID is required");
                        if ((section?.RiskItems?.Count ?? 0) == 0)
                            errors.Add($"Sections[{i}] ({section?.SectionID}): at least one risk item is required");
                    }
                    if (tokens.Count > 0)
                        warnings.Add("SelectedSectionTokens is ignored for ADDITIONAL — it leaves the original sections alone.");
                    break;

                case EndorsementTypes.Return:
                case EndorsementTypes.Redo:
                    if (tokens.Count == 0)
                        errors.Add($"{type} needs at least one ticked section in SelectedSectionTokens");
                    if (sections.Count > 0)
                        warnings.Add($"Sections is ignored for {type} — the ticked sections are copied at their original figures.");
                    break;

                case EndorsementTypes.Extension:
                    if (!request.NewEndDate.HasValue)
                        errors.Add("EXTENSION needs NewEndDate");
                    goto case EndorsementTypes.Reduction;

                case EndorsementTypes.Reversal:
                case EndorsementTypes.Reduction:
                    if (sections.Count > 0)
                        warnings.Add($"Sections is ignored for {type} — every section on cover is copied at its original figures.");
                    if (tokens.Count > 0)
                        warnings.Add($"SelectedSectionTokens is ignored for {type} — it applies to every section on cover.");
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

        /// <summary>
        /// One endorsement per policy at a time. Endorsing reads rows, then numbers, expires and stamps
        /// them; two at once would collide on the count-based endorsement number and could both copy a
        /// section the other just expired. Released with the transaction.
        /// </summary>
        private static async Task AcquireFireEndorsementLockAsync(
            string policyNo, SqlConnection connection, SqlTransaction transaction)
        {
            using var command = new SqlCommand("sp_getapplock", connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@Resource", $"FIRE_ENDORSEMENT::{policyNo}");
            command.Parameters.AddWithValue("@LockMode", "Exclusive");
            command.Parameters.AddWithValue("@LockOwner", "Transaction");
            command.Parameters.AddWithValue("@LockTimeout", 15000);
            var returnValue = command.Parameters.Add("@ReturnValue", SqlDbType.Int);
            returnValue.Direction = ParameterDirection.ReturnValue;

            await command.ExecuteNonQueryAsync();

            // 0 = granted, 1 = granted after waiting.
            var result = returnValue.Value is int value ? value : -1;
            if (result < 0)
                throw new FireEndorsementRuleException(
                    $"Another endorsement is being saved on policy {policyNo}. Try again in a moment.");
        }

        /// <summary>Issues the endorsement number and the record's cert number, in that order.</summary>
        private async Task IssueEndorsementNumbersAsync(FireEndorsementContext ctx)
        {
            ctx.EndorsementNo = await NextFireEndorsementNumberAsync(ctx.PolicyNo, ctx.Connection, ctx.Transaction);

            var subRiskId = FireEndorsementRows.FirstText(ctx.Policy.SubRiskID, ctx.Source.SubRiskID);
            if (string.IsNullOrWhiteSpace(subRiskId))
                throw new FireEndorsementRuleException(
                    $"Policy {ctx.PolicyNo} has no SubRiskID — the endorsement's certificate number can't be generated.");

            ctx.CertOrDocNo = await NextCalcCertAsync(ctx.Policy.BranchID, subRiskId, MapEndorseType(ctx.Type));
        }

        /// <summary>
        /// END-{policy}-{n}. Same format the module has always used; safe against collisions because
        /// the caller holds the per-policy lock.
        /// </summary>
        private static async Task<string> NextFireEndorsementNumberAsync(
            string policyNo, SqlConnection connection, SqlTransaction transaction)
        {
            const string sql = @"
SELECT COUNT(*)
FROM PolicyDetails
WHERE PolicyNo = @PolicyNo AND ISNULL(EndorsementNo, '') <> '' AND Deleted = 0";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@PolicyNo", policyNo);
            var count = (int)FireEndorsementRows.Number(await command.ExecuteScalarAsync());
            return $"END-{policyNo}-{(count + 1):D4}";
        }

        #endregion

        #region Fire endorsements — reading the record being endorsed

        /// <summary>Loads the record being endorsed, locked for the transaction.</summary>
        private static async Task<FireEndorsementSource> LoadFireEndorsementSourceAsync(
            string policyNo, string detailId, SqlConnection connection, SqlTransaction transaction)
        {
            var shape = await FireEndorsementRows.GetShapeAsync("PolicyDetails", connection, transaction);
            var rows = await FireEndorsementRows.ReadAsync(shape,
                "PolicyNo = @PolicyNo AND DetailID = @DetailID AND Deleted = 0",
                null, true, connection, transaction,
                ("@PolicyNo", policyNo), ("@DetailID", detailId));

            return rows.Count == 0 ? null : new FireEndorsementSource(rows[0].Values);
        }

        /// <summary>
        /// Sections of a record — every PolicyTempDetails row for its cert except the proposal-level
        /// adjustment row. forUpdate locks them for the transaction.
        /// </summary>
        private static async Task<List<FireEndorsementSection>> LoadFireEndorsementSectionsAsync(
            string policyNo, string certOrDocNo, bool forUpdate, SqlConnection connection, SqlTransaction transaction)
        {
            var shape = await FireEndorsementRows.GetShapeAsync(FireSectionsTable, connection, transaction);
            var rows = await FireEndorsementRows.ReadAsync(shape,
                @"PolicyNo = @PolicyNo AND Field20 = @CertOrDocNo
  AND ISNULL(SectionID, '') <> 'PROPOSAL_ADJUSTMENTS'
  AND ISNULL(Tag, '') <> 'PROPOSAL_ADJUSTMENTS'
  AND ISNULL(Field1, '') NOT IN ('PROPOSAL_ADJUSTMENTS', 'ENDORSEMENT_PROPOSAL_ADJUSTMENTS')",
                "SubmitOn", forUpdate, connection, transaction,
                ("@PolicyNo", policyNo), ("@CertOrDocNo", certOrDocNo));

            return rows.Select(row => new FireEndorsementSection(row.Values, row.Key)).ToList();
        }

        /// <summary>Matches the ticked tokens onto in-force sections. Throws on anything unmatched, expired or duplicated.</summary>
        private static List<FireEndorsementSection> ResolveTickedFireSections(
            List<FireEndorsementSection> sections, List<string> tokens)
        {
            var wanted = (tokens ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var ticked = new List<FireEndorsementSection>();
            var missing = new List<string>();
            var offCover = new List<string>();

            foreach (var token in wanted)
            {
                var matches = sections.Where(s =>
                    string.Equals(s.SelectionToken, token, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.DeclarationNo, token, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matches.Count == 0) { missing.Add(token); continue; }
                if (matches.Count > 1)
                    throw new FireEndorsementRuleException(
                        $"Token '{token}' matches {matches.Count} sections on this record. Use the SelectionToken from " +
                        "GET endorsement/fire/sections, which is unique per section.");

                var section = matches[0];
                if (section.IsExpired) { offCover.Add(section.Label); continue; }
                ticked.Add(section);
            }

            if (missing.Count > 0)
                throw new FireEndorsementRuleException(
                    $"These sections are not on this record: {string.Join(", ", missing)}. " +
                    "Tokens come from GET endorsement/fire/sections for the same DetailID.");

            if (offCover.Count > 0)
                throw new FireEndorsementRuleException(
                    $"These sections are already off cover from an earlier endorsement and can't be endorsed again: {string.Join(", ", offCover)}.");

            if (ticked.Count == 0)
                throw new FireEndorsementRuleException("No sections were ticked.");

            return ticked;
        }

        private static string DescribeStamp(FireEndorsementSection section) =>
            string.IsNullOrWhiteSpace(section.Stamp) ? "an earlier endorsement" : $"endorsement {section.Stamp}";

        #endregion

        #region Fire endorsements — expiring and stamping originals

        /// <summary>REVERSAL and REDO: sets the endorsed record's BizOption to EXPIRED.</summary>
        private static async Task ExpireFireEndorsementSourceAsync(FireEndorsementContext ctx)
        {
            // Only BizOption and the audit columns. An earlier version also wrote a prose note into
            // Field3; nothing else in the module writes that column, so its width is unknown and it may
            // well hold real data in another module sharing this table. BizOption = EXPIRED plus the
            // endorsement's own record is the full story anyway.
            const string sql = @"
UPDATE PolicyDetails
SET BizOption = @Expired,
    ModifiedBy = @ModifiedBy,
    ModifiedOn = @ModifiedOn
WHERE PolicyNo = @PolicyNo AND DetailID = @DetailID AND Deleted = 0";

            using var command = new SqlCommand(sql, ctx.Connection, ctx.Transaction);
            command.Parameters.AddWithValue("@Expired", EndorsementTypes.Expired);
            command.Parameters.AddWithValue("@ModifiedBy", ctx.CreatedBy);
            command.Parameters.AddWithValue("@ModifiedOn", ctx.Now);
            command.Parameters.AddWithValue("@PolicyNo", ctx.PolicyNo);
            command.Parameters.AddWithValue("@DetailID", ctx.Source.DetailID);

            if (await command.ExecuteNonQueryAsync() == 0)
                throw new FireEndorsementRuleException(
                    $"Could not set record {ctx.Source.DetailID} to EXPIRED — it may have been changed by someone else.");
        }

        /// <summary>
        /// Marks an original section. expire sets its Tag (and its risk items') to EXPIRED; stamp writes
        /// the endorsement number into the stamp column while the Tag is left as it is. A REVERSAL stamps
        /// without expiring (the record itself expires), a RETURN does both, a REDO expires without stamping.
        /// </summary>
        private static async Task MarkOriginalFireSectionAsync(
            FireEndorsementContext ctx, FireEndorsementSection section, bool expire, bool stamp)
        {
            if (!expire && !stamp) return;

            // The stamp is an identifier: a truncated one breaks the section -> endorsement link
            // silently, so check it against the real column width and fail loudly instead.
            if (stamp)
            {
                var shape = await FireEndorsementRows.GetShapeAsync(FireSectionsTable, ctx.Connection, ctx.Transaction);
                FireEndorsementRows.RequireFits(shape, SectionStampColumn, ctx.EndorsementNo);
            }

            var sets = new List<string>();
            if (expire) sets.Add("[Tag] = @Expired");
            if (stamp) sets.Add($"{FireEndorsementRows.Q(SectionStampColumn)} = @Stamp");
            sets.Add("[SubmitBy] = @SubmitBy");
            sets.Add("[SubmitOn] = @SubmitOn");

            using (var command = new SqlCommand(
                $"UPDATE {FireEndorsementRows.Q(FireSectionsTable)} SET {string.Join(", ", sets)} " +
                $"WHERE [PolicyNo] = @PolicyNo AND {section.KeyPredicate}",
                ctx.Connection, ctx.Transaction))
            {
                if (expire) command.Parameters.AddWithValue("@Expired", EndorsementTypes.Expired);
                if (stamp) command.Parameters.AddWithValue("@Stamp", ctx.EndorsementNo);
                command.Parameters.AddWithValue("@SubmitBy", ctx.CreatedBy);
                command.Parameters.AddWithValue("@SubmitOn", ctx.Now);
                command.Parameters.AddWithValue("@PolicyNo", ctx.PolicyNo);
                command.Parameters.AddWithValue("@KeyValue", section.KeyValue);

                if (await command.ExecuteNonQueryAsync() == 0)
                    throw new FireEndorsementRuleException(
                        $"Could not update section {section.Label} — it may have been changed by someone else.");
            }

            // A section off cover takes its risk items with it.
            if (!expire) return;

            var itemPredicate = !string.IsNullOrEmpty(section.DeclarationNo)
                ? "[Field10] = @Link"
                : "[Field1] = @Link AND [Tag] = 'RISK_ITEM'";
            var link = !string.IsNullOrEmpty(section.DeclarationNo) ? section.DeclarationNo : section.TransGuid;
            if (string.IsNullOrEmpty(link)) return;

            using var itemCommand = new SqlCommand($@"
UPDATE {FireEndorsementRows.Q(FireItemsTable)}
SET [Tag] = @Expired, [SubmitBy] = @SubmitBy, [SubmitOn] = @SubmitOn
WHERE [PolicyNo] = @PolicyNo AND {itemPredicate}", ctx.Connection, ctx.Transaction);
            itemCommand.Parameters.AddWithValue("@Expired", EndorsementTypes.Expired);
            itemCommand.Parameters.AddWithValue("@SubmitBy", ctx.CreatedBy);
            itemCommand.Parameters.AddWithValue("@SubmitOn", ctx.Now);
            itemCommand.Parameters.AddWithValue("@PolicyNo", ctx.PolicyNo);
            itemCommand.Parameters.AddWithValue("@Link", link);
            await itemCommand.ExecuteNonQueryAsync();
        }

        #endregion

        #region Fire endorsements — failure handling

        private static FireEndorsementResponseDto FailFireEndorsement(
            FireEndorsementResponseDto response, string message, List<string> errors = null)
        {
            response.Success = false;
            response.Message = message;
            response.Status = null;
            if (errors != null) response.ValidationErrors.AddRange(errors);
            return response;
        }

        private void RollbackFireEndorsement(SqlTransaction transaction)
        {
            try { transaction.Rollback(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Rollback failed for fire endorsement"); }
        }

        /// <summary>A broken rule the caller can fix — reported as a message, not a 500.</summary>
        private class FireEndorsementRuleException : Exception
        {
            public FireEndorsementRuleException(string message) : base(message) { }
        }

        #endregion

        #region Fire endorsements — working types

        /// <summary>Everything one endorsement needs while it runs.</summary>
        private class FireEndorsementContext
        {
            public string Type { get; set; }

            /// <summary>Tag written on the section/item rows this endorsement creates (NEW for a REDO).</summary>
            public string RowTag { get; set; }

            public FireEndorsementRequestDto Request { get; set; }
            public ProposalDto Policy { get; set; }
            public FireEndorsementSource Source { get; set; }
            public string CreatedBy { get; set; }
            public DateTime Now { get; set; }
            public SqlConnection Connection { get; set; }
            public SqlTransaction Transaction { get; set; }

            /// <summary>Set by IssueEndorsementNumbersAsync.</summary>
            public string EndorsementNo { get; set; }
            public string CertOrDocNo { get; set; }

            public string PolicyNo => Request?.PolicyNo;
        }

        /// <summary>The PolicyDetails row being endorsed.</summary>
        private class FireEndorsementSource
        {
            private readonly Dictionary<string, object> _values;

            public FireEndorsementSource(Dictionary<string, object> values) => _values = values;

            public object Value(string column) => _values.TryGetValue(column, out var value) ? value : null;
            public decimal Number(string column) => FireEndorsementRows.Number(_values, column);
            public string Text(string column) => FireEndorsementRows.Text(_values, column);

            public string DetailID => Text("DetailID");
            public string CertOrDocNo => Text("CertOrDocNo");
            public string SubRiskID => Text("SubRiskID");
            public string InsuredName => Text("InsuredName");
            public string BizOption => Text("BizOption");
            public DateTime? StartDate => FireEndorsementRows.Date(_values, "StartDate");
            public DateTime? EndDate => FireEndorsementRows.Date(_values, "EndDate");

            public decimal ExRate => Number("ExRate");
            public string ExCurrency => Text("ExCurrency");
            public decimal ProportionRate => Number("ProportionRate");
            public decimal OtherDiscountsRate => Number("A10");
            public decimal OtherLoadingsRate => Number("A11");

            public int ProRataDays => (int)Number("ProRataDays");

            /// <summary>Cover days for pricing copies off this record; 365 (no pro-rata) when it holds nothing usable.</summary>
            public int PricingDays => ProRataDays > 0 ? ProRataDays : 365;

            /// <summary>True once an earlier REVERSAL or REDO expired this record.</summary>
            public bool IsExpired => string.Equals(BizOption, EndorsementTypes.Expired, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>One section row of the record being endorsed.</summary>
        private class FireEndorsementSection
        {
            public FireEndorsementSection(Dictionary<string, object> values, object key)
            {
                Values = values;
                KeyValue = key;
            }

            public Dictionary<string, object> Values { get; }

            /// <summary>Primary key value, or the TransGUID/declaration number when the table has no identity column.</summary>
            public object KeyValue { get; }

            /// <summary>WHERE clause for this row alone, against @KeyValue.</summary>
            public string KeyPredicate => FireEndorsementRows.KeyPredicateFor(FireSectionsTable, Values);

            public string DeclarationNo => FireEndorsementRows.Text(Values, "ENDTNum");
            public string TransGuid => FireEndorsementRows.Text(Values, "TransGUID");
            public string SectionID => FireEndorsementRows.Text(Values, "SectionID");
            public string SectionName => FireEndorsementRows.Text(Values, "ContentSection");
            public string Location => FireEndorsementRows.Text(Values, "Location");
            public string Tag => FireEndorsementRows.Text(Values, "Tag");
            public string Stamp => FireEndorsementRows.Text(Values, SectionStampColumn);

            public decimal SumInsured => FireEndorsementRows.Number(Values, "A1");
            public decimal NetPremium => FireEndorsementRows.Number(Values, "A11");

            /// <summary>Token the tick list hands out — stable per row.</summary>
            public string SelectionToken => FireEndorsementRows.FirstText(TransGuid, DeclarationNo);

            /// <summary>True once an earlier RETURN or REDO took this section off cover.</summary>
            public bool IsExpired => string.Equals(Tag, EndorsementTypes.Expired, StringComparison.OrdinalIgnoreCase);

            /// <summary>Readable name for messages.</summary>
            public string Label
            {
                get
                {
                    var name = FireEndorsementRows.FirstText(SectionName, SectionID, "(unnamed)");
                    return string.IsNullOrEmpty(DeclarationNo) ? name : $"{name} [{DeclarationNo}]";
                }
            }
        }

        private class CopiedFireSection
        {
            public string Token { get; set; }
            public string DeclarationNo { get; set; }
            public int ItemCount { get; set; }
            public decimal SumInsured { get; set; }
            public decimal NetPremium { get; set; }
        }

        private class PricedEndorsementSection
        {
            public FireSectionDto Input { get; set; }
            public SectionCalculationResponseDto Result { get; set; }

            /// <summary>Set only when the section was priced with named adjustments.</summary>
            public FireSectionDynamicAdjustmentResponseDto Dynamic { get; set; }
        }

        #endregion

        #region Fire endorsements — generic row helper

        /// <summary>
        /// Reads rows as column→value maps and writes them back, so an endorsement copy carries every
        /// column exactly as stored. That matters because PolicyTempDetails and PolicyPremDetails hold
        /// dozens of generic Field#/A# columns whose meaning varies by module: a hand-written column list
        /// would silently drop whatever it left out. Column names come from the live schema and are cached
        /// per process, and only real columns are ever written.
        /// </summary>
        private static class FireEndorsementRows
        {
            private static readonly ConcurrentDictionary<string, TableShape> Shapes = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Columns of a table, with the identity column and whatever is not safe to copy.</summary>
            /// <summary>
            /// Columns whose content is a diagnostic note rather than data. These are shortened to fit
            /// instead of failing: PolicyTempDetails.Field3 holds "Items: 4", Field5 a status string,
            /// PolicyDetails.Field2 a provenance line. Everything else is data or an identifier and must
            /// fit exactly, so it raises a precise error instead of being silently cut.
            /// </summary>
            private static readonly HashSet<string> ShortenableNoteColumns =
                new(StringComparer.OrdinalIgnoreCase) { "Field2", "Field3", "Field5" };

            internal class TableShape
            {
                public string Table { get; init; }
                public List<string> Columns { get; init; }
                public string IdentityColumn { get; init; }
                public HashSet<string> Writable { get; init; }

                /// <summary>Max characters per character-typed column. -1 means MAX (unbounded).</summary>
                public Dictionary<string, int> MaxChars { get; init; }

                /// <summary>Drops anything that isn't a writable column of this table.</summary>
                public Dictionary<string, object> OnlyKnown(Dictionary<string, object> values)
                {
                    var kept = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in values.Where(p => Writable.Contains(p.Key)))
                    {
                        kept[pair.Key] = pair.Value;
                    }
                    return kept;
                }
            }

            internal static async Task<TableShape> GetShapeAsync(
                string table, SqlConnection connection, SqlTransaction transaction)
            {
                if (Shapes.TryGetValue(table, out var cached)) return cached;

                // max_length is in BYTES and is -1 for MAX; n-types store 2 bytes per character.
                const string sql = @"
SELECT c.name,
       c.is_identity,
       c.is_computed,
       c.max_length,
       t.name AS TypeName
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(@Table)
ORDER BY c.column_id";

                var columns = new List<string>();
                string identity = null;
                var writable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var maxChars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                using (var command = new SqlCommand(sql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@Table", table);
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var name = reader.GetString(0);
                        columns.Add(name);

                        var maxLength = reader.GetInt16(3);
                        var typeName = reader.GetString(4);
                        if (typeName is "char" or "varchar" or "text")
                            maxChars[name] = maxLength;
                        else if (typeName is "nchar" or "nvarchar" or "ntext")
                            maxChars[name] = maxLength == -1 ? -1 : maxLength / 2;

                        if (reader.GetBoolean(1)) { identity = name; continue; }
                        if (reader.GetBoolean(2)) continue; // computed
                        writable.Add(name);
                    }
                }

                if (columns.Count == 0)
                    throw new InvalidOperationException($"Table {table} not found — cannot copy its rows.");

                var shape = new TableShape
                {
                    Table = table,
                    Columns = columns,
                    IdentityColumn = identity,
                    Writable = writable,
                    MaxChars = maxChars
                };
                Shapes[table] = shape;
                return shape;
            }

            /// <summary>
            /// Reads rows as column→value maps. forUpdate takes row locks for the transaction. Each row comes
            /// back with the value that identifies it: the identity column, or TransGUID / ENDTNum as a fallback.
            /// </summary>
            internal static async Task<List<(Dictionary<string, object> Values, object Key)>> ReadAsync(
                TableShape shape, string where, string orderBy, bool forUpdate,
                SqlConnection connection, SqlTransaction transaction, params (string Name, object Value)[] parameters)
            {
                var hint = forUpdate ? " WITH (UPDLOCK, ROWLOCK)" : string.Empty;
                var order = string.IsNullOrWhiteSpace(orderBy) ? string.Empty : $" ORDER BY {orderBy}";
                var sql = $"SELECT * FROM {Q(shape.Table)}{hint} WHERE {where}{order}";

                using var command = new SqlCommand(sql, connection, transaction);
                foreach (var (name, value) in parameters)
                {
                    command.Parameters.AddWithValue(name, value ?? DBNull.Value);
                }

                var rows = new List<(Dictionary<string, object> Values, object Key)>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }

                    var key = shape.IdentityColumn != null && values.TryGetValue(shape.IdentityColumn, out var identity) && identity != null
                        ? identity
                        : FirstText(Text(values, "TransGUID"), Text(values, "ENDTNum"));
                    rows.Add((values, key));
                }

                return rows;
            }

            /// <summary>WHERE clause matching one row against @KeyValue, mirroring ReadAsync's key choice.</summary>
            internal static string KeyPredicateFor(string table, Dictionary<string, object> values)
            {
                if (Shapes.TryGetValue(table, out var shape) && shape.IdentityColumn != null &&
                    values.TryGetValue(shape.IdentityColumn, out var identity) && identity != null)
                {
                    return $"{Q(shape.IdentityColumn)} = @KeyValue";
                }

                return !string.IsNullOrEmpty(Text(values, "TransGUID"))
                    ? "[TransGUID] = @KeyValue"
                    : "[ENDTNum] = @KeyValue";
            }

            /// <summary>
            /// Characters a column accepts, or int.MaxValue when it is not length-bound (or unknown).
            /// </summary>
            internal static int WidthOf(TableShape shape, string column) =>
                shape.MaxChars != null && shape.MaxChars.TryGetValue(column, out var width) && width >= 0
                    ? width
                    : int.MaxValue;

            /// <summary>
            /// Throws a precise error if the value will not fit. Use for identifiers, where a silent
            /// truncation would break a link between rows. SQL Server's own message names neither the
            /// column nor the table, which is what made this class of bug painful to trace.
            /// </summary>
            internal static void RequireFits(TableShape shape, string column, string value)
            {
                var width = WidthOf(shape, column);
                if (value == null || value.Length <= width) return;

                throw new FireEndorsementRuleException(
                    $"{shape.Table}.{column} holds {width} characters but the value needs {value.Length} " +
                    $"(\"{value}\"). Widen the column, or shorten the value that feeds it.");
            }

            /// <summary>Shortens a diagnostic note to fit its column.</summary>
            internal static string Fit(TableShape shape, string column, string value)
            {
                var width = WidthOf(shape, column);
                return value != null && value.Length > width ? value.Substring(0, width) : value;
            }

            /// <summary>
            /// Inserts one row from a column→value map, skipping the identity column. Every string is
            /// checked against the live column width first: notes are shortened, anything else raises an
            /// error naming the table, column and width.
            /// </summary>
            internal static async Task InsertAsync(
                string table, Dictionary<string, object> values, SqlConnection connection, SqlTransaction transaction)
            {
                var shape = await GetShapeAsync(table, connection, transaction);

                foreach (var column in values.Keys.ToList())
                {
                    if (values[column] is not string text) continue;
                    if (text.Length <= WidthOf(shape, column)) continue;

                    if (ShortenableNoteColumns.Contains(column))
                        values[column] = Fit(shape, column, text);
                    else
                        RequireFits(shape, column, text);
                }

                var columns = values.Keys
                    .Where(c => shape.Writable.Contains(c) && !string.Equals(c, shape.IdentityColumn, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (columns.Count == 0)
                    throw new InvalidOperationException($"Nothing to insert into {table}.");

                var sql = $"INSERT INTO {Q(table)} ({string.Join(", ", columns.Select(Q))}) " +
                          $"VALUES ({string.Join(", ", columns.Select((_, i) => $"@p{i}"))})";

                using var command = new SqlCommand(sql, connection, transaction);
                for (var i = 0; i < columns.Count; i++)
                {
                    command.Parameters.AddWithValue($"@p{i}", values[columns[i]] ?? DBNull.Value);
                }
                await command.ExecuteNonQueryAsync();
            }

            /// <summary>
            /// Brackets an identifier. These tables carry columns whose names are T-SQL reserved words
            /// (Desc, for one), and the generic copy builds its column list from the live schema, so
            /// every table and column name it emits has to be quoted or the statement won't parse.
            /// A literal ] inside a name is escaped by doubling, per T-SQL.
            /// </summary>
            internal static string Q(string identifier) =>
                identifier == null ? null : "[" + identifier.Replace("]", "]]") + "]";

            // --- value readers -----------------------------------------------------------------

            internal static string Text(object value) => value == null || value == DBNull.Value ? null : value.ToString()?.Trim();

            internal static string Text(Dictionary<string, object> values, string column) =>
                values != null && values.TryGetValue(column, out var value) ? Text(value) : null;

            internal static decimal Number(object value)
            {
                if (value == null || value == DBNull.Value) return 0m;
                if (value is decimal d) return d;
                if (value is double dbl) return (decimal)dbl;
                if (value is float f) return (decimal)f;
                if (value is int i) return i;
                if (value is long l) return l;
                if (value is short s) return s;
                if (value is byte b) return b;
                return decimal.TryParse(Text(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
            }

            internal static decimal Number(Dictionary<string, object> values, string column) =>
                values != null && values.TryGetValue(column, out var value) ? Number(value) : 0m;

            internal static DateTime? Date(Dictionary<string, object> values, string column)
            {
                if (values == null || !values.TryGetValue(column, out var value) || value == null || value == DBNull.Value)
                    return null;
                if (value is DateTime date) return date;
                return DateTime.TryParse(Text(value), out var parsed) ? parsed : null;
            }

            /// <summary>Value for a DB parameter — null and empty become DBNull.</summary>
            internal static object Db(string value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

            internal static string NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            /// <summary>First non-blank of the candidates, trimmed.</summary>
            internal static string FirstText(params string[] candidates) =>
                candidates?.Select(NullIfBlank).FirstOrDefault(v => v != null);

            /// <summary>Requested value when positive, else the stored one, else the fallback.</summary>
            internal static decimal PositiveOr(decimal? requested, decimal stored, decimal fallback)
            {
                if (requested.HasValue && requested.Value > 0) return requested.Value;
                return stored > 0 ? stored : fallback;
            }
        }

        #endregion
    }
}
