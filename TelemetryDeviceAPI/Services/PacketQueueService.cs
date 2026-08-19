using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TelemetryDeviceAPI.Services
{
    public class PacketQueueService : IPacketQueueService
    {
        private readonly BufferBlock<byte[]> _bufferBlock;
        private readonly ActionBlock<byte[]> _kafkaActionBlock;
        private readonly ILogger<PacketQueueService> _logger;
        private readonly string _topic;

        public PacketQueueService(
            IKafkaProducerService kafkaProducer,
            IConfiguration configuration,
            ILogger<PacketQueueService> logger)
        {
            _logger = logger;
            _topic = configuration["Kafka:Topic"] ?? "telemetry-packets";

            _bufferBlock = new BufferBlock<byte[]>();

            _kafkaActionBlock = new ActionBlock<byte[]>(
                async (byte[] packet) =>
                {
                    try
                    {
                        await kafkaProducer.ProduceAsync(_topic, packet);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while processing and sending packet to Kafka.");
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });

            _bufferBlock.LinkTo(_kafkaActionBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
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