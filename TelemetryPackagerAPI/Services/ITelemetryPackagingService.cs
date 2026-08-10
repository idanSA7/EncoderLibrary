using TelemetryPackagerAPI.DTOs;

namespace TelemetryPackagerAPI.Services
{
    public interface ITelemetryPackagingService
    {
        bool StartPackagingAndBroadcasting(TelemetryPackagingRequestDto configuration);
        void StopPackagingAndBroadcasting();
        bool IsBroadcastingActive { get; }
    }
}