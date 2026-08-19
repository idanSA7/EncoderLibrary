using System.Threading.Tasks;

namespace TelemetryDeviceAPI.Services
{
    public interface IPacketQueueService
    {
        bool Enqueue(byte[] packet);
        void Complete();
        Task Completion { get; }
    }
}