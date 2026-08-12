using IcdModelsLIbrary;
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
            if (targetIcdItem.Type == DataType.Float)
            {
                return DecoderMath.ReadFloat(rawPacketBuffer, targetIcdItem.Location);
            }

            if (targetIcdItem.Type == DataType.Int)
            {
                return ExtractIntegerFieldValue(targetIcdItem, rawPacketBuffer);
            }

            throw new InvalidOperationException($"Unsupported type for item: {targetIcdItem.Name}");
        }

        private object ExtractIntegerFieldValue(IcdItem targetIcdItem, byte[] rawPacketBuffer)
        {
            int byteOffsetLocation = targetIcdItem.Location;

            if (targetIcdItem.Size <= BIT_SIZE_8)
            {
                return ExtractSubByteOrSingleByteValue(targetIcdItem, rawPacketBuffer, byteOffsetLocation);
            }

            if (targetIcdItem.Size == BIT_SIZE_16)
            {
                return ExtractInt16Value(targetIcdItem, rawPacketBuffer, byteOffsetLocation);
            }

            if (targetIcdItem.Size == BIT_SIZE_32)
            {
                return ExtractInt32Value(targetIcdItem, rawPacketBuffer, byteOffsetLocation);
            }

            throw new InvalidOperationException($"Unsupported size {targetIcdItem.Size} for item: {targetIcdItem.Name}");
        }

        private object ExtractSubByteOrSingleByteValue(IcdItem targetIcdItem, byte[] rawPacketBuffer, int byteOffsetLocation)
        {
            byte rawByte = DecoderMath.ReadByte(rawPacketBuffer, byteOffsetLocation);

            if (targetIcdItem.Size == BIT_SIZE_8 && string.IsNullOrWhiteSpace(targetIcdItem.Mask))
            {
                return rawByte;
            }

            byte bitMask = ResolveByteMask(targetIcdItem);
            int bitShiftAmount = targetIcdItem.GetShiftFromMask();

            return (byte)((rawByte & bitMask) >> bitShiftAmount);
        }

        private byte ResolveByteMask(IcdItem targetIcdItem)
        {
            if (!string.IsNullOrWhiteSpace(targetIcdItem.Mask))
            {
                return Convert.ToByte(targetIcdItem.Mask, 2);
            }

            return (byte)((1 << targetIcdItem.Size) - 1);
        }

        private object ExtractInt16Value(IcdItem targetIcdItem, byte[] rawPacketBuffer, int byteOffsetLocation)
        {
            return targetIcdItem.IsSigned()
                ? DecoderMath.ReadInt16(rawPacketBuffer, byteOffsetLocation)
                : DecoderMath.ReadUInt16(rawPacketBuffer, byteOffsetLocation);
        }

        private object ExtractInt32Value(IcdItem targetIcdItem, byte[] rawPacketBuffer, int byteOffsetLocation)
        {
            return targetIcdItem.IsSigned()
                ? DecoderMath.ReadInt32(rawPacketBuffer, byteOffsetLocation)
                : DecoderMath.ReadUInt32(rawPacketBuffer, byteOffsetLocation);
        }
    }
}