using Microsoft.AspNetCore.Mvc;
using TelemetrySimulator.DTOs;
using TelemetrySimulator.Services;

namespace TelemetrySimulator.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryPackagerController : ControllerBase
    {
        private const string BROADCAST_ALREADY_ACTIVE_MESSAGE = "Broadcasting is already active.";
        private const string BROADCAST_STARTED_MESSAGE = "Broadcasting started successfully.";
        private const string BROADCAST_STOPPED_MESSAGE = "Broadcasting stopped successfully.";

        private readonly ITelemetrySimulationService _telemetryService;

        public TelemetryPackagerController(ITelemetrySimulationService telemetryService)
        {
            _telemetryService = telemetryService;
        }

        [HttpPost("start")]
        public IActionResult Start([FromBody] TelemetrySimulationRequestDto configuration)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            bool isStarted = _telemetryService.Start(configuration);

            if (!isStarted)
            {
                return Conflict(new { message = BROADCAST_ALREADY_ACTIVE_MESSAGE });
            }

            return Ok(new { message = BROADCAST_STARTED_MESSAGE });
        }

        [HttpPost("stop")]
        public IActionResult Stop()
        {
            _telemetryService.Stop();
            return Ok(new { message = BROADCAST_STOPPED_MESSAGE });
        }
    }
}