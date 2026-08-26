using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpPcap;
using System;
using System.Linq;

namespace TelemetryDeviceAPI.Services
{
    public class SnifferService : ISnifferService, IDisposable
    {
        private readonly IPacketQueueService _queueService;
        private readonly ILogger<SnifferService> _logger;
        private readonly int _targetPort;
        private readonly string _targetIp;
        private readonly string _sourceIp;
        private ILiveDevice? _device;

        private const string TARGET_PORT_CONFIG_KEY = "packetsDestination:targetPort";
        private const string TARGET_IP_CONFIG_KEY = "packetsDestination:targetIp";
        private const string SOURCE_IP_CONFIG_KEY = "packetsDestination:sourceIp";

        private const int READ_TIMEOUT_MS = 1000;

        public bool IsRunning { get; private set; }

        public SnifferService(
            IPacketQueueService queueService,
            ILogger<SnifferService> logger,
            IConfiguration configuration)
        {
            _queueService = queueService;
            _logger = logger;

            _targetPort = configuration.GetValue<int>(TARGET_PORT_CONFIG_KEY);
            _targetIp = configuration[TARGET_IP_CONFIG_KEY] ?? string.Empty;
            _sourceIp = configuration[SOURCE_IP_CONFIG_KEY] ?? string.Empty;
        }

        public bool StartSniffing(string? deviceName = null)
        {
            if (IsRunning)
            {
                return false;
            }

            _device = ResolveDevice(deviceName);
            if (_device == null)
            {
                return false;
            }

            InitializeAndStartCapture(_device);
            return true;
        }

        private ILiveDevice? ResolveDevice(string? deviceName)
        {
            CaptureDeviceList devices = CaptureDeviceList.Instance;
            if (devices.Count == 0)
            {
                _logger.LogError("No network devices found. Ensure Npcap is installed.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return devices[0];
            }

            ILiveDevice? matchedDevice = devices.FirstOrDefault(device =>
                (device.Description?.Contains(deviceName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (device.Name?.Contains(deviceName, StringComparison.OrdinalIgnoreCase) ?? false));

            if (matchedDevice == null)
            {
                _logger.LogError("Network device '{DeviceName}' was not found.", deviceName);
            }

            return matchedDevice;
        }

        private void InitializeAndStartCapture(ILiveDevice device)
        {
            device.OnPacketArrival += Device_OnPacketArrival;
            device.Open(DeviceModes.Promiscuous, READ_TIMEOUT_MS);

            string bpfFilter = BuildBpfFilter();
            device.Filter = bpfFilter;

            device.StartCapture();
            IsRunning = true;

            _logger.LogInformation("Sniffer started on device: {DeviceDescription} with BPF Filter: '{Filter}'",
                device.Description, bpfFilter);
        }

        private string BuildBpfFilter()
        {
            return $"udp and dst port {_targetPort} and dst host {_targetIp} and src host {_sourceIp}";
        }

        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            RawCapture rawPacket = e.GetPacket();
            if (rawPacket != null && rawPacket.Data != null && rawPacket.Data.Length > 0)
            {
                _queueService.Enqueue(rawPacket.Data);
            }
        }

        public bool StopSniffing()
        {
            if (!IsRunning || _device == null)
            {
                return false;
            }

            _device.StopCapture();
            _device.Close();
            _device.OnPacketArrival -= Device_OnPacketArrival;

            IsRunning = false;
            _logger.LogInformation("Sniffer stopped.");
            return true;
        }

        public void Dispose()
        {
            StopSniffing();
        }
    }
}