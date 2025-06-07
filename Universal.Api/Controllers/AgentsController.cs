using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace GibsLifesMicroWebApi.Controllers
{
    //[Authorize(Roles = "APP")]
    public class AgentsController : SecureControllerBase
    {
        private readonly Settings _settings;
        private readonly DataContext _db;

        public AgentsController(Repository repository, AuthContext authContext, DataContext db, Settings settings) : base(repository, authContext)
        {
            _db = db;

            _settings = settings;
        }

        // <summary>
        // Agents should Login using this endpoint. It creates a JWT Token that can be used to access secured endpoints of the API.
        // </summary>
        // <returns>A JWT Token and it's expiry time.</returns>
        //[HttpPost("Login"), AllowAnonymous]
        //public async Task<ActionResult<LoginResult>> LoginAgent(AgentLoginRequest login)
        //{
        //    if (login is null)
        //        return BadRequest("Request body is null");

        //    try
        //    {
        //        var party = await _repository.PartyLoginAsync(login.AppID, login.AgentID, login.Password);

        //        if (party is null)
        //            return NotFound("AgentID or Password is incorrect");

        //        else if (party.ApiStatus != "ENABLED")
        //            return Unauthorized("This Agent has not been activated");

        //        string token = CreateToken(_settings.JwtSecret,
        //                                   _settings.JwtExpiresIn,
        //                                   login.AppID, 
        //                                   login.AgentID, 
        //                                   party.PartyID, "AGENT");
        //        var response = new LoginResult
        //        {
        //            TokenType = "Bearer",
        //            ExpiresIn = _settings.JwtExpiresIn,
        //            AccessToken = token
        //        };
        //        return Ok(response);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        /// <summary>
        /// Fetch a collection of Agents.
        /// </summary>
        /// <returns>A collection of Agents.</returns>
        //[HttpGet]
        //public async Task<ActionResult<IEnumerable<AgentResult>>> ListAgents([FromQuery] FilterPaging filter)
        //{
        //    try
        //    {
        //        var agents = await _repository.PartySelectAsync(filter);
        //        return Ok(agents.Select(x => new AgentResult(x)).ToList());
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}
        [HttpPost("agent/authenticate"), AllowAnonymous]
        public async Task<ActionResult<LoginResult>> AgentAppLogin([FromBody] AppLoginRequest login)
        {
            if (login is null)
                return BadRequest("Request body is null");


            try
            {
                var agentUser = await _repository.AgentAppLogin(login.AppID, login.Password);

                if (agentUser is null)
                    return NotFound("Invalid credentials");

                if ((bool)!agentUser.IsVerified)
                {
                    return Unauthorized("Your registration is pending verification and approval. Please wait 24–48 hours.");
                }

                string token = AgentCreateToken(_settings.JwtSecret, _settings.JwtExpiresIn, agentUser.AgentID, "AGENT");

                var response = new AgentLoginResult
                {
                    TokenType = "Bearer",
                    ExpiresIn = _settings.JwtExpiresIn,
                    AccessToken = token,
                    AgentID = agentUser.AgentID // Return AgentID
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        ///// <summary>
        ///// Your App must Login using this endpoint. It creates a JWT Token that must be used to access secured endpoints of the API.
        ///// </summary>
        ///// <returns>A JWT Token and it's expiry time.</returns>
        //[HttpPost("agent/authenticate"), AllowAnonymous]
        //public async Task<ActionResult<LoginResult>> AgentAppLogin([FromBody] AgentLoginRequest login)
        //{
        //    if (login is null)
        //        return BadRequest("Request body is null");

        //    try
        //    {
        //        var apiUser = await _repository.AgentAppLogin(login.Email, login.Passwords);

        //        if (apiUser == null)
        //            return NotFound("AppID or Password is incorrect");

        //        //if (apiUser is null)
        //        //    return NotFound("AppID or Password is incorrect");

        //        //else if (apiUser.Status == "DISABLED")
        //        //    return Unauthorized("This App has not been activated");

        //        string token = CreateToken(_settings.JwtSecret,
        //                                   _settings.JwtExpiresIn,
        //                                   login.Email, "", "", "APP");

        //        var response = new LoginResult
        //        {
        //            TokenType = "Bearer",
        //            ExpiresIn = _settings.JwtExpiresIn,
        //            AccessToken = token
        //        };
        //        return Ok(response);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        /// <summary>
        /// Fetch a single Agent.
        /// </summary>
        /// <param name="agentId">Id of the Agent to get.</param>
        /// <returns>The Agent with the Id entered.</returns>
        [HttpGet("{agentId}")]
        public async Task<ActionResult> GetAgent(string agentId)
        {
            try
            {
                var agent = await _repository.AgentSelectThisAsync(agentId);

                if (agent is null)
                    return NotFound();

                return Ok(agent);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpPost("admin/verify-agent")]
        [Authorize(Roles = "APP")]
        public async Task<IActionResult> VerifyAgent([FromBody] AgentVerificationRequest request)
        {
            try
            {
                var result = await _repository.VerifyAgentAsync(request);

                if (!result.Success)
                    return BadRequest(result.Message);

                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Agents>>> GetAgents()
        {
            var issues = await _db.Agents
                .Include(a => a.Attachments)
                .OrderByDescending(a => a.SubmittedBy)
                .ToListAsync();


            return Ok(issues);
        }

        /// <summary>
        /// Create an Agent.
        /// </summary>
        //[Authorize(Roles = "APP")]
        [HttpPost]
public async Task<ActionResult> CreateAgent([FromForm]CreateNewAgentRequest request)
{
    try
    {
        var agent = await _repository.AgentCreateAsync(request);
        await _repository.SaveChangesAsync();

        // Send confirmation email
        await _repository.SendEmail(agent.Email, "Registration Received", $@"
            Dear {agent.Agent},

            Thank you for registering on the GIBS Partner Agency Platform.

            Our team will contact you within the next 24–48 hours to:
            - Verify your registration intent,
            - Profile your agency, and
            - Complete your onboarding.

            Please monitor your inbox for further updates.

            Best regards,
            GIBS Support Team
        ");

        // Return response with Thank You page info
        return Ok(new {
            message = "Registration successful. Redirecting to Thank You page.",
        });
    }
    catch (Exception ex)
    {
        return ExceptionResult(ex);
    }
}



        [HttpDelete("{agentId}")]
        public async Task<ActionResult> DeleteAgent(string agentId)
        {
            try
            {
                var agent = await _repository.AgentDeleteAsync(agentId);

                if (agent is null)
                    return NotFound();

                return Ok();
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpPatch("update-password")]
        public async Task<ActionResult> UpdateAgentPassword([FromBody] UpdatePasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                    return BadRequest("Email is required.");

                var agent = await _repository.GetAgentByEmailAsync(request.Email);

                if (agent == null)
                    return NotFound("Agent not found.");

                if (!string.IsNullOrEmpty(agent.Passwords))
                    return BadRequest("Password already exists for this agent.");

                if (string.IsNullOrEmpty(request.Password))
                    return BadRequest("New password is required.");

                agent.Passwords = request.Password; // Hash if needed
                agent.ModifiedOn = DateTime.UtcNow;

                await _repository.UpdateAgentAsync(agent);

                return Ok("Password updated successfully.");
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }


    }
}
