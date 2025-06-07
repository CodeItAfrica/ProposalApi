using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository 
    {
        private readonly DataContext _db;
        private AuthContext _authContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string BRANCH_ID = "19";
        //private const string BRANCH_NAME = "RETAIL OFFICE";
        private const string SUBMITTED_BY = "WEB-API";
        private readonly IConfiguration _config;


        public Repository(IHttpContextAccessor httpContextAccessor, DataContext db, AuthContext sc, IConfiguration config)
        {
            _db = db;
            _authContext = sc;
            _httpContextAccessor = httpContextAccessor;
            _config = config;


        }

        public async Task<ApiUser> AppLogin(string username, string password)
        {
            var user = await _db.OpenApiUsers.FirstOrDefaultAsync(x => x.CompanyName == username);
            if (user == null)
                return null;

            string hashedPassword = HashPassword(password);

            if (user.Password != hashedPassword)
                return null;

            return user;
        }

        private string HashPassword(string password)
        {
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(password);
                var hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
            }
        }
        public Task<Agents> AgentAppLogin(string username, string password)
        {
            return _db.Agents.FirstOrDefaultAsync(x => x.Email == username && x.Passwords == password);
        }

        public Task<int> SaveChangesAsync()
        {
            return _db.SaveChangesAsync();
        }

        public AuditLog AuditLogCreateAsync(string logType, string source, string categories, string logDetails)
        {        
            var log = new AuditLog()
            {
                LogType = logType,
                Source = source,
                Category = categories,
                Description = logDetails,
                SubmittedBy = $"{SUBMITTED_BY}/{_authContext.User.AppId}",
                SubmittedOn = DateTime.Now,
            };

            _db.AuditLogs.Add(log);
            return log;
        }
    }
}
