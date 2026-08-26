using DecoderLibrary;
using IcdModelsLIbrary;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;

namespace TelemetryDeviceAPI.Pipeline
{
    public class PacketDecoderTransformBlock
    {
        private readonly TransformBlock<byte[], Dictionary<string, object>?> _block;
        private readonly ILogger<PacketDecoderTransformBlock> _logger;
        private readonly DecoderFlow _decoderFlow;
        private readonly IcdModel _icdModel;

        public PacketDecoderTransformBlock(
            DecoderFlow decoderFlow,
            IcdModel icdModel,
            ILogger<PacketDecoderTransformBlock> logger)
        {
            _decoderFlow = decoderFlow;
            _icdModel = icdModel;
            _logger = logger;

            _block = new TransformBlock<byte[], Dictionary<string, object>?>(
                DecodePacket,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        public ITargetBlock<byte[]> Input => _block;
        public ISourceBlock<Dictionary<string, object>?> Output => _block;

        private Dictionary<string, object>? DecodePacket(byte[] packet)
        {
            try
            {
                return _decoderFlow.Decode(_icdModel, packet);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode packet.");
                return null;
            }
        }
    }
}