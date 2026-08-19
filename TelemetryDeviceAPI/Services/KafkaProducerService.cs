using Confluent.Kafka;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
namespace TelemetryDeviceAPI.Services
{
    public class KafkaProducerService : IKafkaProducerService, IDisposable
    {
        private readonly IProducer<Null, byte[]> _producer;

        public KafkaProducerService(IConfiguration configuration)
        {
            string bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            ProducerConfig config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers
            };

            _producer = new ProducerBuilder<Null, byte[]>(config).Build();
        }

        public async Task ProduceAsync(string topic, byte[] messageContent)
        {
            try
            {
                Message<Null, byte[]> message = new Message<Null, byte[]>
                {
                    Value = messageContent
                };
                await _producer.ProduceAsync(topic, message);
            }
            catch(ProduceException<Null, byte[]> ex) {
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
