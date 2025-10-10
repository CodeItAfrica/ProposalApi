using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Others
{
    public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        private readonly AuthorizationOptions _options;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
            : base(options)
        {
            _options = options.Value;
        }

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            var policy = await base.GetPolicyAsync(policyName);
            if (policy == null)
            {
                var newPolicy = new AuthorizationPolicyBuilder()
                    .AddRequirements(new PermissionRequirement(policyName))
                    .Build();

                _options.AddPolicy(policyName, newPolicy);
                return newPolicy;
            }

            return policy;
        }
    }
}
