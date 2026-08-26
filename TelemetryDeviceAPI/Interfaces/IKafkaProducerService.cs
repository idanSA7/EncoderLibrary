using System.Threading.Tasks;

namespace TelemetryDeviceAPI.Interfaces
{
    public interface IKafkaProducerService
    {
        public Task ProduceAsync(string topic, string jsonContent);
    }
}