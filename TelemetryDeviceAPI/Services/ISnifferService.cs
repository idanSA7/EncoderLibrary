namespace TelemetryDeviceAPI.Services
{
    public interface ISnifferService
    {
        void StartSniffing(string? deviceName = null);
        void StopSniffing();
        bool IsRunning { get; }
    }
}