namespace TelemetryDeviceAPI.Configuration
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public int FlushTimeoutSeconds { get; set; } = 10;
    }
}