using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using GibsLifesMicroWebApi.Controllers;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Contracts.V1.RiskDetails;
//using Naicom.ApiV1;
using System.Net.Mail;
using System.Text.RegularExpressions;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace GibsLifesMicroWebApi.Contracts.V1
{
#if !DEBUG
    //[Microsoft.AspNetCore.Authorization.Authorize(Roles = "APP,AGENT,CUST")]
#endif

    public class PoliciesController : SecureControllerBase
    {
        public PoliciesController(Repository repository, AuthContext authContext)
            : base(repository, authContext) { }


        [HttpGet("policies/individual")]
        public async Task<ActionResult<List<policydto>>> GetIndividualPolicies()
        {
            try
            {
                var agentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(agentId))
                    return Unauthorized("Agent authentication required.");

                var policies = await _repository.GetIndividualPolicies(agentId);

                return Ok(new { policies });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }


        // ✅ Create a new policy (this updates all related tables)
        [HttpPost("create")]
        public async Task<IActionResult> CreatePolicy([FromBody] Policiesdto policyDto)
        {
            if (policyDto == null)
            {
                return BadRequest("Policy data is required.");
            }

            try
            {
                await _repository.CreatePolicyAsync(policyDto);
                await _repository.SaveChangesAsync();
                return Ok(new { message = "Policy created successfully." });
            }
            catch (Exception ex)
           {
                Console.WriteLine($"Error Saving Changes: {ex.InnerException?.Message}");
                throw;
            }
        }
        [HttpPost("create/individual")]
        public async Task<IActionResult> CreatePolicyMaster([FromBody] PolicyModel policyDto)
        {
            if (policyDto == null)
            {
                return BadRequest("Policy data is required.");
            }

            try
            {
                await _repository.CreatePolicyMasterAsync(policyDto);
                await _repository.SaveChangesAsync();
                return Ok(new { message = "Policy created successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Saving Changes: {ex.InnerException?.Message}");
                throw;
            }
        }

        [HttpGet("group")]
        public async Task<IActionResult> GetGroupPolicies()
        {
            try
            {
                var agentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(agentId))
                    return Unauthorized("Agent authentication required.");
                var groupPolicies = await _repository.GetGroupPoliciesAsync(agentId);

                if (!groupPolicies.Any())
                    return NotFound(new { message = "No group policies found." });

                return Ok(groupPolicies);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchPolicies(
    [FromQuery] string? policy_number,
    [FromQuery] string? holder_name,
    [FromQuery] string? status)
        {
            var policies = await _repository.SearchPoliciesAsync(policy_number, holder_name, status);
            return Ok(policies);
        }


        //[HttpGet]
        //public async Task<ActionResult<IEnumerable<PolicyResult>>> ListPolicies([FromQuery] FilterPaging filter)
        //{
        //    try
        //    {
        //        var policy = await _repository.PolicySelectAsync(filter);
        //        return policy.Select(x => new PolicyResult(x)).ToList();
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        //[HttpGet("{policyNo}")]
        //public async Task<ActionResult<PolicyResult>> GetPolicy(string policyNo)
        //{
        //    try
        //    {
        //        //policyNo contains / forward slashes
        //        policyNo = System.Web.HttpUtility.UrlDecode(policyNo);
        //        var policy = await _repository.PolicySelectThisAsync(policyNo);

        //        if (policy is null)
        //            return NotFound();

        //        return Ok(new PolicyResult(policy));
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        //#region Get Policy

        //[HttpGet("Agric/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsAgric>>> GetAgricPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsAgric>(policyNo);
        //}

        //[HttpGet("Aviation/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsAviation>>> GetAviationPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsAviation>(policyNo);
        //}

        //[HttpGet("Bond/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsBond>>> GetBondPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsBond>(policyNo);
        //}

        //[HttpGet("Engineering/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsEngineering>>> GetEngineeringPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsEngineering>(policyNo);
        //}

        //[HttpGet("Fire/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsFire>>> GetFirePolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsFire>(policyNo);
        //}

        //[HttpGet("Accident/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsAccident>>> GetAccidentPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsAccident>(policyNo);
        //}

        //[HttpGet("MarineCargo/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsMarineCargo>>> GetMarineCargoPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsMarineCargo>(policyNo);
        //}

        //[HttpGet("MarineHull/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsMarineHull>>> GetMarineHullPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsMarineHull>(policyNo);
        //}

        //[HttpGet("Motor/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsMotor>>> GetMotorPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsMotor>(policyNo);
        //}

        //[HttpGet("OilGas/{policyNo}")]
        //public Task<ActionResult<PolicyResult<PolicyAsOilGas>>> GetOilGasPolicy(string policyNo)
        //{
        //    return GetPolicy<PolicyAsOilGas>(policyNo);
        //}

        //#endregion

        //[HttpPost("renew/{policyNo}")]
        //public async Task<ActionResult<PolicyResult>> RenewPolicy(string policyNo, DateTime effectiveDate)
        //{
        //    try
        //    {
        //        //policyNo contains / forward slashes
        //        policyNo = System.Web.HttpUtility.UrlDecode(policyNo);
        //        //var result = _repository.PolicyUpdate(policyNo, effectiveDate);

        //        //var policy = _repository.PolicySelectThis(policyNo);
        //        //return Ok(new policiesdto(policy));
        //        await Task.Delay(1000);

        //        return NotFound(policyNo);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        //[HttpDelete("{policyNo}")]
        //public async Task<ActionResult<string>> TerminatePolicy(string policyNo, string remarks)
        //{
        //    try
        //    {
        //        //policyNo contains / forward slashes
        //        policyNo = System.Web.HttpUtility.UrlDecode(policyNo);
        //        await Task.Delay(1000);

        //        return NotFound(policyNo);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        //#region Create Policy

        //[HttpPost("Agric")]
        //public Task<ActionResult<PolicyResult<PolicyAsAgric>>> NewAgricPolicy(CreateNew<PolicyAsAgric> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("Aviation")]
        //public Task<ActionResult<PolicyResult<PolicyAsAviation>>> NewAviationPolicy(CreateNew<PolicyAsAviation> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("Bond")]
        //public Task<ActionResult<PolicyResult<PolicyAsBond>>> NewBondPolicy(CreateNew<PolicyAsBond> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("Engineering")]
        //public Task<ActionResult<PolicyResult<PolicyAsEngineering>>> NewEngineeringPolicy(CreateNew<PolicyAsEngineering> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("Fire")]
        //public Task<ActionResult<PolicyResult<PolicyAsFire>>> NewFirePolicy(CreateNew<PolicyAsFire> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("Accident")]
        //public Task<ActionResult<PolicyResult<PolicyAsAccident>>> NewAccidentPolicy(CreateNew<PolicyAsAccident> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("MarineCargo")]
        //public Task<ActionResult<PolicyResult<PolicyAsMarineCargo>>> NewMarineCargoPolicy(CreateNew<PolicyAsMarineCargo> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("MarineHull")]
        //public Task<ActionResult<PolicyResult<PolicyAsMarineHull>>> NewMarineHullPolicy(CreateNew<PolicyAsMarineHull> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("Motor")]
        //public Task<ActionResult<PolicyResult<PolicyAsMotor>>> NewMotorPolicy(CreateNew<PolicyAsMotor> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //[HttpPost("OilGas")]
        //public Task<ActionResult<PolicyResult<PolicyAsOilGas>>> NewOilGasPolicy(CreateNew<PolicyAsOilGas> policiesdto)
        //{
        //    return NewPolicy(policiesdto);
        //}

        //#endregion

        //private async Task<ActionResult<PolicyResult<T>>> NewPolicy<T>(CreateNew<T> newpoliciesdto)
        //    where T : RiskDetail
        //{
        //    try
        //    {
        //        var policy = await _repository.PolicyCreateAsync(newpoliciesdto);
        //        await _repository.SaveChangesAsync();

        //        // where T is Motor/Marine send to NIID & NAICOM

        //        //var naicomClient = new NaicomService(_repository);
        //        //var result = await naicomClient.PublishAndSaveNaicomID(policy);

        //        ////save naicom status
        //        //_repository.SaveNaicomStatus(policy, result);
        //        //await _repository.SaveChangesAsync();

        //        var niidClient = new NiidService(_repository);
        //        await niidClient.PublishAndSaveNIID(policy);


        //        var uri = new Uri($"{Request.Path}/{policy.PolicyNo}", UriKind.Relative);
        //        return Created(uri, new PolicyResult<T>(policy));
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        //private async Task<ActionResult<PolicyResult<T>>> GetPolicy<T>(string policyNo)
        //    where T : RiskDetail
        //{
        //    try
        //    {
        //        //policyNo contains / forward slashes
        //        policyNo = System.Web.HttpUtility.UrlDecode(policyNo);
        //        var policy = await _repository.PolicySelectThisAsync(policyNo);

        //        if (policy is null)
        //            return NotFound();

        //        return Ok(new PolicyResult<T>(policy));
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}
    }

}