using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Contracts.V1
{
    public class AppLoginRequest
    {
        [Required]
        public string AppID { get; set; }
        [Required]
        public string Password { get; set; }
    }

    public class AgentLoginRequest
    {
     
        [Required]
        public string Email { get; set; }
        [Required]
        public string Passwords { get; set; }
    }

    public class CustomerLoginRequest
    {
        [Required]
        public string AppID { get; set; }
        [Required]
        public string CustomerID { get; set; }
        [Required]
        public string Password { get; set; }
    }

    public class LoginResult
    {
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
    }

    public class AgentLoginResult
    {
        public string AgentID { get; set; }
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
    }
}
