namespace TelemetryMongoConsumer.Interfaces
{
    public interface IKafkaConsumerManager
    {
        bool Start();
        bool Stop();
    }
}