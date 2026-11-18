using TelemetryMongoConsumer.Models;

namespace TelemetryMongoConsumer.Interfaces
{
    public interface ITelemetryMongoRepository
    {
        Task InsertDecodedPacketAsync(DecodedPacketDocument document);
    }
}
