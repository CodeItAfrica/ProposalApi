using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GibsLifesMicroWebApi.Domain;
using System.Threading.Tasks;
using System;
using GibsLifesMicroWebApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuotationController : SecureControllerBase
    {
        public QuotationController(Repository repository, AuthContext authContext)
            : base(repository, authContext) { }


        [HttpPost("calculate")]
        public async Task<IActionResult> CalculateQuote([FromBody] QuotationRequest request)
        {
            

            try
            {
                if (request == null)
                    return BadRequest("Request is null");

                var result = await _repository.CalculateAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Calculate a traditional quotation
        /// </summary>
        [HttpPost("tradcalculate")]
        public async Task<IActionResult> CalculateTraditional([FromBody] TraditionalQuotationRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request is null.");

                var result = await _repository.CalculateTraditionalAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        [HttpPut("{quoteId}")]
        public async Task<IActionResult> UpdateQuote(long quoteId, [FromBody] QuotationRequest request)
        {
            var result = await _repository.UpdateAsync(quoteId, request);
            return Ok(result);
        }


        [HttpGet("filterquotation")]
        public async Task<IActionResult> GetQuotation(
    [FromQuery] string quoteNo,
    [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null)
        {
            var result = await _repository.GetQuotationByNoAsync(quoteNo, fromDate, toDate);
            if (result == null || result.Count == 0)
                return NotFound("No quotations found matching the criteria.");

            return Ok(result);
        }


        [HttpGet("quotations")]
        public async Task<IActionResult> GetAllQuotes([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var pagedResult = await _repository.GetAllAsync(page, pageSize);
            return Ok(pagedResult);
        }


        [HttpGet("quote-no")]
        public async Task<IActionResult> GetByQuoteNo([FromQuery] string quoteNo)
        {
            var quotes = await _repository.GetByQuoteNoAsync(quoteNo);
            return Ok(quotes);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var quote = await _repository.GetByIdAsync(id);
            if (quote == null)
                return NotFound();

            return Ok(quote);
        }

        [HttpDelete("quote-no")]
        public async Task<IActionResult> DeleteByQuoteNo([FromQuery] string quoteNo)
        {
            await _repository.DeleteByQuoteNoAsync(quoteNo);
            return NoContent();
        }
        [HttpDelete("{quoteId}")]
        public async Task<IActionResult> DeleteById(long quoteId)
        {
            await _repository.DeleteByIdAsync(quoteId);
            return NoContent();
        }


        [HttpGet("ProductSettings")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _repository.GetAllProductAsync();
            return Ok(list);
        }

        [HttpGet("product/{code}")]
        public async Task<IActionResult> GetProductByCode(string code)
        {
            var item = await _repository.GetByCodeAsync(code);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("productsettings")]
        public async Task<IActionResult> AddProductSetting([FromBody] ProductSettings config)
        {
            if (config == null)
                return BadRequest("Invalid product config");

            await _repository.AddAsync(config);
            return CreatedAtAction(nameof(GetProductByCode), new { code = config.ProductID }, config);
        }

        [HttpPut("ProductSettings/{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ProductSettings model)
        {
            if (id != model.ProductID)
                return BadRequest("ID mismatch");

            await _repository.UpdateAsync(model);
            return NoContent();
        }

        [HttpDelete("ProductSettings/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _repository.DeleteAsync(id);
            return NoContent();
        }
        [HttpGet("Branches")]
        public async Task<IActionResult> GetAllBranches()
        {
            var branches = await _repository.GetAllBranchesAsync();
            return Ok(branches);
        }
    }
}
