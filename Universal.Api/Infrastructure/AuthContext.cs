using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace GibsLifesMicroWebApi.Data
{
    public class AuthContext
    {
        private readonly IHttpContextAccessor _db;

        public AuthUser User
        {
            get
            {
                var user = _db.HttpContext.User;

                if (user == null || !user.Identity.IsAuthenticated)
                    return null;

                if (user.IsInRole("CUST"))
                    return new CustomerUser(user);

                else if (user.IsInRole("AGENT"))
                    return new AgentUser(user);

                else if (user.IsInRole("APP"))
                    return new AppUser(user);

                else
                    return null; // Anonymous User
            }
        }

        public AuthContext(IHttpContextAccessor context)
        {
            _db = context;
        }
    }

    public abstract class AuthUser
    {
        public string AppId { get; }

        public AuthUser(ClaimsPrincipal user)
        {
            AppId = user?.FindFirst(c => c.Type == "AppID")?.Value ?? "";
        }
    }

    public class AppUser: AuthUser
    {
        public AppUser(ClaimsPrincipal user) : base(user)
        {
        }
    }
    public class AgentUser : AuthUser
    {
        public string AgentId { get; }

        public AgentUser(ClaimsPrincipal user) : base(user)
        {
            AgentId = user.FindFirst("AgentId")?.Value ?? string.Empty;
        }
    }


    public class CustomerUser : AuthUser
    {
        public string CustomerId { get; }
        public string InsuredId { get; }
        public CustomerUser(ClaimsPrincipal user) : base(user)
        {
            CustomerId = user?.FindFirst(c => c.Type == "UserID")?.Value ?? "";
            InsuredId = user?.FindFirst(c => c.Type == "TableID")?.Value ?? "";
        }
    }

    public static class ClaimsPrincipalExtensions
    {
        public static string SafeFind(this ClaimsPrincipal user, string claimType)
        {
            return user?.FindFirst(claimType)?.Value ?? string.Empty;
        }
    }


}