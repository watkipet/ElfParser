using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ElfParser
{
    internal static class EBitConverter
    {
        public static bool DataIsLittleEndian = BitConverter.IsLittleEndian;

        public static uint ToUInt32(byte[] value, int startIndex)
        {
            return BitConverter.ToUInt32(Convert(value, startIndex, 4).ToArray(), 0);
        }

        public static ushort ToUInt16(byte[] value, int startIndex)
        {
            return BitConverter.ToUInt16(Convert(value, startIndex, 2).ToArray(), 0);
        }

        public static ulong ToUInt64(byte[] value, int startIndex)
        {
            return BitConverter.ToUInt64(Convert(value, startIndex, 8).ToArray(), 0);
        }

        public static long ToInt64(byte[] value, int startIndex)
        {
            return BitConverter.ToInt64(Convert(value, startIndex, 8).ToArray(), 0);
        }

        public static int ToInt32(byte[] value, int startIndex)
        {
            return BitConverter.ToInt32(Convert(value, startIndex, 4).ToArray(), 0);
        }

        public static int ToInt16(byte[] value, int startIndex)
        {
            return BitConverter.ToInt16(Convert(value, startIndex, 2).ToArray(), 0);
        }

        public static byte[] GetBytes(long value)
        {
            var bytes = BitConverter.GetBytes(value);
            return DataIsLittleEndian == BitConverter.IsLittleEndian
                ? bytes
                : bytes.Reverse().ToArray();
        }

        public static byte[] GetBytes(ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            return DataIsLittleEndian == BitConverter.IsLittleEndian
                ? bytes
                : bytes.Reverse().ToArray();
        }

        public static IEnumerable<byte> Convert(byte[] value, int startIndex, int size)
        {
            // TODO: Verify that there are no conversions that are reading fewer bytes than they
            // should.

            // To troubleshoot someone attempting to read too few bytes from an array of bytes,
            // uncomment the following lines:

            //if (value.Length > size)
            //{
            //  Console.WriteLine("Request for {0} bytes out of a {1} length item.", size, value.Length);
            //}

            var data = value.Skip(startIndex).Take(size);
            if (DataIsLittleEndian != BitConverter.IsLittleEndian)
            {
                data = data.Reverse();
            }

            return data;
        }
    }
}
