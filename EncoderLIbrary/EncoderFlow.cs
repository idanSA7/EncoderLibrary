using System;
using System.Collections.Generic;
using System.Linq;

namespace EncoderLIbrary
{
    public class EncoderFlow
    {
        private const int FLOAT_32_BYTE_COUNT = 4;

        public byte[] Encode(IcdModel model, Dictionary<string, string> userInputs)
        {
            if (model?.IcdItems == null || !model.IcdItems.Any())
            {
                return null;
            }

            int bufferSize = BufferSizeCalc(model);
            byte[] outputBuffer = new byte[bufferSize];

            IEnumerable<IGrouping<int, IcdItem>> itemsGroupedByLocation = model.IcdItems.GroupBy(icdItem => icdItem.Location);

            foreach (IGrouping<int, IcdItem> itemsAtLocation in itemsGroupedByLocation)
            {
                EncodeLocationGroup(itemsAtLocation, userInputs, outputBuffer);
            }

            return outputBuffer;
        }

        private static int BufferSizeCalc(IcdModel model)
        {
            IcdItem lastIcdItem = model.IcdItems.Last();
            int lastItemBytesSpan = (int)Math.Ceiling(lastIcdItem.Size / (double)EncoderMath.BITS_PER_BYTE);
            return lastIcdItem.Location + lastItemBytesSpan;
        }

        private void EncodeLocationGroup(IGrouping<int, IcdItem> itemsAtLocation, Dictionary<string, string> userInputs, byte[] outputBuffer)
        {
            int currentLocation = itemsAtLocation.Key;
            foreach (IcdItem targetIcdItem in itemsAtLocation)
            {
                if (userInputs.TryGetValue(targetIcdItem.Name, out string rawInputString))
                {
                    if (!EncoderMath.ValidateValueSize(targetIcdItem, rawInputString))
                        continue;

                    RouteItemEncoding(targetIcdItem, rawInputString, outputBuffer, currentLocation);
                }
            }
        }

        private void RouteItemEncoding(IcdItem targetIcdItem, string rawInputString, byte[] outputBuffer, int currentLocation)
        {
            if (IsFloatItem(targetIcdItem))
            {
                EncodeFloatItem(targetIcdItem, rawInputString, outputBuffer);
            }
            else
            {
                if (targetIcdItem.Size > EncoderMath.BITS_PER_BYTE)
                {
                    uint processedValueBits = EncoderMath.FormatValueToBits(targetIcdItem, rawInputString);
                    EncodeMultiByteItem(targetIcdItem, processedValueBits, outputBuffer);
                }
                else
                {
                    byte processedByte = EncoderMath.FormatValueToSingleByte(targetIcdItem, rawInputString);
                    EncodeSingleByteItem(targetIcdItem, processedByte, outputBuffer, currentLocation);
                }
            }
        }

        internal static bool IsFloatItem(IcdItem targetIcdItem)
        {
            return targetIcdItem.Type == DataType.Float;
        }

        private void EncodeFloatItem(IcdItem targetIcdItem, string rawInputString, byte[] outputBuffer)
        {
            float floatNumericVal = Convert.ToSingle(rawInputString);
            byte[] floatBytes = BitConverter.GetBytes(floatNumericVal);

            // Convert to Big-Endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(floatBytes);
            }

            for (int byteIndex = 0; byteIndex < FLOAT_32_BYTE_COUNT; byteIndex++)
            {
                outputBuffer[targetIcdItem.Location + byteIndex] = floatBytes[byteIndex];
            }
        }

        private void EncodeSingleByteItem(IcdItem targetIcdItem, byte encodedByte, byte[] outputBuffer, int currentLocation)
        {
            outputBuffer[currentLocation] |= encodedByte;
        }

        private void EncodeMultiByteItem(IcdItem targetIcdItem, uint formattedValueBits, byte[] outputBuffer)
        {
            ulong rawBitsVal = formattedValueBits;
            int bytesCount = (int)Math.Ceiling(targetIcdItem.Size / (double)EncoderMath.BITS_PER_BYTE);

            for (int byteIndex = 0; byteIndex < bytesCount; byteIndex++)
            {
                int shiftIndex = (bytesCount - 1 - byteIndex) * EncoderMath.BITS_PER_BYTE;
                byte byteValue = (byte)((rawBitsVal >> shiftIndex) & EncoderMath.DEFAULT_BYTE_MASK);

                outputBuffer[targetIcdItem.Location + byteIndex] = byteValue;
            }
        }
    }
}