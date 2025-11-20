using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using Microsoft.Extensions.Logging;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Plc.Adapter.Modbus;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;
using MQTTnet.Protocol;
using jb.smartchangeover.Service.Domain.Shared.Commons;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public class Fx3dBinaryClient : PlcClient
    {
        public Fx3dBinaryClient(IEquipmentConfig config, MqttClientService mqttClient, ILogger log = null) : base(config,mqttClient, log)
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
                        throw new NotSupportedException($"[{NetConfig.Name}][{IP}]：not supported");
                    }

            }
            return result;
        }
        public byte[] getCmd(Fx3MCCommandType MCCmd, string address, byte length, params byte[] data)
        {
            //example:
            //read D454:
            //01 FF 0A 00 C6 01 00 00 20 44 01(长度) 00 实际数据字节
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
            byte[] deviceAddressBytes = BitConverter.GetBytes(deviceAddress);
            byte[] deviceCodeBytes = getDevcieCode(deviceCodeStr);
            int bufferLength = 12;
            if (data != null && data.Length > 0)
            {
                bufferLength = bufferLength + data.Length;
            }

            byte[] cmd = new byte[bufferLength];
            cmd[0] = (byte)MCCmd;  //Subheader
            cmd[1] = 0xFF;  //pc no
            cmd[2] = 0x0A;  //Monitoring timer
            cmd[3] = 0x00;

            cmd[4] = deviceAddressBytes[0];  //Head device 4 bytes   // C6010000 D454
            cmd[5] = deviceAddressBytes[1];
            cmd[6] = deviceAddressBytes[2];
            cmd[7] = deviceAddressBytes[3];

            cmd[8] = deviceCodeBytes[0];  //Device Code 2bytes
            cmd[9] = deviceCodeBytes[1];

            cmd[10] = length;     // Number of device points
            cmd[11] = 0x00;     //fixed 0x00

            //Data for the number of designed device points
            if (data != null && data.Length > 0)
            {
                Array.Copy(data, 0, cmd, 12, data.Length);
            }
#if DEBUG2
            Log.LogDebug($"[{NetConfig.Name}][{IP}]：exc {MCCmd}[{address}] Hex:{ByteToHexString(cmd)} \r\n 0x:{BitConverter.ToString(cmd).Replace("-", " ")}");
#endif
            return cmd;
        }

        private void checkReadResult(string address, byte[] cmd, byte[] result)
        {
            if (result.Length < 2)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}][{address}] result leng<2, error :{ByteToHexString(result)}");
                //throw new ArgumentException("error:" + ByteToHexString(result));
            }

            if (result[0] != cmd[0] + 0x80 || result[1] != 0x00)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}][{address}] result 0, communication error :{ByteToHexString(result)}");
                //throw new ArgumentException($"[{NetConfig.Name}][{IP}]：communication error:" + ByteToHexString(result));
            }

            byte cmdByte = cmd[0];
            var match = cmdByte == 0x00 || cmdByte == 0x01 || cmdByte == 0x15 || cmdByte == 0x16;
            if (match && result.Length < 3)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}][{address}] result 0 or 1 or 15 or 16 and leng<3, communication error :{ByteToHexString(result)}");
                //throw new ArgumentException($"[{NetConfig.Name}][{IP}]：communication error:" + ByteToHexString(result));
            }
        }

        private IResult<byte[]> ReadRaw(Fx3MCCommandType type, string address, byte length)
        {
            byte[] cmd = getCmd(type, address, length);
            var result = base.Send(cmd);
            if (result.Success)
                checkReadResult(address,cmd, result.Data);
            else
            {
                Log.LogWarning($"[{NetConfig.Name}][{IP}] [{type}][{address}] Read fail. \r\n result:{result.Msg}");
                result.Data = new byte[1] { 0 };
            }
#if DEBUG2
            Log.LogDebug($"[{NetConfig.Name}][{IP}]：exc {type}[{address}] Hex:{ByteToHexString(cmd)} \r\n 0x:{BitConverter.ToString(cmd).Replace("-", " ")}\r\n result Hex:{ByteToHexString(result.Data)} \r\n 0x:{BitConverter.ToString(result.Data).Replace("-", " ")}");
#endif
            return result;
        }

        private byte[] WriteRaw(Fx3MCCommandType type, string address, byte length, params byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] value not exists");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：value not exists");
            }
            byte[] cmd = getCmd(type, address, length, value);
            byte[] result = base.Send(cmd).Data;
#if DEBUG2
            Log.LogDebug($"[{NetConfig.Name}][{IP}]：exc {type}[{address}] Hex:{ByteToHexString(cmd)} \r\n 0x:{BitConverter.ToString(cmd).Replace("-", " ")}\r\n result Hex:{ByteToHexString(result)} \r\n 0x:{BitConverter.ToString(result).Replace("-", " ")}");
#endif
            checkReadResult(address,cmd, result);
            return result;
        }
        #endregion

        #region  implement


        public override ushort ReadUInt16(string address)
        {
            return ReadUInt16(address, 1)[0];
        }


        /// <summary>
        /// 宽度读三十二位
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public override ushort[] ReadUInt16(string address, byte length)
        {
            //if(address== PlcBufferRegister.RealWidth)
            //{
            //    //ReadWidth32(address,length);
            //}
            UInt16[] result = new UInt16[length];
            var buffer = ReadRaw(Fx3MCCommandType.ReadByByte, address, length);
            for (int i = 0; i < length; i++)
            {
                result[i] = BitConverter.ToUInt16(buffer.Data, 2 + i * 2);
            }

            return result;
        }

        public override ushort[] ReadCore(string startAddress, byte length)
        {
            return ReadUInt16(startAddress, length);
        }

        public override void WriteCore(string startAddress, byte[] values)
        {
            WriteRaw(Fx3MCCommandType.WriteByByte, startAddress, (byte)values.Length, values);
        }

        public override ushort[] ReadAndWriteCore(string startAddress, byte[] values)
        {
            byte[] result = WriteRaw(Fx3MCCommandType.WriteByByte, startAddress, (byte)values.Length, values);
            return ModbusHelper.Bytes2Ushorts(result);
        }

        public override byte[] ReadBit(string address, byte length)
        {
            var result = ReadRaw(Fx3MCCommandType.ReadByBit, address, length);
            byte[] ret = new byte[length];
            if (result.Success)
            {
                for (int i = 0; i < ret.Length; i += 2)
                {
                    ret[i] = (byte)((result.Data[2 + i / 2] & 0x10) >> 4);
                    if (i + 1 >= length)
                    {
                        break;
                    }
                    ret[i + 1] = (byte)(result.Data[2 + i / 2] & 0x01);
                }
                return ret;
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
                Array.Copy(BitConverter.GetBytes(value[i]), 0, buffer, i * 2, 2);
            }
            byte[] result = WriteRaw(Fx3MCCommandType.WriteByByte, address, (byte)value.Length, buffer);
            return result[result.Length - 1] == 0x00;
        }

        public override bool WriteBool(string address, bool value)
        {
            byte[] result = WriteRaw(Fx3MCCommandType.WriteByBit, address, 1, value ? (byte)0x10 : (byte)0x00);
            return result[1] == 0x00;
        }

        #endregion
    }
}
