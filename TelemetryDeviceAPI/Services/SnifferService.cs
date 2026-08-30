using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpPcap;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using TelemetryDeviceAPI.Interfaces;
using TelemetryDeviceAPI.Models;

namespace TelemetryDeviceAPI.Services
{
    public class SnifferService : ISnifferService, IDisposable
    {
        private readonly IPacketQueueService _queueService;
        private readonly ILogger<SnifferService> _logger;
        private readonly int _basePort;
        private readonly string _targetIp;
        private readonly string _sourceIp;
        private ILiveDevice? _device;

        private const string BASE_PORT_CONFIG_KEY = "TelemetrySettings:BasePort";
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

            _basePort = configuration.GetValue<int>(BASE_PORT_CONFIG_KEY, 11505);
            _targetIp = configuration[TARGET_IP_CONFIG_KEY] ?? string.Empty;
            _sourceIp = configuration[SOURCE_IP_CONFIG_KEY] ?? string.Empty;
        }

        public bool StartSniffing(string? deviceName = null)
        {
            if (IsRunning) return false;

            _device = ResolveDevice(deviceName);
            if (_device == null) return false;

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

            if (string.IsNullOrWhiteSpace(deviceName)) return devices[0];

            return devices.FirstOrDefault(device =>
                (device.Description?.Contains(deviceName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (device.Name?.Contains(deviceName, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        private void InitializeAndStartCapture(ILiveDevice device)
        {
            device.OnPacketArrival += Device_OnPacketArrival;
            device.Open(DeviceModes.Promiscuous, READ_TIMEOUT_MS);

            string bpfFilter = BuildDynamicBpfFilter();
            device.Filter = bpfFilter;

            device.StartCapture();
            IsRunning = true;

            _logger.LogInformation("Sniffer started on device: {DeviceDescription} with BPF Filter: '{Filter}'",
                device.Description, bpfFilter);
        }

        private string BuildDynamicBpfFilter()
        {
            List<string> filterParts = new List<string>();

            foreach (IcdType icdType in Enum.GetValues(typeof(IcdType)))
            {
                int targetPort = _basePort + (int)icdType;

                string part = $"udp dst port {targetPort}";
                if (!string.IsNullOrWhiteSpace(_targetIp)) part += $" and dst host {_targetIp}";
                if (!string.IsNullOrWhiteSpace(_sourceIp)) part += $" and src host {_sourceIp}";

                filterParts.Add($"({part})");
            }

            return string.Join(" or ", filterParts);
        }

        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            RawCapture rawCapture = e.GetPacket();
            if (rawCapture == null || rawCapture.Data == null || rawCapture.Data.Length == 0) return;

            var parsedPacket = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);
            var udpPacket = parsedPacket.Extract<UdpPacket>();

            if (udpPacket != null)
            {
                int destPort = udpPacket.DestinationPort;
                IcdType type = (IcdType)(destPort - _basePort);

                var context = new PacketContext
                {
                    RawData = udpPacket.PayloadData,
                    DestinationPort = destPort,
                    IcdType = type
                };

                _queueService.Enqueue(context);
            }
        }

        public bool StopSniffing()
        {
            if (!IsRunning || _device == null) return false;

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