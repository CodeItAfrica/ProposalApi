using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Services.GroupLife;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    /// <summary>
    /// Replaces postings_dnotes_edit.aspx — the group life debit note screen.
    ///
    /// Legacy screen event  ->  endpoint
    ///   Page_Load (new)        GET  api/grouplife/debit-notes/new
    ///   Page_Load (edit)       GET  api/grouplife/debit-notes/{dncnNo}
    ///   cmdVerify_Click        GET  api/grouplife/debit-notes/{dncnNo}/refresh
    ///   cmdApply_Click         POST api/grouplife/debit-notes/calculate
    ///   cmdCalcfire_Click      POST api/grouplife/debit-notes/distribute
    ///   btnOk_Click (new)      POST api/grouplife/debit-notes
    ///   btnOk_Click (edit)     PUT  api/grouplife/debit-notes/{dncnNo}
    ///
    /// Errors are raised as BusinessRuleException / NotFoundException and mapped to
    /// 400 / 404 by the existing exception middleware — no per-action handling here.
    /// </summary>
    [ApiController]
    [Route("api/grouplife/debit-notes")]
    [Produces("application/json")]
    public class GroupLifeDebitNotesController : ControllerBase
    {
        private readonly GroupLifeDebitNoteService _service;

        public GroupLifeDebitNotesController(GroupLifeDebitNoteService service)
        {
            _service = service;
        }

        private string CurrentUser => User?.Identity?.Name ?? "SYSTEM";

        /// <summary>
        /// List debit notes. Returns head notes only unless headNotesOnly=false;
        /// use pendingOnly=true for notes still carrying the INV prefix.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(Models.PagedResult<DebitNoteListItemDto>), 200)]
        public async Task<ActionResult<Models.PagedResult<DebitNoteListItemDto>>> List(
            [FromQuery] DebitNoteListQuery query, CancellationToken ct)
            => await _service.ListAsync(query, ct);

        /// <summary>Child notes raised against this one — commission, participant and ORC notes.</summary>
        [HttpGet("{dncnNo}/children")]
        [ProducesResponseType(typeof(List<DebitNoteListItemDto>), 200)]
        public async Task<ActionResult<List<DebitNoteListItemDto>>> Children(
            string dncnNo, CancellationToken ct)
            => await _service.ListChildrenAsync(dncnNo, ct);

        /// <summary>Prefill for a new debit note raised against a policy endorsement.</summary>
        /// <param name="policyNo">Legacy "edit-id".</param>
        /// <param name="detailId">Legacy "trans-id" (PolicyDetails.DetailID).</param>
        /// <param name="riskClass">Legacy "class" (V, F, M, H, O, B, E, G).</param>
        [HttpGet("new")]
        [ProducesResponseType(typeof(DebitNotePrefillDto), 200)]
        public async Task<ActionResult<DebitNotePrefillDto>> GetPrefill(
            [FromQuery] string policyNo,
            [FromQuery] long? detailId,
            [FromQuery] string riskClass,
            CancellationToken ct)
            => await _service.GetPrefillAsync(policyNo, detailId, riskClass, ct);

        /// <summary>Load an existing debit note with its co-insurance participations.</summary>
        [HttpGet("{dncnNo}")]
        [ProducesResponseType(typeof(DebitNoteDto), 200)]
        public async Task<ActionResult<DebitNoteDto>> Get(
            string dncnNo, [FromQuery] string riskClass, CancellationToken ct)
            => await _service.GetAsync(dncnNo, riskClass, ct);

        /// <summary>Re-pull sum insured / gross premium from the underlying policy detail.</summary>
        [HttpGet("{dncnNo}/refresh")]
        [ProducesResponseType(typeof(DebitNoteDto), 200)]
        public async Task<ActionResult<DebitNoteDto>> Refresh(
            string dncnNo, [FromQuery] string riskClass, CancellationToken ct)
            => await _service.RefreshFromPolicyDetailAsync(dncnNo, riskClass, ct);

        /// <summary>Commission, WHT, ORC and net premium for the header figures.</summary>
        [HttpPost("calculate")]
        [ProducesResponseType(typeof(PremiumBreakdownDto), 200)]
        public ActionResult<PremiumBreakdownDto> Calculate([FromBody] CalculatePremiumRequest request)
            => _service.Calculate(request);

        /// <summary>
        /// Spread the risk across co-insurance participants. Always 200 — inspect
        /// <c>isBalanced</c> and <c>errors</c> before allowing a save.
        /// </summary>
        [HttpPost("distribute")]
        [ProducesResponseType(typeof(DistributionResultDto), 200)]
        public ActionResult<DistributionResultDto> Distribute([FromBody] DistributeRequest request)
            => _service.Distribute(request);

        /// <summary>Raise the debit note and any elected participation/ORC/commission notes.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateDebitNoteResponse), 201)]
        public async Task<ActionResult<CreateDebitNoteResponse>> Create(
            [FromBody] CreateDebitNoteRequest request, CancellationToken ct)
        {
            var result = await _service.CreateAsync(request, CurrentUser, ct);
            return CreatedAtAction(nameof(Get), new { dncnNo = result.DebitNoteNo }, result);
        }

        /// <summary>
        /// Approve the note. A debit note is raised as INV and becomes DN here, so
        /// this renames the note number and every reference to it — the response
        /// carries the new number.
        /// </summary>
        /// <param name="approvalStatus">2 = processed into the ledger (legacy default).</param>
        [HttpPut("{dncnNo}/approve")]
        [ProducesResponseType(typeof(ApproveDebitNoteResponse), 200)]
        public async Task<ActionResult<ApproveDebitNoteResponse>> Approve(
            string dncnNo, [FromQuery] byte approvalStatus, CancellationToken ct)
            => await _service.ApproveAsync(
                dncnNo, approvalStatus == 0 ? (byte)2 : approvalStatus, CurrentUser, ct);

        /// <summary>Amend an unposted debit note.</summary>
        [HttpPut("{dncnNo}")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Update(
            string dncnNo, [FromBody] UpdateDebitNoteRequest request, CancellationToken ct)
        {
            var saved = await _service.UpdateAsync(dncnNo, request, CurrentUser, ct);
            return Ok(new { debitNoteNo = saved, message = $"Data saved successfully for [{saved}]" });
        }
    }
}