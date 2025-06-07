using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;

namespace GibsLifesMicroWebApi.Controllers
{
#if !DEBUG
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "APP,AGENT,CUST")]
#endif
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : SecureControllerBase
    {

        public DashboardController(Repository repository, AuthContext authContext)
            : base(repository, authContext) { }

        [HttpGet("total_insured")]
        public async Task<IActionResult> GetTotalInsuredCount()
        {
            try
            {
                int totalInsured = await _repository.GetTotalInsuredCountAsync();
                return Ok(new { total_insured = totalInsured });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        [HttpGet("total_payments")]
        public async Task<IActionResult> GetTotalPaymentsCount()
        {
            try
            {
                int totalPayments = await _repository.GetTotalPaymentsCountAsync();
                return Ok(new { total_payments = totalPayments });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        [HttpGet("total_claims")]
        public async Task<IActionResult> GetTotalClaimsCount()
        {
            try
            {
                int totalClaims = await _repository.GetTotalClaimsCountAsync();
                return Ok(new { total_claims = totalClaims });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        [HttpGet("individual/active_policies")]
        public async Task<IActionResult> GetIndividualTotalActivePolicies()
        {
            try
            {
                int totalActivePolicies = await _repository.GetIndividualTotalActivePoliciesAsync();
                return Ok(new { total_active_policies = totalActivePolicies });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
        
        [HttpGet("group/active_policies")]
        public async Task<IActionResult> GetGroupTotalActivePolicies()
        {
            try
            {
                int totalActivePolicies = await _repository.GetGroupTotalActivePoliciesAsync();
                return Ok(new { total_active_policies = totalActivePolicies });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
    }
}
