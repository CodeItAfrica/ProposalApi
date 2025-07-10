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

        public async Task UpdateAgentAsync(Agents agent, UpdateAgentRequest dto)
        {
            agent.UnitID = dto.UnitID ?? agent.UnitID;
            agent.Agent = dto.Agent ?? agent.Agent;
            agent.Address = dto.Address ?? agent.Address;
            agent.Area = dto.Area ?? agent.Area;
            agent.City = dto.City ?? agent.City;
            agent.State = dto.State ?? agent.State;
            agent.Phone1 = dto.Phone1 ?? agent.Phone1;
            agent.Phone2 = dto.Phone2 ?? agent.Phone2;
            agent.FaxNo = dto.FaxNo ?? agent.FaxNo;
            agent.Email = dto.Email ?? agent.Email;
            agent.InsPerson = dto.InsPerson ?? agent.InsPerson;
            agent.FinPerson = dto.FinPerson ?? agent.FinPerson;
            agent.Balance = dto.Balance ?? agent.Balance;
            agent.CreditLimit = dto.CreditLimit ?? agent.CreditLimit;
            agent.ComRate = dto.ComRate ?? agent.ComRate;
            agent.Remark = dto.Remark ?? agent.Remark;
            agent.Accountno = dto.AccountNo ?? agent.Accountno;
            agent.Bankname = dto.Bankname ?? agent.Bankname;
            agent.Tag = dto.Tag ?? agent.Tag;
            agent.Passwords = dto.Passwords ?? agent.Passwords;
            agent.IsVerified = dto.IsVerified ?? agent.IsVerified;

            agent.ModifiedOn = DateTime.Now;
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
        public async Task<List<Unit>> GetUnitsAsync()
        {
            return await _db.Units
               
                .ToListAsync();
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
                    From = new MailAddress(smtpUser, "Intteck Global Systems"),  // Replace with your name
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


        //public async Task<Agents> AgentCreateAsync(CreateNewAgentRequest dto)
        //{
        //    //string appId = _authContext.User.AppId;

        //    //check for duplicate
        //    var existing = await _db.Agents.Where(x =>/* x.ApiId.Contains(dto.Email) ||*/
        //                                                x.Email == dto.Email ||
        //                                                x.Phone1 == dto.Phone1)
        //                                    //.Where(x => x.SubmittedBy == $"{SUBMITTED_BY}/{appId}")
        //                                    .FirstOrDefaultAsync();
        //    if (existing != null)
        //        throw new ArgumentException($"Duplicate agent found. ID={existing.AgentID} {existing.Email}, {existing.Phone1}");

        //    var agent = new Agents()
        //    {
        //        AgentID = GetNextAutoNumber("AGENTS","01"),

        //         UnitID= dto.      UnitID,
        //         Agent = dto.       Agent,      
        //         Address= dto.     Address,    
        //         Area= dto.        Area,       
        //         City= dto.        City,       
        //         State= dto.       State,      
        //         Phone1= dto.      Phone1,     
        //         Phone2= dto.      Phone2,     
        //         FaxNo= dto.       FaxNo,      
        //         Email= dto.       Email,      
        //         InsPerson= dto.   InsPerson,  
        //         FinPerson= dto.   FinPerson,  
        //         Balance= dto.     Balance,    
        //         CreditLimit= dto. CreditLimit,
        //         ComRate= dto.     ComRate,    
        //         Remark= dto.      Remark,     
        //         AccountNo= dto.   AccountNo,  
        //         Bankname= dto.    Bankname,   
        //         Tag= dto.         Tag,        
        //         Deleted= 0,    
        //         Passwords= dto.Password,
        //         SubmittedBy= dto.Agent,
        //        SubmittedOn = DateTime.Now,
        //         ModifiedBy= dto.Agent,
        //        ModifiedOn = DateTime.Now,

        //    };

        //    _db.Agents.Add(agent);

        //    if (dto.Attachments != null && dto.Attachments.Any())
        //    {
        //        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        //        Directory.CreateDirectory(uploadsFolder);

        //        foreach (var file in dto.Attachments)
        //        {
        //            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        //            var filePath = Path.Combine(uploadsFolder, fileName);

        //            using (var stream = new FileStream(filePath, FileMode.Create))
        //            {
        //                await file.CopyToAsync(stream);
        //            }

        //            var attachment = new AgentAttachment
        //            {
        //                AgentId = agent.AgentID,
        //                FileName = file.FileName,
        //                FilePath = $"/uploads/{fileName}",
        //                UploadedAt = DateTime.UtcNow
        //            };

        //            _db.AgentAttachments.Add(attachment);
        //        }
        //    }
        //        return agent;
        //}

        public async Task<string> CreateAgentWithDocumentsAsync(Agents agent, IEnumerable<AttachDoc> documents)
        {
            var strategy = _db.Database.CreateExecutionStrategy();

            string agentId = null;

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                _db.Agents.Add(agent);
                await _db.SaveChangesAsync();

                agentId = agent.AgentID;

                foreach (var doc in documents)
                {
                    doc.DocNo = agent.AgentID;
                }

                _db.AttachDocs.AddRange(documents);
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
            });

            return agentId;
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
