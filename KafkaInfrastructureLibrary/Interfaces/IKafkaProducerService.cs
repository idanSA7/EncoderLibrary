using System.Threading.Tasks;

namespace KafkaInfrastructure.Interfaces
{
    public interface IKafkaProducerService
    {
        public Task ProduceAsync(string topic, string jsonContent);
    }
}