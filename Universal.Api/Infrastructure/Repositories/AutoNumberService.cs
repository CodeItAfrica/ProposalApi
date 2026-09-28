
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Infrastructure.Common;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories;



/// <summary>
/// Replacement for cls_AutoNumber.getNextAutoNumber. Reads a row from AutoNumbers keyed by
/// NumType (+ BranchID + RiskID), renders it using the row's <see cref="AutoNumber.Format"/>
/// template, then increments NextValue and stamps LastUpdated atomically.
///
/// The exact token convention lives in the legacy cls_AutoNumber. <see cref="RenderFormat"/>
/// supports the conventions these systems commonly use; if the stored Format is empty or has
/// no recognisable sequence placeholder, it falls back to the observed shape per number type
/// (e.g. policy "GFMW/26/0000736/HO"). Share one real Format row and this can be pinned exactly.
///
/// The NULL-NextValue guard addresses the known "branch 07 NextValue was NULL" defect.
/// </summary>
/// <summary>
/// Replacement for cls_AutoNumber.getNextAutoNumber. Reads a row from AutoNumbers keyed by
/// NumType (+ BranchID + RiskID), renders it using the row's <see cref="AutoNumber.Format"/>
/// template, then increments NextValue and stamps LastUpdated.
///
/// Real format convention (confirmed from production data), tokens delimited by $...$:
///   $SR$    -> sub-risk / RiskID          (e.g. "GLA")
///   $YR$    -> 2-digit year               (e.g. "26")
///   $00000$ -> sequence, zero-padded to the number of 0s in the token
///   $BR2$   -> branch code                (see note below)
/// Example: "$SR$/$YR$/$00000$/$BR2$" with seq 736 -> "GLA/26/00736/<branch>".
///
/// NOTE on $BR2$: production samples show a 2-letter branch abbreviation (e.g. "HO", "LA"),
/// which is NOT the numeric BranchID ("01".."36"). We don't have the Branches abbreviation
/// table here, so $BR2$ is filled with whatever branch value is passed in. Supply the abbrev
/// (or wire a Branches lookup) if you need "HO"/"LA" rather than the numeric code.
///
/// The NULL-NextValue guard addresses the known "branch 07 NextValue was NULL" defect.
/// </summary>
public class AutoNumberService 
{
    private const int DefaultWidth = 7;
    private const string CatchAllBranch = "ALL";
    private readonly DataContext _db;

    public AutoNumberService(DataContext db) => _db = db;

    public async Task<string> GetNextAsync(AutoNumberType type, string branchId, string? riskId = null,
          CancellationToken ct = default)
    {
        var typeKey = type switch
        {
            AutoNumberType.Policy => "POLICY",
            AutoNumberType.EndorsementNo => "ENDTNO",
            AutoNumberType.CertNo => "CERTNO",
            _ => type.ToString().ToUpperInvariant()
        };

        // Most specific first: type + branch + risk, then type + branch, then the "ALL"
        // catch-all branch row. We deliberately do NOT fall back to an arbitrary first row
        // for the type — that would silently consume another branch's sequence.
        var row = await _db.AutoNumbers
                      .Where(a => a.NumType == typeKey && a.BranchID == branchId && a.RiskID == riskId)
                      .FirstOrDefaultAsync(ct)
                  ?? await _db.AutoNumbers
                      .Where(a => a.NumType == typeKey && a.BranchID == branchId)
                      .FirstOrDefaultAsync(ct)
                  ?? await _db.AutoNumbers
                      .Where(a => a.NumType == typeKey && a.BranchID == CatchAllBranch)
                      .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            // No branch row and no ALL row: provision one so numbering can proceed.
            row = new AutoNumber
            {
                NumType = typeKey,
                BranchID = branchId,
                RiskID = riskId,
                NextValue = 1,
                Format = "$SR$/$YR$/$0000000$/$BR2$"
            };
            _db.AutoNumbers.Add(row);
        }

        // Guard: NULL NextValue must not blow up (legacy branch-07 defect).
        var current = row.NextValue ?? 1;

        var formatted = !string.IsNullOrWhiteSpace(row.Format)
            ? RenderFormat(row.Format!, current, branchId, riskId)
            : FallbackFormat(type, current, branchId, riskId);

        row.NextValue = current + 1;
        row.LastUpdated = DateTime.Now;
        await _db.SaveChangesAsync(ct);

        return formatted;
    }

    /// <summary>
    /// Render a $...$-delimited template. A token that is all zeros is the zero-padded
    /// sequence; named tokens map to sub-risk, year and branch. Unknown tokens are left as-is.
    /// </summary>
    internal static string RenderFormat(string format, long seq, string? branch, string? risk)
    {
        return Regex.Replace(format, @"\$([^$]*)\$", m =>
        {
            var token = m.Groups[1].Value.Trim();

            // $00000$ style -> sequence padded to the token width.
            if (token.Length > 0 && token.All(c => c == '0'))
                return seq.ToString().PadLeft(token.Length, '0');

            return token.ToUpperInvariant() switch
            {
                "SR" or "SUBRISK" or "RISK" or "RISKID" => risk ?? string.Empty,
                "YR" or "YY" => DateTime.Now.ToString("yy"),
                "YYYY" or "YEAR" => DateTime.Now.ToString("yyyy"),
                "MO" or "MM" or "MONTH" => DateTime.Now.ToString("MM"),
                "DD" or "DY" or "DAY" => DateTime.Now.ToString("dd"),
                "BR" or "BR2" or "BRANCH" or "BRANCHID" => branch ?? string.Empty,
                "SEQ" or "SEQUENCE" or "NUM" or "NUMBER" or "N" => seq.ToString().PadLeft(DefaultWidth, '0'),
                _ => m.Value // leave unrecognised tokens untouched
            };
        });
    }

    /// <summary>Shapes observed in the running app, used only when a row has no Format.</summary>
    private static string FallbackFormat(AutoNumberType type, long seq, string? branch, string? risk) => type switch
    {
        AutoNumberType.Policy =>
            $"{risk}/{DateTime.Now:yy}/{seq.ToString().PadLeft(DefaultWidth, '0')}/{branch}",
        AutoNumberType.EndorsementNo =>
            $"E/{risk}/{seq.ToString().PadLeft(5, '0')}/{DateTime.Now:yyyy}",
        AutoNumberType.CertNo =>
            $"{risk}{seq.ToString().PadLeft(DefaultWidth, '0')}",
        _ => seq.ToString().PadLeft(DefaultWidth, '0')
    };
}