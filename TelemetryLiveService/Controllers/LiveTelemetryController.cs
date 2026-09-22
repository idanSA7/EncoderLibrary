using Microsoft.AspNetCore.Mvc;
using TelemetryLiveService.Services;

namespace TelemetryLiveService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LiveTelemetryController : ControllerBase
{
    private readonly LiveTelemetryConsumerService _consumerService;

    public LiveTelemetryController(LiveTelemetryConsumerService consumerService)
    {
        _consumerService = consumerService;
    }

    [HttpPost("start")]
    public IActionResult Start()
    {
        if (_consumerService.IsRunning)
        {
            return BadRequest("Consumer is already running.");
        }

        _consumerService.StartConsuming();
        return Ok("Live telemetry streaming started.");
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        if (!_consumerService.IsRunning)
        {
            return BadRequest("Consumer is not running.");
        }

        _consumerService.StopConsuming();
        return Ok("Live telemetry streaming stopped.");
    }
}