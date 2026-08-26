using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;

namespace TelemetryDeviceAPI.Pipeline
{
    public class FrameBuilderTransformManyBlock
    {
        private readonly TransformManyBlock<byte[], byte[]> _block;
        private readonly ILogger<FrameBuilderTransformManyBlock> _logger;
        private const byte SYNC_BYTE = 2;

        public FrameBuilderTransformManyBlock(ILogger<FrameBuilderTransformManyBlock> logger)
        {
            _logger = logger;
            _block = new TransformManyBlock<byte[], byte[]>(
                FilterAndBuildFrames,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });
        }

        public ITargetBlock<byte[]> Input => _block;
        public ISourceBlock<byte[]> Output => _block;

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
    }
}