using DecoderLibrary;
using IcdModelsLIbrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TelemetryDeviceAPI.Services
{
    public class PacketQueueService : IPacketQueueService, IDisposable
    {
        private readonly BufferBlock<byte[]> _bufferBlock;
        private readonly TransformManyBlock<byte[], byte[]> _frameBuilderBlock;
        private readonly TransformBlock<byte[], string?> _decodeTransformBlock;
        private readonly ActionBlock<string?> _kafkaActionBlock;
        private readonly ILogger<PacketQueueService> _logger;
        private readonly string _topic;

        private const string KAFKA_TOPIC_CONFIG_KEY = "Kafka:Topic";
        private const string DEFAULT_KAFKA_TOPIC = "telemetry-packets";
        private const int DEFAULT_PORT_CONFIG = 5000;
        private const string TARGET_PORT_CONFIG_KEY = "packetsDestination:targetPort";
        private const byte SYNC_BYTE = 2;

        private readonly UdpClient? _dummyListener;

        public PacketQueueService(
            IKafkaProducerService kafkaProducer,
            DecoderFlow decoderFlow,
            IcdModel icdModel,
            IConfiguration configuration,
            ILogger<PacketQueueService> logger)
        {
            _logger = logger;
            _topic = configuration[KAFKA_TOPIC_CONFIG_KEY] ?? DEFAULT_KAFKA_TOPIC;

            int targetPort = configuration.GetValue<int>(TARGET_PORT_CONFIG_KEY, DEFAULT_PORT_CONFIG);
            try
            {
                _dummyListener = new UdpClient(targetPort);
                _logger.LogInformation("Dummy UDP listener bound to port {Port} successfully.", targetPort);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not bind dummy UDP listener to port {Port}.", targetPort);
            }

            _bufferBlock = new BufferBlock<byte[]>();

            _frameBuilderBlock = CreateFrameBuilderBlock();
            _decodeTransformBlock = CreateDecodeTransformBlock(decoderFlow, icdModel);
            _kafkaActionBlock = CreateKafkaActionBlock(kafkaProducer);

            LinkPipelineBlocks();
        }

        private TransformManyBlock<byte[], byte[]> CreateFrameBuilderBlock()
        {
            return new TransformManyBlock<byte[], byte[]>(
                (byte[] rawPacket) => FilterAndBuildFrames(rawPacket),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        private IEnumerable<byte[]> FilterAndBuildFrames(byte[] rawPacket)
        {
            if (rawPacket == null || rawPacket.Length == 0)
            {
                yield break;
            }

            if (rawPacket[0] != SYNC_BYTE)
            {
                _logger.LogWarning("Discarded packet with invalid Sync byte: {SyncByte}", rawPacket[0]);
                yield break;
            }

            yield return rawPacket;
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

            _bufferBlock.LinkTo(_frameBuilderBlock, linkOptions);
            _frameBuilderBlock.LinkTo(_decodeTransformBlock, linkOptions);
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

      

        public void Dispose()
        {
            _dummyListener?.Close();
            _dummyListener?.Dispose();
        }
    }
}