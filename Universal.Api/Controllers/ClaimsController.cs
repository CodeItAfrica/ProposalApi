using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    public class ClaimsController : SecureControllerBase
    {

 private readonly ClaimReserveService _service;
        public ClaimsController(Repository repository, AuthContext authContext, ClaimReserveService service) : base(repository, authContext)
        {
            _service = service;
        }


        /// <summary>
        /// Fetch a collection of Claim notifications.
        /// </summary>
        /// <param name="filter"></param>
        /// <returns>A collection of Claim notifications.</returns>
        [HttpGet()]
        public async Task<ActionResult<IEnumerable<ClaimResult>>> ListClaims([FromQuery] FilterPaging filter)
        {
            try
            {
                var claims = await _repository.SelectClaimsAsync(filter);
                return claims.Select((x => new ClaimResult(x))).ToList();
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Fetch a single Claim notification.
        /// </summary>
        /// <param name="claimNo"></param>
        /// <returns>The Claim notification for the claimNo entered.</returns>
        [HttpGet("{claimNo}")]
        public async Task<ActionResult<ClaimResult>> GetClaim(string claimNo)
        {
            try
            {
                var claim = await _repository.ClaimSelectThisAsync(claimNo);

                if (claim is null)
                    return NotFound();

                return Ok(new ClaimResult(claim));
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        ///// <summary>
        ///// Create a Claim notification.
        ///// </summary>
        ///// <returns>A newly created Claim notification</returns>
        //[HttpPost]
        //public async Task<ActionResult<ClaimResult>> CreateClaim(CreateNewClaimRequest request)
        //{
        //    try
        //    {
        //        var claim = await _repository.ClaimCreateAsync(request);
        //        var uri = new Uri($"{Request.Path}/{claim.ClaimNo}", UriKind.Relative);

        //        return Created(uri, new ClaimResult(claim));
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}


        [HttpGet("GetAllClaimReserveSearch")]
        public async Task<IActionResult> Search([FromQuery] ClmReserveSearchRequest request)
        {
            try
            {
                var result = await _service.SelectWhereINDAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }





        /// <summary>
        /// Get all Claim Reserves
        /// </summary>
        [HttpGet("GetallClaim")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        /// <summary>
        /// Get Claim Reserve by Id
        /// </summary>
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);

            if (result == null)
                return NotFound();

            return Ok(result);
        }

   

        /// <summary>
        /// Create Claim Reserve
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] clmReserves model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var id = await _service.CreateAsync(model);

            return CreatedAtAction(nameof(GetById), new { id }, model);
        }

        /// <summary>
        /// Update Claim Reserve
        /// </summary>
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] clmReserves model)
        {
            if (id != model.ClaimID)
                return BadRequest("ClaimID mismatch.");

            var updated = await _service.UpdateAsync(model);

            if (!updated)
                return NotFound();

            return NoContent();
        }

        /// <summary>
        /// Delete Claim Reserve
        /// </summary>
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var deleted = await _service.DeleteAsync(id);

            if (!deleted)
                return NotFound();

            return NoContent();
        }



        [HttpPost("DoInsert")]
        public async Task<IActionResult> Save(clmReserves model)
        {
            var id = await _service.SaveAsync(model);
            return Ok(id);
        }

    }
}
