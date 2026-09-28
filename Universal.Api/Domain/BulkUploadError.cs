using System;
using System.ComponentModel.DataAnnotations;
namespace GibsLifesMicroWebApi.Domain
{
    public class BulkUploadError
    {
        public long ID { get; set; }
        public string? UploadBatchID { get; set; }
        public string? PolicyNo { get; set; }
        public string? MemberNo { get; set; }
        public string? MemberName { get; set; }
        public string? ErrorReason { get; set; }
        public string? RawData { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime? UploadedOn { get; set; }
        public string? IsResolved { get; set; }
        public DateTime? ResolvedOn { get; set; }
        public string? ResolvedBy { get; set; }
    }
}
