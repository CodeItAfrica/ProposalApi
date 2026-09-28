using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers;

/// <summary>
/// Dropdown options for the group-life policy form (account exec, business source,
/// account type, currency, business channel).
/// </summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly LookupService _lookups;

    public LookupsController(LookupService lookups) => _lookups = lookups;

    /// <summary>All policy-form dropdowns in a single call (convenient for form load).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PolicyFormOptionsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyFormOptionsDto>> FormOptions(CancellationToken ct)
    {
        return Ok(new PolicyFormOptionsDto
        {
            AccountExecutives = await _lookups.GetAccountExecutivesAsync(ct),
            BusinessSources = _lookups.GetBusinessSources(),
            AccountTypes = _lookups.GetAccountTypes(),
            Currencies = await _lookups.GetCurrenciesAsync(ct),
            BusinessChannels = await _lookups.GetBusinessChannelsAsync(ct)
        });
    }

    [HttpGet("account-executives")]
    public async Task<ActionResult<IReadOnlyList<LookupItemDto>>> AccountExecutives(CancellationToken ct)
        => Ok(await _lookups.GetAccountExecutivesAsync(ct));

    [HttpGet("business-sources")]
    public ActionResult<IReadOnlyList<LookupItemDto>> BusinessSources()
        => Ok(_lookups.GetBusinessSources());

    [HttpGet("account-types")]
    public ActionResult<IReadOnlyList<LookupItemDto>> AccountTypes()
        => Ok(_lookups.GetAccountTypes());

    [HttpGet("currencies")]
    public async Task<ActionResult<IReadOnlyList<CurrencyOptionDto>>> Currencies(CancellationToken ct)
        => Ok(await _lookups.GetCurrenciesAsync(ct));

    [HttpGet("channels")]
    public async Task<ActionResult<IReadOnlyList<LookupItemDto>>> Channels(CancellationToken ct)
        => Ok(await _lookups.GetBusinessChannelsAsync(ct));
}