using System;
using IcdModelsLIbrary;

namespace DecoderLibrary
{
    public static class DecoderMath
    {
        public const int BITS_PER_BYTE = 8;

        public static byte ReadByte(byte[] buffer, int offset) => buffer[offset];

        public static float ReadFloat(byte[] buffer, int offset)
        {
            byte[] floatBytes = new byte[4];
            Array.Copy(buffer, offset, floatBytes, 0, 4);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(floatBytes);
            }

            return BitConverter.ToSingle(floatBytes, 0);
        }
    }
}