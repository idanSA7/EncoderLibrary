using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
namespace TelemetryDeviceAPI.Services
{
    public class PacketQueueService : IPacketQueueService
    {
        private readonly BufferBlock<byte[]> _queue;
        public PacketQueueService()
        {
            _queue = new BufferBlock<byte[]>();
        }
        public bool Enqueue(byte[] packet)
        {
            if (packet == null||packet.Length==0)
                return false;
            return _queue.Post(packet);

        }
        public async ValueTask<byte[]> DequeueAsync(CancellationToken cancellationToken = default)
        {
            return await _queue.ReceiveAsync(cancellationToken);
        }
    }
}
