namespace TelemetryDeviceAPI.Interfaces
{
    public interface ISnifferService
    {
        public bool StartSniffing(string? deviceName = null);
        public bool StopSniffing();
        public bool IsRunning { get; }
    }
}