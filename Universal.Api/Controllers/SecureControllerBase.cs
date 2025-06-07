using System;
using System.Text;
using System.Security.Claims;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Data;

namespace GibsLifesMicroWebApi.Controllers
{
    [ApiController]
    [ValidateModel]
    [Produces("application/json")]
    [Route("api/v1/[controller]")]
    [ApiConventionType(typeof(DefaultApiConventions))]
    public class SecureControllerBase : ControllerBase
    {
        protected readonly Repository _repository;
        protected readonly AuthContext _authContext;

        public SecureControllerBase(Repository repository, AuthContext authContext)
        {
            _repository = repository;
            _authContext = authContext;
        }

        protected ActionResult ExceptionResult(Exception ex)
        {
            if (ex is null)
                return StatusCode(500, "SecureControllerBase.ExceptionResult() ex parameter cannot be null");

            if (ex is ArgumentException || ex is ArgumentNullException || ex is KeyNotFoundException)
                return BadRequest(ex.Message);

            if (ex.InnerException != null)
                return StatusCode(500, ex.Message + "\n\n\n --- inner exception --- " + ex.InnerException.ToString());

            return StatusCode(500, ex.Message);
        }
        protected string AgentCreateToken(string secret, int expiresIn, string agentId, string role)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, agentId), // Store AgentID
                new Claim("AgentId", agentId),
        new Claim(ClaimTypes.Role, role) // Store Role (e.g., AGENT)
    };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresIn),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected string CreateToken(string secret, int expiresIn, 
                                     string appId, string userId, string tableId, string role)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim("AppID", appId),
                    new Claim("UserID", userId),
                    new Claim("TableID", tableId),
                    new Claim(ClaimTypes.Name, $"{appId}/{userId}"),
                    new Claim(ClaimTypes.Role, role)
                }),
                Expires = DateTime.UtcNow.AddSeconds(expiresIn),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature),
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
