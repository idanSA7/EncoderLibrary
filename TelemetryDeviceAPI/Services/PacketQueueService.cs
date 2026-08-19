using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IcdModelsLIbrary;
using DecoderLibrary;

namespace TelemetryDeviceAPI.Services
{
    public class PacketQueueService : IPacketQueueService
    {
        private readonly BufferBlock<byte[]> _bufferBlock;
        private readonly TransformBlock<byte[], string?> _decodeTransformBlock;
        private readonly ActionBlock<string?> _kafkaActionBlock;
        private readonly ILogger<PacketQueueService> _logger;
        private readonly string _topic;

        private const string KAFKA_TOPIC_CONFIG_KEY = "Kafka:Topic";
        private const string DEFAULT_KAFKA_TOPIC = "telemetry-packets";


        public PacketQueueService(
        IKafkaProducerService kafkaProducer,
        DecoderFlow decoderFlow,
        IcdModel icdModel,
        IConfiguration configuration,
        ILogger<PacketQueueService> logger)
        {
            _logger = logger;
            _topic = configuration[KAFKA_TOPIC_CONFIG_KEY] ?? DEFAULT_KAFKA_TOPIC;

            _bufferBlock = new BufferBlock<byte[]>();
            _decodeTransformBlock = CreateDecodeTransformBlock(decoderFlow, icdModel);
            _kafkaActionBlock = CreateKafkaActionBlock(kafkaProducer);

            LinkPipelineBlocks();
        }

        private TransformBlock<byte[], string?> CreateDecodeTransformBlock(DecoderFlow decoderFlow, IcdModel icdModel)
        {
            return new TransformBlock<byte[], string?>(
                (byte[] packet) => DecodeAndSerializePacket(packet, decoderFlow, icdModel),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        private string? DecodeAndSerializePacket(byte[] packet, DecoderFlow decoderFlow, IcdModel icdModel)
        {
            try
            {
                Dictionary<string, object> decodedPacket = decoderFlow.Decode(icdModel, packet);
                return JsonSerializer.Serialize(decodedPacket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while decoding packet.");
                return null;
            }
        }

        private ActionBlock<string?> CreateKafkaActionBlock(IKafkaProducerService kafkaProducer)
        {
            return new ActionBlock<string?>(
                async (string? jsonPacket) => await SendPacketToKafkaAsync(jsonPacket, kafkaProducer),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        private async Task SendPacketToKafkaAsync(string? jsonPacket, IKafkaProducerService kafkaProducer)
        {
            if (string.IsNullOrEmpty(jsonPacket))
            {
                return;
            }

            try
            {
                await kafkaProducer.ProduceAsync(_topic, jsonPacket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing and sending packet to Kafka.");
            }
        }

        private void LinkPipelineBlocks()
        {
            DataflowLinkOptions linkOptions = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };

            _bufferBlock.LinkTo(_decodeTransformBlock, linkOptions);
            _decodeTransformBlock.LinkTo(_kafkaActionBlock, linkOptions);
        }

        public bool Enqueue(byte[] packet)
        {
            if (packet == null || packet.Length == 0)
            {
                return false;
            }

            return _bufferBlock.Post(packet);
        }

        public void Complete()
        {
            _bufferBlock.Complete();
        }

        public Task Completion => _kafkaActionBlock.Completion;
    }
}
