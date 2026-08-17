using EncoderLIbrary;
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
        private readonly IOptionsMonitor<NetworkSettings> _networkOptionsMonitor;
        private readonly ITelemetryDataGenerator _dataGenerator; 

        private CancellationTokenSource? _transmissionCancellationTokenSource;

        public bool IsBroadcastingActive { get; private set; }

        public TelemetrySimulationService(
            IOptionsMonitor<NetworkSettings> networkOptionsMonitor,
            ITelemetryDataGenerator dataGenerator) 
        {
            _networkOptionsMonitor = networkOptionsMonitor;
            _dataGenerator = dataGenerator;
        }

        public void StopPackagingAndBroadcasting()
        {
            if (!IsBroadcastingActive) return;

            _transmissionCancellationTokenSource?.Cancel();
            IsBroadcastingActive = false;
        }

        public bool StartPackagingAndBroadcasting(TelemetrySimulationRequestDto configuration)
        {
            if (IsBroadcastingActive) return false;

            IsBroadcastingActive = true;
            _transmissionCancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => ExecutePackagingLoop(configuration, _transmissionCancellationTokenSource.Token));

            return true;
        }

        private async Task ExecutePackagingLoop(TelemetrySimulationRequestDto configuration, CancellationToken cancellationToken)
        {
            using UdpClient targetUdpSocketClient = new UdpClient();
            string targetIp = _networkOptionsMonitor.CurrentValue.TargetIp;

            try
            {
                IcdModel targetIcdDefinition = await LoadIcdDefinitionAsync(cancellationToken);

                if (targetIcdDefinition == null)
                {
                    return;
                }

                Console.WriteLine($"[DEBUG] Starting UDP broadcast to IP: {targetIp}, Port: {configuration.DestinationNetworkPort}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    Dictionary<string, string> currentTelemetryInputs = _dataGenerator.PrepareTelemetryData(targetIcdDefinition, configuration);

                    byte[] encodedPacketBuffer = _telemetryEncoderFlow.Encode(targetIcdDefinition, currentTelemetryInputs);

                    await TransmitPacketAsync(targetUdpSocketClient, encodedPacketBuffer, targetIp, configuration.DestinationNetworkPort);

                    Console.WriteLine($"[TEST] Copy this to Console App:\nbyte[] testPacket = new byte[] {{ {string.Join(", ", encodedPacketBuffer)} }};");

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

        private async Task<IcdModel> LoadIcdDefinitionAsync(CancellationToken cancellationToken)
        {
            string icdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ICD_DIRECTORY_NAME, ICD_FILE_NAME);

            Console.WriteLine($"[DEBUG] Looking for ICD file at: {icdFilePath}");

            if (!File.Exists(icdFilePath))
            {
                Console.WriteLine("[ERROR] The ICD file was NOT FOUND! Aborting UDP broadcast.");
                return null;
            }

            Console.WriteLine("[DEBUG] ICD file found successfully. Loading...");
            string icdJsonContent = await File.ReadAllTextAsync(icdFilePath, cancellationToken);

            return IcdModel.LoadFromJson(icdJsonContent)
                   ?? throw new InvalidOperationException("Failed to load ICD file.");
        }

        private async Task TransmitPacketAsync(UdpClient targetUdpSocketClient, byte[] encodedPacketBuffer, string targetIp, int targetPort)
        {
            if (encodedPacketBuffer != null && encodedPacketBuffer.Length > 0)
            {
                await targetUdpSocketClient.SendAsync(
                    encodedPacketBuffer,
                    encodedPacketBuffer.Length,
                    targetIp,
                    targetPort
                );

                Console.WriteLine($"[SUCCESS] UDP Packet sent! Size: {encodedPacketBuffer.Length} bytes.");
            }
            else
            {
                Console.WriteLine("[WARNING] Encoder returned an empty or null buffer.");
            }
        }
    }
}