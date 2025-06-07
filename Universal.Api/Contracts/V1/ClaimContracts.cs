using System;
using System.ComponentModel.DataAnnotations;

namespace GibsLifesMicroWebApi.Contracts.V1
{
    public class ClaimResult
    {
        public ClaimResult(Models.Claim claim)
        {
            PolicyNo = claim.PolicyNo;
            LossNotifyDate = claim.NotifyDate.Value;
            LossDate = claim.LossDate.Value;
            LossDescription = claim.LossDetails;
            LossType = claim.LossType;
        }

        public string PolicyNo { get; set; }
        public DateTime LossNotifyDate { get; set; }
        public DateTime LossDate { get; set; }
        public string LossType { get; set; } //TODO: make enums
        public string LossDescription { get; set; }
        //[Required]
        //public string LossLocation { get; set; }
        //[Required]
        //public string LossCause { get; set; }
    }

    public class CreateNewClaimRequest
    {
        [Required]
        public string PolicyNo { get; set; }
        [Required]
        public DateTime? LossNotifyDate { get; set; }
        [Required]
        public DateTime? LossDate { get; set; }
        [Required]
        public string LossType { get; set; } //TODO: make enums
        [Required]
        public string LossDescription { get; set; }
        //[Required]
        //public string LossLocation { get; set; }
        //[Required]
        //public string LossCause { get; set; }
    }
}
