using KafkaIntegrationLibrary.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TelemetryDeviceAPI.Models;

namespace TelemetryDeviceAPI.Pipeline
{
    public class KafkaProducerBlock
    {
        private readonly ActionBlock<PacketContext?> _block;
        private readonly IKafkaProducerService _kafkaProducer;
        private readonly ILogger<KafkaProducerBlock> _logger;

        public KafkaProducerBlock(
            IKafkaProducerService kafkaProducer,
            ILogger<KafkaProducerBlock> logger)
        {
            _kafkaProducer = kafkaProducer;
            _logger = logger;

            _block = new ActionBlock<PacketContext?>(
                SendPacketToKafkaAsync,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        public ITargetBlock<PacketContext?> Input => _block;

        private async Task SendPacketToKafkaAsync(PacketContext? context)
        {
            if (context == null || context.DecodedData == null)
            {
                return;
            }

            try
            {
                string dynamicTopicName = $"telemetry-{context.IcdType.ToString().ToLower()}";

                string jsonPayload = JsonSerializer.Serialize(context.DecodedData);

                await _kafkaProducer.ProduceAsync(dynamicTopicName, jsonPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while serializing or sending packet to Kafka for port {Port}.", context.DestinationPort);
            }
        }
    }
}