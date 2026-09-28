using DocumentFormat.OpenXml.InkML;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        [HttpPut("agent/{agentId}")]
        public async Task<ActionResult> UpdateAgent(string agentId, [FromBody] UpdateAgentRequest dto)
        {
            try
            {
                var agent = await _repository.AgentSelectThisAsync(agentId);
                if (agent == null)
                    return NotFound($"Agent with ID {agentId} not found.");

                await _repository.UpdateAgentAsync(agent, dto);
                await _repository.SaveChangesAsync();

                return Ok(agent);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpGet("Unit")]
        public async Task<IActionResult> GetUnits()
        {
            var units = await _repository.GetUnitsAsync();
            return Ok(units);
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

        //        /// <summary>
        //        /// Create an Agent.
        //        /// </summary>
        //        //[Authorize(Roles = "APP")]
        //        [HttpPost]
        //public async Task<ActionResult> CreateAgent([FromForm]CreateNewAgentRequest request)
        //{
        //    try
        //    {
        //        var agent = await _repository.AgentCreateAsync(request);
        //        await _repository.SaveChangesAsync();

        //        // Send confirmation email
        //        await _repository.SendEmail(agent.Email, "Registration Received", $@"
        //            Dear {agent.Agent},

        //            Thank you for registering on the GIBS Partner Agency Platform.

        //            Our team will contact you within the next 24–48 hours to:
        //            - Verify your registration intent,
        //            - Profile your agency, and
        //            - Complete your onboarding.

        //            Please monitor your inbox for further updates.

        //            Best regards,
        //            GIBS Support Team
        //        ");

        //        // Return response with Thank You page info
        //        return Ok(new {
        //            message = "Registration successful. Redirecting to Thank You page.",
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        //[HttpPost("create")]
        //public async Task<IActionResult> CreateAgent([FromForm] CreateAgentRequest request)
        //{
        //    var agentno = _repository.GetNextAutoNumber("AGENTS", "01");

        //    if (request == null || request.Agent == null)
        //        return BadRequest("Invalid request.");

        //    var agent = new Agents
        //    {
        //        AgentID = agentno,
        //        UnitID = request.Agent.UnitId,
        //        Agent = request.Agent.AgentName,
        //        Address = request.Agent.Address,
        //        Area = request.Agent.Area,
        //        City = request.Agent.City,
        //        State = request.Agent.State,
        //        Phone1 = request.Agent.Phone1,
        //        Phone2 = request.Agent.Phone2,
        //        FaxNo = request.Agent.FaxNo,
        //        Email = request.Agent.Email,
        //        InsPerson = request.Agent.InsPerson,
        //        FinPerson = request.Agent.FinPerson,
        //        Balance = request.Agent.Balance,
        //        CreditLimit = request.Agent.CreditLimit,
        //        ComRate = request.Agent.ComRate,
        //        Remark = request.Agent.Remark,
        //        Accountno = request.Agent.AccountNo,
        //        Bankname = request.Agent.BankName,
        //        Tag = request.Agent.Tag,
        //        Passwords =request.Agent.Password,
        //        IsVerified = false,
        //        Deleted = 0,
        //        SubmittedBy = "api",
        //        SubmittedOn = DateTime.UtcNow
        //    };

        //    var documents = request.Documents?.Select(doc => new AttachDoc
        //    {
        //        EntryDate = DateTime.UtcNow,
        //        DocTypes = doc.DocType,
        //        DocFrom = doc.DocFrom,
        //        DocDetails = doc.DocDetails,
        //        Document = Convert.FromBase64String(doc.DocumentBase64),
        //        Deleted = false,
        //        Active = true,
        //        OptionType = "AgentDoc",
        //        SubmittedBy = "api",
        //        SubmittedOn = DateTime.UtcNow
        //    }) ?? Enumerable.Empty<AttachDoc>();

        //    var agentId = await _repository.CreateAgentWithDocumentsAsync(agent, documents);

        //    return Ok(new
        //    {
        //        AgentID = agentId,
        //        Message = "Agent created successfully."
        //    });
        //}

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

        [HttpPost("create")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateAgent([FromForm] CreateAgentFormRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.AgentName))
                    return BadRequest("Invalid request.");

                if (request.Files == null || !request.Files.Any())
                    return BadRequest("At least one document is required.");

                // ✅ Check for duplicate email
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    var emailExists = await _db.Agents
                        .AnyAsync(a => a.Email == request.Email && a.Deleted != 1);

                    if (emailExists)
                    {
                        return BadRequest(new
                        {
                            message = "An agent with this email already exists.",
                            field = "Email"
                        });
                    }

                    // Optional: Also check in Users table to prevent conflicts
                    var emailExistsInUsers = await _db.Users
                        .AnyAsync(u => u.Email == request.Email && u.Active == 1);

                    if (emailExistsInUsers)
                    {
                        return BadRequest(new
                        {
                            message = "This email is already registered in the system.",
                            field = "Email"
                        });
                    }
                }

                // Optional: Check for duplicate AgentID if manually provided
                // (Skip if auto-generated)
                // if (!string.IsNullOrWhiteSpace(request.AgentID))
                // {
                //     var agentIdExists = await _db.Agents
                //         .AnyAsync(a => a.AgentID == request.AgentID && a.Deleted != 1);
                //     
                //     if (agentIdExists)
                //     {
                //         return BadRequest(new 
                //         { 
                //             message = "An agent with this AgentID already exists.",
                //             field = "AgentID"
                //         });
                //     }
                // }

                // Map agent
                var agent = new Agents
                {
                    AgentID = _repository.GetNextAutoNumber("AGENTS", "01").ToString(),
                    UnitID = request.UnitId,
                    Agent = request.AgentName,
                    Address = request.Address,
                    Area = request.Area,
                    City = request.City,
                    State = request.State,
                    Phone1 = request.Phone1,
                    Phone2 = request.Phone2,
                    FaxNo = request.FaxNo,
                    Email = request.Email,
                    InsPerson = request.InsPerson,
                    FinPerson = request.FinPerson,
                    Balance = request.Balance,
                    CreditLimit = request.CreditLimit,
                    ComRate = request.ComRate,
                    Remark = request.Remark,
                    Accountno = request.AccountNo,
                    Bankname = request.BankName,
                    Tag = request.Tag,
                    Passwords = request.Password, // ⚠️ Consider hashing this password!
                    IsVerified = false,
                    Deleted = 0,
                    SubmittedBy = "api",
                    SubmittedOn = DateTime.UtcNow
                };

                // Handle uploaded documents
                var documents = new List<AttachDoc>();
                foreach (var file in request.Files)
                {
                    var fileName = file.FileName;
                    var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? ".unknown";

                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);

                    documents.Add(new AttachDoc
                    {
                        EntryDate = DateTime.UtcNow,
                        DocTypes = "AGENCY ONBOARDING",
                        DocFrom = request.AgentName,
                        DocDetails = fileName,
                        zDocuments = ms.ToArray(),
                        Deleted = 0,
                        Active = 1,
                        OptionType = "AgentDoc",
                        SubmittedBy = "api",
                        SubmittedOn = DateTime.UtcNow
                    });
                }

                string agentId = await _repository.CreateAgentWithDocumentsAsync(agent, documents);

                return Ok(new
                {
                    AgentID = agentId,
                    Message = "Agent created successfully with documents."
                });
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

        //[HttpGet("{agentCode}")]
        //public async Task<IActionResult> GetByCode(string agentCode)
        //{
        //    var agent = await _repository.GetAgentByCodeAsync(agentCode);
        //    return agent == null ? NotFound() : Ok(agent);
        //}

        //[HttpPost]
        //public async Task<IActionResult> Create(AgentDto dto)
        //{
        //    var result = await _repository.AddAgentAsync(dto);
        //    return Ok(result);
        //}
    }
}
