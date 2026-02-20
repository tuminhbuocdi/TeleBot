using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Management.Api.Hubs;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/crash-game")]
public class CrashGameController : ControllerBase
{
    private readonly CrashRecordRepository _crashRecords;
    private readonly IHubContext<AppHub> _hub;

    public CrashGameController(CrashRecordRepository crashRecords, IHubContext<AppHub> hub)
    {
        _crashRecords = crashRecords;
        _hub = hub;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] long? take = null)
    {
        if (take.HasValue && take.Value > 0)
        {
            var top = await _crashRecords.GetTop(take.Value);
            return Ok(top);
        }

        var data = await _crashRecords.GetAll();
        return Ok(data);
    }

    [HttpPost("realtime-test")]
    public async Task<IActionResult> RealtimeTest()
    {
        await _hub.Clients.All.SendAsync("crashRecordsUpdated");
        return Ok();
    }
}
