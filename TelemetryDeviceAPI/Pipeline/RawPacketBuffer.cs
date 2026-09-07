using System.Threading.Tasks.Dataflow;
using TelemetryDeviceAPI.Models;

namespace TelemetryDeviceAPI.Pipeline
{
    public class RawPacketBuffer
    {
        private readonly BufferBlock<PacketContext> _bufferBlock;

        public RawPacketBuffer()
        {
            _bufferBlock = new BufferBlock<PacketContext>();
        }

        public ISourceBlock<PacketContext> Output => _bufferBlock;

        public bool Enqueue(PacketContext context)
        {
            if (context.RawData == null || context.RawData.Length == 0)
            {
                return false;
            }

            return _bufferBlock.Post(context);
        }

    }
}


