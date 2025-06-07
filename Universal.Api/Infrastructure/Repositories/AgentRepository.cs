using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Domain;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using System.Net;
using System.IO;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {
        //public Task<Party?> PartyLoginAsync(string appId, string agentId, string password)
        //{
        //    return _db.Parties.FirstOrDefaultAsync(x => 
        //                          (x.PartyID == agentId || x.Email == agentId)
        //                        && x.ApiPassword == password
        //                        && x.SubmittedBy == $"{SUBMITTED_BY}/{appId}");
        //}

        public Task<Agents> AgentSelectThisAsync([FromRoute] string agentId)
        {
            string appId = _authContext.User.AppId;

            if (string.IsNullOrWhiteSpace(agentId))
                throw new ArgumentNullException(nameof(agentId), "Agent ID cannot be empty");

            return _db.Agents.Where(x => x.AgentID == agentId || 
                                          x.Email   == agentId)
                              //.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{appId}")
                              .FirstOrDefaultAsync();
        }


        public async Task<VerificationResult> VerifyAgentAsync(AgentVerificationRequest request)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.AgentID == request.AgentID);

            if (agent == null)
                return new VerificationResult { Success = false, Message = "Agent not found." };

            agent.IsVerified = request.IsVerified;
            agent.ModifiedBy = $"{SUBMITTED_BY}/{_authContext.User.AppId}";
            agent.ModifiedOn = DateTime.Now;

            await _db.SaveChangesAsync();

            // Optionally send approval email
            if (request.IsVerified)
            {
                await SendEmail(agent.Email, "Account Approved", $@"
            Dear {agent.Agent},

            Your agency account on GIBS has been successfully verified and approved.

            You can now log in to the platform using your Agent email and passowrd.

            Welcome aboard!
            – GIBS Support Team
        ");
            }

            return new VerificationResult { Success = true, Message = "Agent verification updated successfully." };
        }

        public async Task SendEmail(string toEmail, string subject, string body)
        {
            var smtpHost = _config["Smtp:Host"];
            var smtpPort = int.Parse(_config["Smtp:Port"]);
            var smtpUser = _config["Smtp:Username"];
            var smtpPass = _config["Smtp:Password"];
            var sender = _config["Smtp:Sender"];

            using (var client = new SmtpClient(smtpHost, smtpPort))
            {
                client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUser, "Goxi Insurance"),  // Replace with your name
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                try
                {
                    // Console.WriteLine("I ran 22")
                    await client.SendMailAsync(mailMessage);
                }
                catch (SmtpException ex)
                {
                    // Print more detailed exception message for debugging
                    Console.WriteLine($"Error sending email: {ex.ToString()}");
                }
            }
        }

        public Task<List<Party>> PartySelectAsync(FilterPaging filter)
        {
            if (filter == null)
                filter = new FilterPaging();

            var query = _db.Parties.Where(x => x.Active == 1);

            foreach (string item in filter.SearchTextItems)
                query = query.Where(x => x.PartyName.Contains(item)).AsQueryable();

            return query.OrderBy(x => x.PartyName)
                        //.Skip(filter.SkipCount)
                        .Take(filter.PageSize)
                        .ToListAsync();
        }

        public async Task<Agents> GetAgentByEmailAsync(string email)
        {
            return await _db.Agents.FirstOrDefaultAsync(a => a.Email == email);
        }

        public async Task UpdateAgentAsync(Agents agent)
        {
            _db.Agents.Update(agent);
            await _db.SaveChangesAsync();
        }


        public async Task<Agents> AgentCreateAsync(CreateNewAgentRequest dto)
        {
            //string appId = _authContext.User.AppId;

            //check for duplicate
            var existing = await _db.Agents.Where(x =>/* x.ApiId.Contains(dto.Email) ||*/
                                                        x.Email == dto.Email ||
                                                        x.Phone1 == dto.Phone1)
                                            //.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{appId}")
                                            .FirstOrDefaultAsync();
            if (existing != null)
                throw new ArgumentException($"Duplicate agent found. ID={existing.AgentID} {existing.Email}, {existing.Phone1}");

            var agent = new Agents()
            {
                AgentID = GetNextAutoNumber("AGENTS","01"),

                 UnitID= dto.      UnitID,
                 Agent = dto.       Agent,      
                 Address= dto.     Address,    
                 Area= dto.        Area,       
                 City= dto.        City,       
                 State= dto.       State,      
                 Phone1= dto.      Phone1,     
                 Phone2= dto.      Phone2,     
                 FaxNo= dto.       FaxNo,      
                 Email= dto.       Email,      
                 InsPerson= dto.   InsPerson,  
                 FinPerson= dto.   FinPerson,  
                 Balance= dto.     Balance,    
                 CreditLimit= dto. CreditLimit,
                 ComRate= dto.     ComRate,    
                 Remark= dto.      Remark,     
                 AccountNo= dto.   AccountNo,  
                 Bankname= dto.    Bankname,   
                 Tag= dto.         Tag,        
                 Deleted= 0,    
                 Passwords= dto.Password,
                 SubmittedBy= dto.Agent,
                SubmittedOn = DateTime.Now,
                 ModifiedBy= dto.Agent,
                ModifiedOn = DateTime.Now,

            };

            _db.Agents.Add(agent);

            if (dto.Attachments != null && dto.Attachments.Any())
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                Directory.CreateDirectory(uploadsFolder);

                foreach (var file in dto.Attachments)
                {
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var attachment = new AgentAttachment
                    {
                        AgentId = agent.AgentID,
                        FileName = file.FileName,
                        FilePath = $"/uploads/{fileName}",
                        UploadedAt = DateTime.UtcNow
                    };

                    _db.AgentAttachments.Add(attachment);
                }
            }
                return agent;
        }

        public async Task<Agents?> AgentDeleteAsync(string agentId)
        {
            string appId = _authContext.User.AppId;

            if (string.IsNullOrWhiteSpace(agentId))
                throw new ArgumentNullException(nameof(agentId), "Agent ID cannot be empty");

            var agent = await _db.Agents.Where(x => x.AgentID == agentId ||
                                                     x.Email   == agentId)
                              //.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{appId}")
                              .FirstOrDefaultAsync();
            if (agent == null)
                return null;

            _db.Agents.Remove(agent);
            _db.SaveChanges();
            return agent;
        }

    }
}
