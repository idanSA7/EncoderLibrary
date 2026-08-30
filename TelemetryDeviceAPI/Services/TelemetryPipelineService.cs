using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks.Dataflow;
using TelemetryDeviceAPI.Interfaces;
using TelemetryDeviceAPI.Pipeline;
using KafkaInfrastructure.Interfaces;
using KafkaInfrastructure.Configuration;
using DecoderLibrary;
using IcdModelsLIbrary;

namespace TelemetryDeviceAPI.Services
{
    public class TelemetryPipelineService : IPacketQueueService
    {
        private readonly RawPacketBuffer _bufferBlock;
        private readonly FrameBuilderBlock _frameBuilderBlock;
        private readonly PacketDecoderBlock _decodeBlock;
        private readonly KafkaProducerBlock _kafkaBlock;

        public TelemetryPipelineService(
            IKafkaProducerService kafkaProducer,
            IOptions<KafkaSettings> kafkaOptions,
            DecoderFlow decoderFlow,
            IcdModel icdModel,
            ILoggerFactory loggerFactory)
        {
            // אתחול הבלוקים בתוך השירות כפי שהמנחה ביקש
            _bufferBlock = new RawPacketBuffer();
            _frameBuilderBlock = new FrameBuilderBlock(loggerFactory.CreateLogger<FrameBuilderBlock>());
            _decodeBlock = new PacketDecoderBlock(decoderFlow, icdModel, loggerFactory.CreateLogger<PacketDecoderBlock>());
            _kafkaBlock = new KafkaProducerBlock(kafkaProducer, kafkaOptions, loggerFactory.CreateLogger<KafkaProducerBlock>());

            LinkPipeline();
        }

        private void LinkPipeline()
        {
            DataflowLinkOptions linkOptions = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };

            _bufferBlock.Output.LinkTo(_frameBuilderBlock.Input, linkOptions);
            _frameBuilderBlock.Output.LinkTo(_decodeBlock.Input, linkOptions);
            _decodeBlock.Output.LinkTo(_kafkaBlock.Input, linkOptions);
        }

        public bool Enqueue(byte[] packet)
        {
            return _bufferBlock.Enqueue(packet);
        }
    }
}