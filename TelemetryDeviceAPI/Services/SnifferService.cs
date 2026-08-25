using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpPcap;
using System;

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

        private const int DEFAULT_PORT = 5000;
        private const string DEFAULT_IP = "127.0.0.1";

        public bool IsRunning { get; private set; }

        public SnifferService(
            IPacketQueueService queueService,
            ILogger<SnifferService> logger,
            IConfiguration configuration)
        {
            _queueService = queueService;
            _logger = logger;

            _targetPort = configuration.GetValue<int>(TARGET_PORT_CONFIG_KEY, DEFAULT_PORT);
            _targetIp = configuration[TARGET_IP_CONFIG_KEY] ?? DEFAULT_IP;
            _sourceIp = configuration[SOURCE_IP_CONFIG_KEY] ?? DEFAULT_IP;
        }

        public void StartSniffing(string? deviceName = null)
        {
            if (IsRunning)
            {
                _logger.LogWarning("Sniffer is already running.");
                return;
            }

            _device = ResolveDevice(deviceName);
            if (_device == null)
            {
                return;
            }

            InitializeAndStartCapture(_device);
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

            ILiveDevice? matchedDevice = devices.FirstOrDefault(d =>
                (d.Description?.Contains(deviceName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.Name?.Contains(deviceName, StringComparison.OrdinalIgnoreCase) ?? false));

            if (matchedDevice == null)
            {
                _logger.LogError("Network device '{DeviceName}' was not found.", deviceName);
            }

            return matchedDevice;
        }

        private void InitializeAndStartCapture(ILiveDevice device)
        {
            device.OnPacketArrival += Device_OnPacketArrival;
            device.Open(DeviceModes.Promiscuous, 1000);

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