//using GibsLifesMicroWebApi.Data;
//using GibsLifesMicroWebApi.Data.Repositories;
//using GibsLifesMicroWebApi.Domain;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System.Threading.Tasks;

//namespace GibsLifesMicroWebApi.Controllers
//{
//    [Route("api/members")]
//    [ApiController]
//    public class MemberController : SecureControllerBase
//    {
//        public MemberController(Repository repo, AuthContext ctx) : base(repo, ctx) { }

//        [HttpGet("{memberId:long}")]
//        public async Task<IActionResult> Get(long memberId)
//        {
//            var member = await _repository.GetMemberByIdAsync(memberId);
//            return member == null ? NotFound() : Ok(member);
//        }

//        [HttpGet("by-quotation/{quotationNumber}")]
//        public async Task<IActionResult> GetByQuotation(string quotationNumber)
//        {
//            var members = await _repository.GetGrpMembersByPolicyAsync(quotationNumber);
//            return Ok(members);
//        }

//        [HttpPost("{quotationNumber}")]
//        public async Task<IActionResult> Add(string quotationNumber, MemberDetailsDto dto)
//        {
//            var user = GetCurrentUser();
//            var result = await _repository.AddMemberAndRecalculateAsync(quotationNumber, dto, user);
//            return Ok(result);
//        }

//        [HttpPut("{quotationNumber}/{memberId}")]
//        public async Task<IActionResult> Update(string quotationNumber, long memberId, MemberDetailsDto dto)
//        {
//            var user = GetCurrentUser();
//            var result = await _repository.UpdateMemberAndRecalculateAsync(quotationNumber, memberId, dto, user);
//            return Ok(result);
//        }

//        [HttpDelete("{quotationNumber}/{memberId}")]
//        public async Task<IActionResult> Remove(string quotationNumber, long memberId)
//        {
//            var user = GetCurrentUser();
//            var result = await _repository.RemoveMemberAndRecalculateAsync(quotationNumber, memberId, user);
//            return Ok(result);
//        }

//        [HttpDelete("{memberId}/permanent")]
//        public async Task<IActionResult> PermanentDelete(long memberId)
//        {
//            await _repository.PermanentlyDeleteMemberAsync(memberId);
//            return NoContent();
//        }

//        private string GetCurrentUser()
//        {
//            // This should be implemented based on your authentication system
//            // For now, returning a default value
//            return User?.Identity?.Name ?? "SYSTEM";
//        }
//    }
//}
