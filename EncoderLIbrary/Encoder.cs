using System;
using System.Collections.Generic;
using System.Linq;

namespace EncoderLIbrary
{
    public class Encoder
    {
        public byte[] Encode(IcdModel model, Dictionary<string, object> userInputs)
        {
            if (model?.items == null || !model.items.Any())
            {
                return new byte[0];
            }

            int bufferSize = BufferSizeCalc(model);
            byte[] buffer = new byte[bufferSize];

            IEnumerable<IGrouping<int, IcdItem>> locationGroups = model.items.GroupBy(item => item.Location);

            foreach (IGrouping<int, IcdItem> locationGroup in locationGroups)
            {
                EncodeLocationGroup(locationGroup, userInputs, buffer);
            }

            return buffer;
        }

        private int BufferSizeCalc(IcdModel model)
        {
            IcdItem lastItem = model.items.Last();
            int lastItemBytesSpan = (int)Math.Ceiling(lastItem.Bit / 8.0);
            return lastItem.Location + lastItemBytesSpan;
        }

        private void EncodeLocationGroup(IGrouping<int, IcdItem> locationGroup, Dictionary<string, object> userInputs, byte[] buffer)
        {
            int currentLocation = locationGroup.Key;

            foreach (IcdItem item in locationGroup)
            {
                if (userInputs.TryGetValue(item.Name, out object value))
                {
                    ValidateValueSize(item, value);
                    RouteItemEncoding(item, value, buffer, currentLocation);
                }
            }
        }
        private void ValidateValueSize(IcdItem item, object value)
        {
            double val = Convert.ToDouble(value);

            if (val < item.Min || val > item.Max)
            {
                throw new ArgumentOutOfRangeException(
                    item.Name,
                    $"Error: Value {val} for '{item.Name}' is out of range [{item.Min}..{item.Max}]!"
                );
            }

            if (IsFloatItem(item)) return;

            ValidateBitCapacity(item, val);
        }

        private void ValidateBitCapacity(IcdItem item, double val)
        {
            if (item.Min >= 0)
            {
                double maxAllowed = Math.Pow(2, item.Bit) - 1;
                if (val > maxAllowed)
                {
                    throw new ArgumentOutOfRangeException(
                        item.Name,
                        $"Error: Value {val} exceeds bit capacity ({maxAllowed}) for {item.Bit} unsigned bits!"
                    );
                }
            }
            else
            {
                double minAllowed = -Math.Pow(2, item.Bit - 1);
                double maxAllowed = Math.Pow(2, item.Bit - 1) - 1;

                if (val < minAllowed || val > maxAllowed)
                {
                    throw new ArgumentOutOfRangeException(
                        item.Name,
                        $"Error: Value {val} exceeds signed bit capacity [{minAllowed} .. {maxAllowed}] for {item.Bit} bits!"
                    );
                }
            }
        }

        private void RouteItemEncoding(IcdItem item, object value, byte[] buffer, int currentLocation)
        {
            if (IsFloatItem(item))
            {
                EncodeFloatItem(item, value, buffer);
            }
            else if (item.Bit > 8)
            {
                EncodeMultiByteItem(item, value, buffer);
            }
            else
            {
                EncodeSingleByteItem(item, value, buffer, currentLocation);
            }
        }
        private bool IsFloatItem(IcdItem item)
        {
            return string.Equals(item.Type, "float", StringComparison.OrdinalIgnoreCase);
        }


        private void EncodeFloatItem(IcdItem item, object value, byte[] buffer)
        {
            if (item.Bit == 32)
            {
                Encode32BitFloat(item, value, buffer);
            }
            else
            {
                float floatVal = Convert.ToSingle(value);
                uint rawBits = (uint)BitConverter.SingleToInt32Bits(floatVal);

                if (item.Bit > 8)
                    EncodeMultiByteItem(item, rawBits, buffer);
                else
                    EncodeSingleByteItem(item, rawBits, buffer, item.Location);
            }
        }
        private void Encode32BitFloat(IcdItem item, object value, byte[] buffer)
        {
            float floatVal = Convert.ToSingle(value);
            byte[] floatBytes = BitConverter.GetBytes(floatVal);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(floatBytes);
            }

            for (int i = 0; i < 4; i++)
            {
                buffer[item.Location + i] = floatBytes[i];
            }
        }
        
        private void EncodeSingleByteItem(IcdItem item, object value, byte[] buffer, int currentLocation)
        {
            uint maskedVal = FormatValueToBits(item, value);
            buffer[currentLocation] |= (byte)maskedVal;
        }
        private uint FormatValueToBits(IcdItem item, object value)
        {
            uint mask = string.IsNullOrWhiteSpace(item.Mask)
                ? 0xFF
                : Convert.ToUInt32(item.Mask, 2);

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
        private uint EncodeUnsignedValue(object value, int shift, uint mask)
        {
            uint rawVal = Convert.ToUInt32(value);
            uint shiftedVal = rawVal << shift;
            return shiftedVal & mask;
        }

        private uint EncodeSignedValue(object value, int shift, uint mask)
        {
            int rawVal = Convert.ToInt32(value);
            int shiftedVal = rawVal << shift;
            return (uint)shiftedVal & mask;
        }

        private void EncodeMultiByteItem(IcdItem item, object value, byte[] buffer)
        {
            ulong rawVal = Convert.ToUInt64(value);
            int bytesCount = (int)Math.Ceiling(item.Bit / 8.0);

            for (int i = 0; i < bytesCount; i++)
            {
                int shiftIndex = (bytesCount - 1 - i) * 8;
                byte byteValue = (byte)((rawVal >> shiftIndex) & 0xFF);

                buffer[item.Location + i] = byteValue;
            }
        }
    }
}