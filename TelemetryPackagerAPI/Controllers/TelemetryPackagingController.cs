using Microsoft.AspNetCore.Mvc;
using TelemetryPackagerAPI.DTOs;
using TelemetryPackagerAPI.Services;

namespace TelemetryPackagerAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryPackagerController : ControllerBase
    {
        private const string BROADCAST_ALREADY_ACTIVE_MESSAGE = "Broadcasting is already active.";
        private const string BROADCAST_STARTED_MESSAGE = "Broadcasting started successfully.";
        private const string BROADCAST_STOPPED_MESSAGE = "Broadcasting stopped successfully.";

        private readonly ITelemetryPackagingService _telemetryService;

        public TelemetryPackagerController(ITelemetryPackagingService telemetryService)
        {
            _telemetryService = telemetryService;
        }

        [HttpPost("start")]
        public IActionResult Start([FromBody] TelemetryPackagingRequestDto configuration)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            bool isStarted = _telemetryService.StartPackagingAndBroadcasting(configuration);

            if (!isStarted)
            {
                return Conflict(new { message = BROADCAST_ALREADY_ACTIVE_MESSAGE });
            }

            return Ok(new { message = BROADCAST_STARTED_MESSAGE });
        }

        [HttpPost("stop")]
        public IActionResult Stop()
        {
            _telemetryService.StopPackagingAndBroadcasting();
            return Ok(new { message = BROADCAST_STOPPED_MESSAGE });
        }
    }
}