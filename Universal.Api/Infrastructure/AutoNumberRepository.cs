//using System;
//using System.Linq;
//using System.Collections.Generic;
//using GibsLifesMicroWebApi.Models;
//using Microsoft.EntityFrameworkCore;

//namespace GibsLifesMicroWebApi.Data.Repositories
//{
//    public partial class Repository
//    {
//        public string GetNextAutoNumber(
//        string numType,
//        string branchID,
//        string subRiskNo = "",
//        string endorseType = "",
//        DateTime optionalDate = default)
//        {
//            if (optionalDate == default)
//                optionalDate = DateTime.Today;

//            try
//            {
//                string format = GetSerialNoFormat(numType, branchID);
//                if (!format.Contains("$0"))
//                    throw new Exception("Missing serial number format '$0000$'.");

//                long nextSerial = GetNextSerialNo(numType, branchID);
//                if (nextSerial < 1)
//                    throw new Exception("Invalid serial number generation.");

//                // Fill serial number with leading zeros
//                int zeroCount = format.IndexOf("0$") - format.IndexOf("$0");
//                zeroCount = Math.Max(zeroCount, 3);
//                string zeros = new string('0', zeroCount);
//                format = format.Replace($"${zeros}$", nextSerial.ToString(zeros));

//                // Replace placeholders
//                format = format.Replace("$SR$", subRiskNo)
//                               .Replace("$YR$", optionalDate.ToString("yy"))
//                               .Replace("$MO$", optionalDate.ToString("MM"))
//                               .Replace("$BR$", branchID)
//                               .Replace("$P$", GetEndorsementCode(endorseType));

//                if (format.Contains("$RC$"))
//                {
//                    var risk = _db.SubRisks.FirstOrDefault(x => x.SubRiskID == subRiskNo);
//                    format = format.Replace("$RC$", risk?.RiskID ?? string.Empty);
//                }

//                if (format.Contains("$BR2$"))
//                {
//                    var branch = _db.Branches.FirstOrDefault(x => x.BranchID == branchID);
//                    format = format.Replace("$BR2$", branch?.BranchID2 ?? string.Empty);
//                }

//                return format.ToUpper();
//            }
//            catch (Exception ex)
//            {
//                throw new InvalidOperationException("Error in GetNextAutoNumber()", ex);
//            }
//        }

//        public long GetNextSerialNo(string numType, string branchID, string riskID = "")
//        {
//            long IncrementAndReturn(List<AutoNumber> list, long defaultValue)
//            {
//                if (list.Any())
//                {
//                    var item = list[0];
//                    long result = item.NextValue ?? 1;
//                    item.NextValue = result + 1;
//                    _db.SaveChanges();
//                    return result;
//                }
//                return defaultValue;
//            }

//            try
//            {
//                var query = _db.AutoNumbers.Where(x => x.NumType == numType);

//                // Try "ALL" branch first
//                var allBranch = query.Where(x => x.BranchID == "ALL").ToList();
//                if (allBranch.Any()) return IncrementAndReturn(allBranch, 1);

//                // Otherwise filter by specific branch
//                query = query.Where(x => x.BranchID == branchID);

//                if (string.IsNullOrEmpty(riskID) || numType == "CLAIM")
//                    return IncrementAndReturn(query.ToList(), -1);

//                return IncrementAndReturn(query.Where(x => x.RiskID == riskID).ToList(), -1);
//            }
//            catch
//            {
//                return -2;
//            }
//        }

//        private string GetSerialNoFormat(string numType, string branchID, string riskID = "C")
//        {
//            // Helper to get the format from a list
//            string GetFormat(List<AutoNumber> list) => list.FirstOrDefault()?.Format ?? string.Empty;

//            // Query all auto numbers for the type
//            var query = _db.AutoNumbers.Where(x => x.NumType == numType);

//            // Priority 1: Branch = "ALL"
//            var allBranch = query.FirstOrDefault(x => x.BranchID == "ALL");
//            if (allBranch != null) return allBranch.Format;

//            // Priority 2: Specific riskID if provided
//            if (!string.IsNullOrEmpty(riskID) && numType != "CLAIM")
//            {
//                var riskMatch = query.FirstOrDefault(x => x.RiskID == riskID && x.BranchID == branchID);
//                if (riskMatch != null) return riskMatch.Format;
//            }

//            // Priority 3: Specific branch fallback
//            var branchMatch = query.FirstOrDefault(x => x.BranchID == branchID);
//            return branchMatch?.Format ?? string.Empty;
//        }

//        private static string GetEndorsementCode(string endorseType) => endorseType?.ToUpper() switch
//        {
//            "DELETE" or "DELETED" or "REVERSAL" or "RETURN" => "C",
//            "RENEW" or "RENEWAL" => "R",
//            "ADDITIONAL" => "E",
//            "NIL" or "REDO" => "N",
//            _ => "A",
//        };
//    }
//}

using System;
using System.Linq;
using System.Collections.Generic;
using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {
        public string GetNextAutoNumber(string numType, string branchID, string subRiskNo = "", string endorseType = "", DateTime optionalDate = default)
        {
            //'$SR$ - Sub Risk Code
            //'$RC$ - Risk Code
            //'$YR$ - Current Year (2 digits)
            //'$MO$ - Current Month (2 Digits)
            //'$00000$ - Serial Number
            //'$BR$ - Branch Code
            //'$BR2$ - Branch Code 2

            //'$P$ - Endorsement something

            if (optionalDate == default)
                optionalDate = DateTime.Today;

            static string GetEndorsementCode(string endorseType)
            {
                return endorseType switch
                {
                    "DELETE" or "DELETED" or "REVERSAL" or "RETURN" => "C",
                    "RENEW" or "RENEWAL" => "R",
                    "ADDITIONAL" => "E",
                    "NIL" or "REDO" => "N",
                    _ => "A",
                };
            }

            try
            {
                //if (customNo.ToUpper().Contains("AUTO"))
                //{
                string strAutoNum = GetSerialNoFormat(numType, branchID);

                if (!strAutoNum.Contains("$0", StringComparison.CurrentCulture))
                    throw new Exception("Missing format $0000$");

                long nextNum = GetNextSerialNo(numType, branchID);

                if (nextNum < 1)
                    throw new Exception("Invalid format in $0000$");

                int len = strAutoNum.IndexOf("0$") - strAutoNum.IndexOf("$0");
                if (len < 3) len = 3;

                var zeros = new string('0', len);
                strAutoNum = strAutoNum.Replace($"${zeros}$", nextNum.ToString(zeros)); //formation of number

                strAutoNum = strAutoNum.Replace("$SR$", subRiskNo);
                strAutoNum = strAutoNum.Replace("$YR$", optionalDate.ToString("yy"));
                strAutoNum = strAutoNum.Replace("$MO$", optionalDate.ToString("MM"));
                strAutoNum = strAutoNum.Replace("$BR$", branchID);

                if (strAutoNum.Contains("$RC$"))
                {
                    var risk = _db.SubRisks.FirstOrDefault(x => x.SubRiskID == subRiskNo);
                    strAutoNum = strAutoNum.Replace("$RC$", risk?.RiskID ?? string.Empty);
                }

                if (strAutoNum.Contains("$BR2$"))
                {
                    var branch = _db.Branches.FirstOrDefault(x => x.BranchID == branchID);
                    strAutoNum = strAutoNum.Replace("$BR2$", branch?.BranchID2 ?? string.Empty);
                }

                if (strAutoNum.Contains("$P$"))
                {
                    strAutoNum = strAutoNum.Replace("$P$", GetEndorsementCode(endorseType));
                }

                return strAutoNum.ToUpper();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error in GetNextAutoNumber()\n\r" + ex.ToString());
            }
        }

        public long GetNextSerialNo(string numType, string branchID, string riskID = "")
        {
            static long ResultOf(List<AutoNumber> list, long errorValue)
            {
                if (list.Count > 0)
                {
                    long result = 0;
                    if (list[0].NextValue.HasValue)
                        result = list[0].NextValue.Value;
                    else
                        result = 1;

                    //'then increment and update
                    list[0].NextValue += 1;
                    //_db.SaveChanges();
                    return result;
                }
                return errorValue;
            }

            try
            {
                var query = _db.AutoNumbers.Where(x => x.NumType == numType);
                var T = query.Where(x => x.BranchID == "ALL").ToList();

                if (T.Count > 0)
                    return ResultOf(T, 1);

                query = query.Where(x => x.BranchID == branchID);

                if (riskID == "")
                    return ResultOf(query.ToList(), -1);

                if (numType == "CLAIM")
                    return ResultOf(query.ToList(), -1);

                return ResultOf(query.Where(x => x.RiskID == riskID).ToList(), -1);
            }
            catch
            {
                return -2;
            }
        }

        private string GetSerialNoFormat(string numType, string branchID, string riskID = "")
        {
            static string ResultOf(List<AutoNumber> list) =>
                list.Count > 0 ? list[0].Format : string.Empty;

            try
            {
                var query = _db.AutoNumbers.Where(x => x.NumType == numType);
                var T = query.Where(x => x.BranchID == "ALL").ToList();

                if (T.Count > 0)
                    return T[0].Format;

                query = query.Where(x => x.BranchID == branchID);

                if (riskID == "" || numType == "CLAIM")
                    return ResultOf(query.ToList());

                return ResultOf(query.Where(x => x.RiskID == riskID).ToList());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error in GetSerialNoFormat()", ex); // keep inner ex
            }
        }
    }
}
