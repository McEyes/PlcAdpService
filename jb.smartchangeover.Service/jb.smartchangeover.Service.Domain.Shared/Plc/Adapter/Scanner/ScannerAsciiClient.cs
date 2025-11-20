using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using jb.smartchangeover.Service.Domain.Shared.Commons;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;
using jb.smartchangeover.Service.Domain.Shared.Plc.Adapter.Modbus;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;

using Microsoft.Extensions.Logging;
using MQTTnet.Server;
using Newtonsoft.Json;

using NModbus;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public class ScannerAsciiClient : PlcClient
    {

        private const int DefaultTcpConnectionTimeoutMilliseconds = 5 * 1000;
        private byte slaveId = 1;
        /// <summary>
        /// 失败次数，没失败一次，重连时间翻倍
        /// </summary>
        private int ConnRetries = 0;
        private DateTime LastConnTime = DateTime.Now;

        private static object sendlock = new object();



        public ScannerAsciiClient(IEquipmentConfig config, MqttClientService mqttClient, ILogger log) : base(config, mqttClient, log)
        {
        }



        public override IAsyncResult Open(AsyncCallback cb = null)
        {
            _cts = new CancellationTokenSource();
            var result = base.Open(cb);
            if (!IsListening)
            {
                Task.Factory.StartNew(() =>
                {
                    StartReceive();
                });
            }
            return result;
        }


        public override IResult<string> Send(params string[] data)
        {
            if (data.Length < 1) return new Result<string>() { Success = false, Msg = $"[{NetConfig.Name}][{NetConfig.Ip}]命令错误:{data.JoinAsString("&")}" };
            data[0] = ConvertPlcCmdToScannerCmd(data[0]);
            if (data[0].IsNullOrWhiteSpace())
            {
                return new Result<string>() { Success = false, Msg = $"[{NetConfig.Name}][{NetConfig.Ip}]不支持当前命令:{data.JoinAsString("&")}" };
            }
            var cmdList = new List<string>
            {
                ScannerDeviceType.TcpCommand
            };
            cmdList.AddRange(data);
            lock (sendlock)
            {
                return SendCmd(cmdList.ToArray());
            }
        }


        private static object sendLockObj = new object();
        public virtual IResult<string> SendCmd(params string[] data)
        {
            IResult<string> resultData = new Result<string>();
            var cmd = data.JoinAsString("&");
#if DEBUG2
            Log.LogDebug($"send data string: {cmd}");
#endif 
            if (data.Length < 2) return resultData;
            var cmdbuffer = Encoding.UTF8.GetBytes(cmd);
            //lock (sendLockObj)
            //{
            CmdCacheItem cmdCache = new CmdCacheItem(data[1], data[1]);
            var flag = !ScannerDeviceType.GetMachineState.Equals(data[1], StringComparison.InvariantCultureIgnoreCase);
            if (flag)
            {
                this.SendQueue.Push(cmdCache);
                Log.LogDebug($"send cmd : {cmd}");
            }
            var result = SendByte(cmdbuffer);
            // Log.LogDebug($"[{data[1]}]wait recevie result : {cmdCache.ReceiveEventArgs?.ReceiveText}\r\n\r\n ");
            if (flag)
            {
                cmdCache.ResetEvent.WaitOne(1000);
                Log.LogDebug($"[{data[1]}]wait recevie result : {cmdCache.ReceiveEventArgs?.ToJSON()}\r\n\r\n ");
                if (cmdCache.ReceiveEventArgs != null && cmdCache.ReceiveEventArgs.ListData.Any(f => f.SourceId == data[1]))
                {
                    var cmdresult = cmdCache.ReceiveEventArgs.ListData.FirstOrDefault(f => f.SourceId == data[1]);
                    cmdresult.Code = EquipmentErrorCode.OK.IntValue();
                    resultData.AddError(cmdresult);
                }
                else
                {
                    Log.LogDebug($"[{NetConfig.Name}][{IP}][{data[1]}]执行超时。{cmdCache.ReceiveEventArgs?.ReceiveText}\r\n\r\n");
                    CmdCacheItem precmd = null;
                    if (this.CmdQueue.TryPop(out precmd) && precmd.Cmd != data[1])
                    {
                        Log.LogDebug($"[{NetConfig.Name}][{IP}][{data[1]}]不是当前命令回复，push回去{precmd.ToJSON()}\r\n\r\n");
                        CmdQueue.Push(precmd);
                    }
                    ErrorCode = EquipmentErrorCode.CommandTimeout;
                    resultData.Code = (int)DeviceCommandStatus.Timeout;
                    resultData.SetError(ErrorCode);
                    if (IsListening == true)
                    {
                        IsListening = false;
                    }
                }
            }
            else
                resultData.AddError(result);
            return resultData;
        }


        public IResult<byte[]> SendByte(byte[] data)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<byte[]>();
            result.Data = new byte[0];
            if (!IsConnected && !IcmpCheck.Check(IP))
            {
                EquipmentStatus = EquipmentStatus.Disconnect;
                ErrorCode = EquipmentErrorCode.Disconnect;
                result.SetError(ErrorCode);
                result.SetError($"[{NetConfig.Name}][{IP}] Device is offline.");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Device is offline.");
                return result;
            }
            long wait = 0;
            int waitTick = 50;
            var iar = Open();
            var cts = _cts.Token;
            while (iar.IsCompleted && !IsConnected && wait < NetConfig.ConnectTimeout && !cts.IsCancellationRequested)
            {
                Thread.Sleep(waitTick);
                wait += waitTick;
            }
            if (!IsConnected)
            {
                result.SetError($"[{NetConfig.Name}][{IP}] Device IsConnected :{IsConnected}.");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Device  IsConnected :{IsConnected}.");
                return result;
            }

            try
            {
                var sendbyte = TCPSocket.Send(data);
                // Log?.LogDebug($"[{NetConfig.Name}][{IP}] Send 实际发送字节数量:{sendbyte}, 耗时：{watch.ElapsedMilliseconds}毫秒");
                if (sendbyte == 0)
                {
                    if (CmdExecStatus == DeviceCommandStatus.Executing)
                        CmdExecStatus = DeviceCommandStatus.Failed;
                    ErrorCode = EquipmentErrorCode.CommandFailed;
                    result.SetError(ErrorCode);
                    result.SetError($"[{NetConfig.Name}][{IP}] Send 实际发送字节数量0,发送失败！");
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (CmdExecStatus == DeviceCommandStatus.Executing)
                    CmdExecStatus = DeviceCommandStatus.Failed;
                ErrorCode = EquipmentErrorCode.UnknownError;
                result.SetError(ErrorCode);
                result.SetError($"[{NetConfig.Name}][{IP}] Send data error:{ex.Message}");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Send error:{ex.Message},\r\n{ex.StackTrace}, 耗时：{watch.ElapsedMilliseconds}毫秒");
                if (ex.Message.Contains("你的主机中的软件中止了一个已建立的连接"))
                {
                    result.SetError($"[{NetConfig.Name}][{IP}] tcp close");
                    result.SetError($"[{NetConfig.Name}][{IP}] tcp close");
                    Close();
                }
                return result;
            }
            return result;
        }


        public ConcurrentStack<CmdCacheItem> SendQueue { get; set; } = new ConcurrentStack<CmdCacheItem>();
        public delegate void ReceiveEventHandler(INetClient? netClient, ReceiveEventArgs e);
        public virtual event ReceiveEventHandler ReceiveHandler;
        public bool IsListening = false;
        private static object startReceiveLockObj = new Object();
        public void StartReceive()
        {
            lock (startReceiveLockObj)
            {
                if (IsListening) return; IsListening = true;
            }
            int waitTick = 10;
            var cts = _cts.Token;
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        if (TCPSocket != null && TCPSocket.Connected && TCPSocket.Available > 0)
                        {
                            byte[] buffer = new byte[TCPSocket.Available];
                            var reLen = TCPSocket.Receive(buffer);
                            var args = new ReceiveEventArgs(buffer);
                            if (args.ListData.Length > 1) Log?.LogError($"[{NetConfig.Name}][{IP}] Receive [{args.Cmd}] Data:{args.ToJSON()}");
                            if (args.Cmd == "GetMachineState")
                            {
                                var statusData = args.ListData.FirstOrDefault(f => f.SourceId == args.Cmd);
                                if (statusData != null)
                                {
                                    // Log?.LogError($"[{NetConfig.Name}][{IP}] 收到状态数据:{statusData.ToJSON()}");
                                    UpdateState(statusData);
                                }
                            }
                            else
                            {
                                Log?.LogError($"[{NetConfig.Name}][{IP}] Receive [{args.Cmd}] Data:{args.ReceiveText}");
                                if (args.Code == EquipmentErrorCode.Executing) CmdExecStatus = (DeviceCommandStatus)(args.Code);

                                //Log?.LogError($"[{NetConfig.Name}][{IP}] Receive Data:{string.Join(' ', buffer)}");
                                if (SendQueue.TryPop(out CmdCacheItem cmdInfo))
                                {
                                    var cmdData = args.ListData.FirstOrDefault(f => f.SourceId == args.Cmd);
                                    if (args.Cmd == cmdInfo.Cmd && cmdData != null)
                                    {
                                        cmdInfo.ReceiveEventArgs = args;
                                        cmdInfo.ResetEvent.Set();
                                        cmdInfo.ResetEvent.Reset();
                                        //回复命令
                                        //Log?.LogError($"[{NetConfig.Name}][{IP}] Receive [{cmdInfo.Cmd}] Data:{cmdData.ToJSON()}");
                                    }
                                    //Log?.LogError($"[{NetConfig.Name}][{IP}] Receive [{cmdInfo.Cmd}] Data:{string.Join(' ', buffer)}");
                                }
                            }
                            if (ReceiveHandler != null)
                            {
                                ReceiveHandler(this, args);
                            }
                        }
                        Task.Delay(waitTick).Wait();
                    }
                    catch (Exception ex)
                    {
                        Log?.LogError($"[{NetConfig.Name}][{IP}] Receive error:{ex.Message},\r\n{ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[{NetConfig.Name}][{IP}] Start Receive Error:{ex.Message},\r\n{ex.StackTrace}");
            }
            finally
            {
                Log?.LogError($"[{NetConfig.Name}][{IP}] end Receive2 ");
                lock (startReceiveLockObj)
                {
                    IsListening = false;
                }
            }
        }


        public class ReceiveEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="data"></param>
            public ReceiveEventArgs(byte[] data)
            {
                ReceiveText = Encoding.UTF8.GetString(data);
                var result = ScannerAsciiHelper.CheckResult(ReceiveText);
                Cmd = result.cmd;
                ListData = result.data.ToArray();
                ReceiveData = data;
                Success = ListData[0].Success;
                Code = (EquipmentErrorCode)ListData[0].Code;
                ErrorMsg = ListData[0].Msg;
            }

            public virtual string Cmd { get; }
            public virtual string ReceiveText { get; }
            public virtual byte[] ReceiveData { get; }
            public bool Success { get; set; }
            public EquipmentErrorCode Code { get; set; }
            public string ErrorMsg { get; set; }
            public IResult<string>[] ListData { get; set; }
        }


        public override void ReadEquipmentState(byte length = 50)
        {
            Send(ScannerDeviceType.GetMachineState);
            return;
            // Stopwatch watch = new Stopwatch();
            // watch.Start();
            // try
            // {
            //     //Log.LogDebug($"执行读取设备[{NetConfig.Name}]运行状态:ReadEquipmentStateScanner");
            //     var result = Send(ScannerDeviceType.GetMachineState);
            //     //UpdateState(result);
            //     //if (hasDataChange && base.PropertyChanged != null)
            //     //{
            //     //    PropertyChanged(this, new PropertyChangedEventArgs("all"));
            //     //}
            // }
            // catch (Exception ex)
            // {
            //     Log.LogError($"读取设备[{NetConfig.Name}]运行状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
            // }
            // finally
            // {
            //     watch.Stop();
            //     // disablePropertyChanged = false;
            //     // hasDataChange = false;
            // }
            // return;
        }

        private void UpdateState(IResult<string> result)
        {
            hasDataChange = false;
            var runStatus = 0;
            if (result.Success && !result.Data.IsNullOrWhiteSpace())
            {
                try
                {
                    var scannerState = JsonConvert.DeserializeObject<ScannerMachineState>(result.Data);
                    if (scannerState != null)
                    {
                        runStatus = scannerState.Status;
                        HasPcb = scannerState.HasPcb;
                        PcbNum = scannerState.PcbNum;
                        IsNeedPcb = scannerState.IsNeedPcb;
                        RealWidth = SetWidth = scannerState.dPcbWidth;
                        RealWidthLane2 = SetWidthLane2 = scannerState.dPcbHeight;
                        CurrModel = scannerState.model;
                        CmdExecStatus = (DeviceCommandStatus)scannerState.cmdStatus;
                        // hasDataChange=true;
                        // Log.LogError($"读取设备[{NetConfig.Name}]更新状态成功:{CmdExecStatus}");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError($"读取设备[{NetConfig.Name}]运行状态失败,返回结果无法格式化:{result.ToJSON()},\r\n{ex.Message},\r\n{ex.StackTrace}");
                }
            }
            else
            {
                Log.LogError($"读取设备[{NetConfig.Name}]运行状态失败 data is empty:{result.ToJSON()}");
                return;
            }
#if DEBUG2
                Log.LogError($"读取设备[{NetConfig.Name}]运行状态:{runStatus},{(EquipmentStatus)runStatus}");
#endif
            if (runStatus == 0) runStatus = (int)EquipmentStatus.Disconnect;//统一定义等于0定义为没拿到状态，表示断开
            EquipmentStatus = (EquipmentStatus)runStatus;
            RunPath = 1;

            TriggerPropertyHandler();
            CheckCmdExecStatus();
            disablePropertyChanged = false;
            hasDataChange = false;
        }


        #region private methods
        private string getDevcieCode(string cmd, string data)
        {
            var result = new List<string>
            {
                ScannerDeviceType.TcpCommand,
                cmd,
                data
            };
            return result.Join("&");
        }
        /// <summary>
        /// 组装命令
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] getCmd(string cmd, string data)
        {
            if (cmd.IsNullOrWhiteSpace())
            {
                Log.LogError($"[{NetConfig.Name}][{IP}]：command error [{cmd}] ");
                throw new NotSupportedException($"[{NetConfig.Name}][{IP}]：command error.");
            }
            byte[] deviceAddressBytes = Encoding.ASCII.GetBytes(getDevcieCode(cmd, data));

            return deviceAddressBytes;
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
            var data = Encoding.ASCII.GetString(cmdType);
            byte[] cmd = getCmd(address, data);
            var result = base.Send(cmd);
            if (result.Success)
                return result;
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
        private byte[] WriteRaw(string address, byte length, byte[] cmdType, params byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Log.LogError($"[{NetConfig.Name}][{IP}] value not exists");
                throw new ArgumentException($"[{NetConfig.Name}][{IP}]：value not exists");
            }
            var data = Encoding.ASCII.GetString(cmdType);
            byte[] cmd = getCmd(address, data);
            if (NetConfig.IsDebug == true)
            {
                Log.LogInformation($"[{NetConfig.Name}][{IP}]：debug WriteRaw [{address}] Hex:{ByteToHexString(cmd)} \r\n 0x:{BitConverter.ToString(cmd).Replace("-", " ")}");
                return new byte[] { 0x81, 0x00, 0x00, 0x00 };
            }
            byte[] result = base.Send(cmd).Data;
            return result;
        }
        #endregion


        /// <summary>
        /// GetCurrentProducts
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public override ushort[] ReadCore(string startAddress, byte length)
        {
            return new ushort[] { 0 };
            //return   Send(ConvertPlcCmdToScannerCmd(startAddress));
        }


        private string ConvertPlcCmdToScannerCmd(string cmd)
        {
            switch (cmd)
            {
                case PlcBufferRegister.Control:
                    return ScannerDeviceType.ControlMachine;
                default:
                    return cmd;
            }
        }

        #region  implement

        public override ushort[] ReadUInt16(string address, byte length)
        {
            return ReadCore(address, length);
        }

        public override byte[] ReadBit(string address, byte length)
        {
            return ModbusHelper.Ushorts2Bytes(ReadCore(address, length));
        }

        public override bool WriteUInt16(string address, params UInt16[] value)
        {
            return true;// ModbusHelper.GetBools(ReadAndWriteCore(address, ModbusHelper.Ushorts2Bytes(value)), 0, 1)[0];
        }

        public override bool WriteBool(string address, bool value)
        {
            return true;// ModbusHelper.GetBools(ReadAndWriteCore(address, BitConverter.GetBytes(value)), 0, 1)[0];
        }

        public override void WriteCore(string startAddress, byte[] values)
        {
            return;
        }

        public override ushort[] ReadAndWriteCore(string startAddress, byte[] values)
        {
            return new ushort[1] { 1 };
        }

        #endregion
    }
}
