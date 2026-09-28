using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    // Controller
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly PaystackService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;

        private readonly Repository _repository;

      
        public PaymentController(PaystackService paymentService, IConfiguration configuration, Repository repository)
        {
            _paymentService = paymentService;
            _configuration = configuration;
            _baseUrl = configuration["AppSettings:FrontendBaseUrl"] ?? "https://fortislife.newgibsonline.com";
            _repository = repository;
        }

        [HttpPost("initialize")]
        public async Task<IActionResult> InitializePayment([FromBody] PolicyPaymentRequest request)
        {
            try
            {
                var response = await _paymentService.InitializePayment(request);

                if (response.status)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Payment initialized successfully",
                        authorization_url = response.data.authorization_url,
                        reference = response.data.reference
                    });
                }

                return BadRequest(new { success = false, message = response.message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("verify/{reference}")]
        public async Task<IActionResult> VerifyPayment(string reference)
        {
            try
            {
                var response = await _paymentService.VerifyPayment(reference);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("callback")]
        public async Task<IActionResult> PaymentCallback([FromQuery] string reference)
        {
            try
            {
                if (string.IsNullOrEmpty(reference))
                    return Redirect($"{_baseUrl}/payment-failed?message=No reference provided");

                var response = await _paymentService.VerifyPayment(reference);

                if (response.status && response.data.status == "success")
                {
                    // Extract policy number from metadata for display
                    var policyNo = response.data.metadata != null ?
                        response.data.metadata.GetValueOrDefault("policy_no", "") : "";

                    return Redirect($"{_baseUrl}/payment-success");
                    //return Redirect($"{_baseUrl}/payment-success");
                }

                return Redirect($"{_baseUrl}/payment-failed");
            }
            catch (Exception ex)
            {
                return Redirect($"{_baseUrl}/payment-failed?message={Uri.EscapeDataString(ex.Message)}");
            }
        }

        [HttpGet("by-partycode")]
        public async Task<IActionResult> GetAgentPayment([FromQuery] string partyCode)
        {
            var payments = await _repository.GetAgentPayment(partyCode);

            if (payments == null || !payments.Any())
            {
                return NotFound(new
                {
                    success = false,
                    message = $"No payment record found for PartyCode {partyCode}"
                });
            }

            return Ok(new
            {
                success = true,
                count = payments.Count,
                data = payments
            });
        }

        // GET: api/InsuredPayment/by-insuredcode?insuredCode=INS001
        [HttpGet("by-insuredcode")]
        public async Task<IActionResult> GetByInsuredCode([FromQuery] string insuredCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(insuredCode))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "InsuredCode is required."
                    });
                }

                var payments = await _repository.GetByInsuredCode(insuredCode.Trim());

                if (payments == null || payments.Count == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"No record found for InsuredCode {insuredCode}"
                    });
                }

                return Ok(new
                {
                    success = true,
                    count = payments.Count,
                    data = payments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing your request.",
                    error = ex.Message
                });
            }

        }



        //[HttpPost("webhook")]
        //public async Task<IActionResult> HandlePaystackWebhook()
        //{
        //    try
        //    {
        //        // Read the request body
        //        using (var reader = new StreamReader(Request.Body))
        //        {
        //            var json = await reader.ReadToEndAsync();

        //            // Verify webhook signature
        //            var sig = Request.Headers["x-paystack-signature"];
        //            if (!VerifyWebhookSignature(json, sig))
        //                return Unauthorized();

        //            // Deserialize the webhook payload
        //            var payload = System.Text.Json.JsonSerializer.Deserialize<PaystackWebhookPayload>(json);

        //            // Process only successful charge events
        //            if (payload.@event == "charge.success" && payload.data.status == "success")
        //            {
        //                await _paymentService.ProcessSuccessfulPayment(payload.data);
        //                return Ok(new { success = true });
        //            }

        //            // Handle other events as needed
        //            if (payload.@event == "charge.failed")
        //            {
        //                await _paymentService.ProcessFailedPayment(payload.data);
        //                return Ok(new { success = true });
        //            }

        //            return Ok(new { success = true });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log the error but return 200 to avoid Paystack retries
        //        Console.WriteLine($"Webhook error: {ex.Message}");
        //        return Ok(new { success = false, message = ex.Message });
        //    }
        //}

        // Verify webhook signature
        private bool VerifyWebhookSignature(string json, string signature)
        {
            var secret = "sk_test_73cda57e40acf17f9de9c589967d42d00cfa8dfa";

            using (var hmac = new System.Security.Cryptography.HMACSHA512(
                Encoding.UTF8.GetBytes(secret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
                var hashString = BitConverter.ToString(hash)
                    .Replace("-", "")
                    .ToLower();

                return hashString == signature;
            }
        }

    }
    }
