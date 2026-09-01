namespace TelemetryMongoConsumer.Configuration
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public int FlushTimeoutSeconds { get; set; } = 10;
        public string GroupId { get; set; } = "telemetry-mongo-consumer-group";
    }
}