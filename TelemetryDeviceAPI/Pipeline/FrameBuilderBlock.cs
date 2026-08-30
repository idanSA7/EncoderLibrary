using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using TelemetryDeviceAPI.Models;

namespace TelemetryDeviceAPI.Pipeline
{
    public class FrameBuilderBlock
    {
        private readonly TransformManyBlock<PacketContext, PacketContext> _frameBuilderBlock;
        private readonly ILogger<FrameBuilderBlock> _logger;
        private const byte SYNC_BYTE = 2;

        public FrameBuilderBlock(ILogger<FrameBuilderBlock> logger)
        {
            _logger = logger;
            _frameBuilderBlock = new TransformManyBlock<PacketContext, PacketContext>(
                FilterAndBuildFrames,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        public ITargetBlock<PacketContext> Input => _frameBuilderBlock;
        public ISourceBlock<PacketContext> Output => _frameBuilderBlock;

        private IEnumerable<PacketContext> FilterAndBuildFrames(PacketContext context)
        {
            if (context == null || context.RawData == null || context.RawData.Length == 0)
            {
                yield break;
            }

            if (context.RawData[0] != SYNC_BYTE)
            {
                _logger.LogWarning("Discarded packet with invalid Sync byte from port {Port}", context.DestinationPort);
                yield break;
            }

            yield return context;
        }
    }
}