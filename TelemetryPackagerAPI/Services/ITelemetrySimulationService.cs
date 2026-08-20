using TelemetrySimulator.DTOs;

namespace TelemetrySimulator.Services
{
    public interface ITelemetrySimulationService
    {
        bool StartPackagingAndBroadcasting(TelemetrySimulationRequestDto configuration);
        void StopPackagingAndBroadcasting();
        bool IsBroadcastingActive { get; }
    }
}