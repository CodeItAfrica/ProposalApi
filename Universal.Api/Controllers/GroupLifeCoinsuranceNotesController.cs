using GibsLifesMicroWebApi.Dtos;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Services.GroupLife;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    /// <summary>
    /// Replaces postings_coins_edit.aspx — one co-insurance credit note per
    /// participating company, raised against an existing head debit note.
    ///
    /// Legacy screen event  ->  endpoint
    ///   Page_Load (new)        GET  api/grouplife/coinsurance-notes/new
    ///   Page_Load (edit)       GET  api/grouplife/coinsurance-notes/{dncnNo}
    ///   businessPropChange2    POST api/grouplife/coinsurance-notes/calculate
    ///   btnOk / btnSave        POST api/grouplife/coinsurance-notes
    ///   btnOk (edit)           PUT  api/grouplife/coinsurance-notes/{dncnNo}
    ///
    /// The legacy "Save &amp; Add New" button was just a POST that cleared the form;
    /// the client repeats the POST per participant.
    ///
    /// Legacy permissions on this screen were UND_ENDORSE_GROUPBIZ_CREATE / _VIEW /
    /// _MODIFY. Apply the equivalent policy here — [Authorize] alone is weaker than
    /// what the VB enforced.
    /// </summary>
    [ApiController]
    [Route("api/grouplife/coinsurance-notes")]
    [Produces("application/json")]
    public class GroupLifeCoinsuranceNotesController : ControllerBase
    {
        private readonly IGroupLifeCoinsuranceNoteService _service;

        public GroupLifeCoinsuranceNotesController(IGroupLifeCoinsuranceNoteService service)
        {
            _service = service;
        }

        private string CurrentUser => User?.Identity?.Name ?? "SYSTEM";

        /// <summary>List co-insurance notes.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<CoinsuranceNoteListItemDto>), 200)]
        public async Task<ActionResult<PagedResult<CoinsuranceNoteListItemDto>>> List(
            [FromQuery] CoinsuranceNoteListQuery query, CancellationToken ct)
            => await _service.ListAsync(query, ct);

        /// <summary>
        /// The whole panel for one head note, with totals showing whether retained
        /// plus ceded actually adds back up to the risk.
        /// </summary>
        [HttpGet("panel")]
        [ProducesResponseType(typeof(CoinsurancePanelSummaryDto), 200)]
        public async Task<ActionResult<CoinsurancePanelSummaryDto>> Panel(
            [FromQuery] string refDebitNoteNo, CancellationToken ct)
            => await _service.GetPanelAsync(refDebitNoteNo, ct);

        /// <summary>Prefill from the head debit note.</summary>
        /// <param name="policyNo">Legacy "edit-id".</param>
        /// <param name="refDebitNoteNo">Legacy "trans-id" — the head debit note.</param>
        /// <param name="type">"CN" for a co-insurance credit note, anything else for a reversal.</param>
        [HttpGet("new")]
        [ProducesResponseType(typeof(CoinsuranceNoteDto), 200)]
        public async Task<ActionResult<CoinsuranceNoteDto>> GetPrefill(
            [FromQuery] string policyNo,
            [FromQuery] string refDebitNoteNo,
            [FromQuery] string type,
            CancellationToken ct)
            => await _service.GetPrefillAsync(policyNo, refDebitNoteNo, type ?? "CN", ct);

        /// <summary>Load an existing co-insurance note.</summary>
        [HttpGet("{dncnNo}")]
        [ProducesResponseType(typeof(CoinsuranceNoteDto), 200)]
        public async Task<ActionResult<CoinsuranceNoteDto>> Get(string dncnNo, CancellationToken ct)
            => await _service.GetAsync(dncnNo, ct);

        /// <summary>Share, commission, WHT, net and ORC for one participant.</summary>
        [HttpPost("calculate")]
        [ProducesResponseType(typeof(CoinsurancePremiumDto), 200)]
        public ActionResult<CoinsurancePremiumDto> Calculate([FromBody] CalculateCoinsRequest request)
            => _service.Calculate(request);

        /// <summary>Raise a credit note for one participating company.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateCoinsuranceNoteResponse), 201)]
        public async Task<ActionResult<CreateCoinsuranceNoteResponse>> Create(
            [FromBody] CreateCoinsuranceNoteRequest request, CancellationToken ct)
        {
            var result = await _service.CreateAsync(request, CurrentUser, ct);
            return CreatedAtAction(nameof(Get), new { dncnNo = result.CreditNoteNo }, result);
        }

        /// <summary>Amend a co-insurance note that has not reached the ledger.</summary>
        [HttpPut("{dncnNo}")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Update(
            string dncnNo, [FromBody] UpdateCoinsuranceNoteRequest request, CancellationToken ct)
        {
            var saved = await _service.UpdateAsync(dncnNo, request, CurrentUser, ct);
            return Ok(new { creditNoteNo = saved, message = $"Data saved successfully for [{saved}]" });
        }
    }
}