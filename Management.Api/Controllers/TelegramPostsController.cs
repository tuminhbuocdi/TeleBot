using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/telegram-posts")]
[Authorize]
public sealed class TelegramPostsController : ControllerBase
{
    private readonly TelegramPostAdminRepository _repo;

    public TelegramPostsController(TelegramPostAdminRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.List(page, pageSize, isActive, q, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("{postId:guid}")]
    public async Task<IActionResult> Detail([FromRoute] Guid postId, CancellationToken cancellationToken)
    {
        var medias = await _repo.GetMedias(postId, cancellationToken);
        return Ok(new { postId, medias });
    }

    public sealed class SetActiveRequest
    {
        public required bool IsActive { get; init; }
    }

    [HttpPut("{postId:guid}/active")]
    public async Task<IActionResult> SetActive([FromRoute] Guid postId, [FromBody] SetActiveRequest req, CancellationToken cancellationToken)
    {
        var n = await _repo.SetActive(postId, req.IsActive, DateTime.UtcNow, cancellationToken);
        if (n <= 0) return NotFound();
        return Ok();
    }
}
