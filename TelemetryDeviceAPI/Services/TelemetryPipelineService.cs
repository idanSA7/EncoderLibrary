using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;
using TelemetryDeviceAPI.Interfaces;
using TelemetryDeviceAPI.Pipeline;

namespace TelemetryDeviceAPI.Services
{
    public class TelemetryPipelineService : IPacketQueueService, IDisposable
    {
        private readonly RawPacketBufferBlock _bufferBlock;
        private readonly ILogger<TelemetryPipelineService> _logger;
        private readonly UdpClient? _dummyListener;

        private const int DEFAULT_PORT_CONFIG = 5000;
        private const string TARGET_PORT_CONFIG_KEY = "packetsDestination:targetPort";

        public TelemetryPipelineService(
            RawPacketBufferBlock bufferBlock,
            FrameBuilderTransformManyBlock frameBuilderBlock,
            PacketDecoderTransformBlock decodeTransformBlock,
            KafkaProducerActionBlock kafkaActionBlock,
            IConfiguration configuration,
            ILogger<TelemetryPipelineService> logger)
        {
            _bufferBlock = bufferBlock;
            _logger = logger;

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

            LinkPipeline(frameBuilderBlock, decodeTransformBlock, kafkaActionBlock);
        }

        private void LinkPipeline(
            FrameBuilderTransformManyBlock frameBuilderBlock,
            PacketDecoderTransformBlock decodeTransformBlock,
            KafkaProducerActionBlock kafkaActionBlock)
        {
            DataflowLinkOptions linkOptions = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };

            _bufferBlock.Output.LinkTo(frameBuilderBlock.Input, linkOptions);
            frameBuilderBlock.Output.LinkTo(decodeTransformBlock.Input, linkOptions);
            decodeTransformBlock.Output.LinkTo(kafkaActionBlock.Input, linkOptions);
        }

        public bool Enqueue(byte[] packet)
        {
            return _bufferBlock.Enqueue(packet);
        }

        public void Dispose()
        {
            _dummyListener?.Close();
            _dummyListener?.Dispose();
        }
    }
}