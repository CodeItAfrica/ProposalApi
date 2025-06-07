using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;

namespace GibsLifesMicroWebApi.Controllers
{
    [Authorize(Roles = "APP")]
    public class IndividualLifeController : SecureControllerBase
    {
        private readonly Settings _settings;
        public IndividualLifeController(Repository repository, AuthContext authContext, Settings settings) : base(repository, authContext)
        {
            _settings = settings;
        }


        /// <summary>
        /// Fetch a single Policy.
        /// </summary>
        /// <param name="policyNo">No of the Policy to get.</param>
        /// <returns>The Policy with the No entered.</returns>
        [HttpGet("{policyNo}")]
        public async Task<ActionResult> GetPolicyMaster(string policyNo)
        {
            try
            {
                var policymaster = await _repository.PolicyMasterSelectThisAsync(policyNo);

                if (policyNo is null)
                    return NotFound();

                return Ok(policymaster);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpPost]
        public async Task<ActionResult> CreatePolicyMaster(CreateNewPolicyMasterRequest request)
        {
            try
            {
                var policymaster = await _repository.PolicyMasterCreateAsync(request);
                await _repository.SaveChangesAsync();

                var uri = new Uri($"{Request.Path}/{policymaster.PolicyID}", UriKind.Relative);
                return Ok(policymaster);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }



    }
}
