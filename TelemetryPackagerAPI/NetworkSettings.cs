namespace TelemetrySimulator.Configuration
{
    public class NetworkSettings
    {
        public const string SectionName = "NetworkSettings";

        public string TargetIp { get; set; } = "127.0.0.1";
        public int DefaultPort { get; set; } = 5000;
    }
}