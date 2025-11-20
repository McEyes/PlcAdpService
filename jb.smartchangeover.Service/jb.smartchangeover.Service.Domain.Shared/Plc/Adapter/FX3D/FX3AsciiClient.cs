using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using Microsoft.Extensions.Logging;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public class Fx3AsciiClient: PlcClient
    {
        
        public Fx3AsciiClient(IEquipmentConfig config, ILogger log) : base(config, log)
        {

        }

        #region private methods
        private byte[] getDevcieCode(string device)
        {
            byte[] result = null;
            switch (device)
            {
                case "D":
                    result = new byte[] { 0x20, 0x44 };
                    break;
                case "M":
                    result = new byte[] { 0x20, 0x4D };
                    break;
                case "R":
                    result = new byte[] { 0x20, 0x52 };
                    break;
                case "X":
                    result = new byte[] { 0x20, 0x58 };
                    break;
                case "Y":
                    result = new byte[] { 0x20, 0x59 };
                    break;
                case "S":
                    result = new byte[] { 0x20, 0x53 };
                    break;
                case "TN":
                    result = new byte[] { 0x4E, 0x54 };
                    break;
                case "TS":
                    result = new byte[] { 0x53, 0x54 };
                    break;
                case "CN":
                    result = new byte[] { 0x53, 0x43 };
                    break;
                case "CS":
                    result = new byte[] { 0x4E, 0x54 };
                    break;
                default:
                    {
                        Log.LogError($"[{NetConfig.Name}][{IP}]：not supported");
                        throw new NotSupportedException("not supported");
                    }

            }
            return result;
        }
        public byte[] getCmd(Fx3MCCommandType MCCmd, string address, byte length, params byte[] data)
        {
            //example:
            //read D454:
            //01FF000A 4420 000001C6 01 00
            string pattern = @"^([A-Z]{1,2})(\d{1,4})$";
            Match match = Regex.Match(address, pattern);
            if (!match.Success || match.Groups.Count != 3)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}]：[{address}] address invalid");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：address invalid");
            }

            string deviceCodeStr = match.Groups[1].ToString();
            string deviceAddressStr = match.Groups[2].ToString();
            UInt32 deviceAddress = 0;
            if (!UInt32.TryParse(deviceAddressStr, out deviceAddress))
            {
                Log.LogError($"[{NetConfig.Name}][{IP}]：parse address failed：{deviceAddressStr}");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：parse address failed");
            }

            byte[] deviceCodeBytes = getDevcieCode(deviceCodeStr);

            StringBuilder sb = new StringBuilder();
            byte subheader = (byte)MCCmd;
            sb.Append(subheader.ToString("X2"));  //subheader
            sb.Append("FF000A");  //pc no, monitoring timer

            sb.Append(deviceCodeBytes[1].ToString("X2"));   //Device Code 2bytes 0x4420
            sb.Append(deviceCodeBytes[0].ToString("X2"));
            sb.Append(deviceAddress.ToString("X8"));    //Head device 4 bytes  0x000001C6
            sb.Append(length.ToString("X2"));
            sb.Append("00");    //fixed 0x00

            //Data for the number of designed device points
            if (data != null && data.Length > 0)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    sb.Append(data[i].ToString("X2"));
                }
            }
            var cmd = Encoding.ASCII.GetBytes(sb.ToString());
            Log.LogDebug($"[{NetConfig.Name}][{IP}]：exc cmd code :{string.Join(" ", cmd)}.");
            return cmd;
        }

        private void checkReadResult(byte[] cmd, byte[] result)
        {
#if DEBUG
            Log.LogDebug($"[{NetConfig.Name}][{IP}]：checkReadResult :{string.Join(" ", cmd)} .\r\n result code :{string.Join(" ", result)}");
#endif
            if (result.Length < 2)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] error :{ByteToHexString(result)}");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：error:" + ByteToHexString(result));
            }
            byte cmdByte = Convert.ToByte(Encoding.ASCII.GetString(cmd).Substring(0, 2));
            if (result[0] != cmdByte + 0x80 || result[1] != 0x00)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] communication error :{ByteToHexString(result)}");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：communication error:" + ByteToHexString(result));
            }
            var match = cmdByte == 0x00 || cmdByte == 0x01 || cmdByte == 0x15 || cmdByte == 0x16;
            if (match && result.Length < 3)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] communication error :{ByteToHexString(result)}");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：communication error:" + ByteToHexString(result));
            }
        }

        private IResult<byte[]> ReadRaw(Fx3MCCommandType type, string address, byte length)
        {
            byte[] cmd = getCmd(type, address, length);
            var result = base.Send(cmd);
            if (result.Success)
            {
                var dr = BitConverter.ToDouble(result.Data, 0);
#if DEBUG
                Log.LogDebug($"byte to ToDouble 0:{BitConverter.ToDouble(result.Data, 0)}");
                Log.LogDebug($"byte to ToDouble 1:{BitConverter.ToDouble(result.Data, 1)}");
                Log.LogDebug($"byte to ToDouble 2:{BitConverter.ToDouble(result.Data, 2)}");

                Log.LogDebug($"byte to ToUInt32 0:{BitConverter.ToUInt32(result.Data, 0)}");
                Log.LogDebug($"byte to ToUInt32 1:{BitConverter.ToUInt32(result.Data, 1)}");
                Log.LogDebug($"byte to ToUInt32 2:{BitConverter.ToUInt32(result.Data, 2)}");

                Log.LogDebug($"byte to ToInt16 0:{BitConverter.ToInt16(result.Data, 0)}");
                Log.LogDebug($"byte to ToInt16 1:{BitConverter.ToInt16(result.Data, 1)}");
                Log.LogDebug($"byte to ToInt16 2:{BitConverter.ToInt16(result.Data, 2)}");
#endif
                result.Data = HexArrayToByteArray(result.Data);
                checkReadResult(cmd, result.Data);
            }
            return result;
        }
        private IResult<byte[]> WriteRaw(Fx3MCCommandType type, string address, byte length, params byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] value not exists");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：value not exists");
            }
            byte[] cmd = getCmd(type, address, length, value);
            var result = base.Send(cmd);
            if (result.Success)
            {
                result.Data = HexArrayToByteArray(result.Data);
                checkReadResult(cmd, result.Data);
            }
            return result;
        }
        #endregion

        #region  implement

        public override ushort[] ReadUInt16(string address, byte length)
        {
            UInt16[] data = new UInt16[length];
            var result = ReadRaw(Fx3MCCommandType.ReadByByte, address, length);
            byte[] buffer = result.Data;
            for (int i = 0; i < length; i++)
            {
                data[i] = (ushort)(buffer[2 + i * 2] * 0x100 + buffer[2 + i * 2 + 1]);
            }
            return data;
        }

        public Double[] ReadDouble(string address, byte length)
        {
            Double[] data = new Double[length];
            var result = ReadRaw(Fx3MCCommandType.ReadByByte, address, length);
            byte[] buffer = result.Data;
            for (int i = 0; i < length; i++)
            {
                data[i] = (ushort)(buffer[2 + i * 2] * 0x100 + buffer[2 + i * 2 + 1]);
            }
            var data2 = BitConverter.ToDouble(buffer, 0);
            return data;
        }

        public override byte[] ReadBit(string address, byte length)
        {
            var result = ReadRaw(Fx3MCCommandType.ReadByBit, address, length);
            byte[] data = result.Data;
#if DEBUG
            Log?.LogDebug($"设备[{NetConfig.Name}] ReadBit address:{address},length:{length},\r\n result:{data}");
#endif
            byte[] ret = new byte[length];

            for (int i = 0; i < ret.Length; i += 2)
            {
                ret[i] = (byte)((data[2 + i / 2] & 0x10) >> 4);
                if (i + 1 >= length)
                {
                    break;
                }
                ret[i + 1] = (byte)(data[2 + i / 2] & 0x01);
            }
            return ret;
        }

        public override bool WriteUInt16(string address, params UInt16[] value)
        {
            if (value == null || value.Length == 0)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] value not exists");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：value not exists");
            }
            byte[] buffer = new byte[value.Length * 2];
            for (int i = 0; i < value.Length; i += 2)
            {
                Array.Copy(BitConverter.GetBytes(value[i]).Reverse().ToArray(), 0, buffer, i * 2, 2);
            }
#if DEBUG
            Log?.LogDebug($"设备[{NetConfig.Name}] WriteUInt16 address:{address},value:{value}");
#endif
            var result = WriteRaw(Fx3MCCommandType.WriteByByte, address, (byte)value.Length, buffer).Data;
#if DEBUG
            Log?.LogDebug($"设备[{NetConfig.Name}] WriteUInt16 address:{address},value:{value},\r\n result:{result}");
#endif
            return result[result.Length - 1] == 0x00;
        }

        public override bool WriteBool(string address, bool value)
        {
#if DEBUG
            Log?.LogDebug($"设备[{NetConfig.Name}] WriteBool address:{address},value:{value}");
#endif
            byte[] result = WriteRaw(Fx3MCCommandType.WriteByBit, address, 1, value ? (byte)0x10 : (byte)0x00).Data;
#if DEBUG
            Log?.LogDebug($"设备[{NetConfig.Name}] WriteBool address:{address},value:{value},\r\n result:{result}");
#endif
            return result[1] == 0x00;
        }


        public override bool WriteWidth(string address, double value)
        {
            throw new NotImplementedException();
        }

        public override bool WriteWidth(string address, ushort value)
        {
            throw new NotImplementedException();
        }

        public override ushort ReadWidth16(string address, byte length)
        {
            throw new NotImplementedException();
        }

        public override uint ReadWidth32(string address, byte length)
        {
            throw new NotImplementedException();
        }

        public override ulong ReadWidth64(string address, byte length)
        {
            throw new NotImplementedException();
        }


        #endregion
    }
}
