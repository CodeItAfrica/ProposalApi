using GibsLifesMicroWebApi.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    // ============================================================
    // MOTOR — VEHICLE FILE PARSER (.csv / .xlsx / .xlsm / .xls)
    //
    // Pure, stateless, no DB, no DI: turns a stream into a
    // MotorVehicleUploadPreviewDto. Static so neither ProposalService nor
    // PolicyAdminController needs a constructor change (both have their ctors
    // in other files of their partial class). File reading is delegated to
    // TabularFileReader.
    //
    // TOLERANCE BY DESIGN — ops send spreadsheets, not payloads:
    //  - Headers match case/space/punctuation-insensitively, with aliases
    //    ("REG NO" / "PLATE NUMBER" -> VehicleRegNo, "VALUE" / "SI" ->
    //    VehicleValue, "CHASSIS"/"VIN" -> ChassisNo).
    //  - Title/logo rows above the header are skipped: the header is the first
    //    row containing a recognizable Vehicle Value plus a Reg No or Type.
    //  - ₦, commas, %, and (parenthesised negatives) are stripped from numbers.
    //  - Blank rows are skipped, not treated as end-of-data.
    //  - Unrecognized headers are REPORTED, never silently dropped.
    //
    // ADJUSTMENTS IN A FLAT FILE — two supported shapes, usable together:
    //  1. Named columns for the standard motor adjustments, e.g. "NCD Rate",
    //     "Fleet Discount Rate", "TPPD Rate", "SRCC Rate" (+ optional
    //     "... Basis" column: PREMIUM (default) or SUMINSURED).
    //  2. Indexed columns for anything custom:
    //     "Discount1 Name/Rate/Basis", "Loading1 Name/Rate/Basis", 1..N.
    //  Names emitted match MotorAdjustmentTypes exactly, so uploaded rows and
    //  UI-entered rows carry identical adjustment names.
    //
    // PREMIUM: this parser never computes premium. Basic premium is worked out
    // at calculation time — VehicleValue × PremiumRate, or the TPD_Premium flat
    // premium when one exists for the type/usage/cover combination. A blank
    // rate is therefore legitimate for THIRD_PARTY rows and only warned about
    // when the cover type looks comprehensive.
    // ============================================================

    public static class MotorVehicleFileParser
    {
        public const int DefaultMaxRows = 5000;

        // ------------------------------------------------------------
        // ENTRY POINT
        // ------------------------------------------------------------

        public static MotorVehicleUploadPreviewDto Parse(
            Stream stream, string fileName, int maxRows = DefaultMaxRows)
        {
            var preview = new MotorVehicleUploadPreviewDto
            {
                FileName = fileName,
                Success = false
            };

            var read = TabularFileReader.Read(stream, fileName, preferSheetContaining: "vehicle");
            preview.FileType = read.FileType;
            preview.SheetName = read.SheetName;

            if (!read.Success)
            {
                preview.FileIssues.Add(FileIssue(read.Error));
                preview.Message = read.Error;
                return preview;
            }

            return MapRows(read.Rows, preview, maxRows);
        }

        // ------------------------------------------------------------
        // HEADER MAPPING
        // ------------------------------------------------------------

        private static MotorVehicleUploadPreviewDto MapRows(
            List<List<string>> rows, MotorVehicleUploadPreviewDto preview, int maxRows)
        {
            var headerIndex = FindHeaderRowIndex(rows);
            if (headerIndex < 0)
            {
                preview.FileIssues.Add(FileIssue(
                    "No header row found. The sheet must contain a 'Vehicle Value' column plus a " +
                    "'Vehicle Reg No' or 'Vehicle Type' column. Download the template from " +
                    "GET /motor/vehicles/upload/template for the exact headers."));
                preview.Message = "No header row found";
                return preview;
            }

            preview.HeaderRowNumber = headerIndex + 1;

            var headers = rows[headerIndex];
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            var indexedAdjustments = new List<IndexedAdjustmentColumn>();

            for (int col = 0; col < headers.Count; col++)
            {
                var raw = (headers[col] ?? "").Trim();
                if (raw.Length == 0) continue;

                var canonical = TabularFileReader.Canonicalize(raw);

                if (AliasToField.TryGetValue(canonical, out var field))
                {
                    if (!columns.ContainsKey(field))
                    {
                        columns[field] = col;
                        preview.RecognizedColumns.Add($"{raw} -> {field}");
                    }
                    else
                    {
                        preview.FileIssues.Add(FileIssue(
                            $"Duplicate column '{raw}' maps to {field}; the first one wins.",
                            "WARNING", headerIndex + 1));
                    }
                    continue;
                }

                var indexed = MatchIndexedAdjustment(canonical, col);
                if (indexed != null)
                {
                    indexedAdjustments.Add(indexed);
                    preview.RecognizedColumns.Add($"{raw} -> {indexed.Type}{indexed.Index}.{indexed.Part}");
                    continue;
                }

                preview.UnrecognizedColumns.Add(raw);
            }

            if (!columns.ContainsKey(F.VehicleValue))
            {
                preview.FileIssues.Add(FileIssue("The header row must contain a 'Vehicle Value' column."));
                preview.Message = "Required columns missing";
                return preview;
            }

            var seenRegNos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int r = headerIndex + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                if (preview.TotalDataRows >= maxRows)
                {
                    preview.FileIssues.Add(FileIssue(
                        $"File contains more than {maxRows} vehicle rows; only the first {maxRows} were read. " +
                        "Split the file and upload in batches (later batches with mode=Append)."));
                    break;
                }

                preview.TotalDataRows++;
                var parsed = MapRow(row, r + 1, columns, indexedAdjustments);

                if (parsed.IsValid && !string.IsNullOrWhiteSpace(parsed.Vehicle.VehicleRegNo))
                {
                    if (seenRegNos.TryGetValue(parsed.Vehicle.VehicleRegNo, out var firstRow))
                    {
                        parsed.Issues.Add(new MotorVehicleUploadIssueDto
                        {
                            RowNumber = r + 1,
                            Column = "Vehicle Reg No",
                            Severity = "WARNING",
                            Message = $"Duplicate registration number — also on row {firstRow}. " +
                                      "Both will be imported as separate vehicles with separate certificates."
                        });
                    }
                    else
                    {
                        seenRegNos[parsed.Vehicle.VehicleRegNo] = r + 1;
                    }
                }

                if (parsed.Vehicle != null) parsed.Vehicle.ItemNo = preview.ValidRowCount + 1;

                if (parsed.IsValid) preview.ValidRowCount++; else preview.InvalidRowCount++;
                preview.WarningCount += parsed.Issues.Count(i => i.Severity == "WARNING");

                preview.Rows.Add(parsed);
            }

            if (preview.TotalDataRows == 0)
            {
                preview.FileIssues.Add(FileIssue("The file contains a header row but no vehicle rows."));
                preview.Message = "No vehicle rows found";
                return preview;
            }

            preview.Success = preview.ValidRowCount > 0;
            preview.Message = preview.Success
                ? $"{preview.ValidRowCount} of {preview.TotalDataRows} row(s) parsed successfully" +
                  (preview.InvalidRowCount > 0 ? $"; {preview.InvalidRowCount} row(s) have errors" : "")
                : "No valid vehicle rows could be read from the file";

            return preview;
        }

        // ------------------------------------------------------------
        // ROW MAPPING
        // ------------------------------------------------------------

        private static MotorVehicleUploadRowDto MapRow(
            List<string> row, int rowNumber,
            Dictionary<string, int> columns,
            List<IndexedAdjustmentColumn> indexedAdjustments)
        {
            var result = new MotorVehicleUploadRowDto { RowNumber = rowNumber, IsValid = true };
            var vehicle = new MotorRiskItemInputDto();

            string Text(string field)
            {
                if (!columns.TryGetValue(field, out var col)) return "";
                return col < row.Count ? (row[col] ?? "").Trim() : "";
            }

            void Error(string column, string message)
            {
                result.IsValid = false;
                result.Issues.Add(new MotorVehicleUploadIssueDto
                { RowNumber = rowNumber, Column = column, Severity = "ERROR", Message = message });
            }

            void Warn(string column, string message)
            {
                result.Issues.Add(new MotorVehicleUploadIssueDto
                { RowNumber = rowNumber, Column = column, Severity = "WARNING", Message = message });
            }

            decimal Number(string field, string label)
            {
                var raw = Text(field);
                if (raw.Length == 0) return 0;
                if (TabularFileReader.TryParseDecimal(raw, out var value)) return value;
                Error(label, $"'{raw}' is not a valid number.");
                return 0;
            }

            int Integer(string field, string label)
            {
                var raw = Text(field);
                if (raw.Length == 0) return 0;
                if (TabularFileReader.TryParseInt(raw, out var value)) return value;
                Error(label, $"'{raw}' is not a valid whole number.");
                return 0;
            }

            // --- identification ---------------------------------------
            vehicle.VehicleRegNo = Text(F.VehicleRegNo);
            if (string.IsNullOrWhiteSpace(vehicle.VehicleRegNo))
                Warn("Vehicle Reg No",
                    "No registration number — the vehicle will import, but it is the label shown " +
                    "everywhere in the breakdown and on the certificate.");

            vehicle.VehicleUser = Text(F.VehicleUser);
            vehicle.VehicleMake = Text(F.VehicleMake);
            vehicle.VehicleModel = Text(F.VehicleModel);
            vehicle.ChassisNo = Text(F.ChassisNo);
            vehicle.EngineNo = Text(F.EngineNo);
            vehicle.Color = Text(F.Color);
            vehicle.State = Text(F.State);
            vehicle.Location = Text(F.Location);
            vehicle.WaxCode = Text(F.WaxCode);

            vehicle.VehicleType = Text(F.VehicleType);
            if (string.IsNullOrWhiteSpace(vehicle.VehicleType))
                Error("Vehicle Type", "Vehicle type is required.");

            vehicle.CoverType = NormalizeCoverType(Text(F.CoverType));
            if (string.IsNullOrWhiteSpace(vehicle.CoverType))
                Error("Cover Type", "Cover type is required (e.g. COMPREHENSIVE, THIRD_PARTY, THIRD_PARTY_FIRE_THEFT).");

            vehicle.Usage = NormalizeUsage(Text(F.Usage));

            vehicle.ModelYear = Integer(F.ModelYear, "Model Year");
            vehicle.SeatCapacity = Integer(F.SeatCapacity, "Seat Capacity");

            // --- money -------------------------------------------------
            vehicle.VehicleValue = Number(F.VehicleValue, "Vehicle Value");
            if (result.IsValid && vehicle.VehicleValue <= 0)
                Error("Vehicle Value", "Vehicle value must be greater than zero.");

            vehicle.PremiumRate = Number(F.PremiumRate, "Premium Rate");
            if (vehicle.PremiumRate == 0 && IsRatedCover(vehicle.CoverType))
                Warn("Premium Rate",
                    $"No premium rate on a {vehicle.CoverType} row. Basic premium will be zero unless a flat " +
                    "premium exists in TPD_Premium for this type/usage/cover.");

            if (vehicle.PremiumRate < 0)
                Error("Premium Rate", "Premium rate cannot be negative.");

            vehicle.TrackingCost = Number(F.TrackingCost, "Tracking Cost");
            vehicle.RescueCost = Number(F.RescueCost, "Rescue Cost");

            if (vehicle.TrackingCost < 0) Error("Tracking Cost", "Tracking cost cannot be negative.");
            if (vehicle.RescueCost < 0) Error("Rescue Cost", "Rescue cost cannot be negative.");

            // --- cover period ------------------------------------------
            var startRaw = Text(F.StartDate);
            if (startRaw.Length > 0)
            {
                if (TabularFileReader.TryParseDate(startRaw, out var start)) vehicle.StartDate = start;
                else Warn("Start Date", $"'{startRaw}' is not a recognizable date — left blank.");
            }

            var endRaw = Text(F.EndDate);
            if (endRaw.Length > 0)
            {
                if (TabularFileReader.TryParseDate(endRaw, out var end)) vehicle.EndDate = end;
                else Warn("End Date", $"'{endRaw}' is not a recognizable date — left blank.");
            }

            if (vehicle.StartDate != default && vehicle.EndDate != default &&
                vehicle.EndDate < vehicle.StartDate)
                Error("End Date", "End date is before the start date.");

            // --- standard adjustments ----------------------------------
            foreach (var spec in StandardAdjustments)
            {
                if (!columns.TryGetValue(spec.RateField, out var rateCol)) continue;
                var rateRaw = rateCol < row.Count ? (row[rateCol] ?? "").Trim() : "";
                if (rateRaw.Length == 0) continue;

                if (!TabularFileReader.TryParseDecimal(rateRaw, out var rate))
                {
                    Error(spec.Name, $"'{rateRaw}' is not a valid rate.");
                    continue;
                }
                if (rate == 0) continue;
                if (!ValidateRate(spec.Name, spec.Type, rate, Error)) continue;

                var adjustment = new MotorAdjustmentDto
                {
                    AdjustmentName = spec.Name,
                    AdjustmentType = spec.Type,
                    Rate = rate,
                    AppliedOn = ReadBasis(row, columns, spec.BasisField),
                    SequenceOrder = spec.SequenceOrder
                };

                if (spec.Type == "DISCOUNT") vehicle.Discounts.Add(adjustment);
                else vehicle.Loadings.Add(adjustment);
            }

            // --- custom indexed adjustments ----------------------------
            foreach (var group in indexedAdjustments.GroupBy(a => new { a.Type, a.Index })
                                                    .OrderBy(g => g.Key.Index))
            {
                var nameCol = group.FirstOrDefault(g => g.Part == "NAME")?.ColumnIndex;
                var rateCol = group.FirstOrDefault(g => g.Part == "RATE")?.ColumnIndex;
                var basisCol = group.FirstOrDefault(g => g.Part == "BASIS")?.ColumnIndex;

                var rateRaw = rateCol.HasValue && rateCol.Value < row.Count
                    ? (row[rateCol.Value] ?? "").Trim() : "";
                var name = nameCol.HasValue && nameCol.Value < row.Count
                    ? (row[nameCol.Value] ?? "").Trim() : "";

                if (rateRaw.Length == 0 && name.Length == 0) continue;

                var label = $"{group.Key.Type} {group.Key.Index}";

                if (name.Length == 0)
                {
                    Error(label, "Adjustment name is required when a rate is supplied.");
                    continue;
                }
                if (rateRaw.Length == 0)
                {
                    Warn(label, $"'{name}' has no rate — ignored.");
                    continue;
                }
                if (!TabularFileReader.TryParseDecimal(rateRaw, out var rate))
                {
                    Error(label, $"'{rateRaw}' is not a valid rate.");
                    continue;
                }
                if (rate == 0) continue;

                var type = group.Key.Type == "DISCOUNT" ? "DISCOUNT" : "LOADING";
                if (!ValidateRate(name, type, rate, Error)) continue;

                var basis = AdjustmentBasis.Premium;
                if (basisCol.HasValue && basisCol.Value < row.Count)
                    basis = ParseBasis(row[basisCol.Value]);

                var adjustment = new MotorAdjustmentDto
                {
                    AdjustmentName = name.ToUpperInvariant(),
                    AdjustmentType = type,
                    Rate = rate,
                    AppliedOn = basis,
                    SequenceOrder = 100 + group.Key.Index
                };

                if (type == "DISCOUNT") vehicle.Discounts.Add(adjustment);
                else vehicle.Loadings.Add(adjustment);
            }

            Resequence(vehicle.Discounts);
            Resequence(vehicle.Loadings);

            if (result.IsValid) result.Vehicle = vehicle;
            return result;
        }

        private static bool ValidateRate(string name, string type, decimal rate, Action<string, string> error)
        {
            if (rate < 0)
            {
                error(name, $"{type.ToLowerInvariant()} rate cannot be negative. Got: {rate}");
                return false;
            }
            if (type == "DISCOUNT" && rate > 100)
            {
                error(name, $"Discount rate must be between 0 and 100. Got: {rate}");
                return false;
            }
            return true;
        }

        private static void Resequence(List<MotorAdjustmentDto> adjustments)
        {
            var ordered = adjustments.OrderBy(a => a.SequenceOrder).ToList();
            adjustments.Clear();
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].SequenceOrder = i + 1;
                adjustments.Add(ordered[i]);
            }
        }

        private static AdjustmentBasis ReadBasis(
            List<string> row, Dictionary<string, int> columns, string basisField)
        {
            if (basisField == null || !columns.TryGetValue(basisField, out var col)) return AdjustmentBasis.Premium;
            return col < row.Count ? ParseBasis(row[col]) : AdjustmentBasis.Premium;
        }

        private static AdjustmentBasis ParseBasis(string raw)
        {
            var canonical = TabularFileReader.Canonicalize(raw ?? "");
            return canonical switch
            {
                "SUMINSURED" or "SI" or "SUM" or "VALUE" or "VEHICLEVALUE" or "S" => AdjustmentBasis.SumInsured,
                _ => AdjustmentBasis.Premium
            };
        }

        /// <summary>
        /// Maps what people type ("Comp", "third party", "TPFT") onto the cover
        /// type strings the calculation and the TPD_Premium lookup expect.
        /// An unrecognized value is passed through untouched rather than
        /// rejected — cover types are configurable per installation.
        /// </summary>
        private static string NormalizeCoverType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var canonical = TabularFileReader.Canonicalize(raw);

            return canonical switch
            {
                "COMPREHENSIVE" or "COMP" or "COMPREHENSIVECOVER" or "FULLCOVER" => "COMPREHENSIVE",
                "THIRDPARTY" or "TP" or "THIRDPARTYONLY" or "TPO" => "THIRD_PARTY",
                "THIRDPARTYFIRETHEFT" or "TPFT" or "THIRDPARTYFIREANDTHEFT" => "THIRD_PARTY_FIRE_THEFT",
                _ => raw.Trim().ToUpperInvariant()
            };
        }

        private static string NormalizeUsage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var canonical = TabularFileReader.Canonicalize(raw);

            return canonical switch
            {
                "PRIVATE" or "PRIVATEUSE" or "PERSONAL" => "PRIVATE",
                "COMMERCIAL" or "COMMERCIALUSE" or "BUSINESS" => "COMMERCIAL",
                "HIREPURCHASE" or "HP" => "HIRE_PURCHASE",
                _ => raw.Trim().ToUpperInvariant()
            };
        }

        /// <summary>
        /// True when the cover type is one where premium comes from
        /// value × rate rather than a flat TPD_Premium amount — i.e. where a
        /// missing rate is worth warning about.
        /// </summary>
        private static bool IsRatedCover(string coverType)
        {
            if (string.IsNullOrWhiteSpace(coverType)) return false;
            return coverType.IndexOf("COMPREHENSIVE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------
        // TEMPLATE
        // ------------------------------------------------------------

        public static string BuildTemplateCsv()
        {
            var headers = new[]
            {
                "Vehicle Reg No","Vehicle User","Vehicle Type","Cover Type","Usage",
                "Vehicle Make","Vehicle Model","Chassis No","Engine No","Color",
                "Model Year","Seat Capacity","State","Location","Wax Code",
                "Vehicle Value","Premium Rate","Tracking Cost","Rescue Cost",
                "Start Date","End Date",
                "NCD Rate","NCD Basis",
                "Fleet Discount Rate","Special Discount Rate","Loyalty Discount Rate",
                "Multiple Vehicle Discount Rate",
                "TPPD Rate","TPPD Basis",
                "SRCC Rate","Flood Rate","Excess Buy Back Rate","TPFT Rate",
                "Young Driver Loading Rate","High Risk Area Loading Rate","Vehicle Age Loading Rate",
                "Discount1 Name","Discount1 Rate","Discount1 Basis",
                "Loading1 Name","Loading1 Rate","Loading1 Basis"
            };

            var example = new[]
            {
                "LAG-123-XY","Adebayo Okonkwo","SALOON","COMPREHENSIVE","PRIVATE",
                "Toyota","Corolla","JTDBR32E320012345","2ZR1234567","Silver",
                "2021","5","Lagos","Ikeja","WX001",
                "18500000","5","75000","25000",
                "2026-01-01","2026-12-31",
                "10","PREMIUM",
                "","5","","",
                "0.25","PREMIUM",
                "0.15","","","",
                "","","",
                "TRACKER FITTED DISCOUNT","2.5","PREMIUM",
                "OFF-ROAD USE LOADING","1.5","PREMIUM"
            };

            var notes = new[]
            {
                "# One vehicle per row. Delete this comment block and the example row before uploading.",
                "# Required: Vehicle Value (> 0), Vehicle Type, Cover Type.",
                "# Cover Type accepts COMPREHENSIVE, THIRD_PARTY, THIRD_PARTY_FIRE_THEFT (also Comp / TP / TPFT).",
                "# Premium Rate is a percentage of Vehicle Value. Leave it blank for THIRD_PARTY rows -",
                "#   those are priced from the flat TPD_Premium table by type/usage/cover.",
                "# Basis columns accept PREMIUM (default) or SUMINSURED. Blank basis = PREMIUM.",
                "# Leave an adjustment rate blank or 0 to skip that adjustment for that vehicle.",
                "# Discount rates must be 0-100. Loading rates must be 0 or more.",
                "# Add Discount2/Discount3/Loading2... columns for more custom adjustments.",
                "# Dates: yyyy-MM-dd or dd/MM/yyyy. Numbers may include commas, currency symbols and % signs."
            };

            var sb = new StringBuilder();
            foreach (var note in notes) sb.AppendLine(note);
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
            sb.AppendLine(string.Join(",", example.Select(EscapeCsv)));
            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            value ??= "";
            return value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }

        // ------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------

        private static int FindHeaderRowIndex(List<List<string>> rows)
        {
            for (int r = 0; r < rows.Count && r < 50; r++)
            {
                var fields = rows[r]
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(TabularFileReader.Canonicalize)
                    .Where(c => AliasToField.ContainsKey(c))
                    .Select(c => AliasToField[c])
                    .ToHashSet();

                if (fields.Contains(F.VehicleValue) &&
                    (fields.Contains(F.VehicleRegNo) || fields.Contains(F.VehicleType)))
                    return r;
            }
            return -1;
        }

        private static MotorVehicleUploadIssueDto FileIssue(
            string message, string severity = "ERROR", int rowNumber = 0)
            => new() { RowNumber = rowNumber, Severity = severity, Message = message };

        private class IndexedAdjustmentColumn
        {
            public string Type { get; set; }   // DISCOUNT / LOADING
            public int Index { get; set; }
            public string Part { get; set; }   // NAME / RATE / BASIS
            public int ColumnIndex { get; set; }
        }

        private static readonly Regex IndexedAdjustmentPattern =
            new(@"^(DISCOUNT|LOADING)(\d+)(NAME|RATE|BASIS|APPLIEDON)$", RegexOptions.Compiled);

        private static IndexedAdjustmentColumn MatchIndexedAdjustment(string canonical, int columnIndex)
        {
            var match = IndexedAdjustmentPattern.Match(canonical);
            if (!match.Success) return null;

            var part = match.Groups[3].Value;
            if (part == "APPLIEDON") part = "BASIS";

            return new IndexedAdjustmentColumn
            {
                Type = match.Groups[1].Value,
                Index = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                Part = part,
                ColumnIndex = columnIndex
            };
        }

        // ------------------------------------------------------------
        // COLUMN CONTRACT
        // ------------------------------------------------------------

        private static class F
        {
            public const string VehicleRegNo = "VehicleRegNo";
            public const string VehicleUser = "VehicleUser";
            public const string VehicleType = "VehicleType";
            public const string CoverType = "CoverType";
            public const string Usage = "Usage";
            public const string VehicleMake = "VehicleMake";
            public const string VehicleModel = "VehicleModel";
            public const string ChassisNo = "ChassisNo";
            public const string EngineNo = "EngineNo";
            public const string Color = "Color";
            public const string ModelYear = "ModelYear";
            public const string SeatCapacity = "SeatCapacity";
            public const string State = "State";
            public const string Location = "Location";
            public const string WaxCode = "WaxCode";
            public const string VehicleValue = "VehicleValue";
            public const string PremiumRate = "PremiumRate";
            public const string TrackingCost = "TrackingCost";
            public const string RescueCost = "RescueCost";
            public const string StartDate = "StartDate";
            public const string EndDate = "EndDate";

            public const string NcdRate = "NcdRate";
            public const string NcdBasis = "NcdBasis";
            public const string FleetDiscountRate = "FleetDiscountRate";
            public const string FleetDiscountBasis = "FleetDiscountBasis";
            public const string SpecialDiscountRate = "SpecialDiscountRate";
            public const string SpecialDiscountBasis = "SpecialDiscountBasis";
            public const string LoyaltyDiscountRate = "LoyaltyDiscountRate";
            public const string LoyaltyDiscountBasis = "LoyaltyDiscountBasis";
            public const string MultipleVehicleDiscountRate = "MultipleVehicleDiscountRate";
            public const string MultipleVehicleDiscountBasis = "MultipleVehicleDiscountBasis";

            public const string TppdRate = "TppdRate";
            public const string TppdBasis = "TppdBasis";
            public const string SrccRate = "SrccRate";
            public const string SrccBasis = "SrccBasis";
            public const string FloodRate = "FloodRate";
            public const string FloodBasis = "FloodBasis";
            public const string ExcessBuyBackRate = "ExcessBuyBackRate";
            public const string ExcessBuyBackBasis = "ExcessBuyBackBasis";
            public const string TpftRate = "TpftRate";
            public const string TpftBasis = "TpftBasis";
            public const string YoungDriverRate = "YoungDriverLoadingRate";
            public const string YoungDriverBasis = "YoungDriverLoadingBasis";
            public const string HighRiskAreaRate = "HighRiskAreaLoadingRate";
            public const string HighRiskAreaBasis = "HighRiskAreaLoadingBasis";
            public const string VehicleAgeRate = "VehicleAgeLoadingRate";
            public const string VehicleAgeBasis = "VehicleAgeLoadingBasis";
        }

        private class StandardAdjustmentSpec
        {
            public string Name { get; set; }   // must match MotorAdjustmentTypes exactly
            public string Type { get; set; }
            public int SequenceOrder { get; set; }
            public string RateField { get; set; }
            public string BasisField { get; set; }
        }

        // Discounts first (they are applied first), then loadings.
        private static readonly List<StandardAdjustmentSpec> StandardAdjustments = new()
        {
            new() { Name = "NO CLAIMS DISCOUNT (NCD)", Type = "DISCOUNT", SequenceOrder = 1, RateField = F.NcdRate, BasisField = F.NcdBasis },
            new() { Name = "FLEET DISCOUNT", Type = "DISCOUNT", SequenceOrder = 2, RateField = F.FleetDiscountRate, BasisField = F.FleetDiscountBasis },
            new() { Name = "SPECIAL DISCOUNT", Type = "DISCOUNT", SequenceOrder = 3, RateField = F.SpecialDiscountRate, BasisField = F.SpecialDiscountBasis },
            new() { Name = "LOYALTY DISCOUNT", Type = "DISCOUNT", SequenceOrder = 4, RateField = F.LoyaltyDiscountRate, BasisField = F.LoyaltyDiscountBasis },
            new() { Name = "MULTIPLE VEHICLE DISCOUNT", Type = "DISCOUNT", SequenceOrder = 5, RateField = F.MultipleVehicleDiscountRate, BasisField = F.MultipleVehicleDiscountBasis },

            new() { Name = "TPPD (Third Party Property Damage)", Type = "LOADING", SequenceOrder = 1, RateField = F.TppdRate, BasisField = F.TppdBasis },
            new() { Name = "SRCC (Strike, Riot & Civil Commotion)", Type = "LOADING", SequenceOrder = 2, RateField = F.SrccRate, BasisField = F.SrccBasis },
            new() { Name = "FLOOD", Type = "LOADING", SequenceOrder = 3, RateField = F.FloodRate, BasisField = F.FloodBasis },
            new() { Name = "EXCESS BUY BACK", Type = "LOADING", SequenceOrder = 4, RateField = F.ExcessBuyBackRate, BasisField = F.ExcessBuyBackBasis },
            new() { Name = "TPFT (Third Party Fire & Theft)", Type = "LOADING", SequenceOrder = 5, RateField = F.TpftRate, BasisField = F.TpftBasis },
            new() { Name = "YOUNG DRIVER LOADING", Type = "LOADING", SequenceOrder = 6, RateField = F.YoungDriverRate, BasisField = F.YoungDriverBasis },
            new() { Name = "HIGH RISK AREA LOADING", Type = "LOADING", SequenceOrder = 7, RateField = F.HighRiskAreaRate, BasisField = F.HighRiskAreaBasis },
            new() { Name = "VEHICLE AGE LOADING", Type = "LOADING", SequenceOrder = 8, RateField = F.VehicleAgeRate, BasisField = F.VehicleAgeBasis },
        };

        private static readonly Dictionary<string, string[]> FieldAliases = new()
        {
            [F.VehicleRegNo] = new[] { "VEHICLEREGNO", "REGNO", "REGISTRATIONNO", "REGISTRATIONNUMBER", "PLATENO", "PLATENUMBER", "VEHICLENUMBER", "VEHICLEREGISTRATION", "REG" },
            [F.VehicleUser] = new[] { "VEHICLEUSER", "USER", "DRIVER", "INSUREDNAME", "OWNER", "NAMEOFUSER", "VEHICLEOWNER" },
            [F.VehicleType] = new[] { "VEHICLETYPE", "TYPE", "BODYTYPE", "CATEGORY", "VEHICLECATEGORY", "CLASS" },
            [F.CoverType] = new[] { "COVERTYPE", "COVER", "TYPEOFCOVER", "POLICYTYPE", "COVERAGE" },
            [F.Usage] = new[] { "USAGE", "USE", "VEHICLEUSAGE", "TYPEOFUSE", "PURPOSE" },
            [F.VehicleMake] = new[] { "VEHICLEMAKE", "MAKE", "MANUFACTURER", "BRAND" },
            [F.VehicleModel] = new[] { "VEHICLEMODEL", "MODEL" },
            [F.ChassisNo] = new[] { "CHASSISNO", "CHASSIS", "CHASSISNUMBER", "VIN", "VINNO", "VINNUMBER" },
            [F.EngineNo] = new[] { "ENGINENO", "ENGINE", "ENGINENUMBER", "MOTORNO" },
            [F.Color] = new[] { "COLOR", "COLOUR", "VEHICLECOLOR", "VEHICLECOLOUR" },
            [F.ModelYear] = new[] { "MODELYEAR", "YEAR", "YEAROFMANUFACTURE", "YEAROFMAKE", "MANUFACTUREYEAR", "YEARMODEL" },
            [F.SeatCapacity] = new[] { "SEATCAPACITY", "SEATS", "NOOFSEATS", "SEATINGCAPACITY", "CAPACITY" },
            [F.State] = new[] { "STATE", "REGISTRATIONSTATE", "STATEOFREGISTRATION" },
            [F.Location] = new[] { "LOCATION", "GARAGE", "GARAGEADDRESS", "ADDRESS", "CITY" },
            [F.WaxCode] = new[] { "WAXCODE", "WAX", "STICKERNO", "WAXNO" },

            [F.VehicleValue] = new[] { "VEHICLEVALUE", "VALUE", "SUMINSURED", "SI", "INSUREDVALUE", "VEHICLESUMINSURED", "AMOUNTINSURED", "MARKETVALUE" },
            [F.PremiumRate] = new[] { "PREMIUMRATE", "RATE", "RATEPERCENT", "PREMRATE", "RATE100", "ANNUALRATE" },
            [F.TrackingCost] = new[] { "TRACKINGCOST", "TRACKING", "TRACKERCOST", "TRACKINGDEVICECOST", "TRACKERFEE" },
            [F.RescueCost] = new[] { "RESCUECOST", "RESCUE", "ROADRESCUE", "ROADSIDEASSISTANCE", "RESCUEFEE" },
            [F.StartDate] = new[] { "STARTDATE", "COVERSTART", "INCEPTIONDATE", "EFFECTIVEDATE", "FROMDATE", "COMMENCEMENTDATE" },
            [F.EndDate] = new[] { "ENDDATE", "COVEREND", "EXPIRYDATE", "TODATE", "EXPIRATIONDATE" },

            [F.NcdRate] = new[] { "NCDRATE", "NCD", "NOCLAIMSDISCOUNTRATE", "NOCLAIMSDISCOUNT", "NOCLAIMDISCOUNT", "NOCLAIMSDISCOUNTNCD" },
            [F.NcdBasis] = new[] { "NCDBASIS", "NOCLAIMSDISCOUNTBASIS" },
            [F.FleetDiscountRate] = new[] { "FLEETDISCOUNTRATE", "FLEETDISCOUNT", "FLEETRATE", "FLEET" },
            [F.FleetDiscountBasis] = new[] { "FLEETDISCOUNTBASIS", "FLEETBASIS" },
            [F.SpecialDiscountRate] = new[] { "SPECIALDISCOUNTRATE", "SPECIALDISCOUNT", "SPECIALDISC" },
            [F.SpecialDiscountBasis] = new[] { "SPECIALDISCOUNTBASIS", "SPECIALDISCBASIS" },
            [F.LoyaltyDiscountRate] = new[] { "LOYALTYDISCOUNTRATE", "LOYALTYDISCOUNT", "LOYALTY" },
            [F.LoyaltyDiscountBasis] = new[] { "LOYALTYDISCOUNTBASIS", "LOYALTYBASIS" },
            [F.MultipleVehicleDiscountRate] = new[] { "MULTIPLEVEHICLEDISCOUNTRATE", "MULTIPLEVEHICLEDISCOUNT", "MULTIVEHICLEDISCOUNT", "MULTIPLEVEHICLE" },
            [F.MultipleVehicleDiscountBasis] = new[] { "MULTIPLEVEHICLEDISCOUNTBASIS", "MULTIVEHICLEDISCOUNTBASIS" },

            [F.TppdRate] = new[] { "TPPDRATE", "TPPD", "THIRDPARTYPROPERTYDAMAGE", "THIRDPARTYPROPERTYDAMAGERATE", "TPPDTHIRDPARTYPROPERTYDAMAGE" },
            [F.TppdBasis] = new[] { "TPPDBASIS", "THIRDPARTYPROPERTYDAMAGEBASIS" },
            [F.SrccRate] = new[] { "SRCCRATE", "SRCC", "SRCCLOADING", "SRCCLOADINGRATE", "STRIKERIOTCIVILCOMMOTION" },
            [F.SrccBasis] = new[] { "SRCCBASIS", "SRCCLOADINGBASIS" },
            [F.FloodRate] = new[] { "FLOODRATE", "FLOOD", "FLOODLOADING", "FLOODLOADINGRATE" },
            [F.FloodBasis] = new[] { "FLOODBASIS", "FLOODLOADINGBASIS" },
            [F.ExcessBuyBackRate] = new[] { "EXCESSBUYBACKRATE", "EXCESSBUYBACK", "EBB", "EXCESSBUYBACKLOADING" },
            [F.ExcessBuyBackBasis] = new[] { "EXCESSBUYBACKBASIS", "EBBBASIS" },
            [F.TpftRate] = new[] { "TPFTRATE", "TPFT", "THIRDPARTYFIRETHEFT", "THIRDPARTYFIREANDTHEFT" },
            [F.TpftBasis] = new[] { "TPFTBASIS", "THIRDPARTYFIRETHEFTBASIS" },
            [F.YoungDriverRate] = new[] { "YOUNGDRIVERLOADINGRATE", "YOUNGDRIVERLOADING", "YOUNGDRIVERRATE", "YOUNGDRIVER" },
            [F.YoungDriverBasis] = new[] { "YOUNGDRIVERLOADINGBASIS", "YOUNGDRIVERBASIS" },
            [F.HighRiskAreaRate] = new[] { "HIGHRISKAREALOADINGRATE", "HIGHRISKAREALOADING", "HIGHRISKAREA", "HIGHRISKLOADING" },
            [F.HighRiskAreaBasis] = new[] { "HIGHRISKAREALOADINGBASIS", "HIGHRISKAREABASIS" },
            [F.VehicleAgeRate] = new[] { "VEHICLEAGELOADINGRATE", "VEHICLEAGELOADING", "AGELOADING", "VEHICLEAGE" },
            [F.VehicleAgeBasis] = new[] { "VEHICLEAGELOADINGBASIS", "AGELOADINGBASIS" },
        };

        private static readonly Dictionary<string, string> AliasToField = BuildAliasMap();

        private static Dictionary<string, string> BuildAliasMap()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in FieldAliases)
            {
                foreach (var alias in entry.Value)
                {
                    if (!map.ContainsKey(alias)) map[alias] = entry.Key;
                }
            }
            return map;
        }
    }
}
