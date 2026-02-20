using Management.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly TelegramNotificationService _telegram;

    public NotificationsController(TelegramNotificationService telegram)
    {
        _telegram = telegram;
    }

    public sealed class TelegramNotifyRequest
    {
        public required string Message { get; init; }
    }

    [HttpPost("telegram")]
    public async Task<IActionResult> SendTelegram([FromBody] TelegramNotifyRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
        {
            return BadRequest("Message is required");
        }

        await _telegram.SendAsync(req.Message, cancellationToken);
        return Ok();
    }
}
