using Microsoft.AspNetCore.Mvc;
using TelemetryMongoConsumer.Interfaces;

namespace TelemetryMongoConsumer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsumerController : ControllerBase
    {
        private readonly IKafkaConsumerManager _consumerManager;

        public ConsumerController(IKafkaConsumerManager consumerManager)
        {
            _consumerManager = consumerManager;
        }

        [HttpPost("start")]
        public IActionResult Start()
        {
            bool started = _consumerManager.Start();
            if (!started)
            {
                return BadRequest("Consumer is already running.");
            }

            return Ok("Kafka Mongo Consumer started successfully.");
        }

        [HttpPost("stop")]
        public IActionResult Stop()
        {
            bool stopped = _consumerManager.Stop();
            if (!stopped)
            {
                return BadRequest("Consumer is not running.");
            }

            return Ok("Kafka Mongo Consumer stopped successfully.");
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new { IsRunning = _consumerManager.IsRunning });
        }
    }
}