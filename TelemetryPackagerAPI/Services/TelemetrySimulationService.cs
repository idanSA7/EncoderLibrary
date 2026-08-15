using EncoderLIbrary;
using IcdModelsLIbrary;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
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
        private const string ICD_FILE_NAME = "FlightBoxDownIcd.json";

        private readonly EncoderFlow _telemetryEncoderFlow = new EncoderFlow();
        private readonly NetworkSettings _networkSettings;
        private readonly ITelemetryDataGenerator _dataGenerator;
        private readonly IcdModel _icdDefinition;

        private CancellationTokenSource? _simulationCancellationTokenSource;

        public bool IsBroadcastingActive { get; private set; }

        public TelemetrySimulationService(
            IOptions<NetworkSettings> networkOptions,
            ITelemetryDataGenerator dataGenerator)
        {
            _networkSettings = networkOptions.Value;
            _dataGenerator = dataGenerator;
            _icdDefinition = LoadIcdDefinition();
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

            try
            {
                Console.WriteLine($"[DEBUG] Starting UDP broadcast to IP: {targetIp}, Port: {configuration.DestinationNetworkPort}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    Dictionary<string, string> currentTelemetryInputs = _dataGenerator.PrepareTelemetryData(_icdDefinition, configuration);

                    byte[] encodedPacketBuffer = _telemetryEncoderFlow.Encode(_icdDefinition, currentTelemetryInputs);

                    await SendPacketAsync(udpSocketClient, encodedPacketBuffer, targetIp, configuration.DestinationNetworkPort);

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

        private IcdModel LoadIcdDefinition()
        {
            string icdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ICD_DIRECTORY_NAME, ICD_FILE_NAME);

            if (!File.Exists(icdFilePath))
            {
                throw new FileNotFoundException($"ICD file was not found at path: {icdFilePath}");
            }

            string icdJsonContent = File.ReadAllText(icdFilePath);

            return IcdModel.LoadFromJson(icdJsonContent)
                   ?? throw new InvalidOperationException("Failed to parse ICD JSON configuration.");
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