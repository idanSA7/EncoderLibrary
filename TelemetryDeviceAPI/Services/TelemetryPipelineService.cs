using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks.Dataflow;
using DecoderLibrary;
using IcdModelsLIbrary;
using KafkaIntegrationLibrary.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelemetryDeviceAPI.Configuration;
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
            IOptions<IcdSettings> icdOptions,
            ILoggerFactory loggerFactory)
        {
            Dictionary<IcdType, IcdModel> icdModels = LoadIcdDefinitions(icdOptions.Value.IcdDefinition);

            _bufferBlock = new RawPacketBuffer();
            _frameBuilderBlock = new FrameBuilderBlock(loggerFactory.CreateLogger<FrameBuilderBlock>());
            _decodeBlock = new PacketDecoderBlock(decoderFlow, icdModels, loggerFactory.CreateLogger<PacketDecoderBlock>());
            _kafkaBlock = new KafkaProducerBlock(kafkaProducer, loggerFactory.CreateLogger<KafkaProducerBlock>());

            LinkPipeline();
        }

        private static Dictionary<IcdType, IcdModel> LoadIcdDefinitions(string folderName)
        {
            string icdDirectory = Path.Combine(AppContext.BaseDirectory, folderName);

            return new Dictionary<IcdType, IcdModel>
            {
                [IcdType.FlightBoxUp] = IcdModel.LoadFromJson(File.ReadAllText(Path.Combine(icdDirectory, "FlightBoxUpIcd.json"))),
                [IcdType.FlightBoxDown] = IcdModel.LoadFromJson(File.ReadAllText(Path.Combine(icdDirectory, "FlightBoxDownIcd.json")))
            };
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