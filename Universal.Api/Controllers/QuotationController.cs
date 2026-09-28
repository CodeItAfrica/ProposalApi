//using GibsLifesMicroWebApi.Controllers;
//using GibsLifesMicroWebApi.Data;
//using GibsLifesMicroWebApi.Data.Repositories;
//using GibsLifesMicroWebApi.Domain;
//using Microsoft.AspNetCore.Mvc;
//using System.Threading.Tasks;

//[Route("api/quotations")]
//[ApiController]
//public class QuotationController : SecureControllerBase
//{
//    public QuotationController(Repository repo, AuthContext ctx) : base(repo, ctx) { }

//    [HttpPost]
//    public async Task<IActionResult> Create([FromBody] QuotationRequestDto request)
//    {
//        if (!ModelState.IsValid) return BadRequest(ModelState);

//        var user = GetCurrentUser();
//        var result = await _repository.CreateQuotationAsync(request, user);

//        return Ok(new { success = true, data = result });
//    }

//    [HttpPost("compute")]
//    public async Task<IActionResult> Compute([FromBody] QuotationRequestDto request)
//    {
//        var result = await _repository.ComputeQuotationAsync(request);
//        return Ok(result);
//    }

//    [HttpGet("by-id/{id:long}")]
//    public async Task<IActionResult> GetById(long id)
//    {
//        var quote = await _repository.GetByIdAsync(id);
//        return quote == null ? NotFound() : Ok(quote);
//    }

//    [HttpGet("by-number/{quoteNo}")]
//    public async Task<IActionResult> GetByNumber(string quoteNo)
//    {
//        var quote = await _repository.GetByQuoteNoAsync(quoteNo);
//        return quote == null ? NotFound() : Ok(quote);
//    }

//    [HttpGet("search")]
//    public async Task<IActionResult> Search([FromQuery] string? quoteNo, int page = 1, int pageSize = 10)
//    {
//        var result = await _repository.SearchQuotationsAsync(null, null, quoteNo, null, null, null, page, pageSize);
//        return Ok(result);
//    }

//    private string GetCurrentUser()
//    {
//        // This should be implemented based on your authentication system
//        // For now, returning a default value
//        return User?.Identity?.Name ?? "SYSTEM";
//    }
//}