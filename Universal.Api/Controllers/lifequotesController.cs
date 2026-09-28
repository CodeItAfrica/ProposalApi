//using GibsLifesMicroWebApi.Data;
//using GibsLifesMicroWebApi.Data.Repositories;
//using GibsLifesMicroWebApi.Domain;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Threading.Tasks;

//namespace GibsLifesMicroWebApi.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class lifequotesController : SecureControllerBase
//    {

//        public lifequotesController(Repository repository, AuthContext authContext)
//            : base(repository, authContext) { }


//        [HttpPost("calculate")]
//        public async Task<IActionResult> CalculateQuote([FromBody] QuotationRequest request)
//        {


//            try
//            {
//                if (request == null)
//                    return BadRequest("Request is null");

//                var result = await _repository.CalculateAsync(request);

//                return Ok(result);
//            }
//            catch (Exception ex)
//            {
//                return ExceptionResult(ex);
//            }
//        }
//        [HttpPost("calculate/preview")]
//        public async Task<IActionResult> PreviewQuote([FromBody] QuotationRequest request)
//        {


//            try
//            {
//                if (request == null)
//                    return BadRequest("Request is null");

//                var result = await _repository.CalculateAsync(request);
//                // Save as QUOTATION
//                await _repository.SaveQuoteAsync(request, result);
//                return Ok(result);
//            }
//            catch (Exception ex)
//            {
//                return ExceptionResult(ex);
//            }
//        }


//        [HttpGet("get-premium-rate")]
//        public async Task<IActionResult> GetPremiumRate(
//       [FromQuery] string productCode,
//       [FromQuery] long duration,
//       [FromQuery] long age,
//       [FromQuery] string rateCode,
//       [FromQuery] string remark)
//        {
//            try
//            {
//                var rate = await _repository.GetPremiumRateAsync(productCode, duration, age, rateCode, remark);
//                return Ok(new { success = true, premiumRate = rate });
//            }

//            catch (Exception ex)
//            {
//                return StatusCode(500, new { success = false, message = "An unexpected error occurred.", error = ex.Message });
//            }
//        }

//        /// <summary>
//        /// Calculate and save traditional quotation
//        /// </summary>
//        [HttpPost("tradcalculate")]
//        public async Task<IActionResult> CalculateTraditional([FromBody] TraditionalQuotationRequest request)
//        {
//            try
//            {
//                if (request == null)
//                    return BadRequest("Request is null.");

//                var result = await _repository.CalculateTraditionalAsync(request);

//                // Save as QUOTATION
//                await _repository.SaveTradQuoteAsync(request, result);
//                return Ok(result);
//            }
//            catch (Exception ex)
//            {
//                return ExceptionResult(ex);
//            }
//        }
//        /// <summary>
//        /// Calculate preview for traditional quotation
//        /// </summary>
//        [HttpPost("tradcalculate/preview")]
//        public async Task<IActionResult> PreviewTraditional([FromBody] TraditionalQuotationRequest request)
//        {
//            try
//            {
//                if (request == null)
//                    return BadRequest("Request is null.");

//                var result = await _repository.CalculateTraditionalAsync(request);


//                return Ok(result);
//            }
//            catch (Exception ex)
//            {
//                return ExceptionResult(ex);
//            }
//        }

//        [HttpGet("filterquotation")]
//        public async Task<IActionResult> GetQuotation(
//    [FromQuery] string quoteNo,
//    [FromQuery] DateTime? fromDate = null,
//    [FromQuery] DateTime? toDate = null)
//        {
//            try
//            {
//                var result = await _repository.GetQuotationByNoAsync(quoteNo, fromDate, toDate);
//                if (result == null || result.Count == 0)
//                    return NotFound("No quotations found matching the criteria.");

//                return Ok(result);
//            }
//            catch (Exception ex)
//            {
//                return ExceptionResult(ex);
//            }
//        }

//    }
//}
