using System;
using System.Text.RegularExpressions;
using jb.smartchangeover.Service.Domain.Shared.Commons;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;
using jb.smartchangeover.Service.Domain.Shared.Plc.Adapter.Modbus;
using Microsoft.Extensions.Logging;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public class FX5DBinaryClient : PlcClient
    {
        public FX5DBinaryClient(IEquipmentConfig config, MqttClientService mqttClient, ILogger log = null) : base(config, mqttClient, log)
        {

        }

        #region private methods
        private byte[] getDevcieCode(string device)
        {
            byte[] result = null;
            switch (device)
            {
                case "D":
                    result = new byte[] { 0x00, 0xA8 };
                    break;
                case "M":
                    result = new byte[] { 0x00, 0x90 };
                    break;
                default:
                    {
                        Log.LogError($"[{NetConfig.Name}][{IP}]：not supported");
                        throw new NotSupportedException($"[{NetConfig.Name}][{IP}]：not supported");
                    }

            }
            return result;
        }
		/// <summary>
		/// 组装命令
		/// </summary>
		/// <param name="address"></param>
		/// <param name="length"></param>
		/// <param name="cmdType"></param>
		/// <param name="data"></param>
		/// <returns></returns>
        public byte[] getCmd(string address, byte length, byte[] cmdType, params byte[] data)
        {
            if ("D900".Equals(address, StringComparison.InvariantCultureIgnoreCase))
            {//GE 的AE-1628机器的D900被占用，改为D502作为命令执行状态反馈
                address = "D502";
            }
            if (cmdType == null || cmdType.Length != 4)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}]：command error [{cmdType}] ");
                throw new NotSupportedException($"[{NetConfig.Name}][{IP}]：command error.");
            }
            //example:
            //read D454:
            //01FF0A00C601000020440100
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

            int bufferLength = 21;
            if (data != null && data.Length > 0)
            {
                bufferLength = bufferLength + data.Length;
            }

            byte[] cmd = new byte[bufferLength];
            cmd[0] = 0x50;  //Subheader
            cmd[1] = 0x00;
            cmd[2] = 0x00;  //network no
            cmd[3] = 0xFF;  //plc no
            cmd[4] = 0xFF;  //target model IO no
            cmd[5] = 0x03;
            cmd[6] = 0x00;  //target model station no
            cmd[7] = (byte)((bufferLength - 9) % 256);  //request data length
            cmd[8] = (byte)((bufferLength - 9) / 256);
            cmd[9] = 0x0A;  //cpu monitor timer
            cmd[10] = 0x00;

            Array.Copy(cmdType, 0, cmd, 11, cmdType.Length);

            cmd[15] = deviceAddressBytes[0];           // start address
            cmd[16] = deviceAddressBytes[1];
            cmd[17] = deviceAddressBytes[2];
            cmd[18] = deviceCodeBytes[1];              // data to read

            cmd[19] = (byte)(length % 256);            // length
            cmd[20] = (byte)(length / 256);

            if (data != null && data.Length > 0)
            {
                Array.Copy(data, 0, cmd, 21, data.Length);
            }
//#if DEBUG2
//            //Log.LogDebug($"[{NetConfig.Name}][{IP}]：exc cmd Hex:{ByteToHexString(cmd)} \r\n 0x:{BitConverter.ToString(cmd).Replace("-", " ")}");
//#endif
            return cmd;
        }

		/// <summary>
		/// 判断返回结果
		/// </summary>
		/// <param name="cmd"></param>
		/// <param name="result"></param>
        private void checkReadResult(byte[] cmd, byte[] result)
        {
//#if DEBUG2
//            Log.LogDebug($"[{NetConfig.Name}][{IP}]：exc cmd Hex:{ByteToHexString(cmd)} \r\n 0x:{BitConverter.ToString(cmd).Replace("-", " ")}");
//#endif
            if (result.Length < 2)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] error :{ByteToHexString(result)}");
                //throw new ArgumentException($"[{NetConfig.Name}][{IP}]：error:" + ByteToHexString(result));
            }

            if (result[0] != cmd[0] + 0x80 || result[1] != 0x00)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] communication error :{ByteToHexString(result)}");
                //throw new ArgumentException($"[{NetConfig.Name}][{IP}]：communication error:" + ByteToHexString(result));
            }

            byte cmdByte = cmd[0];
            var match = cmdByte == 0x00 || cmdByte == 0x01 || cmdByte == 0x15 || cmdByte == 0x16;
            if (match && result.Length < 3)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] communication error :{ByteToHexString(result)}");
                //throw new ArgumentException($"[{NetConfig.Name}][{IP}]：communication error:" + ByteToHexString(result));
            }
        }
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="cmdType"></param>
        /// <returns></returns>
        private IResult<byte[]> ReadRaw(string address, byte length, byte[] cmdType)
        {
            byte[] cmd = getCmd(address, length, cmdType);
            var result = base.Send(cmd);
            if (result.Success)
                checkReadResult(cmd, result.Data);
            else
            {
                Log.LogWarning($"[{NetConfig.Name}][{IP}] [{address}] Read fail. \r\n result:{result.Msg}");
                result.Data = new byte[1] { 0 };
            }
            return result;
        }
        /// <summary>
        /// 写数据
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="cmdType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private IResult<byte[]> WriteRaw(string address, byte length, byte[] cmdType, params byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] value not exists");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：value not exists");
            }
            IResult<byte[]> result = new Result<byte[]>();
            byte[] cmd = getCmd(address, length, cmdType, value);
            if (NetConfig.IsDebug == true)
            {
                Log.LogInformation($"[{NetConfig.Name}][{IP}]：WriteRaw IsDebug [{address}] Hex:{ByteToHexString(cmd)} \r\n 0x:{BitConverter.ToString(cmd).Replace("-", " ")}");
                result.Data = new byte[] { 0x81, 0x00, 0x00, 0x00 };
                return result;
            }
            result = base.Send(cmd);
            if (result.Success)
                checkReadResult(cmd, result.Data);
            else
            {
                Log.LogError($"读取设备[{NetConfig.Name}] WriteRaw Error:{result.Msg}");
            }
            return result;
        }
        #endregion

        #region  implement

        public override ushort[] ReadUInt16(string address, byte length)
        {
            UInt16[] result = new UInt16[length];
            var buffer = ReadRaw(address, length, Fx5UmcCommandType.ReadByWord);
            for (int i = 0; i < length && buffer.Data.Length > 11 + i * 2; i++)
            {
                result[i] = BitConverter.ToUInt16(buffer.Data, 11 + i * 2);
            }

            return result;
        }

        public override ushort[] ReadCore(string startAddress, byte length)
        {
            return ReadUInt16(startAddress, length);
        }

        public override void WriteCore(string startAddress, byte[] values)
        {
            WriteRaw(startAddress, (byte)values.Length, values);
        }

        public override ushort[] ReadAndWriteCore(string startAddress, byte[] values)
        {
            var result = WriteRaw(startAddress, (byte)values.Length, values);
            if (result.Success)
                return ModbusHelper.Bytes2Ushorts(result.Data);
            return new ushort[] { 0 };
        }


        public override byte[] ReadBit(string address, byte length)
        {
            var result = ReadRaw(address, length, Fx5UmcCommandType.ReadByBit);
            int len = length;
            byte[] ret = new byte[len];
            if (result.Success)
            {
                for (int i = 0; i < ret.Length; i += 2)
                {
                    ret[i] = (byte)((result.Data[11 + i / 2] & 0x10) >> 4);
                    if (i + 1 >= len)
                    {
                        break;
                    }
                    ret[i + 1] = (byte)(result.Data[11 + i / 2] & 0x01);
                }
            }
            return ret;
        }

        public override bool WriteUInt16(string address, params UInt16[] value)
        {
            if (value == null || value.Length == 0)
            {
                throw new ArgumentException("value not exists");
            }
            byte[] buffer = new byte[value.Length * 2];
            for (int i = 0; i < value.Length; i++)
            {
                Array.Copy(BitConverter.GetBytes(value[i]), 0, buffer, i * 2, 2);
            }
            var result = WriteRaw(address, (byte)value.Length, Fx5UmcCommandType.WriteByWord, buffer);
            if (result.Success)
                return result.Data[result.Data.Length - 1] == 0x00;
            return false;
        }

        public override bool WriteBool(string address, bool value)
        {
            var result = WriteRaw(address, 1, Fx5UmcCommandType.WriteByBit, value ? (byte)0x10 : (byte)0x00);
            if (result.Success)
                return result.Data[result.Data.Length - 1] == 0x00;
            return false;
        }


        #endregion
    }
}
