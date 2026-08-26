using System.Threading.Tasks;

namespace TelemetryDeviceAPI.Interfaces
{
    public interface IPacketQueueService
    {
        public bool Enqueue(byte[] packet);
       
    }
}