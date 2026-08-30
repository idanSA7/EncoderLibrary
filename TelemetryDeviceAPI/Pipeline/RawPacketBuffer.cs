using System.Threading.Tasks.Dataflow;

namespace TelemetryDeviceAPI.Pipeline
{
    public class RawPacketBuffer
    {
        private readonly BufferBlock<byte[]> _bufferBlock;

        public RawPacketBuffer()
        {
            _bufferBlock = new BufferBlock<byte[]>();
        }

        public ISourceBlock<byte[]> Output => _bufferBlock;//

        public bool Enqueue(byte[] packet)
        {
            if (packet == null || packet.Length == 0)
            {
                return false;
            }

            return _bufferBlock.Post(packet);
        }

    }
}