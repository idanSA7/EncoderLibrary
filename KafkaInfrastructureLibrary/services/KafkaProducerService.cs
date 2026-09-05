using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using KafkaIntegrationLibrary.Configuration;
using KafkaIntegrationLibrary.Interfaces;

namespace KafkaIntegrationLibrary.Services
{
    public class KafkaProducerService : IKafkaProducerService, IDisposable
    {
        private readonly IProducer<Null, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;
        private readonly KafkaSettings _settings;

        public KafkaProducerService(
            IOptions<KafkaSettings> options,
            ILogger<KafkaProducerService> logger)
        {
            _logger = logger;
            _settings = options.Value;

            ProducerConfig config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers
            };

            _producer = new ProducerBuilder<Null, string>(config).Build();
        }

        public async Task ProduceAsync(string topic, string jsonContent)
        {
            try
            {
                Message<Null, string> message = new Message<Null, string>
                {
                    Value = jsonContent
                };

                await _producer.ProduceAsync(topic, message);
            }
            catch (ProduceException<Null, string> ex)
            {
                _logger.LogError(ex, "Failed to send message to Kafka topic '{Topic}'. Reason: {Reason}", topic, ex.Error.Reason);
            }
        }

        public void Dispose()
        {
            int timeoutSeconds = _settings.FlushTimeoutSeconds > 0 ? _settings.FlushTimeoutSeconds : 10;
            _producer?.Flush(TimeSpan.FromSeconds(timeoutSeconds));
            _producer?.Dispose();
        }
    }
}