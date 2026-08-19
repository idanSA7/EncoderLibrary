using SharpPcap;

namespace TelemetryDeviceAPI.Services
{
    public class SnifferService : ISnifferService,IDisposable
    {
        private readonly IPacketQueueService _queueService;
        private readonly ILogger<SnifferService> _logger;
        private ILiveDevice? _device;
        public bool IsRunning { get; private set; }

        public SnifferService(IPacketQueueService queueService, ILogger<SnifferService> logger)
        {
            _queueService = queueService;
            _logger = logger;
        }

        public void StartSniffing(string? deviceName = null)
        {
            if (IsRunning)
            {
                _logger.LogWarning("Sniffer is already running.");
                return;
            }
            CaptureDeviceList devices = CaptureDeviceList.Instance;
            if (devices.Count == 0)
            {
                _logger.LogError("No network devices found. Ensure Npcap is installed.");
                return;
            }
            if (string.IsNullOrEmpty(deviceName))
            {
                _device = devices[0];
            }
            else
            {
                foreach (ILiveDevice device in devices)
                {
                    if (device.Name == deviceName)
                    {
                        _device = device;
                        break;
                    }
                }
            }
            _device.OnPacketArrival += Device_OnPacketArrival;
            _device.Open(DeviceModes.Promiscuous, 1000);
            _device.StartCapture();
            IsRunning = true;

            _logger.LogInformation("Sniffer started on device: {DeviceDescription}", _device.Description);
        }
        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            RawCapture rawPacket = e.GetPacket();
            if (rawPacket != null && rawPacket.Data != null && rawPacket.Data.Length > 0)
            {
                _queueService.Enqueue(rawPacket.Data);
            }
        }
        public void StopSniffing()
        {
            if (!IsRunning || _device == null)
            {
                return;
            }

            _device.StopCapture();

            _device.Close();

            _device.OnPacketArrival -= Device_OnPacketArrival;

            IsRunning = false;

            _logger.LogInformation("Sniffer stopped.");
        }
        public void Dispose()
        {
            StopSniffing();
        }
    }
}
