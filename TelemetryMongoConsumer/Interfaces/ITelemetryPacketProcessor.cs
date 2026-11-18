using System.Threading.Tasks;
using Confluent.Kafka;

namespace TelemetryMongoConsumer.Interfaces
{
    public interface ITelemetryPacketProcessor
    {
        Task ProcessAndSaveAsync(ConsumeResult<Null, string> consumeResult);
    }
}