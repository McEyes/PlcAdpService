using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using jb.smartchangeover.Service.Domain.Shared.Commons;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;
using jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos;
using jb.smartchangeover.Service.Domain.Shared.Plc.Adapter.Modbus;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlcLibrary;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public abstract class PlcClient : NetClient, IDisposable
    {
        public new event StatusChangedEventHandler PropertyChanged;
        private MqttClientService mqttClient = null;
        private System.Timers.Timer _timer = null;
        /// <summary>
        /// 数据采集频率
        /// </summary>
        private int DataReadFreq = 1;
        private bool isTick = false;


        private int _HasPcb = 0;
        private int _PcbNum = 0;
        private int _IsNeedPcb = 0;
        private int _IsDoorOpen = 0;
        private decimal _SetWidth = 0;
        private decimal _RealWidth = 0;
        private decimal _SetWidthLane2 = 0;
        private decimal _RealWidthLane2 = 0;
        private int _RunPath = 0;
        private string _CurrModel = "";
        public string PrevModel = "";

        /// <summary>
        /// 是否有板:1有版，0无板
        /// </summary>
        public int HasPcb
        {
            get { return _HasPcb; }
            set
            {
                if (_HasPcb != value)
                {
                    _HasPcb = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("HasPcb"));
                }
            }
        }
        /// <summary>
        /// 过版数量
        /// </summary>
        public int PcbNum
        {
            get { return _PcbNum; }
            set
            {
                if (_PcbNum != value)
                {
                    _PcbNum = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("PcbNum"));
                }
            }
        }

        /// <summary>
        /// 是否要板
        /// </summary>
        public int IsNeedPcb
        {
            get { return _IsNeedPcb; }
            set
            {
                if (_IsNeedPcb != value)
                {
                    _IsNeedPcb = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("IsNeedPcb"));
                }
            }
        }
        /// <summary>
        ///  开盖：读取是否有人打开盖子,只读,
        /// </summary>
        public int IsDoorOpen
        {
            get { return _IsDoorOpen; }
            set
            {
                if (_IsDoorOpen != value)
                {
                    _IsDoorOpen = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("IsDoorOpen"));
                }
            }
        }
        /// <summary>
        ///  设计宽度
        /// </summary>
        public decimal SetWidth
        {
            get { return _SetWidth; }
            set
            {
                if (_SetWidth != value)
                {
                    _SetWidth = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("SetWidth"));
                }
            }
        }
        /// <summary>
        ///  读取实际宽度
        /// </summary>
        public decimal RealWidth
        {
            get { return _RealWidth; }
            set
            {
                if (_RealWidth != value)
                {
                    _RealWidth = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("RealWidth"));
                }
            }
        }
        /// <summary>
        ///  设计宽度
        /// </summary>
        public decimal SetWidthLane2
        {
            get { return _SetWidthLane2; }
            set
            {
                if (_SetWidthLane2 != value)
                {
                    _SetWidthLane2 = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("SetWidthLane2"));
                }
            }
        }
        /// <summary>
        ///  读取实际宽度
        /// </summary>
        public decimal RealWidthLane2
        {
            get { return _RealWidthLane2; }
            set
            {
                if (_RealWidthLane2 != value)
                {
                    _RealWidthLane2 = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("RealWidthLane2"));
                }
            }
        }
        /// <summary>
        ///  当前轨道传输方向
        /// </summary>
        public int RunPath
        {
            get { return _RunPath; }
            set
            {
                if (_RunPath != value)
                {
                    _RunPath = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("RealWidth"));
                }
            }
        }

        /// <summary>
        ///  当前轨道传输方向
        /// </summary>
        public string CurrModel
        {
            get { return _CurrModel; }
            set
            {
                if (_CurrModel != value)
                {
                    PrevModel = _CurrModel;
                    _CurrModel = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("CurrModel"));
                }
            }
        }

        protected PlcClient(IEquipmentConfig config, MqttClientService mqttClient, ILogger log) : base(config, log)
        {
            if (config.Ip == null)
            {
                throw new ArgumentNullException("ip");
            }

            if (!ValidateTcpPort(config.Port))
            {
                throw new ArgumentOutOfRangeException("port");
            }
            this.IP = config.Ip;
            this.Port = config.Port;
            this.DataReadFreq = config.DataReadFreq;
            this.mqttClient = mqttClient;
            _timer = new System.Timers.Timer();
            _timer.Elapsed += _timer_Elapsed;
            //IsConnected = false;
            base.PropertyChanged += PlcClient_PropertyChanged;
        }

        public void StartMonitor()
        {
            try
            {
                isTick = false;
                _timer.Interval = DataReadFreq * 1000;//采集心率,默认1秒钟采集一次
                _timer_Elapsed(null, null);
                _timer.Start();
            }
            catch (Exception ex)
            {
                Log.LogError("Init Timer Error：" + ex.Message + "\r\n" + ex.StackTrace, ex);
            }
        }

        public void StopMonitor()
        {
            try
            {
                _timer.Enabled = false;
                isTick = true;
                _timer.Stop();
            }
            catch (Exception ex)
            {
                Log.LogError("Init Timer Error：" + ex.Message + "\r\n" + ex.StackTrace, ex);
            }
        }


        /// <summary>
        /// 采集设备数据并上报
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!isTick)
            {
                isTick = true;
                try
                {
                    await Task.Run(() =>
                    {
                        ReadEquipmentState();
                    });
                }
                catch (Exception ex)
                {
                    Log.LogError($"ReadEquipmentState Error:{ex.Message} \r\n {ex.StackTrace}");
                    Log.LogException(ex);
                }
                finally
                {
                    isTick = false;
                }
            }
        }

        private void PlcClient_PropertyChanged(INetClient netClient, PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                this.PropertyChanged(this, e);
        }

        public bool ValidateTcpPort(int port)
        {
            if (port >= 0)
            {
                return port <= 65535;
            }

            return false;
        }

        #region 统一接口

        public List<EquipmentInformationDataParameter> GetChangeInfo()
        {
            var informations = new List<EquipmentInformationDataParameter>();
            informations.Add(new EquipmentInformationDataParameter() { Key = "PcbNum", Value = PcbNum.ToString() });
            informations.Add(new EquipmentInformationDataParameter() { Key = "HasPcb", Value = HasPcb.ToString() });
            informations.Add(new EquipmentInformationDataParameter() { Key = "IsNeedPcb", Value = IsNeedPcb.ToString() });
            informations.Add(new EquipmentInformationDataParameter() { Key = "IsDoorOpen", Value = IsDoorOpen.ToString() });
            informations.Add(new EquipmentInformationDataParameter() { Key = "SetWidthLane1", Value = SetWidth.ToString(), Unit = "mm" });
            informations.Add(new EquipmentInformationDataParameter() { Key = "WidthLane1", Value = RealWidth.ToString(), Unit = "mm" });
            informations.Add(new EquipmentInformationDataParameter() { Key = "RunPath", Value = RunPath.ToString() });
            informations.Add(new EquipmentInformationDataParameter() { Key = "Status", Value = EquipmentStatus.ToString() });
            informations.Add(new EquipmentInformationDataParameter() { Key = "CmdExecStatus", Value = CmdExecStatus.ToString() });
            if (NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
            {
                informations.Add(new EquipmentInformationDataParameter() { Key = "Model", Value = CurrModel, Unit = "" });
            }
            if (this is not ModbusClient && this is not ModbusClient2000)
            {
                return GetChangeInfoFx(informations);
            }
            if (NetConfig.QtyType == ConveyorQtyType.DualConveyor || NetConfig.QtyType == ConveyorQtyType.CornerConveyor)
            {
                var startAddress = PlcBufferRegister.Change2;
                var iStartAddress = startAddress.ToModbusAddress();
                var data = ReadCore(startAddress, 20);
                RealWidthLane2 = data[PlcBufferRegister.Control2.ToModbusAddress() - iStartAddress] / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "WidthLane2", Value = RealWidthLane2.ToString(), Unit = "mm" });
                SetWidthLane2 = data[PlcBufferRegister.RealWidth2.ToModbusAddress() - iStartAddress] / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "SetWidthLane2", Value = SetWidthLane2.ToString(), Unit = "mm" });
                var hasPcb2 = data[PlcBufferRegister.HasPanel2.ToModbusAddress() - iStartAddress];
                informations.Add(new EquipmentInformationDataParameter() { Key = "HasPcb2", Value = hasPcb2.ToString() });
                hasPcb2 = data[PlcBufferRegister.Counter2.ToModbusAddress() - iStartAddress];
                informations.Add(new EquipmentInformationDataParameter() { Key = "PcbNum2", Value = hasPcb2.ToString() });
            }
            if (NetConfig.QtyType == ConveyorQtyType.CornerConveyor)
            {
                var startAddress = PlcBufferRegister.TurnAngle;
                var iStartAddress = startAddress.ToModbusAddress();
                var data = ReadCore(startAddress, 10);
                var turnAngle = data[PlcBufferRegister.TurnAngle.ToModbusAddress() - iStartAddress] / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "TurnAngle", Value = turnAngle.ToString(), Unit = "°" });
                turnAngle = data[PlcBufferRegister.TurnDirection.ToModbusAddress() - iStartAddress];
                informations.Add(new EquipmentInformationDataParameter() { Key = "TurnDirection", Value = turnAngle.ToString() });
                turnAngle = data[PlcBufferRegister.TurnMethod.ToModbusAddress() - iStartAddress];
                informations.Add(new EquipmentInformationDataParameter() { Key = "TurnMethod", Value = turnAngle.ToString() });
            }
            if (NetConfig.QtyType == ConveyorQtyType.ScreeningConveyor || NetConfig.QtyType == ConveyorQtyType.InvertConveyor)
            {
                var startAddress = PlcBufferRegister.TransferMode;
                var iStartAddress = startAddress.ToModbusAddress();
                var data = ReadCore(startAddress, 10);
                var transferMode = data[PlcBufferRegister.TransferMode.ToModbusAddress() - iStartAddress];
                informations.Add(new EquipmentInformationDataParameter() { Key = "TransferMode", Value = transferMode.ToString() });
            }
            if (NetConfig.QtyType == ConveyorQtyType.ScannerConveyor)
            {
                var startAddress = PlcBufferRegister.ScannerDownSetY;
                var iStartAddress = startAddress.ToModbusAddress();
                var data = ReadCore(startAddress, 110);
                var scannerUpSetY = data[PlcBufferRegister.ScannerUpSetY.ToModbusAddress() - iStartAddress] / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "ScannerUpSetY", Value = scannerUpSetY.ToString(), Unit = "mm" });
                scannerUpSetY = data[PlcBufferRegister.ScannerUpRealY.ToModbusAddress() - iStartAddress] / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "ScannerUpRealY", Value = scannerUpSetY.ToString(), Unit = "mm" });
                scannerUpSetY = data[PlcBufferRegister.ScannerDownSetY.ToModbusAddress() - iStartAddress] / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "ScannerDownSetY", Value = scannerUpSetY.ToString(), Unit = "mm" });
                scannerUpSetY = data[PlcBufferRegister.ScannerDownRealY.ToModbusAddress() - iStartAddress] / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "ScannerDownRealY", Value = scannerUpSetY.ToString(), Unit = "mm" });
                var mode = ModbusHelper.GetString(data, PlcBufferRegister.ScannerReadOfProducts.ToModbusAddress() - iStartAddress, 100);
                informations.Add(new EquipmentInformationDataParameter() { Key = "Model", Value = mode, Unit = "" });
            }
            return informations;
        }

        private List<EquipmentInformationDataParameter> GetChangeInfoFx(List<EquipmentInformationDataParameter> informations)
        {
            if (NetConfig.QtyType == ConveyorQtyType.DualConveyor || NetConfig.QtyType == ConveyorQtyType.CornerConveyor)
            {
                var widthLane2 = ReadDataFx(PlcBufferRegister.RealWidth2).Data / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "WidthLane2", Value = widthLane2.ToString(), Unit = "mm" });
                widthLane2 = ReadDataFx(PlcBufferRegister.SetWidth2).Data / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "SetWidthLane2", Value = widthLane2.ToString(), Unit = "mm" });
                widthLane2 = ReadDataFx(PlcBufferRegister.HasPanel2).Data;
                informations.Add(new EquipmentInformationDataParameter() { Key = "HasPcb2", Value = widthLane2.ToString() });
                widthLane2 = ReadDataFx(PlcBufferRegister.Counter2).Data;
                informations.Add(new EquipmentInformationDataParameter() { Key = "PcbNum2", Value = widthLane2.ToString() });
            }
            if (NetConfig.QtyType == ConveyorQtyType.CornerConveyor)
            {
                //var turnAngle = ReadDataFx(PlcBufferRegister.TurnAngle).Data / (decimal)10;
                //informations.Add(new EquipmentInformationDataParameter() { Key = "TurnAngle", Value = turnAngle.ToString(), Unit = "°" });
                var turnAngle = ReadDataFx(PlcBufferRegister.TurnDirection).Data;
                informations.Add(new EquipmentInformationDataParameter() { Key = "TurnDirection", Value = turnAngle.ToString() });
                turnAngle = ReadDataFx(PlcBufferRegister.TurnMethod).Data;
                informations.Add(new EquipmentInformationDataParameter() { Key = "TurnMethod", Value = turnAngle.ToString() });
            }
            if (NetConfig.QtyType == ConveyorQtyType.ScreeningConveyor || NetConfig.QtyType == ConveyorQtyType.InvertConveyor)
            {
                var transferMode = ReadDataFx(PlcBufferRegister.TransferMode).Data;
                informations.Add(new EquipmentInformationDataParameter() { Key = "TransferMode", Value = transferMode.ToString() });
            }
            if (NetConfig.QtyType == ConveyorQtyType.ScannerConveyor)
            {
                var scannerUpSetY = ReadDataFx(PlcBufferRegister.ScannerUpSetY).Data / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "ScannerUpSetY", Value = scannerUpSetY.ToString(), Unit = "mm" });
                scannerUpSetY = ReadDataFx(PlcBufferRegister.ScannerUpRealY).Data / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "ScannerUpRealY", Value = scannerUpSetY.ToString(), Unit = "mm" });
                scannerUpSetY = ReadDataFx(PlcBufferRegister.ScannerDownSetY).Data / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "ScannerDownSetY", Value = scannerUpSetY.ToString(), Unit = "mm" });
                scannerUpSetY = ReadDataFx(PlcBufferRegister.ScannerDownRealY).Data / (decimal)10;
                informations.Add(new EquipmentInformationDataParameter() { Key = "ScannerDownRealY", Value = scannerUpSetY.ToString(), Unit = "mm" });
            }
            else if (NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
            {
                informations.Add(new EquipmentInformationDataParameter() { Key = "WidthLane2", Value = RealWidthLane2.ToString(), Unit = "mm" });
                informations.Add(new EquipmentInformationDataParameter() { Key = "SetWidthLane2", Value = SetWidthLane2.ToString(), Unit = "mm" });
            }
            return informations;
        }

        /// <summary>
        /// 默认读取PLC轨道的状态
        /// </summary>
        /// <param name="length">默认从450读到500，50位，然后解析数据</param>
        /// <returns></returns>
        public virtual void ReadEquipmentState(byte length = 50)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            try
            {
                //Log.LogDebug($"执行读取设备[{NetConfig.Name}]运行状态:ReadEquipmentStateFX");
                hasDataChange = false;
                var runStatus = ReadDataFx(PlcBufferRegister.RunStatus);
                if (!runStatus.Success)
                {
                    Log.LogError($"读取设备[{NetConfig.Name}]运行状态失败:{runStatus.Msg}");
                    return;
                }
                if (runStatus.Data == 0 && !IsConnected)
                {
                    Log.LogError($"设备[{NetConfig.Name}]离线中，无法访问！");
                    return;
                }
                disablePropertyChanged = true;
#if DEBUG2
                Log.LogError($"读取设备[{NetConfig.Name}]运行状态:{runStatus.Data},{(EquipmentStatus)runStatus.Data}");
#endif
                if (runStatus.Data == 0)
                {
                    Log.LogError($"读取设备[{NetConfig.Name}]实际状态为:{runStatus.Data},{(EquipmentStatus)runStatus.Data}，强制标记为掉线状态:{EquipmentStatus.Disconnect}");
                    runStatus.Data = (int)EquipmentStatus.Disconnect;//统一定义等于0定义为没拿到状态，表示断开
                }
                EquipmentStatus = (EquipmentStatus)runStatus.Data;
                CmdExecStatus = (DeviceCommandStatus)ReadDataFx(PlcBufferRegister.CmdExecStatus).Data;
                HasPcb = ReadDataFx(PlcBufferRegister.HasPanel).Data;
                IsNeedPcb = ReadDataFx(PlcBufferRegister.NeedPanel).Data;
                IsDoorOpen = ReadDataFx(PlcBufferRegister.IsDoorOpen).Data;
                RunPath = ReadDataFx(PlcBufferRegister.RunPath).Data;
                SetWidth = ReadDataFx(PlcBufferRegister.Change).Data / (decimal)10;
                RealWidth = ReadDataFx(PlcBufferRegister.RealWidth).Data / (decimal)10;
                PcbNum = ReadDataFx(PlcBufferRegister.Counter).Data;

                disablePropertyChanged = false;
                if (hasDataChange && PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("all"));
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Response was not of expected transaction ID"))
                {
                    Log?.LogError($"[{NetConfig.Name}][{IP}] 网络不稳定，信息错位，关闭链接重连.");
                    this.Close();
                }
                Log.LogError($"读取设备[{NetConfig.Name}]读取信息异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
            }
            finally
            {
                watch.Stop();
                disablePropertyChanged = false;
                hasDataChange = false;
                CheckCmdExecStatus();
            }
            return;
        }

        //        /// <summary>
        //        /// 默认读取PLC轨道的状态
        //        /// </summary>
        //        /// <param name="needReportFlag"></param>
        //        /// <returns></returns>
        //        public virtual void ReadEquipmentStateScanner(Boolean needReportFlag = false)
        //        {
        //            Stopwatch watch = new Stopwatch();
        //            watch.Start();
        //            try
        //            {
        //                //Log.LogDebug($"执行读取设备[{NetConfig.Name}]运行状态:ReadEquipmentStateScanner");
        //                hasDataChange = false;
        //                var result = Send(ScannerDeviceType.GetMachineState);
        //                var runStatus = 0;
        //                if (result.Success && !result.Data.IsNullOrWhiteSpace())
        //                {
        //                    try
        //                    {
        //                        var scannerState = JsonConvert.DeserializeObject<ScannerMachineState>(result.Data);
        //                        if (scannerState != null)
        //                        {
        //                            runStatus = scannerState.Status;
        //                            HasPcb = scannerState.HasPcb;
        //                            PcbNum = scannerState.PcbNum;
        //                            IsNeedPcb = scannerState.IsNeedPcb;
        //                            RealWidth = SetWidth = scannerState.dPcbWidth;
        //                            RealWidthLane2 = SetWidthLane2 = scannerState.dPcbHeight;
        //                            CurrModel = scannerState.model;
        //                            CmdExecStatus = (DeviceCommandStatus)scannerState.cmdStatus;
        //                        }
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        Log.LogError($"读取设备[{NetConfig.Name}]运行状态失败,返回结果无法格式化:{result.ToJSON()},\r\n{ex.Message},\r\n{ex.StackTrace}");
        //                    }
        //                }
        //                else
        //                {
        //                    Log.LogError($"读取设备[{NetConfig.Name}]运行状态失败 data is empty:{result.ToJSON()}");
        //                    return;
        //                }
        //#if DEBUG2
        //                Log.LogError($"读取设备[{NetConfig.Name}]运行状态:{runStatus},{(EquipmentStatus)runStatus}");
        //#endif
        //                if (runStatus == 0) runStatus = (int)EquipmentStatus.Disconnect;//统一定义等于0定义为没拿到状态，表示断开
        //                EquipmentStatus = (EquipmentStatus)runStatus;
        //                RunPath = 1;

        //                disablePropertyChanged = false;
        //                if (hasDataChange && PropertyChanged != null)
        //                {
        //                    PropertyChanged(this, new PropertyChangedEventArgs("all"));
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.LogError($"读取设备[{NetConfig.Name}]运行状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
        //            }
        //            finally
        //            {
        //                watch.Stop();
        //                disablePropertyChanged = false;
        //                hasDataChange = false;
        //            }
        //            return;
        //        }


        private IResult<int> ReadDataFx(string code)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<int>();
            try
            {
                //Log.LogDebug($"设备[{client.NetConfig.Name}]读取指令:{code}, 耗时:{watch.ElapsedMilliseconds}毫秒");
                result.Data = ReadUInt16(code);
                //Log.LogDebug($"设备[{client.NetConfig.Name}]读取指令[{code}]返回数据:{result.Data},耗时:{watch.ElapsedMilliseconds}毫秒");
                return result;
            }
            catch (Exception ex)
            {
                Log.LogError($"读取设备[{NetConfig.Name}]指令[{code}]状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
                result.SetError($"读取设备[{NetConfig.Name}]指令[{code}]状态异常:{ex.Message}");
            }
            finally { watch.Stop(); }
            return result;
        }


        public void CheckCmdExecStatus()
        {
            // 如果命令>0
            if (CmdQueue.TryPop(out CmdCacheItem cmdInfo))
            {
                Log.LogDebug($"[{NetConfig.Name}][{NetConfig.Id}]:{cmdInfo.Cmd}({cmdInfo.CmdId})检查执行状态{cmdInfo.ToJSON()}");
                var result = new Result<DeviceCommandStatus>();

                if (cmdInfo.StartTime <= DateTime.Now.AddMinutes(-1))
                {
                    if ("AdjustParameter".Equals(cmdInfo.Cmd, StringComparison.InvariantCultureIgnoreCase)
                        && (NetConfig.QtyType != ConveyorQtyType.ScannerAutoChangeConveyor && (SetWidth == RealWidth)))
                    {//状态一致认为成功
                        //plcClient.ErrorCode = EquipmentErrorCode.OK;
                        result.SetError("执行超时，但状态一致", true);
                        result.Data = DeviceCommandStatus.Success;
                        CmdExecStatus = DeviceCommandStatus.Ready;
                        SetEquipmentCmdReady(false);
                        Log.LogInformation($"[{NetConfig.Name}][{NetConfig.Id}]:{cmdInfo.Cmd}({cmdInfo.CmdId}) 指令执行超时，但状态一致：{result.Data}");
                    }
                    else if ((("init".Equals(cmdInfo.Cmd, StringComparison.InvariantCultureIgnoreCase) ||
                        "start".Equals(cmdInfo.Cmd, StringComparison.InvariantCultureIgnoreCase) ||
                        "Reset".Equals(cmdInfo.Cmd, StringComparison.InvariantCultureIgnoreCase)) &&
                        EquipmentStatus == EquipmentStatus.Runnling)
                        || (("Shutdown".Equals(cmdInfo.Cmd, StringComparison.InvariantCultureIgnoreCase) ||
                        "stop".Equals(cmdInfo.Cmd, StringComparison.InvariantCultureIgnoreCase))
                            && EquipmentStatus == EquipmentStatus.Shutdown)
                     )
                    {
                        //plcClient.ErrorCode = EquipmentErrorCode.OK;
                        result.Data = DeviceCommandStatus.Success;
                        CmdExecStatus = DeviceCommandStatus.Ready;
                        SetEquipmentCmdReady(false);
                        Log.LogInformation($"[{NetConfig.Name}][{NetConfig.Id}]:{cmdInfo.Cmd}({cmdInfo.CmdId}) 基础操作直接返回：{result.Data}");
                    }
                    else
                    {
                        ErrorCode = EquipmentErrorCode.CommandTimeout;
                        result.Data = DeviceCommandStatus.Timeout;
                        result.SetError(ErrorCode);
                        SetEquipmentCmdReady(false);
                        Log.LogError($"[{NetConfig.Name}][{NetConfig.Id}]:{cmdInfo.Cmd}({cmdInfo.CmdId}) 指令执行超时移除");
                    }
                }
                else if ("AdjustParameter".Equals(cmdInfo.Cmd, StringComparison.InvariantCultureIgnoreCase))
                {
                    if ((int)CmdExecStatus > 1)
                    {
                        if ((this.NetConfig.QtyType != ConveyorQtyType.ScannerConveyor || NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
                            && SetWidth.ToString("F1") != RealWidth.ToString("F1")) result.SetError("设置宽度和实际宽度不一致！");
                        result.Data = DeviceCommandStatus.Success;
                        if (NetConfig.QtyType != ConveyorQtyType.ScannerAutoChangeConveyor)
                            SetEquipmentCmdReady(false);
                        //plcClient.CmdExecStatus = DeviceCommandStatus.Ready;
                        //result.AddError(StartEquipment(plcClient));
                        Log.LogInformation($"[{NetConfig.Name}][{NetConfig.Id}]:{cmdInfo.Cmd}({cmdInfo.CmdId}) 执行执行完成：{result.Data}");
                    }
                    else
                    {
                        CmdQueue.Push(cmdInfo);
                    }
                }
                else
                {
                    result.Data = DeviceCommandStatus.Success;
                    SetEquipmentCmdReady(false);
                    Log.LogInformation($"[{NetConfig.Name}][{NetConfig.Id}]:{cmdInfo.Cmd}({cmdInfo.CmdId})[] 其他状态完成：{result.Data}");
                }
                if (result.Data != DeviceCommandStatus.Ready)
                {
                    SendReponseAsync(cmdInfo.Cmd, cmdInfo.CmdId, NetConfig.Id, result).Wait();
                    Log.LogInformation($"[{NetConfig.Name}][{NetConfig.Id}]:{cmdInfo.Cmd}({cmdInfo.CmdId}) 指令执行{result.Data}");
                }
            }
        }

        private async Task SendReponseAsync(string cmd, string cmdId, string machineId, Result<DeviceCommandStatus> result)
        {
            var responseMsg = new CmdResponse();
            var cmddata = new CmdResponseData();
            cmddata.cmd = cmd;
            cmddata.cmdID = cmdId;
            cmddata.errCode = result.Code.ToString();
            cmddata.errMsg = result.Msg;// status.GetDescription() + (!string.IsNullOrWhiteSpace(resultMsg) ? ":" + resultMsg : "");
            cmddata.result = result.Data.ToString().ToLower();
            responseMsg.data = cmddata;

            var m = mqttClient.MachineList;
            var machineDto = m.FirstOrDefault(p => p.Id == machineId);
            if (machineDto != null)
            {
                await mqttClient.SendAsync(responseMsg, machineDto, machineDto.Retries);
            }
        }

        /// <summary>
        /// 设备执行准备
        /// </summary>
        /// <returns></returns>
        public virtual bool SetEquipmentCmdReady(bool clearErrorCode = true)
        {
            if (clearErrorCode)
                ErrorCode = EquipmentErrorCode.None;
            CmdExecStatus = DeviceCommandStatus.Ready;
            return WriteUInt16(PlcBufferRegister.CmdExecStatus, 0);
        }

        #endregion 统一接口

        public void ReportStatusInfo()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("all"));
            }
        }

        #region abstract methods


        #region Modbus统一接口

        public abstract ushort[] ReadCore(string startAddress, byte length);

        public abstract void WriteCore(string startAddress, byte[] values);

        public abstract ushort[] ReadAndWriteCore(string startAddress, byte[] values);

        #endregion Modbus统一接口

        #region 三菱MC接口
        //public abstract IResult<byte[]> ReadRaw(Fx3MCCommandType type, string address, byte length);

        public abstract UInt16[] ReadUInt16(string address, byte length);

        public abstract byte[] ReadBit(string address, byte length);

        public abstract bool WriteBool(string address, bool value);

        public abstract bool WriteUInt16(string address, params UInt16[] value);

        public virtual UInt16 ReadUInt16(string address)
        {
            return ReadUInt16(address, 1)[0];
        }
        #endregion 三菱MC接口


        private static object sendLockObj = new object();
        public virtual IResult<string> Send(params string[] data)
        {
            IResult<string> resultData = new Result<string>();
            var cmd = data.JoinAsString("&");
#if DEBUG2
            Log.LogDebug($"send data string: {cmd}");
#endif 
            if (data.Length < 2) return resultData;
            var cmdbuffer = Encoding.UTF8.GetBytes(cmd);
            lock (sendLockObj)
            {
                var result = base.Send(cmdbuffer);
                var rdata = Encoding.UTF8.GetString(result.Data);
                Log.LogDebug($"recevie data string: {rdata}");
                resultData = ScannerAsciiHelper.CheckResult(data[1], rdata);
                if (resultData.Success && resultData.Data == "1")
                {
                    result = Receive();
                    rdata = Encoding.UTF8.GetString(result.Data);
                    resultData = ScannerAsciiHelper.CheckResult(data[1], rdata);
                    Log.LogDebug($"recevie data string: {rdata}");
                    Log.LogDebug($"recevie data string2: {resultData.ToJSON()}");
                }
                else if (resultData.Success && !string.IsNullOrWhiteSpace(resultData.Data) && !resultData.Data.StartsWith("{"))
                {
                    Log.LogDebug($"recevie data string: {rdata}");
                    Log.LogDebug($"recevie data string2: {resultData.ToJSON()}");
                    resultData.Data = "";
                    resultData.Code = EquipmentErrorCode.OK.IntValue();
                }
#if DEBUG2
            Log.LogDebug($"result data string: {rdata}");
            Log.LogDebug($"result data string2: {resultData.ToJSON()}");
#endif
            }
            return resultData;
        }


        #endregion abstract methods


        public void TriggerPropertyHandler()
        {
            if (hasDataChange && PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("all"));
            }
        }
        //public override void Close()
        //{
        //    base.Close();
        //}
    }
}
