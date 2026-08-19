using System.Threading.Tasks;

namespace TelemetryDeviceAPI.Services
{
    public interface IKafkaProducerService
    {
        public Task ProduceAsync(string topic, string jsonContent);
    }
}