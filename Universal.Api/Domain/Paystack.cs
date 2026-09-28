using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GibsLifesMicroWebApi.Domain
{
    // Payment Request Model
    public class PolicyPaymentRequest
    {
        public string PolicyNo { get; set; }
        public decimal PaymentAmount { get; set; }
        public string Email { get; set; }
        public string CustomerName { get; set; }
    }

    // Paystack Initialize Request
    public class PaystackInitializeRequest
    {
        public string email { get; set; }
        public string amount { get; set; } // Amount in kobo (multiply by 100)
        public string reference { get; set; }
        public Dictionary<string, string> metadata { get; set; }
        public string callback_url { get; set; }
    }

    // Paystack Response Models
    public class PaystackInitializeResponse
    {
        public bool status { get; set; }
        public string message { get; set; }
        public PaystackInitializeData data { get; set; }
    }

    public class PaystackInitializeData
    {
        public string authorization_url { get; set; }
        public string access_code { get; set; }
        public string reference { get; set; }
    }

    public class PaystackVerifyResponse
    {
        public bool status { get; set; }
        public string message { get; set; }
        public PaystackVerifyData data { get; set; }
    }

    public class PaystackVerifyData
    {
        public string reference { get; set; }
        public string status { get; set; }
        public decimal amount { get; set; }
        public string paid_at { get; set; }
        public string channel { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> metadata { get; set; }

        public PaystackCustomer customer { get; set; }
    }

    public class PaystackCustomer
    {
        public string email { get; set; }
        public string customer_code { get; set; }
    }

    // DNCN Table Model
    public class DNCN
    {
        [Key]
        public int DNCNID { get; set; }
        public string DNCNNo { get; set; }
        public string RefDNCNNo { get; set; }
        public string ReceiptNo { get; set; }
        public string PolicyNo { get; set; }
        public string BrCode { get; set; }
        public DateTime? TDate { get; set; }
        public string TransType { get; set; }
        public DateTime? BillingDate { get; set; }
        public string SubRiskCode { get; set; }
        public string SubRiskName { get; set; }
        public string ChequeNo { get; set; }
        public string PartyCode { get; set; }
        public string PartyName { get; set; }
        public double? PartyRate { get; set; }
        public string InsuredCode { get; set; }
        public string InsuredName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? RenDate { get; set; }
        public string BusType { get; set; }
        public string MktStaff { get; set; }
        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }
        public decimal? Comission { get; set; }
        public decimal? crcoRetPrem { get; set; }
        public decimal? crcoORC { get; set; }
        public decimal? ReversalPrem { get; set; }
        public decimal? NetAmount { get; set; }
        public long? ProRataDays { get; set; }
        public decimal? VatAmount { get; set; }
        public decimal? Amount { get; set; }
        public decimal? AmtPaid { get; set; }
        public double? PropRate { get; set; }
        public string Naration { get; set; }
        public short? HasTreaty { get; set; }
        public string SubmitBy { get; set; }
        public DateTime? SubmitOn { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public string TransGUID { get; set; }
        public string TransSTATUS { get; set; }
        public string Remark { get; set; }
        public string Tag { get; set; }
    }

    public class PaystackWebhookPayload
    {
        [JsonPropertyName("event")]
        public string @event { get; set; }

        [JsonPropertyName("data")]
        public PaystackVerifyData data { get; set; }
    }


    public class GetAgentPaymentDto
    {
        public int DNCNID { get; set; }
        public string? DNCNNo { get; set; }
        public string? RefDNCNNo { get; set; }
        public string? ReceiptNo { get; set; }
        public string? PolicyNo { get; set; }
        public string? BrCode { get; set; }
        public DateTime? TDate { get; set; }
        public string? TransType { get; set; }
        public DateTime? BillingDate { get; set; }
        public string? SubRiskCode { get; set; }
        public string? SubRiskName { get; set; }
        public string? ChequeNo { get; set; }
        public string? PartyCode { get; set; }
        public string? PartyName { get; set; }
        public decimal? PartyRate { get; set; }
        public string? InsuredCode { get; set; }
        public string? InsuredName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? RenDate { get; set; }
        public string? BusType { get; set; }
        public string? MktStaff { get; set; }
        public decimal? SumInsured { get; set; }
        public decimal? GrossPremium { get; set; }
        public decimal? Comission { get; set; }
        public decimal? crcoRetPrem { get; set; }
        public decimal? crcoORC { get; set; }
        public decimal? ReversalPrem { get; set; }
        public decimal? NetAmount { get; set; }
        public int? ProRataDays { get; set; }
        public decimal? VatAmount { get; set; }
        public decimal? Amount { get; set; }
        public decimal? AmtPaid { get; set; }
        public decimal? PropRate { get; set; }
        public string? Naration { get; set; }
        public bool? HasTreaty { get; set; }
        public string? SubmitBy { get; set; }
        public DateTime? SubmitOn { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public string? TransGUID { get; set; }
        public string? TransSTATUS { get; set; }
        public string? Remark { get; set; }
        public string? Tag { get; set; }
    }

}