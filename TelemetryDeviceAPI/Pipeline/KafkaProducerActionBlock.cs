using KafkaInfrastructure.Configuration;
using KafkaInfrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TelemetryDeviceAPI.Interfaces;
using TelemetryDeviceAPI.Services;

namespace TelemetryDeviceAPI.Pipeline
{
    public class KafkaProducerActionBlock
    {
        private readonly ActionBlock<Dictionary<string, object>?> _block;
        private readonly IKafkaProducerService _kafkaProducer;
        private readonly ILogger<KafkaProducerActionBlock> _logger;
        private readonly string _topic;

        public KafkaProducerActionBlock(
            IKafkaProducerService kafkaProducer,
            IOptions<KafkaSettings> options,
            ILogger<KafkaProducerActionBlock> logger)
        {
            _kafkaProducer = kafkaProducer;
            _logger = logger;
            _topic = options.Value.Topic;

            _block = new ActionBlock<Dictionary<string, object>?>(
                SendPacketToKafkaAsync,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        public ITargetBlock<Dictionary<string, object>?> Input => _block;

        private async Task SendPacketToKafkaAsync(Dictionary<string, object>? decodedPacket)
        {
            if (decodedPacket == null)
            {
                return;
            }

            try
            {
                string jsonPayload = JsonSerializer.Serialize(decodedPacket);
                await _kafkaProducer.ProduceAsync(_topic, jsonPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while serializing or sending packet to Kafka.");
            }
        }
    }
}