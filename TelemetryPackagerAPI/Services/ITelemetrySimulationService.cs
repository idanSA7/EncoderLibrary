using TelemetrySimulator.DTOs;

namespace TelemetrySimulator.Services
{
    public interface ITelemetrySimulationService
    {
        bool IsBroadcastingActive { get; }
        bool StartBroadcasting(TelemetrySimulationRequestDto configuration);
        void StopBroadcasting();
    }
}