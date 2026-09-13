namespace TelemetryMongoConsumer.Interfaces
{
    public interface IKafkaConsumerManager
    {
        bool IsRunning { get; }
        bool Start();
        bool Stop();
    }
}