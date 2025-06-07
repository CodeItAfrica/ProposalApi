using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Data;

namespace GibsLifesMicroWebApi.Controllers
{
    [Authorize(Roles = "APP,AGENT,CUST")]
    public class ClaimsController : SecureControllerBase
    {
        public ClaimsController(Repository repository, AuthContext authContext) : base(repository, authContext)
        {
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

        /// <summary>
        /// Create a Claim notification.
        /// </summary>
        /// <returns>A newly created Claim notification</returns>
        [HttpPost]
        public async Task<ActionResult<ClaimResult>> CreateClaim(CreateNewClaimRequest request)
        {
            try
            {
                var claim = await _repository.ClaimCreateAsync(request);
                var uri = new Uri($"{Request.Path}/{claim.ClaimNo}", UriKind.Relative);

                return Created(uri, new ClaimResult(claim));
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
    }
}
