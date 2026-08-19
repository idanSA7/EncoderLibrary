using System.Threading;
using System.Threading.Tasks;

namespace TelemetryDeviceAPI.Services
{
    public interface IPacketQueueService
    {
        bool Enqueue(byte[] packet);
        ValueTask<byte[]> DequeueAsync(CancellationToken cancellationToken = default);
    }
}