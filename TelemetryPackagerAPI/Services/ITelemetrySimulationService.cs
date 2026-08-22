using TelemetrySimulator.DTOs;

namespace TelemetrySimulator.Services
{
    public interface ITelemetrySimulationService
    {
        bool Start(TelemetrySimulationRequestDto configuration);
        void Stop();
        bool IsBroadcastingActive { get; }
    }
}