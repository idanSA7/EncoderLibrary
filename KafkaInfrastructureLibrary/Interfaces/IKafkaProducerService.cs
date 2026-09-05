using System.Threading.Tasks;

namespace KafkaIntegrationLibrary.Interfaces
{
    public interface IKafkaProducerService
    {
        public Task ProduceAsync(string topic, string jsonContent);
    }
}