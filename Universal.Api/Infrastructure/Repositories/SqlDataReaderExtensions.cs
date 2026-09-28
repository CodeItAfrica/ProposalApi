using Microsoft.Data.SqlClient;
using System;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    // Add this helper class for safe database value extraction
    public static class SqlDataReaderExtensions
    {
        public static T GetValueOrDefault<T>(this SqlDataReader reader, string columnName, T defaultValue = default(T))
        {
            var value = reader[columnName];
            if (value == null || value == DBNull.Value)
                return defaultValue;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static T? GetNullableValue<T>(this SqlDataReader reader, string columnName) where T : struct
        {
            var value = reader[columnName];
            if (value == null || value == DBNull.Value)
                return null;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static string GetStringOrDefault(this SqlDataReader reader, string columnName, string defaultValue = null)
        {
            var value = reader[columnName];
            return value == DBNull.Value ? defaultValue : value?.ToString();
        }
    }

}
