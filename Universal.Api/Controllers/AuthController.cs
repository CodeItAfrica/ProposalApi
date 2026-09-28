using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DataContext _db;
        private readonly IConfiguration _config;
        private readonly string _jwtSecret;

        public AuthController(DataContext context, IConfiguration config)
        {
            _db = context;
            _config = config;
            _jwtSecret = _config.GetValue<string>("AppSettings:JwtSecret");
        }

        [HttpPost("login")]
        public IActionResult Login(string username, string password)
        {
            var hashedPassword = HashPassword(password);

            var user = _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefault(u => u.Username == username && u.Password == hashedPassword);

            if (user != null)
            {
                return GenerateUserToken(user);
            }

            var agent = _db.Agents
                .FirstOrDefault(a => a.Email == username && a.Passwords == password && a.IsVerified == true);

            if (agent != null)
            {
                return GenerateAgentToken(agent);
            }

            var customer = _db.InsuredClients
                .FirstOrDefault(c => c.Email == username && c.Password == password);

            if (customer != null)
            {
                return GenerateCustomerToken(customer);
            }

            return Unauthorized(new { message = "Invalid username or password" });
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

        private IActionResult GenerateUserToken(User user)
        {
            var permissions = new List<string>();

            var rolePermissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission?.PermissionName)
                .Where(p => !string.IsNullOrWhiteSpace(p));

            permissions.AddRange(rolePermissions);
            permissions = permissions.Distinct().ToList();

            Console.WriteLine("USER PERMISSIONS CLAIMS:");
            foreach (var p in permissions)
            {
                Console.WriteLine(p);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim("UserType", "User"),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            foreach (var p in permissions)
            {
                claims.Add(new Claim("Permission", p));
            }

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
            if (agent.IsVerified == false)
            {
                return Unauthorized(new { message = "Agent account is not verified. Please contact administrator." });
            }

            Console.WriteLine("AGENT LOGIN:");
            Console.WriteLine($"AgentID: {agent.AgentID}, Agent Name: {agent.Agent}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, agent.AgentID ?? ""),
                new Claim(ClaimTypes.NameIdentifier, agent.INT.ToString()),
                new Claim("UserType", "Agent"),
                new Claim("AgentID", agent.AgentID ?? ""),
                new Claim("AgentName", agent.Agent ?? ""),
                new Claim(ClaimTypes.Email, agent.Email ?? ""),
                new Claim("UnitID", agent.UnitID ?? "")
            };

            var agentPermissions = new List<string>
            {
                "ViewPolicies",
                "CreateData",
                "ViewCommissions"
            };

            foreach (var p in agentPermissions)
            {
                claims.Add(new Claim("Permission", p));
            }

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

        private IActionResult GenerateCustomerToken(InsuredClients customer)
        {
            if (customer.Active == 0)
            {
                return Unauthorized(new { message = "Customer account is not active. Please contact support." });
            }

            Console.WriteLine("CUSTOMER LOGIN:");
            Console.WriteLine($"InsuredID: {customer.InsuredID}, Customer Name: {customer.FullName}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, customer.FullName ?? customer.InsuredID ?? ""),
                new Claim(ClaimTypes.NameIdentifier, customer.InsuredID ?? ""),
                new Claim("UserType", "Customer"),
                new Claim("InsuredID", customer.InsuredID ?? ""),
                new Claim("CustomerName", customer.FullName ?? ""),
                new Claim(ClaimTypes.Email, customer.Email ?? ""),
                new Claim("ClientType", customer.ClientType ?? "")
            };

            var customerPermissions = new List<string>
            {
                "ViewOwnPolicies",
                "ViewOwnProfile",
                "UpdateOwnProfile"
            };

            foreach (var p in customerPermissions)
            {
                claims.Add(new Claim("Permission", p));
            }

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
                userType = "Customer",
                customer = new
                {
                    id = customer.InsuredID,
                    insuredID = customer.InsuredID,
                    fullName = customer.FullName,
                    surname = customer.SurName,
                    otherNames = customer.OtherNames,
                    email = customer.Email,
                    mobilePhone = customer.MobilePhone,
                    clientType = customer.ClientType,
                    dateOfBirth = customer.DateofBirth,
                    isActive = customer.Active,
                    permissions = customerPermissions
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

        [HttpPost("setup-customer-password")]
        public IActionResult SetupCustomerPassword([FromBody] CustomerPasswordSetupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.PhoneNumber) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Email, InsuredID, and NewPassword are required." });
            }

            var customer = _db.InsuredClients
                .FirstOrDefault(c => c.Email == request.Email &&
                                   c.MobilePhone == request.PhoneNumber &&
                                   c.Deleted != 1);

            if (customer == null)
            {
                return NotFound(new { message = "Customer not found with the provided Email and InsuredID." });
            }

            if (!string.IsNullOrWhiteSpace(customer.Password))
            {
                return BadRequest(new { message = "Customer already has a password set." });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Password must be at least 6 characters long." });
            }

            customer.Password = request.NewPassword;
            customer.ModifiedBy = customer.InsuredID;
            customer.ModifiedOn = DateTime.Now;

            try
            {
                _db.SaveChanges();

                Console.WriteLine($"Password setup successful for customer: {customer.InsuredID}");

                return Ok(new
                {
                    message = "Password setup successful. You can now login with your email and password.",
                    insuredID = customer.InsuredID,
                    email = customer.Email
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up password: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while setting up the password." });
            }
        }

        // ==================== FORGOT PASSWORD - 6 DIGIT CODE ====================

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required." });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Email == request.Email);
            var customer = await _db.InsuredClients.FirstOrDefaultAsync(c => c.Email == request.Email && c.Deleted != 1);

            string userType = null;
            string userId = null;
            string fullName = null;

            if (user != null)
            {
                userType = "User";
                userId = user.UserID.ToString();
                fullName = $"{user.FirstName} {user.LastName}";
            }
            else if (agent != null)
            {
                userType = "Agent";
                userId = agent.AgentID;
                fullName = agent.Agent;
            }
            else if (customer != null)
            {
                userType = "Customer";
                userId = customer.InsuredID;
                fullName = customer.FullName;
            }
            else
            {
                // For security, don't reveal if email exists or not
                return Ok(new { message = "If the email exists, a password reset code has been sent." });
            }

            try
            {
                // Invalidate any existing unused tokens for this email
                var existingTokens = await _db.PasswordResets
                    .Where(pr => pr.Email == request.Email && pr.IsUsed == false)
                    .ToListAsync();

                foreach (var token in existingTokens)
                {
                    token.IsUsed = true;
                }

                // Generate 6-digit code
                var resetCode = Generate6DigitCode();
                var tokenExpiry = DateTime.Now.AddMinutes(15); // Code valid for 15 minutes

                var passwordReset = new PasswordReset
                {
                    Email = request.Email,
                    ResetToken = resetCode,
                    TokenExpiry = tokenExpiry,
                    UserType = userType,
                    UserId = userId,
                    IsUsed = false,
                    CreatedAt = DateTime.Now,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

                _db.PasswordResets.Add(passwordReset);
                await _db.SaveChangesAsync();

                // Send email with 6-digit code
                await SendPasswordResetCodeEmail(request.Email, fullName, resetCode);

                Console.WriteLine($"Password reset code sent for {userType}: {request.Email}");

                return Ok(new
                {
                    message = "If the email exists, a 6-digit password reset code has been sent.",
                    expiresInMinutes = 15
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in forgot password: {ex.Message}");
                return StatusCode(500, new { message = $"An error occurred while processing your request: {ex.Message}" });
            }
        }

        [HttpPost("verify-reset-code")]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyCodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Code and email are required." });
            }

            // Normalize the code (remove spaces, convert to uppercase)
            var normalizedCode = request.Code.Replace(" ", "").ToUpper();

            var passwordReset = await _db.PasswordResets
                .FirstOrDefaultAsync(pr => pr.Email == request.Email &&
                                          pr.ResetToken == normalizedCode &&
                                          pr.IsUsed == false &&
                                          pr.TokenExpiry > DateTime.Now);

            if (passwordReset != null)
            {
                return Ok(new
                {
                    message = "Code is valid.",
                    isValid = true,
                    userType = passwordReset.UserType,
                    expiresAt = passwordReset.TokenExpiry
                });
            }

            return BadRequest(new { message = "Invalid or expired reset code.", isValid = false });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithCodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Code) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Email, code, and new password are required." });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Password must be at least 6 characters long." });
            }

            // Normalize the code
            var normalizedCode = request.Code.Replace(" ", "").ToUpper();

            var passwordReset = await _db.PasswordResets
                .FirstOrDefaultAsync(pr => pr.Email == request.Email &&
                                          pr.ResetToken == normalizedCode &&
                                          pr.IsUsed == false &&
                                          pr.TokenExpiry > DateTime.Now);

            if (passwordReset == null)
            {
                return BadRequest(new { message = "Invalid or expired reset code." });
            }

            try
            {
                switch (passwordReset.UserType)
                {
                    case "User":
                        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                        if (user != null)
                        {
                            user.Password = HashPassword(request.NewPassword);
                            Console.WriteLine($"Password reset successful for User: {user.Email}");
                        }
                        break;

                    case "Agent":
                        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Email == request.Email);
                        if (agent != null)
                        {
                            agent.Passwords = request.NewPassword;
                            Console.WriteLine($"Password reset successful for Agent: {agent.Email}");
                        }
                        break;

                    case "Customer":
                        var customer = await _db.InsuredClients.FirstOrDefaultAsync(c => c.Email == request.Email && c.Deleted != 1);
                        if (customer != null)
                        {
                            customer.Password = request.NewPassword;
                            customer.ModifiedBy = customer.InsuredID;
                            customer.ModifiedOn = DateTime.Now;
                            Console.WriteLine($"Password reset successful for Customer: {customer.Email}");
                        }
                        break;
                }

                passwordReset.IsUsed = true;
                passwordReset.UsedAt = DateTime.Now;

                await _db.SaveChangesAsync();

                return Ok(new { message = "Password has been reset successfully. You can now login with your new password." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting password: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while resetting the password." });
            }
        }

        [HttpPost("resend-reset-code")]
        public async Task<IActionResult> ResendResetCode([FromBody] ForgotPasswordRequest request)
        {
            // Same logic as forgot-password, but invalidate old codes first
            return await ForgotPassword(request);
        }

        [HttpGet("password-reset-history")]
        [Authorize]
        public async Task<IActionResult> GetPasswordResetHistory([FromQuery] string email = null)
        {
            var query = _db.PasswordResets.AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(pr => pr.Email == email);
            }

            var history = await query
                .OrderByDescending(pr => pr.CreatedAt)
                .Take(50)
                .Select(pr => new
                {
                    pr.Id,
                    pr.Email,
                    pr.UserType,
                    pr.CreatedAt,
                    pr.TokenExpiry,
                    pr.IsUsed,
                    pr.UsedAt,
                    pr.IpAddress
                })
                .ToListAsync();

            return Ok(history);
        }

        [HttpPost("cleanup-expired-tokens")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CleanupExpiredTokens()
        {
            try
            {
                var expiredTokens = await _db.PasswordResets
                    .Where(pr => pr.TokenExpiry < DateTime.Now || pr.IsUsed == true)
                    .Where(pr => pr.CreatedAt < DateTime.Now.AddDays(-30))
                    .ToListAsync();

                _db.PasswordResets.RemoveRange(expiredTokens);
                await _db.SaveChangesAsync();

                return Ok(new { message = $"Cleaned up {expiredTokens.Count} expired tokens." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up tokens: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred during cleanup." });
            }
        }

        // ==================== HELPER METHODS ====================

        private string Generate6DigitCode()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                var randomNumber = BitConverter.ToUInt32(bytes, 0);
                var code = (randomNumber % 1000000).ToString("D6");
                return code;
            }
        }

        private async Task SendPasswordResetCodeEmail(string toEmail, string fullName, string resetCode)
        {
            try
            {
                var smtpHost = _config.GetValue<string>("Smtp:Host");
                var smtpPort = _config.GetValue<int>("Smtp:Port");
                var smtpUsername = _config.GetValue<string>("Smtp:Username");
                var smtpPassword = _config.GetValue<string>("Smtp:Password");
                var smtpSender = _config.GetValue<string>("Smtp:Sender");

                if (string.IsNullOrWhiteSpace(smtpSender))
                {
                    throw new InvalidOperationException("SMTP Sender email is not configured properly");
                }

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(smtpSender, "GIBS Online"),
                        Subject = "Password Reset Code",
                        IsBodyHtml = true,
                        Body = GetPasswordResetCodeEmailTemplate(fullName, resetCode)
                    };

                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    Console.WriteLine($"Password reset code email sent to: {toEmail}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
                throw;
            }
        }

        private string GetPasswordResetCodeEmailTemplate(string fullName, string resetCode)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #0066cc; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 30px; background-color: #f9f9f9; }}
                        .code-box {{ background-color: #fff; border: 2px dashed #0066cc; padding: 20px; margin: 20px 0; text-align: center; border-radius: 8px; }}
                        .code {{ font-size: 32px; font-weight: bold; color: #0066cc; letter-spacing: 8px; font-family: monospace; }}
                        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
                        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 10px; margin: 15px 0; }}
                        .security-notice {{ background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 10px; margin: 15px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Password Reset Code</h1>
                        </div>
                        <div class='content'>
                            <p>Hello {fullName},</p>
                            <p>We received a request to reset your password. Use the code below to reset your password:</p>
                            
                            <div class='code-box'>
                                <div class='code'>{resetCode}</div>
                                <p style='margin-top: 10px; color: #666; font-size: 14px;'>Enter this code on the password reset page</p>
                            </div>
                            
                            <div class='warning'>
                                <strong>⏰ This code will expire in 15 minutes.</strong>
                            </div>
                            
                            <div class='security-notice'>
                                <strong>🔒 Security Notice:</strong>
                                <ul style='margin: 5px 0; padding-left: 20px;'>
                                    <li>Never share this code with anyone</li>
                                    <li>GIBS staff will never ask for this code</li>
                                    <li>If you didn't request this, please ignore this email</li>
                                </ul>
                            </div>
                            
                            <p>If you didn't request a password reset, please contact support immediately.</p>
                        </div>
                        <div class='footer'>
                            <p>© 2024 GIBS Online. All rights reserved.</p>
                            <p>This is an automated message, please do not reply to this email.</p>
                        </div>
                    </div>
                </body>
                </html>
            ";
        }

        // ==================== REQUEST MODELS ====================

        public class ForgotPasswordRequest
        {
            public string Email { get; set; }
        }

        public class VerifyCodeRequest
        {
            public string Email { get; set; }
            public string Code { get; set; }
        }

        public class ResetPasswordWithCodeRequest
        {
            public string Email { get; set; }
            public string Code { get; set; }
            public string NewPassword { get; set; }
        }

        public class CustomerPasswordSetupRequest
        {
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string NewPassword { get; set; }
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