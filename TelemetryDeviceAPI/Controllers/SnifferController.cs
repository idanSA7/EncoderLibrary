using Microsoft.AspNetCore.Mvc;
using TelemetryDeviceAPI.Services;

[ApiController]
[Route("api/[controller]")]
public class SnifferController : ControllerBase
{
    private readonly ISnifferService _snifferService;

    public SnifferController(ISnifferService snifferService)
    {
        _snifferService = snifferService;
    }

    [HttpPost("start")]
    public IActionResult Start([FromQuery] string? deviceName = null)
    {
        _snifferService.StartSniffing(deviceName);
        return Ok("Sniffer started successfully.");
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        _snifferService.StopSniffing();
        return Ok("Sniffer stopped.");
    }
}