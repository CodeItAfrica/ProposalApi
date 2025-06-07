using System;
using System.IO;
using System.Text.RegularExpressions;

namespace GibsLifesMicroWebApi
{
    public static class Extensions
    {
        public static bool IsNullableEnum(this Type type)
        {
            Type uType = Nullable.GetUnderlyingType(type);
            return (uType != null) && uType.IsEnum;
        }

        public static byte[] ReadFully(this Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static bool IsNumeric(this string source)
        {
            try
            {
                return decimal.TryParse(source, out decimal result);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsValidEmail(this string source)
        {
            try
            {
                return Regex.IsMatch(source, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$");
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}
