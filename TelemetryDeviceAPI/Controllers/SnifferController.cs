using Microsoft.AspNetCore.Mvc;
using TelemetryDeviceAPI.Interfaces;

namespace TelemetryDeviceAPI.Controllers
{
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
            bool isSuccess = _snifferService.StartSniffing(deviceName);
            if (!isSuccess)
            {
                return BadRequest("Failed to start sniffer. Ensure the device exists and sniffer is not already running.");
            }

            return Ok("Sniffer started successfully.");
        }

        [HttpPost("stop")]
        public IActionResult Stop()
        {
            bool isSuccess = _snifferService.StopSniffing();
            if (!isSuccess)
            {
                return BadRequest("Sniffer is not currently running.");
            }

            return Ok("Sniffer stopped successfully.");
        }
    }
}