using System;

namespace EncoderLIbrary
{
    public static class EncoderMath
    {
        public const int BITS_PER_BYTE = 8;
        public const byte DEFAULT_BYTE_MASK = 0xFF;
        public const byte IS_SIGNED_NUM = 0;

        public static bool ValidateValueSize(IcdItem targetIcdItem, string rawInputString)
        {
            double parsedNumericValue;

            try
            {
                parsedNumericValue = Convert.ToDouble(rawInputString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing value for '{targetIcdItem.Name}': {ex.Message}");
                return false;
            }

            if (parsedNumericValue < targetIcdItem.Min || parsedNumericValue > targetIcdItem.Max)
            {
                Console.WriteLine($"Error: Value {parsedNumericValue} for '{targetIcdItem.Name}' is out of range [{targetIcdItem.Min}..{targetIcdItem.Max}]!");
                return false;
            }

            if (EncoderFlow.IsFloatItem(targetIcdItem)) return true;

            return ValidateBitCapacity(targetIcdItem, parsedNumericValue);
        }

        public static bool ValidateBitCapacity(IcdItem targetIcdItem, double parsedNumericValue)
        {
            if (targetIcdItem.Min >= IS_SIGNED_NUM)
            {
                double maxAllowed = Math.Pow(2, targetIcdItem.Size) - 1;
                if (parsedNumericValue > maxAllowed)
                {
                    Console.WriteLine($"Error: Value {parsedNumericValue} exceeds bit capacity ({maxAllowed}) for {targetIcdItem.Size} unsigned bits!");
                    return false;
                }
            }
            else
            {
                double minAllowed = -Math.Pow(2, targetIcdItem.Size - 1);
                double maxAllowed = Math.Pow(2, targetIcdItem.Size - 1) - 1;

                if (parsedNumericValue < minAllowed || parsedNumericValue > maxAllowed)
                {
                    Console.WriteLine($"Error: Value {parsedNumericValue} exceeds signed bit capacity [{minAllowed} .. {maxAllowed}] for {targetIcdItem.Size} bits!");
                    return false;
                }
            }
            return true;
        }

        public static byte FormatValueToSingleByte(IcdItem targetIcdItem, string rawInputString)
        {
            byte maskByte = string.IsNullOrWhiteSpace(targetIcdItem.Mask)
                ? DEFAULT_BYTE_MASK
                : Convert.ToByte(targetIcdItem.Mask, 2);

            int bitShift = targetIcdItem.GetShiftFromMask();

            if (targetIcdItem.Min >= 0)
            {
                return EncodeUnsignedValue(rawInputString, bitShift, maskByte);
            }
            else
            {
                return EncodeSignedValue(rawInputString, bitShift, maskByte);
            }
        }

        public static uint FormatValueToBits(IcdItem targetIcdItem, string rawInputString)
        {
            uint maskBits = !string.IsNullOrWhiteSpace(targetIcdItem.Mask)
                ? Convert.ToUInt32(targetIcdItem.Mask, 2)
                : (targetIcdItem.Size >= 32 ? 0xFFFFFFFF : (1U << targetIcdItem.Size) - 1);

            int bitShift = targetIcdItem.GetShiftFromMask();
            uint rawUnsignedVal;

            if (targetIcdItem.Min < 0)
            {
                long signedVal = Convert.ToInt64(rawInputString);
                rawUnsignedVal = (uint)(signedVal & maskBits);
            }
            else
            {
                rawUnsignedVal = Convert.ToUInt32(rawInputString);
            }

            uint shiftedVal = rawUnsignedVal << bitShift;
            return shiftedVal & maskBits;
        }

        private static byte EncodeUnsignedValue(string rawInputString, int bitShift, byte maskByte)
        {
            byte rawByteVal = Convert.ToByte(rawInputString);
            int shiftedVal = rawByteVal << bitShift;
            return (byte)(shiftedVal & maskByte);
        }

        private static byte EncodeSignedValue(string rawInputString, int bitShift, byte maskByte)
        {
            sbyte rawSignedByteVal = Convert.ToSByte(rawInputString);
            int shiftedVal = rawSignedByteVal << bitShift;
            return (byte)(shiftedVal & maskByte);
        }
    }
}