using IcdModelsLIbrary;
using System;
using System.Collections.Generic;

namespace DecoderLibrary
{
    public class DecoderFlow
    {
        private const int BIT_SIZE_8 = 8;
        private const int BIT_SIZE_32 = 32;
        private const uint FULL_BIT_MASK_32_BITS = 0xFFFFFFFF;
        private const uint SINGLE_BIT_MASK = 1U;

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
                return ExtractSingleByteValue(targetIcdItem, rawPacketBuffer, byteOffsetLocation);
            }

            if (targetIcdItem.Size <= BIT_SIZE_32)
            {
                return ExtractMultiByteValue(targetIcdItem, rawPacketBuffer, byteOffsetLocation);
            }

            throw new InvalidOperationException($"Unsupported size {targetIcdItem.Size} for item: {targetIcdItem.Name}");
        }

        private object ExtractSingleByteValue(IcdItem targetIcdItem, byte[] rawPacketBuffer, int byteOffsetLocation)
        {
            byte rawByte = DecoderMath.ReadByte(rawPacketBuffer, byteOffsetLocation);
            uint finalUnsignedValue;

            if (targetIcdItem.Size == BIT_SIZE_8 && string.IsNullOrWhiteSpace(targetIcdItem.Mask))
            {
                finalUnsignedValue = rawByte;
            }
            else
            {
                byte bitMask = ResolveByteMask(targetIcdItem);
                int bitShiftAmount = targetIcdItem.GetShiftFromMask();
                finalUnsignedValue = (uint)((rawByte & bitMask) >> bitShiftAmount);
            }

            if (targetIcdItem.IsSigned())
            {
                return ApplySignExtension(finalUnsignedValue, targetIcdItem.Size);
            }

            return (byte)finalUnsignedValue;
        }

        private byte ResolveByteMask(IcdItem targetIcdItem)
        {
            if (!string.IsNullOrWhiteSpace(targetIcdItem.Mask))
            {
                return Convert.ToByte(targetIcdItem.Mask, 2);
            }

            return (byte)((1 << targetIcdItem.Size) - 1);
        }

        private object ExtractMultiByteValue(IcdItem targetIcdItem, byte[] rawPacketBuffer, int byteOffsetLocation)
        {
            uint rawBitsVal = ReadRawBits(targetIcdItem, rawPacketBuffer, byteOffsetLocation);

            if (targetIcdItem.IsSigned())
            {
                return ApplySignExtension(rawBitsVal, targetIcdItem.Size);
            }

            return rawBitsVal;
        }

        private uint ReadRawBits(IcdItem targetIcdItem, byte[] rawPacketBuffer, int byteOffsetLocation)
        {
            int bytesCount = (int)Math.Ceiling(targetIcdItem.Size / (double)DecoderMath.BITS_PER_BYTE);
            uint rawBitsVal = 0;

            for (int byteIndex = 0; byteIndex < bytesCount; byteIndex++)
            {
                int shiftIndex = (bytesCount - 1 - byteIndex) * DecoderMath.BITS_PER_BYTE;
                rawBitsVal |= (uint)(rawPacketBuffer[byteOffsetLocation + byteIndex] << shiftIndex);
            }

            return rawBitsVal;
        }

        // Converts N-bit signed value to a standard 32-bit signed int 
        private int ApplySignExtension(uint finalUnsignedValue, int bitSize)
        {
            int signBitShift = bitSize - 1;
            bool isNegative = (finalUnsignedValue & (SINGLE_BIT_MASK << signBitShift)) != 0;

            if (isNegative)
            {
                uint signExtensionMask = FULL_BIT_MASK_32_BITS << bitSize;
                return (int)(finalUnsignedValue | signExtensionMask);
            }

            return (int)finalUnsignedValue;
        }
    }
}