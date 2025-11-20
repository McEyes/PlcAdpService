using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using Microsoft.Extensions.Logging;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using NModbus;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using jb.smartchangeover.Service.Domain.Shared.Plc.Adapter.Modbus;
using NModbus.Extensions.Enron;
using jb.smartchangeover.Service.Domain.Shared.Commons;
using System.Diagnostics;
using System.Threading;
using System.Net.Sockets;
using System.Net.Http;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;
using System.Reflection;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;
using Newtonsoft.Json.Linq;
using System.Reflection.Metadata.Ecma335;
using PlcLibrary;
using System.ComponentModel;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public class ModbusClient : PlcClient
    {
        private const int DefaultTcpConnectionTimeoutMilliseconds = 5 * 1000;
        private IModbusMaster plcMaster = null;
        private TcpClient tcpClient = null;
        private byte slaveId = 1;
        /// <summary>
        /// 失败次数，没失败一次，重连时间翻倍
        /// </summary>
        private int ConnRetries = 0;
        private DateTime LastConnTime = DateTime.Now;

        public override bool IsConnected
        {
            get
            {
                if (tcpClient == null) return false;
                return tcpClient.Connected;
            }
        }



        public ModbusClient(IEquipmentConfig config, MqttClientService mqttClient, ILogger log = null) : base(config, mqttClient, log)
        {
            ConnPlcMaster();
        }

        private void ConnPlcMaster(AsyncCallback cb = null)
        {
            try
            {
                if (!NetConfig.Enable)
                {
                    Log.LogError($"[{NetConfig.Name}][{NetConfig.Ip}:{NetConfig.Port}] TCP Modbus 设备停止监控 ");
                    return;
                }
                if (ConnRetries > 0 && LastConnTime.AddSeconds(NetConfig.GetRetriesTime(ConnRetries)) > DateTime.Now) return;
                if (tcpClient != null && !tcpClient.Connected)
                {
                    tcpClient.Close();
                    tcpClient.Dispose();
                    tcpClient = null;
                }
                if (tcpClient == null)
                {
                    tcpClient = new TcpClient
                    {
                        ReceiveTimeout = NetConfig.ReceiveTimeout,
                        SendTimeout = NetConfig.SendTimeout
                    };

                    if (NetConfig.ConnectTimeout <= 0)
                    {
                        NetConfig.ConnectTimeout = DefaultTcpConnectionTimeoutMilliseconds;
                    }

                    if (!tcpClient.ConnectAsync(NetConfig.Ip, NetConfig.Port).Wait(NetConfig.ConnectTimeout))
                    {
                        EquipmentStatus = Plc.Enums.EquipmentStatus.Disconnect;
                        ErrorCode = Plc.Enums.EquipmentErrorCode.Disconnect;
                        tcpClient.Dispose();
                        tcpClient = null;
                        Log.LogError($"[{NetConfig.Name}][{NetConfig.Ip}:{NetConfig.Port}] Timed out trying to connect to TCP Modbus device");
                        LastConnTime = DateTime.Now;
                        ConnRetries++;
                        return;
                    }
                    else
                    {
                        EquipmentStatus = Plc.Enums.EquipmentStatus.Runnling;
                        ErrorCode = Plc.Enums.EquipmentErrorCode.OK;
                    }
                    plcMaster = new ModbusFactory().CreateMaster(tcpClient);
                    ConnRetries = 0;
                }
            }
            catch (Exception ex)
            {
                ConnRetries++;
                EquipmentStatus = Plc.Enums.EquipmentStatus.Disconnect;
                ErrorCode = Plc.Enums.EquipmentErrorCode.Disconnect;
                Log.LogError($"[{NetConfig.Name}][{NetConfig.Ip}:{NetConfig.Port}]TCP Modbus device connect error:{ex.Message}\r\n{ex.StackTrace} ");
            }
        }

        public override IAsyncResult Open(AsyncCallback cb = null)
        {
            try
            {
                if (!IsConnected) ConnPlcMaster();
                return new AsyncResult(tcpClient, null, true, true);
            }
            catch (Exception e)
            {
                EquipmentStatus = Plc.Enums.EquipmentStatus.Disconnect;
                ErrorCode = Plc.Enums.EquipmentErrorCode.Disconnect;
                Log.LogError($"Modbus client open error :{e.Message}\r\n{e.StackTrace}");
            }
            return new AsyncResult(tcpClient, null, false, false);
        }

        public override void Close()
        {
            try
            {
                //base.Close();
                Closing = true;
                NetConfig.Enable = false;

                if (tcpClient != null)
                {
                    tcpClient.Close();
                    tcpClient.Dispose();
                    tcpClient = null;
                }
                if (plcMaster != null)
                {
                    plcMaster.Dispose();
                    plcMaster = null;
                }

                EquipmentStatus = EquipmentStatus.Disabled;
                ErrorCode = EquipmentErrorCode.Disconnect;
                Log?.LogWarning($"[{NetConfig.Name}][{IP}] TCPSocket Close to dispose.");
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[{NetConfig.Name}][{IP}] TCPSocket close error：{ex.Message}\r\n {ex.StackTrace}");
            }
            finally
            {
                Closing = false;
                NetConfig.Enable = true;
            }
        }

        public override IResult<byte[]> Send(byte[] data)
        {
            var result = new Result<byte[]>();
            return result;
        }


        public override ushort[] ReadCore(string startAddress, byte length)
        {
            return ReadCore(ToModbusAddress(startAddress), length);
        }


        public override ushort[] ReadAndWriteCore(string startAddress, byte[] values)
        {
            if (NetConfig.IsDebug == true)
            {
                Log.LogInformation($"[{NetConfig.Name}][{IP}]：debug exc [{startAddress}] Hex:{ByteToHexString(values)} \r\n 0x:{BitConverter.ToString(values).Replace("-", " ")}");
                return new ushort[] { 0x00 };//81000000,81009808
            }
            Open();
            if (IsConnected)
            {
                var address = ToModbusAddress(startAddress);
                plcMaster.WriteMultipleRegisters(slaveId, address, ModbusHelper.Bytes2Ushorts(values));
                var resData = plcMaster.ReadHoldingRegisters(slaveId, address, (ushort)values.Length);
                var resBytes = ModbusHelper.Ushorts2Bytes(resData);

                //Log.LogInformation($"[{NetConfig.Name}][{IP}]：debug exc [{startAddress}] Hex:{ByteToHexString(values)} \r\n 0x:{BitConverter.ToString(values).Replace("-", " ")}");
                //Log.LogInformation($"[{NetConfig.Name}][{IP}]：debug exc [{startAddress}] result short:{string.Join(',', resData)}");
                //Log.LogInformation($"[{NetConfig.Name}][{IP}]：debug exc [{startAddress}] result Hex:{ByteToHexString(resBytes)} \r\n 0x:{BitConverter.ToString(resBytes).Replace("-", " ")}");
                //if (startAddress != PlcBufferRegister.CmdExecStatus)
                //    return CheckBytes(values, resBytes);
                return new ushort[] { 0x01 };
            }
            else
            {
                EquipmentStatus = Plc.Enums.EquipmentStatus.Disconnect;
                ErrorCode = Plc.Enums.EquipmentErrorCode.Disconnect;
                Log?.LogError($"[{NetConfig.Name}][{IP}] IsConnected：{IsConnected}，EquipmentStatus：{EquipmentStatus}.");
                return new ushort[] { 0x00 };
            }
        }

        private ushort[] CheckBytes(byte[] data1, byte[] data2)
        {
            for (var i = 0; i < data1.Length; i++)
            {
                if (data1[i] != data2[i])
                {
                    Log.LogError($"[{NetConfig.Name}][{IP}]：read is not same----------------");
                    return new ushort[] { 0x00 };
                }
            }

            Log.LogError($"[{NetConfig.Name}][{IP}]：read is same       ===========");
            return new ushort[] { 0x01 };
        }

        protected ushort[] ReadCore(ushort startAddress, ushort numberOfPoints)
        {
            Open();
            if (IsConnected)
            {
                try
                {
                    return plcMaster.ReadHoldingRegisters(slaveId, startAddress, numberOfPoints);
                }
                catch (Exception ex)
                {
                    EquipmentStatus = Plc.Enums.EquipmentStatus.UnknownError;
                    ErrorCode = Plc.Enums.EquipmentErrorCode.UnknownError;
                    Log.LogError($"[{NetConfig.Name}][{IP}]：ReadCore [{startAddress}] write data:{numberOfPoints} errror:{ex.Message}\r\n{ex.StackTrace}");
                    if (ex.Message.Contains("Response was not of expected transaction ID"))
                    {
                        Log?.LogError($"[{NetConfig.Name}][{IP}] 网络不稳定，信息错位，关闭链接重连.");
                        this.Close();
                    }
                }
            }
            else
            {
                EquipmentStatus = Plc.Enums.EquipmentStatus.Disconnect;
                ErrorCode = Plc.Enums.EquipmentErrorCode.Disconnect;
                Log?.LogError($"[{NetConfig.Name}][{IP}] IsConnected：{IsConnected}，EquipmentStatus：{EquipmentStatus}.");
            }
            return new ushort[numberOfPoints];
        }

        public override void WriteCore(string startAddress, byte[] values)
        {
            if (NetConfig.IsDebug == true)
            {
                Log.LogInformation($"[{NetConfig.Name}][{IP}]：debug exc [{startAddress}] Hex:{ByteToHexString(values)} \r\n 0x:{BitConverter.ToString(values).Replace("-", " ")}");
                return;//81000000,81009808
            }
            Open();
            if (IsConnected)
                plcMaster.WriteMultipleRegisters(slaveId, ToModbusAddress(startAddress), ModbusHelper.Bytes2Ushorts(values));
            else
            {
                EquipmentStatus = Plc.Enums.EquipmentStatus.Disconnect;
                ErrorCode = Plc.Enums.EquipmentErrorCode.Disconnect;
                Log?.LogError($"[{NetConfig.Name}][{IP}] IsConnected：{IsConnected}，EquipmentStatus：{EquipmentStatus}.");
            }
        }

        private ushort ToModbusAddress(string address)
        {
            string pattern = @"^([A-Z]{1,2})(\d{1,4})$";
            Match match = Regex.Match(address, pattern);
            if (!match.Success || match.Groups.Count != 3)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}]：[{address}] address invalid");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：address invalid");
            }

            string deviceAddressStr = match.Groups[2].ToString();
            ushort deviceAddress = 0;
            if (!ushort.TryParse(deviceAddressStr, out deviceAddress))
            {
                Log.LogError($"[{NetConfig.Name}][{IP}]：parse address failed：{deviceAddressStr}");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}][{deviceAddressStr}]：parse address failed");
            }
            return deviceAddress;
        }

        #region  implement

        public override ushort[] ReadUInt16(string address, byte length)
        {
            return ReadCore(ToModbusAddress(address), length);
        }

        public override byte[] ReadBit(string address, byte length)
        {
            return ModbusHelper.Ushorts2Bytes(ReadCore(ToModbusAddress(address), length));
        }

        public override bool WriteUInt16(string address, params UInt16[] value)
        {
            var result = ReadAndWriteCore(address, ModbusHelper.Ushorts2Bytes(value));
            return true;
        }

        public override bool WriteBool(string address, bool value)
        {
            return ModbusHelper.GetBools(ReadAndWriteCore(address, BitConverter.GetBytes(value)), 0, 1)[0];
        }

        #endregion

        public override void ReadEquipmentState(byte length = 50)
        {
            try
            {
                hasDataChange = false;
                var startAddress = PlcBufferRegister.Change;
                var iStartAddress = startAddress.ToModbusAddress();

                ushort[] data = ReadCore(PlcBufferRegister.Change, length);
                if (data != null && data.Any(f => f > 0))
                {
                    disablePropertyChanged = true;
                    var status = data[PlcBufferRegister.RunStatus.ToModbusAddress() - iStartAddress];
#if DEBUG2
                    Log.LogError($"读取设备[{NetConfig.Name}]运行状态:{status},{(EquipmentStatus)status}");
#endif
                    if (status == 0)
                    {
                        EquipmentStatus = EquipmentStatus.Disconnect;// EquipmentStatus.Runnling;
                        Log.LogError($"读取设备[{NetConfig.Name}]实际状态为:{status},{(EquipmentStatus)status}，强制标记为掉线状态:{EquipmentStatus}");
                    }
                    else EquipmentStatus = (EquipmentStatus)status;
                    CmdExecStatus = (DeviceCommandStatus)ReadCore(PlcBufferRegister.CmdExecStatus, 1)[0];
                    SetWidth = data[PlcBufferRegister.Change.ToModbusAddress() - iStartAddress] / (decimal)10;
                    PcbNum = data[PlcBufferRegister.Counter.ToModbusAddress() - iStartAddress];
                    HasPcb = data[PlcBufferRegister.HasPanel.ToModbusAddress() - iStartAddress];
                    RunPath = data[PlcBufferRegister.RunPath.ToModbusAddress() - iStartAddress];
                    RealWidth = data[PlcBufferRegister.RealWidth.ToModbusAddress() - iStartAddress] / (decimal)10;
                    IsDoorOpen = data[PlcBufferRegister.IsDoorOpen.ToModbusAddress() - iStartAddress];
                    IsNeedPcb = data[PlcBufferRegister.NeedPanel.ToModbusAddress() - iStartAddress];

                    disablePropertyChanged = false;
                }
                TriggerPropertyHandler();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Response was not of expected transaction ID"))
                {
                    Log?.LogError($"[{NetConfig.Name}][{IP}] 网络不稳定，信息错位，关闭链接重连.");
                    this.Close();
                }
                Log?.LogError($"[{NetConfig.Name}][{IP}] ReadInfo Error: {ex.Message}\r\n{ex.StackTrace}");
            }
            finally
            {
                disablePropertyChanged = false;
                hasDataChange = false;
                CheckCmdExecStatus();
            }
        }
    }
}
