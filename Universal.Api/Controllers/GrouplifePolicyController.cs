
using GibsLifesMicroWebApi.Infrastructure.Common;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{

    [ApiController]
    [Route("api/grouplife/policies")]
    [Produces("application/json")]
    public class GroupLifePoliciesController : ControllerBase
    {
        private readonly PolicyService _policies;
        private readonly GroupLifeDetailService _details;
        private readonly GroupLifeMemberService _members;
        private readonly PremiumCalculator _calc;
        private readonly BulkMemberExcelParser _excelParser;

        public GroupLifePoliciesController(PolicyService policies, GroupLifeDetailService details, GroupLifeMemberService members, PremiumCalculator calc, BulkMemberExcelParser excelParser)
        {
            _policies = policies;
            _details = details;
            _members = members;
            _calc = calc;
            _excelParser = excelParser;

        }
        /// <summary>
        /// List / search group-life policies (paged). All filters are optional; pass
        /// <c>subRiskId</c> (e.g. "GLA") to restrict to a specific group-life product.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResultDto<PolicyListItemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResultDto<PolicyListItemDto>>> List([FromQuery] PolicyQueryDto query,
            CancellationToken ct)
            => Ok(await _details.ListAsync(query, ct));

        /// <summary>Create a group-life policy or proposal (postings_edit "Save &amp; Continue").</summary>
        [HttpPost]
        [ProducesResponseType(typeof(PolicyResponseDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<PolicyResponseDto>> Create([FromBody] PolicyCreateDto dto,
          [FromQuery] bool createEnvelope = true, CancellationToken ct = default)
        {
            var user = CurrentUser();
            var result = await _policies.CreateAsync(dto, user, ct);

            if (createEnvelope)
            {
                var envelope = await _details.EnsureEnvelopeAsync(result.PolicyNo, user, ct);
                result.DetailId = envelope.DetailID;
                result.EndorsementNo = envelope.EndorsementNo;
            }

            return CreatedAtAction(nameof(Get), new { policyNo = result.PolicyNo }, result);
        }

        [HttpGet("policyNo")]
        [ProducesResponseType(typeof(PolicyResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PolicyResponseDto>> Get([FromQuery]string policyNo, CancellationToken ct)
        {
            var result = await _policies.GetAsync(policyNo, ct);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPut("policyNo")]
        [ProducesResponseType(typeof(PolicyResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PolicyResponseDto>> Update([FromQuery]string policyNo, [FromBody] PolicyUpdateDto dto,
            CancellationToken ct)
        {
            var result = await _policies.UpdateAsync(policyNo, dto, CurrentUser(), ct);
            return Ok(result);
        }

        /// <summary>
        /// Ensure the member "envelope" (PolicyDetail/endorsement) exists for this policy.
        /// Call this before adding members (mirrors cmdAddsection).
        /// </summary>
        [HttpPost("policyNo/detail")]
        [ProducesResponseType(typeof(GroupLifeDetailDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<GroupLifeDetailDto>> EnsureDetail([FromQuery]string policyNo, CancellationToken ct)
        {
            var result = await _details.EnsureEnvelopeAsync(policyNo, CurrentUser(), ct);
            return Ok(result);
        }

        [HttpGet("policyNo/detail")]
        [ProducesResponseType(typeof(GroupLifeDetailDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<GroupLifeDetailDto>> GetDetail([FromQuery]string policyNo, CancellationToken ct)
        {
            var result = await _details.GetEnvelopeAsync(policyNo, ct);
            return result is null ? NotFound() : Ok(result);
        }

       
        /// <summary>List every section/endorsement (detail envelope) on the policy, newest first.</summary>
        [HttpGet("details")]
        [ProducesResponseType(typeof(IReadOnlyList<GroupLifeDetailDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<GroupLifeDetailDto>>> ListDetails([FromQuery]string policyNo, CancellationToken ct)
            => Ok(await _details.ListEnvelopesAsync(policyNo, ct));

        /// <summary>
        /// Create an ADDITIONAL section/endorsement for an existing policy (extra details), with a
        /// fresh endorsement number. Unlike <c>POST {policyNo}/detail</c> — which is get-or-create —
        /// this always creates a new one. Optionally pass <c>bizOption</c> (defaults to "END" when the
        /// policy already has a section, "NEW" otherwise).
        /// </summary>
        [HttpPost("{policyNo}/details")]
        [ProducesResponseType(typeof(GroupLifeDetailDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<GroupLifeDetailDto>> CreateDetail(string policyNo,
            [FromQuery] string? bizOption, CancellationToken ct)
        {
            var result = await _details.CreateEnvelopeAsync(policyNo, CurrentUser(), bizOption, ct);
            return CreatedAtAction(nameof(GetDetail), new { policyNo }, result);
        }


        /// <summary>List members for a policy (optionally filtered by endorsement).</summary>
        [HttpGet("policie/members")]
        public async Task<IActionResult> List([FromQuery]string policyNo, [FromQuery] string? endorsementNo, CancellationToken ct)
        {
            var list = await _members.ListAsync(policyNo, endorsementNo, ct);
            return Ok(list);
        }

        /// <summary>
        /// Create a RENEWAL, REVERSAL, or ADDITIONAL endorsement off an existing section.
        /// RENEWAL/REVERSAL clone the source section's figures and members onto a new
        /// endorsement number and mark the source as EXPIRED. ADDITIONAL creates a blank
        /// envelope for the user to fill independently; the source is left untouched.
        /// </summary>
        [HttpPost("policyNo/details/sourceEndorsementNo/endorsement")]
        [ProducesResponseType(typeof(GroupLifeDetailDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<GroupLifeDetailDto>> CreateEndorsement(
            [FromQuery]string policyNo, [FromQuery]string sourceEndorsementNo, [FromQuery] EndorsementType type, CancellationToken ct)
        {
            var result = await _details.CreateEndorsementAsync(policyNo, sourceEndorsementNo, type, CurrentUser(), ct);
            return CreatedAtAction(nameof(GetDetail), new { policyNo }, result);
        }

        /// <summary>Compute member premium without saving ("Calc.." button).</summary>
        [HttpPost("members/calc")]
        [ProducesResponseType(typeof(PremiumCalcResultDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PremiumCalcResultDto>> Calc([FromBody] PremiumCalcRequestDto dto, CancellationToken ct)
            => Ok(await _members.CalcAsync(dto, ct));

        /// <summary>Add or update a member and persist its cover breakdown ("Save &amp; Update Details").</summary>
        [HttpPost("members")]
        [ProducesResponseType(typeof(MemberResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MemberResponseDto>> Upsert([FromBody] MemberUpsertDto dto, CancellationToken ct)
            => Ok(await _members.UpsertAsync(dto, CurrentUser(), ct));

        /// <summary>Remove a member ("Remove Member").</summary>
        [HttpDelete("members/{memberId:long}")]
        public async Task<IActionResult> Remove(long memberId, CancellationToken ct)
        {
            var ok = await _members.RemoveAsync(memberId, ct);
            return ok ? NoContent() : NotFound();
        }

        /// <summary>Aggregate sum-insured / premium totals across members.</summary>
        [HttpGet("policies/members/aggregate")]
        [ProducesResponseType(typeof(AggregatePremiumDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<AggregatePremiumDto>> Aggregate([FromQuery] string policyNo,
            [FromQuery] string? endorsementNo, CancellationToken ct)
            => Ok(await _members.GetAggregateAsync(policyNo, endorsementNo, ct));

        /// <summary>
        /// Bulk-upload members (the "Upload Data From Excel" modal). Parse the workbook client-side
        /// to JSON rows and post them here, or wire up a server-side parser and call this internally.
        /// </summary>
        [HttpPost("members/bulk")]
        [ProducesResponseType(typeof(BulkMemberUploadResultDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<BulkMemberUploadResultDto>> Bulk([FromBody] BulkMemberUploadDto dto, CancellationToken ct)
            => Ok(await _members.BulkUploadAsync(dto, CurrentUser(), ct));
        /// <summary>
        /// Parse an uploaded member workbook (e.g. the LAPO nationwide report) into a ready-to-push
        /// payload WITHOUT saving. Use this to preview/validate the mapping, then POST the returned
        /// <c>payload</c> to <c>members/bulk</c> (or call <c>members/bulk/upload-excel</c> to do both).
        /// </summary>
        [HttpPost("members/bulk/parse-excel")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ExcelParseResultDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<ExcelParseResultDto>> ParseExcel(
            IFormFile file,
            [FromForm] string policyNo,
            [FromForm] string? endorsementNo,
            [FromForm] BulkPricingMode pricingMode = BulkPricingMode.PolicyRate,
            [FromForm] decimal fixedPremiumRate = 0,
            [FromForm] decimal loadingRate = 0,
            [FromForm] decimal discountRate = 0,
            [FromForm] string? sheetName = null,
            CancellationToken ct = default)
        {
            if (file is null || file.Length == 0)
                return BadRequest("No file was uploaded.");

            await using var stream = file.OpenReadStream();
            var result = _excelParser.Parse(stream, policyNo, endorsementNo, pricingMode,
                fixedPremiumRate, loadingRate, discountRate, sheetName);
            return Ok(result);
        }

        /// <summary>
        /// Parse an uploaded member workbook AND persist it in one call: extracts the rows and hands
        /// them straight to the bulk-upload service. Returns the insert count and aggregate totals.
        /// </summary>
        [HttpPost("members/bulk/upload-excel")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(BulkMemberUploadResultDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<BulkMemberUploadResultDto>> UploadExcel(
            IFormFile file,
            [FromForm] string policyNo,
            [FromForm] string? endorsementNo,
            [FromForm] BulkPricingMode pricingMode = BulkPricingMode.PolicyRate,
            [FromForm] decimal fixedPremiumRate = 0,
            [FromForm] decimal loadingRate = 0,
            [FromForm] decimal discountRate = 0,
            [FromForm] string? sheetName = null,
            [FromForm] string? BizOption = null,
            CancellationToken ct = default)
        {
            if (file is null || file.Length == 0)
                return BadRequest("No file was uploaded.");

            ExcelParseResultDto parsed;
            await using (var stream = file.OpenReadStream())
            {
                parsed = _excelParser.Parse(stream, policyNo, endorsementNo, pricingMode,
                    fixedPremiumRate, loadingRate, discountRate, sheetName, BizOption);
            }

            if (parsed.Payload.Rows.Count == 0)
                return BadRequest(new { message = "No member rows could be extracted.", parsed.Warnings, parsed.UnmappedColumns });

            var result = await _members.BulkUploadAsync(parsed.Payload, CurrentUser(), ct);
            return Ok(result);
        }
        /// <summary>Age Next Birthday ("Get ANB").</summary>
        [HttpPost("compute/anb")]
        public ActionResult<int> Anb([FromBody] AnbRequestDto dto)
            => Ok(_calc.AgeNextBirthday(dto.DateOfBirth, dto.StartDate));

        /// <summary>Cover/loan duration from frequency of payment ("Get Duration").</summary>
        [HttpPost("compute/duration")]
        public ActionResult<int> Duration([FromBody] DurationRequestDto dto)
            => Ok(_calc.Duration(dto.FreqOfPayment, dto.StartDate, dto.EndDate));
        private string CurrentUser() => User?.Identity?.Name ?? "system";

        [HttpGet("policy-business-types")]
        public IActionResult GetPolicyBusinessTypes()
        {
            var items = new List<DropdownItemDto>
            {
                new DropdownItemDto
                {
                    Text = "- select -",
                    Value = "NULL"
                },
                new DropdownItemDto
                {
                    Text = "Direct Without Coinsurance",
                    Value = "DIRECT"
                },
                new DropdownItemDto
                {
                    Text = "Direct With Coinsurance",
                    Value = "CO-INSURANCE LEAD"
                },
                new DropdownItemDto
                {
                    Text = "Inward Coinsurance",
                    Value = "ACCEPTED"
                }
            };

            return Ok(items);
        }

        [HttpGet("business-share-types")]
        public IActionResult GetBusinessShareTypes()
        {
            var items = new List<DropdownItemDto>
    {
        new DropdownItemDto
        {
            Text = "- select -",
            Value = "NULL"
        },
        new DropdownItemDto
        {
            Text = "100% Accounting",
            Value = "100"
        },
        new DropdownItemDto
        {
            Text = "Our Share Accounting",
            Value = "0"
        }
    };

            return Ok(items);
        }


    }
}