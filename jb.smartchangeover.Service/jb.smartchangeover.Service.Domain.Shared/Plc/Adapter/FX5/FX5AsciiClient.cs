using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;
using jb.smartchangeover.Service.Domain.Shared.Plc.Adapter.Modbus;
using Microsoft.Extensions.Logging;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public class Fx5AsciiClient : PlcClient
    {
        public Fx5AsciiClient(IEquipmentConfig config, MqttClientService mqttClient, ILogger log = null) : base(config,mqttClient,log)
        {

        }



        private string getDevcieCode(string device)
        {
            string result = null;
            switch (device)
            {
                case "D":
                    result = "D*";
                    break;
                case "W":
                    result = "W*";
                    break;
                case "G":
                    result = "G*";
                    break;
                case "SW":
                    result = "SW";
                    break;
                case "M":
                    result = "M*";
                    break;
                case "L":
                    result = "L*";
                    break;
                case "F":
                    result = "F*";
                    break;
                case "V":
                    result = "V*";
                    break;
                case "B":
                    result = "B*";
                    break;
                case "S":
                    result = "S*";
                    break;
                case "R":
                    result = "R*";
                    break;
                case "ZR":
                    result = "ZR";
                    break;
                case "X":
                    result = "X*";
                    break;
                case "Y":
                    result = "Y*";
                    break;
                case "Z":
                    result = "Z*";
                    break;
                case "LZ":
                    result = "LZ";
                    break;
                case "TN":
                    result = "TN";
                    break;
                case "TS":
                    result = "TS";
                    break;
                case "TC":
                    result = "TC";
                    break;
                case "CN":
                    result = "CN";
                    break;
                case "CS":
                    result = "CS";
                    break;
                case "CC":
                    result = "CC";
                    break;
                case "SN":
                    result = "SN";
                    break;
                case "SS":
                    result = "SS";
                    break;
                case "SC":
                    result = "SC";
                    break;
                case "SB":
                    result = "SB";
                    break;

                case "SM":
                    result = "SM";
                    break;
                case "SD":
                    result = "SD";
                    break;
                default:
                    {
                        Log.LogError($"[{NetConfig.Name}][{IP}]：not supported");
                        throw new NotSupportedException($"[{NetConfig.Name}][{IP}]：not supported");
                    }

            }
            return result;
        }

        public string getCmd(string address, byte length, bool isBit)
        {
            string baseCmd = "500000FF03FF0000180010";
            StringBuilder sb = new StringBuilder();

            sb.Append(baseCmd);
            sb.Append("0401000");
            sb.Append(isBit ? "1" : "0");

            string pattern = @"^([A-Z]{1,2})(\d{1,4})$";
            Match match = Regex.Match(address, pattern);
            if (!match.Success || match.Groups.Count != 3)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}]：[{address}] address invalid");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：address invalid");
            }

            string deviceCodeStr = match.Groups[1].ToString();
            string deviceAddressStr = match.Groups[2].ToString();
            string deviceCodeBytes = getDevcieCode(deviceCodeStr);

            sb.Append(deviceCodeBytes);
            sb.Append(FixString(deviceAddressStr, 6));
            sb.Append(FixString(length.ToString(), 4));
            Log.LogDebug($"[{NetConfig.Name}][{IP}]：exc cmd code :{sb}.");
            return sb.ToString();
        }

        private byte[] ReadRaw(string address, byte length, bool isBit)
        {
            string cmdStr = getCmd(address, length, isBit);
            byte[] result = base.Send(Encoding.ASCII.GetBytes(cmdStr)).Data;
            result = HexArrayToByteArray(result);
            return result;
        }

        /// <summary>
        /// 写数据
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private byte[] WriteRaw(string address, byte length, params byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] value not exists");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：value not exists");
            }
            //byte[] cmd = getCmd(address, length, cmdType, value);
            byte[] result = new byte[length];// base.Send(cmd).Data;
            //checkReadResult(cmd, result);
            return result;
        }
        #region  implement

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
            byte[] result = WriteRaw( startAddress, (byte)values.Length, values);
            return ModbusHelper.Bytes2Ushorts(result);
        }

        public override byte[] ReadBit(string address, byte length)
        {
            byte[] result = ReadRaw(address, length, true);
            return result;
        }

        public override ushort[] ReadUInt16(string address, byte length)
        {
            throw new NotImplementedException();
        }

        public override bool WriteBool(string address, bool value)
        {
            throw new NotImplementedException();
        }

        public override bool WriteUInt16(string address, params UInt16[] value)
        {
            throw new NotImplementedException();
        }



        #endregion
    }
}
