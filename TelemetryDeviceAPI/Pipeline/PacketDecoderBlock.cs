using DecoderLibrary;
using IcdModelsLIbrary;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using TelemetryDeviceAPI.Models;

namespace TelemetryDeviceAPI.Pipeline
{
    public class PacketDecoderBlock
    {
        private readonly TransformBlock<PacketContext, PacketContext?> _decodeTransformBlock;
        private readonly ILogger<PacketDecoderBlock> _logger;
        private readonly DecoderFlow _decoderFlow;
        private readonly Dictionary<IcdType, IcdModel> _icdModels;

        public PacketDecoderBlock(
            DecoderFlow decoderFlow,
            Dictionary<IcdType, IcdModel> icdModels,
            ILogger<PacketDecoderBlock> logger)
        {
            _decoderFlow = decoderFlow;
            _icdModels = icdModels;
            _logger = logger;

            _decodeTransformBlock = new TransformBlock<PacketContext, PacketContext?>(
                DecodePacket,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        public ITargetBlock<PacketContext> Input => _decodeTransformBlock;
        public ISourceBlock<PacketContext?> Output => _decodeTransformBlock;

        private PacketContext? DecodePacket(PacketContext context)
        {
            try
            {
                if (!_icdModels.TryGetValue(context.IcdType, out IcdModel selectedIcd))
                {
                    _logger.LogWarning("No ICD model found for type {IcdType}", context.IcdType);
                    return null;
                }

                context.DecodedData = _decoderFlow.Decode(selectedIcd, context.RawData);
                return context;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode packet from port {Port}.", context.DestinationPort);
                return null;
            }
        }
    }
}