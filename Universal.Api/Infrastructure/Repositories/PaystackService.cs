using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Infrastructure.Repositories
{
    public class PaystackService
    {
        private readonly HttpClient _httpClient;
        private readonly string _paystackSecretKey;
        private readonly string _callbackUrl;
        private readonly IConfiguration _configuration;
        private readonly DataContext _context;

        public PaystackService(
            HttpClient httpClient,
            IConfiguration configuration,
            DataContext context)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _context = context;
            _paystackSecretKey = configuration["Paystack:SecretKey"];
            _callbackUrl = configuration["Paystack:CallbackUrl"];

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _paystackSecretKey);
        }

        public async Task<PaystackInitializeResponse> InitializePayment(PolicyPaymentRequest request)
        {
            // Get policy details
            var policy = await _context.PolicyMaster
                .FirstOrDefaultAsync(p => p.PolicyNo == request.PolicyNo);

            if (policy == null)
                throw new Exception("Policy not found");

            // Validate payment amount based on FOP and BasicPremium
            //if (!ValidatePaymentAmount(policy, request.PaymentAmount))
            //    throw new Exception($"Payment amount must be at least the basic premium amount based on {policy.FOP} frequency");

            // Generate unique reference - sanitize policy number to remove invalid characters
            string sanitizedPolicyNo = SanitizeReference(policy.PolicyNo);
            string internalReference = $"POL-{sanitizedPolicyNo}-{DateTime.Now.Ticks}";

            // Prepare request (amount in kobo - multiply by 100)
            var paystackRequest = new PaystackInitializeRequest
            {
                email = request.Email ?? policy.Email,
                amount = ((int)(request.PaymentAmount * 100)).ToString(),
                reference = internalReference,
                callback_url = _callbackUrl,
                metadata = new Dictionary<string, string>
        {
            { "policy_no", policy.PolicyNo },
            { "policy_id", policy.PolicyID.ToString() },
            { "customer_name", request.CustomerName ?? policy.FullName },
            { "fop", policy.FOP },
            { "basic_premium", policy.BasicPremium.ToString() }
        }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(paystackRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.paystack.co/transaction/initialize",
                content);

            var responseString = await response.Content.ReadAsStringAsync();
            var paystackResponse = System.Text.Json.JsonSerializer.Deserialize<PaystackInitializeResponse>(responseString);

            if (paystackResponse.status)
            {
                // Store the Paystack reference returned in the response
                string paystackReference = paystackResponse.data.reference;

                // Update policy with transaction details
                policy.TransGUID = paystackReference; // Store Paystack's reference
                policy.TransSTATUS = "PENDING";
                policy.PaymentAmount = request.PaymentAmount;
                await _context.SaveChangesAsync();
            }

            return paystackResponse;
        }

        // Verify Payment
        public async Task<PaystackVerifyResponse> VerifyPayment(string reference)
        {
            var response = await _httpClient.GetAsync(
                $"https://api.paystack.co/transaction/verify/{reference}");

            var responseString = await response.Content.ReadAsStringAsync();
            var verifyResponse = System.Text.Json.JsonSerializer.Deserialize<PaystackVerifyResponse>(responseString);

            if (verifyResponse.status && verifyResponse.data.status == "success")
            {
                await ProcessSuccessfulPayment(verifyResponse.data);
            }

            return verifyResponse;
        }

        // ProcessSuccessfulPayment - no changes needed, already uses paymentData.reference
        public async Task ProcessSuccessfulPayment(PaystackVerifyData paymentData)
        {
            // Get policy by reference - this now matches Paystack's reference
            var policy = await _context.PolicyMaster
                .FirstOrDefaultAsync(p => p.TransGUID == paymentData.reference);

            if (policy == null)
                throw new Exception("Policy not found for this transaction");

            // Convert amount from kobo to naira
            decimal amountPaid = paymentData.amount / 100;

            // Generate receipt number
            string receiptNo = $"RCP-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Ticks}";
            string dncnNo = $"DNCN-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Ticks}";

            // Create DNCN record
            var dncn = new DNCN
            {
                DNCNNo = dncnNo,
                RefDNCNNo = paymentData.reference,
                ReceiptNo = receiptNo,
                PolicyNo = policy.PolicyNo,
                BrCode = policy.BrCode,
                TDate = DateTime.Now,
                TransType = "PREMIUM PAYMENT",
                BillingDate = DateTime.Now,
                PartyCode = policy.Agentcode,
                PartyName =policy.AgentDescription,
                InsuredCode = policy.AssuredCode,
                InsuredName = policy.FullName,
                SubRiskCode= policy.CoverCode,
                SubRiskName= policy.Covertype,
                BusType = "RENEWAL",
                StartDate = policy.StartDate,
                EndDate = policy.MaturityDate,
                MktStaff = " ",
                SumInsured = policy.SumAssured,
                GrossPremium = policy.BasicPremium,
                Amount = amountPaid,
                AmtPaid = amountPaid,
                NetAmount = amountPaid,
                Naration = $"Premium payment via Paystack - {paymentData.channel}",
                SubmitBy = "PAYSTACK_API",
                SubmitOn = DateTime.Now,
                TransGUID = paymentData.reference,
                TransSTATUS = "COMPLETED",
                Remark = $"Paid via {paymentData.channel} on {paymentData.paid_at}",
                Tag = policy.FOP
            };

            _context.undDNCN.Add(dncn);

            // Update policy
            policy.TransSTATUS = "COMPLETED";
            policy.PaymentAmount = amountPaid;
            policy.Dateofpayment = DateTime.Now;
            policy.ReceiptNo = receiptNo;
            policy.PaymentType = "PAYSTACK";

            await _context.SaveChangesAsync();
        }



    


        // Validate payment amount based on FOP
        private bool ValidatePaymentAmount(PolicyMaster policy, decimal paymentAmount)
        {
            if (policy.BasicPremium == null || policy.BasicPremium <= 0)
                return false;

            // Allow any amount >= BasicPremium for flexible payments
            // Adjust this logic based on your business rules
            decimal minimumAmount = policy.BasicPremium.Value;

            // Optional: Apply FOP-based validation
            // if (policy.FOP == "MONTHLY") minimumAmount = policy.BasicPremium.Value;
            // if (policy.FOP == "QUARTERLY") minimumAmount = policy.BasicPremium.Value * 3;
            // if (policy.FOP == "ANNUALLY") minimumAmount = policy.BasicPremium.Value * 12;

            return paymentAmount >= minimumAmount;
        }

        // Add to PaystackService
        public async Task ProcessFailedPayment(PaystackVerifyData paymentData)
        {
            var policy = await _context.PolicyMaster
                .FirstOrDefaultAsync(p => p.TransGUID == paymentData.reference);

            if (policy == null)
                throw new Exception("Policy not found for this transaction");

            policy.TransSTATUS = "FAILED";
            policy.Remarks = $"Payment failed via {paymentData.channel}";

            await _context.SaveChangesAsync();
        }
        // Sanitize reference to contain only valid characters
        private string SanitizeReference(string input)
        {
            // Paystack accepts: alphanumeric, hyphens, and underscores
            // Replace forward slashes and other invalid characters with hyphens
            return Regex.Replace(input, @"[^a-zA-Z0-9\-_]", "-");
        }

      
    }
}