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

namespace TelemetrySimulator.Services
{
    public class TelemetrySimulationService : ITelemetrySimulationService
    {
        private const string ICD_DIRECTORY_NAME = "IcdDefinitions";
        private const string ICD_FILE_NAME = "FlightBoxDownIcd.json";

        private const string SYNC_FIELD_VALUE = "172";
        private const string LENGTH_FIELD_VALUE = "38";
        private const string FLOAT_FORMAT_SPECIFIER = "F2";

        private const string FLOAT_TYPE_NAME = "float";
        private const string DOUBLE_TYPE_NAME = "double";

        private readonly EncoderFlow _telemetryEncoderFlow = new EncoderFlow();
        private readonly IOptionsMonitor<NetworkSettings> _networkOptionsMonitor;
        private CancellationTokenSource? _transmissionCancellationTokenSource;
        private byte _packetCounter = 0;

        public bool IsBroadcastingActive { get; private set; }

        public TelemetrySimulationService(IOptionsMonitor<NetworkSettings> networkOptionsMonitor)
        {
            _networkOptionsMonitor = networkOptionsMonitor;
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

            // Access target IP strongly-typed via Options Pattern
            string targetIp = _networkOptionsMonitor.CurrentValue.TargetIp;

            try
            {
                string icdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ICD_DIRECTORY_NAME, ICD_FILE_NAME);

                Console.WriteLine($"[DEBUG] Looking for ICD file at: {icdFilePath}");

                if (!File.Exists(icdFilePath))
                {
                    Console.WriteLine("[ERROR] The ICD file was NOT FOUND! Aborting UDP broadcast.");
                    return;
                }

                Console.WriteLine("[DEBUG] ICD file found successfully. Loading...");
                string icdJsonContent = await File.ReadAllTextAsync(icdFilePath, cancellationToken);

                IcdModel targetIcdDefinition = IcdModel.LoadFromJson(icdJsonContent)
                                               ?? throw new InvalidOperationException("Failed to load ICD file.");

                Console.WriteLine($"[DEBUG] Starting UDP broadcast to IP: {targetIp}, Port: {configuration.DestinationNetworkPort}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    Dictionary<string, string> currentTelemetryInputs = GenerateRandomTelemetryInputs(targetIcdDefinition);

                    if (configuration.TelemetryInputs != null && configuration.TelemetryInputs.Count > 0)
                    {
                        foreach (KeyValuePair<string, string> input in configuration.TelemetryInputs)
                        {
                            currentTelemetryInputs[input.Key] = input.Value;
                        }
                    }

                    byte[] encodedPacketBuffer = _telemetryEncoderFlow.Encode(targetIcdDefinition, currentTelemetryInputs);

                    if (encodedPacketBuffer != null && encodedPacketBuffer.Length > 0)
                    {
                        await targetUdpSocketClient.SendAsync(
                            encodedPacketBuffer,
                            encodedPacketBuffer.Length,
                            targetIp,
                            configuration.DestinationNetworkPort
                        );

                        Console.WriteLine($"[SUCCESS] UDP Packet sent! Size: {encodedPacketBuffer.Length} bytes.");
                    }
                    else
                    {
                        Console.WriteLine("[WARNING] Encoder returned an empty or null buffer.");
                    }

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

        private Dictionary<string, string> GenerateRandomTelemetryInputs(IcdModel icd)
        {
            Random random = new Random();
            Dictionary<string, string> randomInputs = new Dictionary<string, string>();

            _packetCounter++;

            foreach (IcdItem item in icd.IcdItems)
            {
                string fieldName = item.Name;
                string fieldType = item.Type.ToString().ToLower();
                SpecialTelemetryField specialField = ParseSpecialField(fieldName);

                if (specialField == SpecialTelemetryField.Sync)
                {
                    randomInputs[fieldName] = SYNC_FIELD_VALUE;
                    continue;
                }

                if (specialField == SpecialTelemetryField.Counter)
                {
                    randomInputs[fieldName] = _packetCounter.ToString();
                    continue;
                }

                if (specialField == SpecialTelemetryField.Length)
                {
                    randomInputs[fieldName] = LENGTH_FIELD_VALUE;
                    continue;
                }

                int minVal = item.Min;
                int maxVal = item.Max;

                if (fieldType.Contains(FLOAT_TYPE_NAME) || fieldType.Contains(DOUBLE_TYPE_NAME))
                {
                    double range = maxVal - minVal;
                    double sample = (random.NextDouble() * range) + minVal;
                    randomInputs[fieldName] = sample.ToString(FLOAT_FORMAT_SPECIFIER);
                }
                else
                {
                    if (minVal == maxVal)
                    {
                        randomInputs[fieldName] = minVal.ToString();
                    }
                    else
                    {
                        randomInputs[fieldName] = random.Next(minVal, maxVal + 1).ToString();
                    }
                }
            }

            return randomInputs;
        }

        private SpecialTelemetryField ParseSpecialField(string fieldName)
        {
            if (fieldName.Equals(nameof(SpecialTelemetryField.Sync), StringComparison.OrdinalIgnoreCase))
            {
                return SpecialTelemetryField.Sync;
            }

            if (fieldName.Equals(nameof(SpecialTelemetryField.Counter), StringComparison.OrdinalIgnoreCase))
            {
                return SpecialTelemetryField.Counter;
            }

            if (fieldName.Equals(nameof(SpecialTelemetryField.Length), StringComparison.OrdinalIgnoreCase))
            {
                return SpecialTelemetryField.Length;
            }

            return SpecialTelemetryField.None;
        }
    }
}