using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/telegram-crawl-sources")]
[Authorize]
public sealed class TelegramCrawlSourcesController : ControllerBase
{
    private readonly TelegramCrawlRepository _repo;

    public TelegramCrawlSourcesController(TelegramCrawlRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool? isEnabled = null,
        [FromQuery] bool includeHidden = false,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListSources(isEnabled, includeHidden, q, cancellationToken);
        return Ok(rows);
    }

    public sealed class SetEnabledRequest
    {
        public required bool IsEnabled { get; init; }
    }

    [HttpPut("{sourceId:guid}/enabled")]
    public async Task<IActionResult> SetEnabled(
        [FromRoute] Guid sourceId,
        [FromBody] SetEnabledRequest req,
        CancellationToken cancellationToken)
    {
        var n = await _repo.SetEnabled(sourceId, req.IsEnabled, DateTime.UtcNow, cancellationToken);
        if (n <= 0) return NotFound();
        return Ok();
    }

    public sealed class SetHiddenRequest
    {
        public required bool IsHidden { get; init; }
    }

    [HttpPut("{sourceId:guid}/hidden")]
    public async Task<IActionResult> SetHidden(
        [FromRoute] Guid sourceId,
        [FromBody] SetHiddenRequest req,
        CancellationToken cancellationToken)
    {
        var n = await _repo.SetHidden(sourceId, req.IsHidden, DateTime.UtcNow, cancellationToken);
        if (n <= 0) return NotFound();
        return Ok();
    }
}
