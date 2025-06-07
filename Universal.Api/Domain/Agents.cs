using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Domain
{
        public class Agents
        {
            [Key]
            public int INT  {get; set;}
            public string? AgentID  {get; set;}
            public string? UnitID  {get; set;}
            public string? Agent  {get; set;}
            public string? Address  {get; set;}
            public string? Area  {get; set;}
            public string? City  {get; set;}
            public string? State  {get; set;}
            public string? Phone1  {get; set;}
            public string? Phone2  {get; set;}
            public string? FaxNo  {get; set;}
            public string? Email  {get; set;}
            public string? InsPerson  {get; set;}
            public string? FinPerson  {get; set;}
            public decimal? Balance { get; set; }
            public decimal? CreditLimit { get; set; }
            public double? ComRate { get; set; }
            public string? Remark { get; set; }
            public string? AccountNo { get; set; }
            public string? Bankname { get; set; }
            public string? Tag { get; set; }
            public byte? Deleted { get; set; }
            public string? SubmittedBy { get; set; }
            public DateTime? SubmittedOn { get; set; }
            public string? ModifiedBy { get; set; }
            public DateTime? ModifiedOn { get; set; }
            public string? Passwords { get; set; }

        public bool? IsVerified { get; set; } = false;
        public ICollection<AgentAttachment>? Attachments { get; set; }


    }

    public class AgentAttachment
    {
        public int Id { get; set; }
        public string? AgentId { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public DateTime? UploadedAt { get; set; } = DateTime.UtcNow;
        //public Agents? Agents { get; set; }

        public Agents? Agents { get; set; }

    }

    //public class AgentAttachmentAgent
    //{
    //    public int AgentId { get; set; }
    //    public Agents Agent { get; set; }

    //    public int AgentAttachmentId { get; set; }
    //    public AgentAttachment Attachment { get; set; }
    //}

    public class UpdatePasswordRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class AgentVerificationRequest
    {
        public string AgentID { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }

    public class VerificationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }


}
