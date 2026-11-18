namespace TelemetryMongoConsumer.Configuration
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = null!;
        public string GroupId { get; set; } = null!;
        public string Topic { get; set; } = null!;
        public int StopTimeoutSeconds { get; set; } = 5; 
    }
}