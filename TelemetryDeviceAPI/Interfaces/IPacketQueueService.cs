using TelemetryDeviceAPI.Models;

namespace TelemetryDeviceAPI.Interfaces
{
    public interface IPacketQueueService
    {
        bool Enqueue(PacketContext context);
    }
}