using Confluent.Kafka;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TelemetryDeviceAPI.Services
{
    public class KafkaProducerService : IKafkaProducerService, IDisposable
    {
        private readonly IProducer<Null, string> _producer;

        public KafkaProducerService(IConfiguration configuration)
        {
            string bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            ProducerConfig config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers
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
                Console.WriteLine($"[Kafka Error] Failed to send message to topic '{topic}'. Reason: {ex.Error.Reason}");
            }
        }

        public void Dispose()
        {
            _producer?.Flush(TimeSpan.FromSeconds(10));
            _producer?.Dispose();
        }
    }
}