using System.Threading.Tasks;

namespace TelemetryDeviceAPI.Services
{
    public interface IPacketQueueService
    {
        public bool Enqueue(byte[] packet);
       
    }
}