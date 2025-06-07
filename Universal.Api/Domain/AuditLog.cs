using System;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Models
{
    public class AuditLog
    {
        [Key]
        public long AuditLogID { get; set; }

        public string LogType { get; set; }

        public string Source { get; set; }

        public string Category { get; set; }

        public string Description { get; set; }

        public string SubmittedBy { get; set; }

        public DateTime? SubmittedOn { get; set; }
    }
}
