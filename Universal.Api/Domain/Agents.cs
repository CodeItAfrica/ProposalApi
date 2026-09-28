using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Domain
{

    public class CreateAgentRequest
    {
        public AgentDto Agent { get; set; }
        public List<AttachDocDto> Documents { get; set; }
    }
    public class CreateAgentFormRequest
    {
        // Agent fields
        public string? UnitId { get; set; }
        public string? AgentName { get; set; }
        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Phone1 { get; set; }
        public string? Phone2 { get; set; }
        public string? FaxNo { get; set; }
        public string? Email { get; set; }
        public string? InsPerson { get; set; }
        public string? FinPerson { get; set; }
        public decimal? Balance { get; set; }
        public decimal? CreditLimit { get; set; }
        public double? ComRate { get; set; }
        public string? Remark { get; set; }
        public string? AccountNo { get; set; }
        public string? BankName { get; set; }
        public string? Tag { get; set; }
        public string? Password { get; set; }

        //// Document metadata (same length as Files)
        //public List<string>? DocTypes { get; set; }
        //public List<string>? DocFroms { get; set; }
        //public List<string>? DocDetails { get; set; }

        // Uploaded files
        public List<IFormFile>? Files { get; set; }
    }


    public class AgentDto
    {
        public string UnitId { get; set; }
        public string AgentName { get; set; }
        public string Address { get; set; }
        public string Area { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Phone1 { get; set; }
        public string Phone2 { get; set; }
        public string FaxNo { get; set; }
        public string Email { get; set; }
        public string InsPerson { get; set; }
        public string FinPerson { get; set; }
        public decimal Balance { get; set; }
        public decimal CreditLimit { get; set; }
        public double ComRate { get; set; }
        public string Remark { get; set; }
        public string AccountNo { get; set; }
        public string BankName { get; set; }
        public string Tag { get; set; }
        public string Password { get; set; }
    }
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
            public string? Accountno { get; set; }
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

    public class Unit
    {
        public string? UnitID { get; set; }
        public string? CentreID { get; set; }
        public string? UnitName { get; set; }
        public string? Description { get; set; }
        public string? unitHead { get; set; }
        public double? Comission { get; set; }
        public double? A1 { get; set; }
        public double? A2 { get; set; }
        public string? Remark { get; set; }
        public string? Tag { get; set; }
        public byte? Deleted { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
    public class AttachDoc
    {
        [Key]
        public long AttachID { get; set; }
        public string DocNo { get; set; }
        public DateTime EntryDate { get; set; }
        public string DocTypes { get; set; }
        public string DocFrom { get; set; }
        public string DocDetails { get; set; }
        public byte[] zDocuments { get; set; }
        public byte Deleted { get; set; }
        public byte Active { get; set; }
        public string OptionType { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedOn { get; set; }
    }
    namespace YourProject.DTOs
    {
        public class CreateAttachDocDto
        {
            public string? DocNo { get; set; }
            public string? DocTypes { get; set; }
            public string? DocFrom { get; set; }
            public IFormFile? DocumentFile { get; set; }
            public string? OptionType { get; set; }
            public string? SubmittedBy { get; set; }
        }

        public class UpdateAttachDocDto
        {
            public string? DocNo { get; set; }
            public DateTime EntryDate { get; set; }
            public string? DocTypes { get; set; }
            public string? DocFrom { get; set; }
            public string? DocDetails { get; set; }
            public IFormFile? DocumentFile { get; set; }
            public byte Active { get; set; }
            public string? OptionType { get; set; }
        }

        public class AttachDocResponseDto
        {
            public long AttachID { get; set; }
            public string? DocNo { get; set; }
            public DateTime EntryDate { get; set; }
            public string? DocTypes { get; set; }
            public string? DocFrom { get; set; }
            public string? DocDetails { get; set; }
            public int DocumentSize { get; set; }
            public byte Active { get; set; }
            public string? OptionType { get; set; }
            public string? SubmittedBy { get; set; }
            public DateTime SubmittedOn { get; set; }
        }
    }
    public class AttachDocDto
    {
        public string DocType { get; set; }
        public string DocFrom { get; set; }
        public string DocDetails { get; set; }
        public string DocumentBase64 { get; set; }
    }
    public class UpdateAgentRequest
    {
        public string? AgentID { get; set; }
        public string? UnitID { get; set; }
        public string? Agent { get; set; }
        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Phone1 { get; set; }
        public string? Phone2 { get; set; }
        public string? FaxNo { get; set; }
        public string? Email { get; set; }
        public string? InsPerson { get; set; }
        public string? FinPerson { get; set; }
        public decimal? Balance { get; set; }
        public decimal? CreditLimit { get; set; }
        public double? ComRate { get; set; }
        public string? Remark { get; set; }
        public string? AccountNo { get; set; }
        public string? Bankname { get; set; }
        public string? Tag { get; set; }
        public string? Passwords { get; set; }
        public bool? IsVerified { get; set; }
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
