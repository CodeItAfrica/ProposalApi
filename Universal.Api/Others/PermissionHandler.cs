using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Others
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PermissionHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var user = context.User;

            if (user == null || !(user.Identity?.IsAuthenticated ?? false))
                return Task.CompletedTask;

            // Check if the user has the required permission (case-sensitive match)
            // Changed from "permission" to "Permission" to match the claim name in AuthController
            if (user.HasClaim(c => c.Type == "Permission" && c.Value == requirement.Permission))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}