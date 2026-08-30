using EncoderLIbrary;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using TelemetrySimulator.Configuration;
using TelemetrySimulator.DTOs;
using IcdModelsLIbrary;

namespace TelemetrySimulator.Services
{
    public class TelemetrySimulationService : ITelemetrySimulationService
    {
        private const string ICD_DIRECTORY_NAME = "IcdDefinitions";

        private readonly EncoderFlow _telemetryEncoderFlow = new EncoderFlow();
        private readonly NetworkSettings _networkSettings;
        private readonly ITelemetryDataGenerator _dataGenerator;
        private readonly Dictionary<IcdType, IcdModel> _icdDefinitions;

        private CancellationTokenSource? _simulationCancellationTokenSource;

        public bool IsBroadcastingActive { get; private set; }

        public TelemetrySimulationService(
            IOptions<NetworkSettings> networkOptions,
            ITelemetryDataGenerator dataGenerator)
        {
            _networkSettings = networkOptions.Value;
            _dataGenerator = dataGenerator;
            _icdDefinitions = LoadIcdDefinitions();
        }

        public void StopBroadcasting()
        {
            if (!IsBroadcastingActive) return;

            _simulationCancellationTokenSource?.Cancel();
            IsBroadcastingActive = false;
        }

        public bool StartBroadcasting(TelemetrySimulationRequestDto configuration)
        {
            if (IsBroadcastingActive) return false;

            IsBroadcastingActive = true;
            _simulationCancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => ExecutePackagingLoop(configuration, _simulationCancellationTokenSource.Token));

            return true;
        }

        private async Task ExecutePackagingLoop(TelemetrySimulationRequestDto configuration, CancellationToken cancellationToken)
        {
            using UdpClient udpSocketClient = new UdpClient();
            string targetIp = _networkSettings.TargetIp;
            int basePort = (configuration.DestinationNetworkPort.HasValue && configuration.DestinationNetworkPort.Value > 0)
                ? configuration.DestinationNetworkPort.Value
                : _networkSettings.BasePort;
            try
            {
                Console.WriteLine($"[DEBUG] Starting UDP broadcast to IP: {targetIp}, Base Port: {basePort}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    await BroadcastAllIcdsAsync(udpSocketClient, targetIp, basePort, configuration);
                    await Task.Delay(configuration.TransmissionIntervalMilliseconds, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("[DEBUG] Broadcasting was stopped by the user.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL ERROR] The background task crashed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                IsBroadcastingActive = false;
                Console.WriteLine("[DEBUG] Broadcasting loop has completely terminated.");
            }
        }

        private async Task BroadcastAllIcdsAsync(
            UdpClient udpSocketClient,
            string targetIp,
            int basePort,
            TelemetrySimulationRequestDto configuration)
        {
            foreach (KeyValuePair<IcdType, IcdModel> kvp in _icdDefinitions)
            {
                IcdType currentType = kvp.Key;
                IcdModel currentIcd = kvp.Value;

                int targetPort = basePort + (int)currentType;

                Dictionary<string, string> currentTelemetryInputs = _dataGenerator.PrepareTelemetryData(currentIcd, configuration);
                byte[] encodedPacketBuffer = _telemetryEncoderFlow.Encode(currentIcd, currentTelemetryInputs);

                await SendPacketAsync(udpSocketClient, encodedPacketBuffer, targetIp, targetPort);

                Console.WriteLine($"[TEST] Sent {currentType} to Port {targetPort}");
            }
        }

        private Dictionary<IcdType, IcdModel> LoadIcdDefinitions()
        {
            Dictionary<IcdType, IcdModel> definitions = new Dictionary<IcdType, IcdModel>();

            definitions[IcdType.FlightBoxUp] = LoadSingleIcd("FlightBoxUpIcd.json");
            definitions[IcdType.FlightBoxDown] = LoadSingleIcd("FlightBoxDownIcd.json");

            return definitions;
        }

        private IcdModel LoadSingleIcd(string fileName)
        {
            string icdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ICD_DIRECTORY_NAME, fileName);

            if (!File.Exists(icdFilePath))
            {
                throw new FileNotFoundException($"ICD file was not found at path: {icdFilePath}");
            }

            string icdJsonContent = File.ReadAllText(icdFilePath);

            return IcdModel.LoadFromJson(icdJsonContent)
                   ?? throw new InvalidOperationException($"Failed to parse ICD JSON configuration for {fileName}.");
        }

        private async Task SendPacketAsync(UdpClient udpSocketClient, byte[] encodedPacketBuffer, string targetIp, int targetPort)
        {
            if (encodedPacketBuffer != null && encodedPacketBuffer.Length > 0)
            {
                await udpSocketClient.SendAsync(
                    encodedPacketBuffer,
                    encodedPacketBuffer.Length,
                    targetIp,
                    targetPort
                );
            }
            else
            {
                Console.WriteLine("[WARNING] Encoder returned an empty or null buffer.");
            }
        }
    }
}