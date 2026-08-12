using System;

namespace DecoderLibrary
{
    public static class DecoderMath
    {
        public const int BITS_PER_BYTE = 8;

        public static byte ReadByte(byte[] buffer, int offset) => buffer[offset];

        public static short ReadInt16(byte[] buffer, int offset) => BitConverter.ToInt16(buffer, offset);
        public static int ReadInt32(byte[] buffer, int offset) => BitConverter.ToInt32(buffer, offset);

        public static ushort ReadUInt16(byte[] buffer, int offset) => BitConverter.ToUInt16(buffer, offset);
        public static uint ReadUInt32(byte[] buffer, int offset) => BitConverter.ToUInt32(buffer, offset);

        public static float ReadFloat(byte[] buffer, int offset) => BitConverter.ToSingle(buffer, offset);
    }
}