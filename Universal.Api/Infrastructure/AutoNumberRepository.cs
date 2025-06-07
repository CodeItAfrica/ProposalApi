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
                    var risk = _db.SubRisks.First(x => x.SubRiskID == subRiskNo);
                    strAutoNum = strAutoNum.Replace("$RC$", risk.RiskID);
                }

                if (strAutoNum.Contains("$BR2$"))
                {
                    var branch = _db.Branches.First(x => x.BranchID == branchID);
                    strAutoNum = strAutoNum.Replace("$BR2$", branch.BranchID2);
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

        private long GetNextSerialNo(string numType, string branchID, string riskID = "")
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
            static string ResultOf(List<AutoNumber> list)
            {
                if (list.Count > 0)
                    return list[0].Format;
                //'no number setup
                return string.Empty;
            }

            try
            {
                var query = _db.AutoNumbers.Where(x => x.NumType == numType);
                var T = query.Where(x => x.BranchID == "ALL").ToList();

                if (T.Count > 0)
                    return T[0].Format;

                query = query.Where(x => x.BranchID == branchID);

                if (riskID == "")
                    return ResultOf(query.ToList());

                if (numType == "CLAIM")
                    return ResultOf(query.ToList());

                return ResultOf(query.Where(x => x.RiskID == riskID).ToList());
            }
            catch
            {
                throw new InvalidOperationException("Error in GetSerialNoFormat()");
            }
        }
    }
}
