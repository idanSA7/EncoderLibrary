using EncoderLIbrary;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TelemetryPackagerAPI.DTOs;

namespace TelemetryPackagerAPI.Services
{
    

    public class TelemetryPackagingService : ITelemetryPackagingService
    {
        private const string DEFAULT_TARGET_IP = "127.0.0.1";
        private const string CONFIG_TARGET_IP_KEY = "NetworkSettings:TargetIp";
        private const string ICD_DIRECTORY_NAME = "IcdDefinitions";
        private const string ICD_FILE_NAME = "FlightBoxDownIcd.json";

        private const string SYNC_FIELD_VALUE = "172";
        private const string LENGTH_FIELD_VALUE = "38";
        private const string FLOAT_FORMAT_SPECIFIER = "F2";

        private const string FLOAT_TYPE_NAME = "float";
        private const string DOUBLE_TYPE_NAME = "double";

        private readonly EncoderFlow _telemetryEncoderFlow = new EncoderFlow();
        private readonly IConfiguration _configuration;
        private CancellationTokenSource? _transmissionCancellationTokenSource;
        private byte _packetCounter = 0;

        public bool IsBroadcastingActive { get; private set; }

        public TelemetryPackagingService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void StopPackagingAndBroadcasting()
        {
            if (!IsBroadcastingActive) return;

            _transmissionCancellationTokenSource?.Cancel();
            IsBroadcastingActive = false;
        }

        public bool StartPackagingAndBroadcasting(TelemetryPackagingRequestDto configuration)
        {
            if (IsBroadcastingActive) return false;

            IsBroadcastingActive = true;
            _transmissionCancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => ExecutePackagingLoop(configuration, _transmissionCancellationTokenSource.Token));

            return true;
        }

        private async Task ExecutePackagingLoop(TelemetryPackagingRequestDto configuration, CancellationToken cancellationToken)
        {
            using UdpClient targetUdpSocketClient = new UdpClient();
            string targetIp = _configuration[CONFIG_TARGET_IP_KEY] ?? DEFAULT_TARGET_IP;

            try
            {
                string icdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ICD_DIRECTORY_NAME, ICD_FILE_NAME);

                if (!File.Exists(icdFilePath))
                {
                    return;
                }

                string icdJsonContent = await File.ReadAllTextAsync(icdFilePath, cancellationToken);

                IcdModel targetIcdDefinition = IcdModel.LoadFromJson(icdJsonContent)
                                               ?? throw new InvalidOperationException("Failed to load ICD file.");

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
                    }

                    await Task.Delay(configuration.TransmissionIntervalMilliseconds, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                
            }
            finally
            {
                IsBroadcastingActive = false;
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