using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly IConfiguration _config;
        private readonly string _jwtSecret;

        public AuthController(DataContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            // Read the JWT secret from configuration
            _jwtSecret = _config.GetValue<string>("AppSettings:JwtSecret");
        }

        [HttpPost("login")]
        public IActionResult Login(string username, string password)
        {
            // First, try to authenticate as a User
            var user = _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefault(u => u.Username == username && u.Password == password);

            if (user != null)
            {
                // User found - return user token
                return GenerateUserToken(user);
            }

            // If not found in Users, check Agents table
            var agent = _context.Agents
                .FirstOrDefault(a => a.Email == username && a.Passwords == password && a.Deleted != 1);

            if (agent != null)
            {
                // Agent found - return agent token
                return GenerateAgentToken(agent);
            }

            // Neither user nor agent found
            return Unauthorized(new { message = "Invalid username or password" });
        }

        private IActionResult GenerateUserToken(User user)
        {
            var permissions = new List<string>();

            // From role-permissions
            var rolePermissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission?.PermissionName)
                .Where(p => !string.IsNullOrWhiteSpace(p));

            permissions.AddRange(rolePermissions);
            permissions = permissions.Distinct().ToList();

            // 🔍 Debug print - confirm permission loading
            Console.WriteLine("USER PERMISSIONS CLAIMS:");
            foreach (var p in permissions)
            {
                Console.WriteLine(p);
            }

            // Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim("UserType", "User"), // Distinguish between User and Agent
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            foreach (var p in permissions)
            {
                claims.Add(new Claim("Permission", p));
            }

            // Token signing - USE CONFIG VALUE
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );

            var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);

            var response = new
            {
                token = tokenStr,
                userType = "User",
                user = new
                {
                    id = user.UserID,
                    username = user.Username,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    roles = user.UserRoles.Select(ur => ur.Role.RoleName).Distinct().ToList(),
                    permissions = permissions
                }
            };

            return Ok(response);
        }

        private IActionResult GenerateAgentToken(Agents agent)
        {
            // Check if agent is verified
            if (agent.IsVerified == false)
            {
                return Unauthorized(new { message = "Agent account is not verified. Please contact administrator." });
            }

            Console.WriteLine("AGENT LOGIN:");
            Console.WriteLine($"AgentID: {agent.AgentID}, Agent Name: {agent.Agent}");

            // Claims for agent
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, agent.AgentID ?? ""),
                new Claim(ClaimTypes.NameIdentifier, agent.INT.ToString()),
                new Claim("UserType", "Agent"), // Distinguish between User and Agent
                new Claim("AgentID", agent.AgentID ?? ""),
                new Claim("AgentName", agent.Agent ?? ""),
                new Claim(ClaimTypes.Email, agent.Email ?? ""),
                new Claim("UnitID", agent.UnitID ?? "")
            };

            // Agents might have default permissions or specific agent permissions
            // Add default agent permissions here if needed
            var agentPermissions = new List<string>
            {
                "ViewPolicies",
                "CreateData",
                "ViewCommissions"
                // Add other default agent permissions as needed
            };

            foreach (var p in agentPermissions)
            {
                claims.Add(new Claim("Permission", p));
            }

            // Token signing - USE CONFIG VALUE
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );

            var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);

            var response = new
            {
                token = tokenStr,
                userType = "Agent",
                agent = new
                {
                    id = agent.INT,
                    agentID = agent.AgentID,
                    agentName = agent.Agent,
                    unitID = agent.UnitID,
                    email = agent.Email,
                    phone = agent.Phone1,
                    balance = agent.Balance,
                    creditLimit = agent.CreditLimit,
                    comRate = agent.ComRate,
                    isVerified = agent.IsVerified,
                    permissions = agentPermissions
                }
            };

            return Ok(response);
        }

        [Authorize(Policy = "CreateData")]
        [HttpPost("create-policy")]
        public IActionResult CreatePolicy()
        {
            return Ok("You have permission to create a policy.");
        }

        [Authorize(Policy = "DeletePolicy")]
        [HttpPost("delete-policy")]
        public IActionResult DeletePolicy()
        {
            return Ok("You have permission to DeletePolicy a policy.");
        }

        public static string EncodeToHash(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}