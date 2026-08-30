using DecoderLibrary;
using IcdModelsLIbrary;
using KafkaInfrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using TelemetryDeviceAPI.Interfaces;
using TelemetryDeviceAPI.Models;
using TelemetryDeviceAPI.Pipeline;

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
            DecoderFlow decoderFlow,
            Dictionary<IcdType, IcdModel> icdModels,
            ILoggerFactory loggerFactory)
        {
            _bufferBlock = new RawPacketBuffer();
            _frameBuilderBlock = new FrameBuilderBlock(loggerFactory.CreateLogger<FrameBuilderBlock>());
            _decodeBlock = new PacketDecoderBlock(decoderFlow, icdModels, loggerFactory.CreateLogger<PacketDecoderBlock>());
            _kafkaBlock = new KafkaProducerBlock(kafkaProducer, loggerFactory.CreateLogger<KafkaProducerBlock>());

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

        public bool Enqueue(PacketContext context)
        {
            return _bufferBlock.Enqueue(context);
        }
    }
}