using System.Text.RegularExpressions;
using System;

namespace PlcLibrary
{
    public static class ByteExtension
    {
        public static byte[] toSubBytes(this byte[] input, int offset, int length)
        {
            if (input == null)
            {
                return new byte[0];
            }
            if (offset > input.Length || offset + length > input.Length)
            {
                return input;
            }
            byte[] result = new byte[length];
            for (int i = offset; i < offset + length; i++)
            {
                result[i - offset] = input[i];
            }

            return result;
        }

        public static ushort ToModbusAddress(this string address)
        {
            ushort deviceAddress = 0;
            string pattern = @"^([A-Z]{1,2})(\d{1,4})$";
            Match match = Regex.Match(address, pattern);
            if (!match.Success || match.Groups.Count != 3)
            {
                return deviceAddress;
            }
            string deviceAddressStr = match.Groups[2].ToString();
            if (!ushort.TryParse(deviceAddressStr, out deviceAddress))
            {
                return deviceAddress;
            }
            return deviceAddress;
        }

    }
}
