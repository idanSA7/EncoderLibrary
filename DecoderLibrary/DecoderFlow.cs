using DecoderLIbrary;
using System;
using System.Collections.Generic;

namespace DecoderLibrary
{
    public class DecoderFlow
    {
        private const int BIT_SIZE_8 = 8;
        private const int BIT_SIZE_16 = 16;
        private const int BIT_SIZE_32 = 32;

        public Dictionary<string, object> Decode(IcdModel icdModel, byte[] rawPacketBuffer)
        {
            Dictionary<string, object> decodedTelemetryData = new Dictionary<string, object>();

            if (icdModel?.IcdItems == null || rawPacketBuffer == null)
            {
                return decodedTelemetryData;
            }

            foreach (IcdItem targetIcdItem in icdModel.IcdItems)
            {
                object extractedFieldValue = ExtractFieldValue(targetIcdItem, rawPacketBuffer);
                decodedTelemetryData[targetIcdItem.Name] = extractedFieldValue;
            }

            return decodedTelemetryData;
        }

        private object ExtractFieldValue(IcdItem targetIcdItem, byte[] rawPacketBuffer)
        {
            int byteOffsetLocation = targetIcdItem.Location;

            if (targetIcdItem.Type == DataType.Float)
            {
                return DecoderMath.ReadFloat(rawPacketBuffer, byteOffsetLocation);
            }

            if (targetIcdItem.Type == DataType.Int)
            {
                if (targetIcdItem.Size == BIT_SIZE_8)
                {
                    return DecoderMath.ReadByte(rawPacketBuffer, byteOffsetLocation);
                }

                if (targetIcdItem.Size == BIT_SIZE_16)
                {
                    return targetIcdItem.IsSigned()
                        ? DecoderMath.ReadInt16(rawPacketBuffer, byteOffsetLocation)
                        : DecoderMath.ReadUInt16(rawPacketBuffer, byteOffsetLocation);
                }

                if (targetIcdItem.Size == BIT_SIZE_32)
                {
                    return targetIcdItem.IsSigned()
                        ? DecoderMath.ReadInt32(rawPacketBuffer, byteOffsetLocation)
                        : DecoderMath.ReadUInt32(rawPacketBuffer, byteOffsetLocation);
                }
            }

            throw new InvalidOperationException($"Unsupported type or size for item: {targetIcdItem.Name}");
        }
    }
}