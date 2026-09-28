using ClosedXML.Excel;

using GibsLifesMicroWebApi.Infrastructure.Common;
using GibsLifesMicroWebApi.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories;

public class BulkMemberExcelParser
{
    // Normalised header alias -> canonical field name.
    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.Ordinal)
    {
        ["SN"] = "SerialNo",
        ["SERIALNO"] = "SerialNo",
        ["SNO"] = "SerialNo",
        ["CUSTNO"] = "CustomerNo",
        ["CUSTOMERNO"] = "CustomerNo",
        ["CUSTOMERNUMBER"] = "CustomerNo",
        ["ACCOUNTNO"] = "CustomerNo",
        ["ACCTNO"] = "CustomerNo",
        ["CUSTNM"] = "CustomerName",
        ["CUSTOMERNAME"] = "CustomerName",
        ["CUSTNAME"] = "CustomerName",
        ["CUSTOMERNM"] = "CustomerName",
        ["NAME"] = "CustomerName",
        ["PRODUCTDESC"] = "ProductDescription",
        ["PRODDESC"] = "ProductDescription",
        ["PRODUCTDESCRIPTION"] = "ProductDescription",
        ["DISBAMT"] = "DisbursementAmount",
        ["DISBURSEMENTAMOUNT"] = "DisbursementAmount",
        ["DISBURSEDAMOUNT"] = "DisbursementAmount",
        ["LOANAMOUNT"] = "DisbursementAmount",
        ["AMOUNT"] = "DisbursementAmount",
        ["DISBSMNTDT"] = "DisbursementDate",
        ["DISBURSEMENTDATE"] = "DisbursementDate",
        ["DISBDATE"] = "DisbursementDate",
        ["DISBURSEDDATE"] = "DisbursementDate",
        ["DISBURSMENTDATE"] = "DisbursementDate",
        ["MATURITYDATE"] = "MaturityDate",
        ["MATDATE"] = "MaturityDate",
        ["MATURITY"] = "MaturityDate",
        ["TENOR"] = "Tenor",
        ["TERM"] = "Tenor",
        ["LOANTYPE"] = "LoanType",
        ["CUSTADDR"] = "CustomerAddress",
        ["CUSTOMERADDRESS"] = "CustomerAddress",
        ["ADDRESS"] = "CustomerAddress",
        ["ADDR"] = "CustomerAddress",
        ["CUSTSTATE"] = "States",
        ["STATE"] = "States",
        ["STATES"] = "States",
        ["AREA"] = "Area",
        ["ZONE"] = "Zones",
        ["ZONES"] = "Zones",
        ["BIRTHDATE"] = "DateOfBirth",
        ["DOB"] = "DateOfBirth",
        ["DATEOFBIRTH"] = "DateOfBirth",
        ["BIRTHDT"] = "DateOfBirth",
        ["GENDER"] = "Gender",
        ["SEX"] = "Gender",
        ["OCCUPATION"] = "Occupation",
        ["OCC"] = "Occupation",
        ["CONTACT"] = "PhoneNo",
        ["PHONE"] = "PhoneNo",
        ["PHONENO"] = "PhoneNo",
        ["MOBILE"] = "PhoneNo",
        ["MOBILENO"] = "PhoneNo",
        ["GSM"] = "PhoneNo",
        ["BVN"] = "Bvn",
        ["NEXTOFKIN"] = "NextOfKin",
        ["NOK"] = "NextOfKin",
        ["BRANCHNAME"] = "BranchName",
        ["BRANCH"] = "BranchName",
    };

    private static readonly string[] DateFormats =
    {
        "dd-MMM-yy", "d-MMM-yy", "dd-MMM-yyyy", "d-MMM-yyyy",
        "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy"
    };

    public ExcelParseResultDto Parse(Stream excelStream, string policyNo, string? endorsementNo,
        BulkPricingMode pricingMode, decimal fixedPremiumRate, decimal loadingRate, decimal discountRate,
        string? sheetName = null, string? BizOption = null)
    {
        var result = new ExcelParseResultDto
        {
            Payload = new BulkMemberUploadDto
            {
                PolicyNo = policyNo,
                EndorsementNo = endorsementNo,
                PricingMode = pricingMode,
                FixedPremiumRate = fixedPremiumRate,
                LoadingRate = loadingRate,
                DiscountRate = discountRate,
                BizOption = BizOption
            }
        };

        using var wb = new XLWorkbook(excelStream);
        var ws = string.IsNullOrWhiteSpace(sheetName)
            ? wb.Worksheets.First()
            : wb.Worksheet(sheetName);

        var rows = ws.RowsUsed().ToList();
        if (rows.Count == 0)
        {
            result.Warnings.Add("The worksheet contains no data.");
            return result;
        }

        // ---- Locate the header row (tolerant of title rows above the table) ----
        var headerRowIndex = -1;
        Dictionary<string, int> columnMap = new();

        for (var i = 0; i < Math.Min(rows.Count, 15); i++)
        {
            var (map, _) = BuildColumnMap(rows[i]);
            if (map.ContainsKey("CustomerName") && map.ContainsKey("DisbursementAmount"))
            {
                headerRowIndex = i;
                columnMap = map;
                break;
            }
        }

        if (headerRowIndex < 0)
        {
            result.Warnings.Add(
                "Could not find a header row containing both a customer-name and a disbursement-amount column.");
            return result;
        }

        // Record any headers we couldn't place, so nothing silently disappears.
        var (_, unmapped) = BuildColumnMap(rows[headerRowIndex]);
        result.UnmappedColumns = unmapped;

        // ---- Read data rows ----
        var serial = 0;
        for (var i = headerRowIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            serial++;

            var name = GetString(row, columnMap, "CustomerName");
            var amount = GetDecimal(row, columnMap, "DisbursementAmount");

            // Skip blank/summary rows: no name and no amount.
            if (string.IsNullOrWhiteSpace(name) && amount == 0m)
            {
                result.RowsSkipped++;
                continue;
            }

            result.DataRowsRead++;

            var dto = new BulkMemberRowDto
            {
                SerialNo = GetString(row, columnMap, "SerialNo") ?? serial.ToString(),
                CustomerNo = GetString(row, columnMap, "CustomerNo"),
                CustomerName = name,
                CustomerAddress = GetString(row, columnMap, "CustomerAddress"),
                PhoneNo = GetString(row, columnMap, "PhoneNo"),
                Gender = NormalizeGender(GetString(row, columnMap, "Gender")),
                Occupation = GetString(row, columnMap, "Occupation"),
                Bvn = GetString(row, columnMap, "Bvn"),
                BranchName = GetString(row, columnMap, "BranchName"),
                States = GetString(row, columnMap, "States"),
                Area = GetString(row, columnMap, "Area"),
                Zones = GetString(row, columnMap, "Zones"),
                NextOfKin = GetString(row, columnMap, "NextOfKin"),
                ProductDescription = GetString(row, columnMap, "ProductDescription"),
                LoanType = GetString(row, columnMap, "LoanType"),
                Tenor = GetDecimal(row, columnMap, "Tenor"),
                DisbursementAmount = amount,
                DateOfBirth = GetDate(row, columnMap, "DateOfBirth", i + 1, result),
                DisbursementDate = GetDate(row, columnMap, "DisbursementDate", i + 1, result),
                MaturityDate = GetDate(row, columnMap, "MaturityDate", i + 1, result)
            };

            result.Payload.Rows.Add(dto);
            result.RowsMapped++;
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Header + value helpers
    // ------------------------------------------------------------------
    private static (Dictionary<string, int> map, List<string> unmapped) BuildColumnMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>();
        var unmapped = new List<string>();

        foreach (var cell in headerRow.CellsUsed())
        {
            var raw = cell.GetString();
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var norm = Normalize(raw);
            if (HeaderAliases.TryGetValue(norm, out var field))
            {
                // First occurrence wins; ignore duplicate header columns.
                map.TryAdd(field, cell.Address.ColumnNumber);
            }
            else
            {
                unmapped.Add(raw.Trim());
            }
        }

        return (map, unmapped);
    }

    private static string Normalize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string? GetString(IXLRow row, Dictionary<string, int> map, string field)
    {
        if (!map.TryGetValue(field, out var col)) return null;
        var s = row.Cell(col).GetString()?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static decimal GetDecimal(IXLRow row, Dictionary<string, int> map, string field)
    {
        if (!map.TryGetValue(field, out var col)) return 0m;

        var cell = row.Cell(col);
        if (cell.TryGetValue<decimal>(out var dec)) return dec;

        var s = cell.GetString()?.Trim();
        if (string.IsNullOrEmpty(s)) return 0m;

        s = s.Replace(",", string.Empty).Replace("₦", string.Empty).Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    private static DateTime? GetDate(IXLRow row, Dictionary<string, int> map, string field,
        int rowNumber, ExcelParseResultDto result)
    {
        if (!map.TryGetValue(field, out var col)) return null;

        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime();

        var s = cell.GetString()?.Trim();
        if (string.IsNullOrEmpty(s)) return null;

        // Lenient first (handles "06-MAY-26"), then explicit formats.
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose))
            return loose;

        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
            return exact;

        result.Warnings.Add($"Row {rowNumber}: could not parse {field} value '{s}'.");
        return null;
    }

    private static string? NormalizeGender(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var g = raw.Trim().ToUpperInvariant();
        return g switch
        {
            "M" or "MALE" => "M",
            "F" or "FEMALE" => "F",
            _ => raw.Trim()
        };
    }
}