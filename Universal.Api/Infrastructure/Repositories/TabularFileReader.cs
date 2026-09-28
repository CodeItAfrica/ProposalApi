using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    // ============================================================
    // SHARED TABULAR FILE READER (.csv / .xlsx / .xlsm / .xls)
    //
    // Turns an uploaded stream into rows of strings. Knows nothing about
    // vehicles, vessels or insurance — module parsers (MotorVehicleFileParser,
    // and eventually HullVesselFileParser) own their own column contracts and
    // validation on top of this.
    //
    // NuGet:
    //   dotnet add package ExcelDataReader
    //   dotnet add package System.Text.Encoding.CodePages   (needed for .xls)
    //
    // NOTE: HullVesselFileParser currently carries its own private copy of
    // this logic. It works and is in production, so it's left alone — but if
    // you touch it again, moving it onto this class is the tidy-up.
    // ============================================================

    public static class TabularFileReader
    {
        private static bool _encodingProviderRegistered;

        public class ReadResult
        {
            public bool Success { get; set; }
            public string Error { get; set; }
            public string FileType { get; set; }     // CSV / XLSX / XLS / UNKNOWN
            public string SheetName { get; set; }    // Excel only
            public List<List<string>> Rows { get; set; } = new();
        }

        /// <summary>
        /// Read a file into rows. Never throws for bad content — an unreadable
        /// file comes back with Success=false and Error set.
        /// </summary>
        /// <param name="preferSheetContaining">
        /// If a sheet's name contains this (case-insensitive), prefer it over
        /// sheet 1 — so a workbook with notes or rate tables up front still
        /// reads the right data.
        /// </param>
        public static ReadResult Read(Stream stream, string fileName, string preferSheetContaining = null)
        {
            var extension = (Path.GetExtension(fileName) ?? "").ToLowerInvariant();
            var result = new ReadResult
            {
                FileType = extension switch
                {
                    ".csv" or ".txt" => "CSV",
                    ".xlsx" or ".xlsm" => "XLSX",
                    ".xls" => "XLS",
                    _ => "UNKNOWN"
                }
            };

            if (result.FileType == "UNKNOWN")
            {
                result.Error = $"Unsupported file type '{extension}'. Upload a .csv, .xlsx, .xlsm or .xls file.";
                return result;
            }

            try
            {
                if (result.FileType == "CSV")
                {
                    result.Rows = ReadDelimited(stream);
                }
                else
                {
                    result.Rows = ReadExcel(stream, preferSheetContaining, out var sheetName);
                    result.SheetName = sheetName;
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"The file could not be read: {ex.Message}. If it was renamed to change its " +
                               "extension, re-save it from Excel in the correct format.";
                return result;
            }
        }

        // ------------------------------------------------------------
        // CSV (RFC4180-ish, no external dependency)
        // ------------------------------------------------------------

        private static List<List<string>> ReadDelimited(Stream stream)
        {
            string text;
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true))
            {
                text = reader.ReadToEnd();
            }

            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

            var delimiter = DetectDelimiter(text);

            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cell.Append(c);
                    continue;
                }

                if (c == '"') { inQuotes = true; continue; }
                if (c == delimiter) { row.Add(cell.ToString()); cell.Clear(); continue; }
                if (c == '\r') continue;

                if (c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    continue;
                }

                cell.Append(c);
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            // Drop leading '#' comment lines (the templates ship with them).
            return rows
                .Where(r => !(r.Count > 0 && (r[0] ?? "").TrimStart().StartsWith("#")))
                .Select(r => r.Select(c => (c ?? "").Trim()).ToList())
                .ToList();
        }

        private static char DetectDelimiter(string text)
        {
            var firstLine = text.Split('\n')
                .FirstOrDefault(l => !l.TrimStart().StartsWith("#") && l.Trim().Length > 0) ?? "";

            var best = ',';
            var bestCount = 0;
            foreach (var candidate in new[] { ',', ';', '\t', '|' })
            {
                var count = firstLine.Count(c => c == candidate);
                if (count > bestCount) { bestCount = count; best = candidate; }
            }
            return best;
        }

        // ------------------------------------------------------------
        // EXCEL
        // ------------------------------------------------------------

        private static List<List<string>> ReadExcel(
            Stream stream, string preferSheetContaining, out string sheetName)
        {
            if (!_encodingProviderRegistered)
            {
                // Required by ExcelDataReader for legacy .xls code pages on .NET Core.
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _encodingProviderRegistered = true;
            }

            var sheets = new List<(string Name, List<List<string>> Rows)>();

            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                do
                {
                    var rows = new List<List<string>>();
                    while (reader.Read())
                    {
                        var row = new List<string>(reader.FieldCount);
                        for (int i = 0; i < reader.FieldCount; i++)
                            row.Add(CellToString(reader.GetValue(i)));
                        rows.Add(row);
                    }
                    sheets.Add((reader.Name, rows));
                } while (reader.NextResult());
            }

            var withData = sheets
                .Where(s => s.Rows.Any(r => r.Any(c => !string.IsNullOrWhiteSpace(c))))
                .ToList();

            if (!withData.Any())
            {
                sheetName = sheets.FirstOrDefault().Name;
                return new List<List<string>>();
            }

            var chosen = withData[0];
            if (!string.IsNullOrEmpty(preferSheetContaining))
            {
                foreach (var sheet in withData)
                {
                    if ((sheet.Name ?? "").IndexOf(preferSheetContaining, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        chosen = sheet;
                        break;
                    }
                }
            }

            sheetName = chosen.Name;
            return chosen.Rows;
        }

        public static string CellToString(object value)
        {
            switch (value)
            {
                case null:
                case DBNull:
                    return "";
                case DateTime dt:
                    return dt.TimeOfDay == TimeSpan.Zero
                        ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                case double d:
                    return d.ToString("0.##########", CultureInfo.InvariantCulture);
                case float f:
                    return f.ToString("0.##########", CultureInfo.InvariantCulture);
                case decimal m:
                    return m.ToString("0.##########", CultureInfo.InvariantCulture);
                case bool b:
                    return b ? "TRUE" : "FALSE";
                default:
                    return (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").Trim();
            }
        }

        // ------------------------------------------------------------
        // VALUE PARSING
        // ------------------------------------------------------------

        /// <summary>Uppercase, strip everything that isn't a letter or digit.</summary>
        public static string Canonicalize(string header)
        {
            if (string.IsNullOrEmpty(header)) return "";
            var sb = new StringBuilder(header.Length);
            foreach (var c in header)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses money/rate cells the way ops actually type them:
        /// "₦1,250,000.00", "(500)", "1.5%", "N250000", " 12 ".
        /// </summary>
        public static bool TryParseDecimal(string raw, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var text = raw.Trim();
            var negative = false;

            if (text.StartsWith("(") && text.EndsWith(")"))
            {
                negative = true;
                text = text.Substring(1, text.Length - 2);
            }

            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (char.IsDigit(c) || c == '.' || c == '-') sb.Append(c);
                else if (c == ',' || c == ' ' || c == '%' || c == '₦' || c == '$' ||
                         c == '£' || c == '€' || c == '\'') continue;
                else if (char.IsLetter(c)) continue; // "NGN", "N", "USD" prefixes
                else return false;
            }

            var cleaned = sb.ToString();
            if (cleaned.Length == 0) return false;

            if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return false;

            if (negative) value = -value;
            return true;
        }

        public static bool TryParseInt(string raw, out int value)
        {
            value = 0;
            if (!TryParseDecimal(raw, out var dec)) return false;
            value = (int)Math.Round(dec, MidpointRounding.AwayFromZero);
            return true;
        }

        /// <summary>
        /// Excel real dates arrive as yyyy-MM-dd via CellToString; text dates are
        /// tried day-first (dd/MM/yyyy), which is what Nigerian spreadsheets use.
        /// </summary>
        public static bool TryParseDate(string raw, out DateTime value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var text = raw.Trim();
            var formats = new[]
            {
                "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd",
                "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
                "dd/MM/yy", "d/M/yy", "dd.MM.yyyy",
                "dd-MMM-yyyy", "d MMM yyyy", "MMM d, yyyy"
            };

            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out value))
                return true;

            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }
    }
}
