using Microsoft.Extensions.Configuration;
namespace TelemetryDeviceAPI.Services
{
    public class PacketProcessingWorker : BackgroundService
    {
        private readonly IPacketQueueService _queueService;
        private readonly IKafkaProducerService _kafkaProducerService;
        private readonly ILogger<PacketProcessingWorker> _logger;
        private readonly string _topic;
        public PacketProcessingWorker(
        IPacketQueueService queueService,
        IKafkaProducerService kafkaProducerService,
        IConfiguration configuration,
        ILogger<PacketProcessingWorker> logger)
        {
            _queueService = queueService;
            _kafkaProducerService = kafkaProducerService;
            _logger = logger;
            _topic = configuration["Kafka:Topic"] ?? "default-packets-topic";
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PacketProcessingWorker started listening to queue.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    byte[] packet = await _queueService.DequeueAsync(stoppingToken);

                    await _kafkaProducerService.ProduceAsync(_topic, packet);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing packet and publishing to Kafka topic: {Topic}", _topic);
                }
            }

            _logger.LogInformation("PacketProcessingWorker stopped.");
        }


    }
}
