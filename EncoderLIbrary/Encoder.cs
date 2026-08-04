using System;
using System.Collections.Generic;
using System.Linq;

namespace EncoderLIbrary
{
    public class Encoder
    {
        private const double BitsPerByte = 8.0;
        private const int Float32BitSize = 32;
        private const int Float32ByteCount = 4;
        private const byte DefaultByteMask = 0xFF;

        public byte[] Encode(IcdModel model, Dictionary<string, string> userInputs)
        {
            if (model?.IcdItems == null || !model.IcdItems.Any())
            {
                return null;
            }

            int bufferSize = BufferSizeCalc(model);
            byte[] buffer = new byte[bufferSize];

            IEnumerable<IGrouping<int, IcdItem>> itemsGroupedByLocation = model.IcdItems.GroupBy(item => item.Location);

            foreach (IGrouping<int, IcdItem> itemsAtLocation in itemsGroupedByLocation)
            {
                EncodeLocationGroup(itemsAtLocation, userInputs, buffer);
            }

            return buffer;
        }

        private int BufferSizeCalc(IcdModel model)
        {
            IcdItem lastItem = model.IcdItems.Last();
            int lastItemBytesSpan = (int)Math.Ceiling(lastItem.Size / BitsPerByte);
            return lastItem.Location + lastItemBytesSpan;
        }

        private void EncodeLocationGroup(IGrouping<int, IcdItem> itemsAtLocation, Dictionary<string, string> userInputs, byte[] buffer)
        {
            int currentLocation = itemsAtLocation.Key;

            foreach (IcdItem currentIcdItem in itemsAtLocation)
            {
                if (userInputs.TryGetValue(currentIcdItem.Name, out string value))
                {
                    if (!ValidateValueSize(currentIcdItem, value))
                        continue;

                    RouteItemEncoding(currentIcdItem, value, buffer, currentLocation);
                }
            }
        }

        private bool ValidateValueSize(IcdItem item, string value)
        {
            double parsedValue;

            try
            {
                parsedValue = Convert.ToDouble(value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing value for '{item.Name}': {ex.Message}");
                return false;
            }

            if (parsedValue < item.Min || parsedValue > item.Max)
            {
                Console.WriteLine($"Error: Value {parsedValue} for '{item.Name}' is out of range [{item.Min}..{item.Max}]!");
                return false;
            }

            if (IsFloatItem(item)) return true;

            return ValidateBitCapacity(item, parsedValue);
        }

        private bool ValidateBitCapacity(IcdItem item, double parsedValue)
        {
            if (item.Min >= 0)
            {
                double maxAllowed = Math.Pow(2, item.Size) - 1;
                if (parsedValue > maxAllowed)
                {
                    Console.WriteLine($"Error: Value {parsedValue} exceeds bit capacity ({maxAllowed}) for {item.Size} unsigned bits!");
                    return false;
                }
            }
            else
            {
                // Using Size - 1 for Two's complement signed integer range calculation
                double minAllowed = -Math.Pow(2, item.Size - 1);
                double maxAllowed = Math.Pow(2, item.Size - 1) - 1;

                if (parsedValue < minAllowed || parsedValue > maxAllowed)
                {
                    Console.WriteLine($"Error: Value {parsedValue} exceeds signed bit capacity [{minAllowed} .. {maxAllowed}] for {item.Size} bits!");
                    return false;
                }
            }
            return true;
        }

        private void RouteItemEncoding(IcdItem item, string value, byte[] buffer, int currentLocation)
        {
            if (IsFloatItem(item))
            {
                EncodeFloatItem(item, value, buffer);
            }
            else
            {
                if (item.Size > BitsPerByte)
                {
                    uint processedValue = FormatValueToBits(item, value);
                    EncodeMultiByteItem(item, processedValue, buffer);
                }
                else
                {
                    byte processedByte = FormatValueToSingleByte(item, value);
                    EncodeSingleByteItem(item, processedByte, buffer, currentLocation);
                }
            }

            
        private bool IsFloatItem(IcdItem currentIcdItem)
        {
            return (currentIcdItem.Type == DataType.Float);
        }

        private void EncodeFloatItem(IcdItem item, string value, byte[] buffer)
        {
            if (item.Size == Float32BitSize)
            {
                Encode32BitFloat(item, value, buffer);
            }
            else
            {
                float floatVal = Convert.ToSingle(value);
                uint rawBits = (uint)BitConverter.SingleToInt32Bits(floatVal);

                if (item.Size > BitsPerByte)
                    EncodeMultiByteItem(item, rawBits, buffer);
                else
                    EncodeSingleByteItem(item, (byte)rawBits, buffer, item.Location);
            }
        }

        private void Encode32BitFloat(IcdItem item, string value, byte[] buffer)
        {
            float floatVal = Convert.ToSingle(value);
            byte[] floatBytes = BitConverter.GetBytes(floatVal);

            // Convert to Big-Endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(floatBytes);
            }

            for (int byteIndex = 0; byteIndex < Float32ByteCount; byteIndex++)
            {
                buffer[item.Location + byteIndex] = floatBytes[byteIndex];
            }
        }

        private void EncodeSingleByteItem(IcdItem item, byte value, byte[] buffer, int currentLocation)
        {
            buffer[currentLocation] |= value;
        }

        private byte FormatValueToSingleByte(IcdItem item, string value)
        {
            byte mask = string.IsNullOrWhiteSpace(item.Mask)
                ? DefaultByteMask
                : Convert.ToByte(item.Mask, 2);

            int shift = item.GetShiftFromMask();

            if (item.Min >= 0)
            {
                return EncodeUnsignedValue(value, shift, mask);
            }
            else
            {
                return EncodeSignedValue(value, shift, mask);
            }
        }

        private uint FormatValueToBits(IcdItem item, string value)
        {
            uint mask = string.IsNullOrWhiteSpace(item.Mask)
                ? DefaultByteMask
                : Convert.ToUInt32(item.Mask, 2);

            int shift = item.GetShiftFromMask();

            uint rawVal = Convert.ToUInt32(value);
            uint shiftedVal = rawVal << shift;
            return shiftedVal & mask;
        }

        private byte EncodeUnsignedValue(string value, int shift, byte mask)
        {
            byte rawVal = Convert.ToByte(value);
            int shiftedVal = rawVal << shift;
            return (byte)(shiftedVal & mask);
        }

        private byte EncodeSignedValue(string value, int shift, byte mask)
        {
            sbyte rawVal = Convert.ToSByte(value);
            int shiftedVal = rawVal << shift;
            return (byte)(shiftedVal & mask);
        }

        private void EncodeMultiByteItem(IcdItem item, uint value, byte[] buffer)
        {
            ulong rawVal = value;
            int bytesCount = (int)Math.Ceiling(item.Size / BitsPerByte);

            for (int byteIndex = 0; byteIndex < bytesCount; byteIndex++)
            {
                int shiftIndex = (bytesCount - 1 - byteIndex) * (int)BitsPerByte;
                byte byteValue = (byte)((rawVal >> shiftIndex) & DefaultByteMask);

                buffer[item.Location + byteIndex] = byteValue;
            }
        }
    }
}