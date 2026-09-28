using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    public abstract class BaseRepository
    {
        private readonly string _connectionString;

        protected BaseRepository(IConfiguration config)
        {
            _connectionString = config["AppSettings:SqldbConnString"];

        }

        protected IDbConnection OpenConnection() => new SqlConnection(_connectionString);
    }
}
