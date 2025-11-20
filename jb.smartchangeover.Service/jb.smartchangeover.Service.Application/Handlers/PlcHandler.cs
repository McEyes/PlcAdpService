using Confluent.Kafka;
using Elasticsearch.Net;
using Jabil.Service.Frameworks.Memory;
using jb.smartchangeover.Service.Application.Mqtts;
using jb.smartchangeover.Service.Domain.Shared;
using jb.smartchangeover.Service.Domain.Shared.Commons;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;
using jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos;
using jb.smartchangeover.Service.Domain.Shared.PhysicalCache;
using jb.smartchangeover.Service.Domain.Shared.Plc;
using jb.smartchangeover.Service.Domain.Shared.Plc.Adapter.Modbus;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio.DataModel;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using Nest;
using Newtonsoft.Json;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using Scriban.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace jb.smartchangeover.Service.Application.Handlers
{
    public class PlcHandler : Volo.Abp.DependencyInjection.ISingletonDependency
    {
        private ImmutableList<PlcConfig> PlcConfigList = null;
        private ImmutableList<PlcClient> PlcClientList = null;
        private MqttClientService mqttClient = null;
        private CmdCacheManager _cmdCache = null;

        private System.Timers.Timer _timer = null;


        private readonly ILogger<PlcHandler> Log = null;
        protected readonly IConfiguration _config = null;

        private int oldHash = 0;

        private bool isTick = false;
        // private int RetryTime = 3;
        // private bool IsDebug = false;
        // private int ResetWidthType = 3; 
        // /// <summary>
        // /// 单位秒
        // /// </summary>
        // private int HeartBeat = 30;

        private PlcServiceConfig serviceConfig;


        /// <summary>
        /// PLC 但前的状态
        /// </summary>
        public int PlcStatus { get; set; }


        //private readonly System.Collections.Concurrent.ConcurrentStack<CmdCacheItem> CmdList = null;

        public EquipmentReportEventHandler ReportHandler { get; set; } = null;

        public PlcHandler(MqttClientService mqttClient, ILogger<PlcHandler> loger, IConfiguration config)
        {
            Log = loger;
            _config = config;
            //CmdList = new System.Collections.Concurrent.ConcurrentStack<CmdCacheItem>();
            this.mqttClient = mqttClient;
            this.mqttClient.MessageReceived += MqttClient_MessageReceived;
            serviceConfig = new PlcServiceConfig();
            InitConfig();
            _cmdCache = new CmdCacheManager(_config);
        }

        /// <summary>
        /// 获取服务PLC设备配置，监控哪些设备
        /// 初始化监控配置
        /// </summary>
        private void InitConfig()
        {
            try
            {
                serviceConfig = _config.GetSection("PlcService").Get<PlcServiceConfig>();
                mqttClient.CheckOtherSysConnect = serviceConfig.CheckOtherSysConnect;
                
                var list = _config.GetSection("Equipments").Get<List<PlcConfig>>();
                if (list == null || list.Any() == false)
                {
                    PlcConfigList = ImmutableList<PlcConfig>.Empty;
                    PlcClientList = ImmutableList<PlcClient>.Empty;
                    Log.LogWarning($" PLC Init Equipments Config Empty");
                    return;
                }

                list = list.Where(f => f.Enable).ToList();
                PlcConfigList = ImmutableList.CreateRange(list);
                if (!CheckConfigChange(list) && PlcClientList != null) return;
                InitNetClient();
            }
            catch (Exception ex)
            {
                Log.LogError($" PLC Init Config Error:{ex.Message}\r\n{ex.StackTrace}");
                Log.LogException(ex);
            }
        }

        private bool CheckConfigChange(List<PlcConfig> list)
        {
            var rs = JsonConvert.SerializeObject(list);
            var hash = mqttClient.BKDRHash(rs);
            if (oldHash == hash) return false;
            oldHash = hash;
            return true;
        }

        private void InitNetClient()
        {
            var list = new List<PlcClient>();
            if (PlcClientList != null && PlcClientList.Count > 0)
            {
                PlcClientList.ForEach(p =>
                {
                    p.StopMonitor();
                    Thread.Sleep(500);
                    p.Close();
                    p.Dispose();
                });
                PlcClientList.Clear();
            }
            foreach (var item in PlcConfigList)
            {
                if (!item.IsDebug.HasValue) item.IsDebug = serviceConfig.IsDebug;
                var netclient = NetClientFactory.GetPlcInstance(item, this.mqttClient, Log);
                netclient.PropertyChanged += Netclient_PropertyChanged;
                netclient.StartMonitor();
                list.Add(netclient);
            }
            PlcClientList = ImmutableList.CreateRange(list);
        }

        private async void Netclient_PropertyChanged(INetClient? netClient, PropertyChangedEventArgs e)
        {
            if (netClient is PlcClient)
            {
                var plcClient = netClient as PlcClient;
                if ("all".Equals(e.PropertyName, StringComparison.CurrentCultureIgnoreCase))
                {
                    var informations = plcClient.GetChangeInfo();
                    if (informations.Count > 0)
                    {
                        if (!await SendEquipmentImfoReport(plcClient.NetConfig, informations))
                        {
                            Log.LogError($"mqtt Client send Heart beat fail topic ={plcClient.NetConfig.Topic}");
                        }
                    }
                }
                else if ("EquipmentStatus".Equals(e.PropertyName, StringComparison.CurrentCultureIgnoreCase))
                {
                    var oldStatus = plcClient.PreEquipmentStatus;
                    //异常状态报警
                    if (plcClient.EquipmentStatus.IntValue() > 2
                        && !await SendErrorReport(plcClient, ((int)plcClient.EquipmentStatus).ToString(), ((int)plcClient.EquipmentStatus).ToString(), plcClient.EquipmentStatus.GetDescription()))
                    {
                        Log.LogError($"mqtt Client send Heart beat fail topic ={plcClient.NetConfig.Topic}");
                    }
                    //清除报警
                    if ((plcClient.EquipmentStatus.IntValue() < 3) &&
                        (oldStatus.IntValue() > 2))
                    {
                        plcClient.PreEquipmentStatus = plcClient.EquipmentStatus;
                        if (!await SendErrorCleardReport(plcClient, ((int)oldStatus).ToString(), ((int)oldStatus).ToString(), oldStatus.GetDescription()))
                        {
                            Log.LogError($"mqtt Client send Heart beat fail topic ={plcClient.NetConfig.Topic}");
                        }
                    }
                }
            }
        }

        private void StartTimer()
        {
            try
            {
                _timer = new System.Timers.Timer();
                _timer.Elapsed += _timer_Elapsed;
                _timer.Interval = serviceConfig.HeartBeat * 1000;//采集心率,默认1秒钟采集一次
                _timer.Start();
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
        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!isTick && PlcClientList != null)
            {
                isTick = true;
                try
                {
                    InitConfig();
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


        public async Task StartAsync()
        {
            await this.mqttClient.StartAsync();
            StartTimer();
            if (PlcClientList == null) InitConfig();
            foreach (var item in PlcClientList)
            {
                item.StartMonitor();
            }
        }

        private void MqttClient_MessageReceived(object? sender, string msgBody)
        {
            var result = new Result<DeviceCommandStatus>();
            BaseMsg cmdMsg = null;
            PlcClient plcClient = null;
            var iotId = string.Empty;
            try
            {
                Log.LogDebug($"[Mqtt] Received Message:[{msgBody}]");
                cmdMsg = msgBody.FromJSON<BaseMsg>();
                if (sender is MqttApplicationMessageReceivedEventArgs)
                {
                    var client = sender as MqttApplicationMessageReceivedEventArgs;
                    if (PlcClientList == null) InitConfig();
                    plcClient = PlcClientList?.FirstOrDefault(f => client.ApplicationMessage.Topic.EndsWith(f.NetConfig.Id));
                    if (plcClient == null)
                    {
                        InitConfig();
                        plcClient = PlcClientList?.FirstOrDefault(f => client.ApplicationMessage.Topic.EndsWith(f.NetConfig.Id));
                        if (plcClient == null)
                        {
                            Log.LogError($"[Mqtt][{client.ApplicationMessage.Topic}]消息PLC对象不存在，当前接收的消息内容为:{msgBody} .\r\n");
                            result.SetError(EquipmentErrorCode.PlcClientNullError);
                            result.Data = DeviceCommandStatus.Failed;
                            iotId = client.ApplicationMessage.Topic.Split(new char[] { '\\' }).Last();
                            return;
                        }
                    }
                    iotId = plcClient.NetConfig.Id;
                    mqttClient.SetBaseInfo(cmdMsg, plcClient.NetConfig);
                    //检查消息时效，超过10秒的数据不在处理
                    var datetime = DateTime.Now;
                    if (!DateTime.TryParse(cmdMsg.TimeStamp, out datetime) || !(datetime > DateTime.Now.AddSeconds(-10) && datetime < DateTime.Now.AddSeconds(10)))
                    {
                        //超过时效数据不在执行
                        Log.LogWarning($"[Mqtt][{client.ApplicationMessage.Topic}] The message is expired:{msgBody}.\r\n");
                        result.SetError(EquipmentErrorCode.CommandExpired);
                        result.Data = DeviceCommandStatus.Failed;
                        return;
                    }
                    // reset的时候不检查是否执行中
                    if (plcClient.CmdExecStatus == DeviceCommandStatus.Executing && !"Reset".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //命令执行中，稍后重试
                        Log.LogWarning($"[Mqtt][{client.ApplicationMessage.Topic}] The message is Busy:{msgBody}.\r\n");
                        result.SetError(EquipmentErrorCode.DeviceBusy);
                        result.Data = DeviceCommandStatus.Failed;
                        return;
                    }


                    SetEquipmentCmdReady(plcClient);

                    //添加到指令执行队列中，等指令执行完成后，上报执行状态，
                    var item = new CmdCacheItem(cmdMsg.MessageType, cmdMsg.Id);
                    if ("AdjustParameter".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var adjustParameter = msgBody.FromJSON<BaseMsg<AdjustParameter>>();
                        mqttClient.SetBaseInfo(adjustParameter, plcClient.NetConfig);
                        if (adjustParameter.Data == null || adjustParameter.Data.Parameters == null || adjustParameter.Data.Parameters.Count == 0)
                        {
                            Log.LogError($"[Mqtt] [{plcClient.NetConfig.Name}]消息命令格式错误，缺少命令信息,当前接收的消息内容为:{msgBody}.\r\n");
                            plcClient.ErrorCode = EquipmentErrorCode.CommandError;
                            result.Data = DeviceCommandStatus.Failed;
                            result.SetError(EquipmentErrorCode.UnknownCmdError);
                            result.SetError("AdjustParameter消息命令格式错误，缺少命令信息.");
                            return;
                        }
                        //if (_cmdCache.IsExistsSameRecipe(adjustParameter,60))
                        //{
                        //    Log.LogError($"[Mqtt] [{plcClient.NetConfig.Name}] {_cmdCache.CmdRepeatCheckTime}秒内重复指令,当前接收的消息内容为:{msgBody}.\r\n");
                        //    plcClient.ErrorCode = EquipmentErrorCode.CommandRepeat;
                        //    result.Data = DeviceCommandStatus.Failed;
                        //    result.SetError(EquipmentErrorCode.CommandRepeat);
                        //    result.SetError($"{_cmdCache.CmdRepeatCheckTime}秒内重复指令.");
                        //    return;
                        //}
                        plcClient.CmdQueue.Push(item);
                        var dataResult = AdjustParameter(plcClient, adjustParameter, msgBody);
                        result.AddError(dataResult);
                        result.Data = dataResult.Data;
                        if (plcClient.NetConfig.IsDebug == true)
                        {
                            result.Msg = "调试模式直接返回";
                        }
                    }
                    else
                    {
                        //if (_cmdCache.IsExistsSameRecipe(cmdMsg,10))
                        //{
                        //    Log.LogError($"[Mqtt] [{plcClient.NetConfig.Name}] {_cmdCache.CmdRepeatCheckTime}秒内重复指令,当前接收的消息内容为:{msgBody}.\r\n");
                        //    plcClient.ErrorCode = EquipmentErrorCode.CommandRepeat;
                        //    result.Data = DeviceCommandStatus.Failed;
                        //    result.SetError(EquipmentErrorCode.CommandRepeat);
                        //    result.SetError($"{_cmdCache.CmdRepeatCheckTime}秒内重复指令.");
                        //    result.Success = true;
                        //    return;
                        //}
                        _cmdCache.UpdateRecord(cmdMsg);
                        if ("init".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase) ||
                        "start".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //CmdList.Push(item);
                            //result.AddError(StartEquipment(plcClient));
                            StartEquipment(plcClient);
                            result.Data = DeviceCommandStatus.Success;
                        }
                        else if ("Shutdown".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase)
                            || "stop".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //CmdList.Push(item);
                            //result.AddError(StopEquipment(plcClient));
                            StopEquipment(plcClient);
                            result.Data = DeviceCommandStatus.Success;
                        }
                        else if ("Alarm".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            var msg = msgBody.FromJSON<BaseMsg<ushort>>();
                            mqttClient.SetBaseInfo(msg, plcClient.NetConfig);
                            if (msg.Data <= 100)
                            {
                                result.Data = DeviceCommandStatus.Failed;
                                result.SetError($"该报警代码[{msg.Data}]不支持.");
                                return;
                            }
                            var errorCode = (EquipmentErrorCode)msg.Data;
                            if (!errorCode.GetDescription().IsNullOrWhiteSpace())
                            {
                                //CmdList.Push(item);
                                result.Data = DeviceCommandStatus.Success;
                                result.AddError(WriteData(plcClient, PlcBufferRegister.Control, msg.Data));
                            }
                            else
                            {
                                result.Data = DeviceCommandStatus.Failed;
                                result.SetError($"该报警代码[{msg.Data}]不支持.");
                            }
                            return;
                        }
                        else if ("Reset".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //CmdList.Push(item);
                            result.AddError(ResetStatusEquipment(plcClient));
                            result.Data = DeviceCommandStatus.Success;
                        }
                        else if ("ResetWidth".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            plcClient.CmdQueue.Push(item);
                            result.AddError(ResetWidth2(plcClient));
                        }
                        else if ("report".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //CmdList.Push(item);
                            plcClient.ReportStatusInfo();
                            result.Data = DeviceCommandStatus.Success;
                        }
                        else if ("AutomaticSwitchingOfProducts".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            var scannerMsg = msgBody.FromJSON<BaseMsg<string>>();
                            mqttClient.SetBaseInfo(scannerMsg, plcClient.NetConfig);
                            if (plcClient.NetConfig.QtyType != ConveyorQtyType.ScannerAutoChangeConveyor)
                            {
                                result.SetError(EquipmentErrorCode.CommandError);
                                result.SetError($"The device does not support this cmd {cmdMsg.MessageType}");
                                return;
                            }
                            var resultData = Send(plcClient, ScannerDeviceType.AutomaticSwitchingOfProducts, scannerMsg.Data + "");
                            result.AddError(resultData);
                            Log.LogDebug($"[{plcClient.NetConfig.Topic}]接收数据:{resultData.Data}");
                            if (resultData.Success)
                            {
                                result.Data = DeviceCommandStatus.Success;
                                if (resultData.Data == ((int)DeviceCommandStatus.Executing).ToString())
                                {
                                    result.Data = DeviceCommandStatus.Executing;
                                }
                            }
                            else
                            {
                                result.Data = DeviceCommandStatus.Failed;
                                Log.LogError($"{cmdMsg.MessageType}send commond failed");
                            }
                        }
                        else if ("CheckProductExistence".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // plcClient.CmdQueue.Push(item);
                            var scannerMsg = msgBody.FromJSON<BaseMsg<string>>();
                            mqttClient.SetBaseInfo(scannerMsg, plcClient.NetConfig);
                            if (plcClient.NetConfig.QtyType != ConveyorQtyType.ScannerAutoChangeConveyor)
                            {
                                result.SetError(EquipmentErrorCode.CommandError);
                                result.SetError($"The device does not support this cmd {cmdMsg.MessageType}");
                                return;
                            }
                            var resultData = Send(plcClient, ScannerDeviceType.CheckProductExistence, scannerMsg.Data + "");
                            result.AddError(resultData);
                            // plcClient.CmdExecStatus= DeviceCommandStatus.Executing;
                            // Log.LogDebug($"[{plcClient.NetConfig.Topic}]接收数据:{resultData.ToJSON()}");
                            if (resultData.Success)
                            {
                                result.Data = DeviceCommandStatus.Success;
                            }
                            else
                            {
                                result.Data = DeviceCommandStatus.Failed;
                                // if (resultData is IResult<string>) result.SetError((resultData as IResult<string>).Data);
                                Log.LogError($"{cmdMsg.MessageType}send commond failed");
                            }
                        }
                        else
                        {
                            plcClient.ErrorCode = EquipmentErrorCode.CommandError;
                            result.Data = DeviceCommandStatus.Failed;
                            result.SetError(EquipmentErrorCode.UnknownCmdError);
                            Log.LogWarning($"[Mqtt][{plcClient.NetConfig.Name}] unknown cmd Msg:{msgBody}");
                        }
                    }
                }
                else
                {
                    result.Data = DeviceCommandStatus.Failed;
                    result.SetError(EquipmentErrorCode.UnknownMqttMessageError);
                    Log.LogWarning($"[Mqtt][{plcClient.NetConfig.Name}] MessageReceived sender is not MqttApplicationMessageReceivedEventArgs:{msgBody}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[Mqtt] Message Received 异常,\r\n {ex.Message}\r\n {ex.StackTrace},\r\n msgBody: {msgBody} ");
                result.Data = DeviceCommandStatus.Failed;
                result.SetError(EquipmentErrorCode.UnknownError);
                result.SetError($"Message Received error:{ex.Message}");
            }
            finally
            {
                try
                {
                    if (result.Success && result.Data == DeviceCommandStatus.Ready) result.Data = DeviceCommandStatus.Executing;
                    if (cmdMsg == null && plcClient != null)
                        SendReponseAsync("CmdResponse", "", plcClient.NetConfig.Id, result).Wait();
                    else if (plcClient != null)
                        SendReponseAsync(cmdMsg.MessageType, cmdMsg.Id, plcClient.NetConfig.Id, result).Wait();
                    else if (cmdMsg != null)
                        SendReponseAsync(cmdMsg.MessageType, cmdMsg.Id, iotId, result).Wait();
                }
                catch (Exception ex)
                {
                    Log.LogError($"[mqtt] finally send report Error:{ex.Message},\n{ex.StackTrace}");
                }
            }
        }

        private Result<DeviceCommandStatus> AdjustParameter(PlcClient client, BaseMsg<AdjustParameter> msg, string cmdJson)
        {
            var result = new Result<DeviceCommandStatus>();
            try
            {
                _cmdCache.UpdateRecord(msg);
                var needResetWidth = 1;
                foreach (var item in msg.Data.Parameters)
                {
                    switch (item.Key?.ToLower())
                    {
                        case "test":
                            {
                                needResetWidth = -1;
                                break;
                            }
                        case "test2":
                            {
                                needResetWidth = -2;
                                break;
                            }
                        case "widthlane1":
                        case "setwidthlane1":
                            if (client.NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
                            {
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            var result2 = ConvertWidth(item);
                            if (result.Success)
                            {
                                result.AddError(WriteWidth(client, result2.Data));
                            }
                            else
                            {
                                result.AddError(result2);
                            }
                            needResetWidth = 1;
                            break;
                        case "widthlane2":
                        case "setwidthlane2":
                            if (client.NetConfig.QtyType != ConveyorQtyType.DualConveyor && client.NetConfig.QtyType != ConveyorQtyType.CornerConveyor)
                            {
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            var result3 = ConvertWidth(item);
                            if (result3.Success)
                            {
                                result.AddError(WriteWidth2(client, result3.Data));
                            }
                            else
                            {
                                result.AddError(result3);
                            }
                            needResetWidth = 1;
                            break;
                        case "pcbnum":
                            var result6 = ConvertUshot(item);
                            if (result6.Success)
                            {
                                result.AddError(WriteData(client, PlcBufferRegister.Counter, result6.Data));
                            }
                            else
                            {
                                result.AddError(result6);
                            }
                            break;
                        case "pcbnum2":
                            if (client.NetConfig.QtyType != ConveyorQtyType.DualConveyor && client.NetConfig.QtyType != ConveyorQtyType.CornerConveyor)
                            {
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            var pcbNum2 = ConvertUshot(item);
                            if (pcbNum2.Success)
                            {
                                result.AddError(WriteData(client, PlcBufferRegister.Counter2, pcbNum2.Data));
                            }
                            else
                            {
                                result.AddError(pcbNum2);
                            }
                            break;
                        //case "turnangle":
                        //    if (client.NetConfig.QtyType != ConveyorQtyType.CornerConveyor)
                        //    {
                        //        result.SetError($"The device does not support this cmd {item.Key}");
                        //        break;
                        //    }
                        //    var turnangle = ConvertAngle(item);
                        //    if (turnangle.Success)
                        //    {
                        //        result.AddError(WriteData(client, PlcBufferRegister.TurnAngle, turnangle.Data));
                        //    }
                        //    else
                        //    {
                        //        result.AddError(turnangle);
                        //    }
                        //    needResetWidth = 1;
                        //    break;
                        case "turndirection":
                            if (client.NetConfig.QtyType != ConveyorQtyType.CornerConveyor)
                            {
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            needResetWidth = 1;
                            var turndirection = ConvertUshot(item);
                            if (turndirection.Success)
                            {
                                result.AddError(WriteData(client, PlcBufferRegister.TurnDirection, turndirection.Data));
                            }
                            else
                            {
                                result.AddError(turndirection);
                            }
                            break;
                        case "turnmethod":
                            if (client.NetConfig.QtyType != ConveyorQtyType.CornerConveyor)
                            {
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            needResetWidth = 1;
                            var turnmethod = ConvertUshot(item);
                            if (turnmethod.Success)
                            {
                                result.AddError(WriteData(client, PlcBufferRegister.TurnMethod, turnmethod.Data));
                            }
                            else
                            {
                                result.AddError(turnmethod);
                            }
                            break;
                        case "transfermode":
                            if (client.NetConfig.QtyType != ConveyorQtyType.ScreeningConveyor
                                && client.NetConfig.QtyType != ConveyorQtyType.InvertConveyor)
                            {
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            needResetWidth = 1;
                            var transfermode = ConvertUshot(item);
                            if (transfermode.Success)
                            {
                                result.AddError(WriteData(client, PlcBufferRegister.TransferMode, transfermode.Data));
                            }
                            else
                            {
                                result.AddError(transfermode);
                            }
                            break;
                        case "scannerlane1":
                        case "scannerdownsety":
                        case "scannerdownrealy":
                            if (client.NetConfig.QtyType != ConveyorQtyType.ScannerConveyor)
                            {
                                result.SetError(EquipmentErrorCode.CommandError);
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            var result4 = ConvertWidth(item);
                            if (result.Success)
                            {
                                result.AddError(WriteData(client, PlcBufferRegister.ScannerDownSetY, result4.Data));
                            }
                            else
                            {
                                result.AddError(result4);
                            }
                            needResetWidth = 1;
                            break;
                        case "scannerlane2":
                        case "scannerupsety":
                        case "scanneruprealy":
                            if (client.NetConfig.QtyType != ConveyorQtyType.ScannerConveyor)
                            {
                                result.SetError(EquipmentErrorCode.CommandError);
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            var result5 = ConvertWidth(item);
                            if (result5.Success)
                            {
                                result.AddError(WriteData(client, PlcBufferRegister.ScannerUpSetY, result5.Data));
                            }
                            else
                            {
                                result.AddError(result5);
                            }
                            needResetWidth = 1;
                            break;
                        case "autochange"://AutomaticSwitchingOfProducts
                        case "model"://AutomaticSwitchingOfProducts
                            if (client.NetConfig.QtyType != ConveyorQtyType.ScannerAutoChangeConveyor && client.NetConfig.QtyType != ConveyorQtyType.ScannerConveyor)
                            {
                                result.SetError(EquipmentErrorCode.CommandError);
                                result.SetError($"The device does not support this cmd {item.Key}");
                                break;
                            }
                            if (client.NetConfig.QtyType == ConveyorQtyType.ScannerConveyor)
                            {
                                result.AddError(StopEquipment(client, false));
                                result.AddError(WriteData(client, PlcBufferRegister.ScannerSwitchingOfProducts, item.Value));
                                needResetWidth = 2;
                                break;
                            }
                            //else if (client.NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
                            //{
                            //    //result.AddError(StopEquipment(client, false));
                            //}

                            needResetWidth = 0;
                            var resultData = Send(client, ScannerDeviceType.AutomaticSwitchingOfProducts, item.Value);
                            result.AddError(resultData);
                            //Log.LogDebug($"[{client.NetConfig.Topic}]接收数据:{resultData.Data}");
                            if (resultData.Success)
                            {
                                result.Data = DeviceCommandStatus.Executing;
                            }
                            else
                            {
                                result.Data = DeviceCommandStatus.Failed;
                                result.SetError(resultData.Data);
                            }
                            //client.CmdQueue.TryPop(out _);
                            break;
                            // case "resetwidthtype"://调轨方式：1只归零，2归零并调整到设置宽度，3，直接调整到设置宽度
                            //     if (!string.IsNullOrWhiteSpace(item.Value))
                            //     {
                            //         int.TryParse(item.Value, out ResetWidthType);
                            //     }
                            //     break;
                    }
                }
                if (needResetWidth == 1)
                {
                    result.AddError(StopEquipment(client, false));
                    if (serviceConfig.ResetWidthType == 2)
                        result.AddError(ResetWidth2(client));
                    else
                        result.AddError(ResetWidth3(client));
                    result.Data = DeviceCommandStatus.Executing;
                    if (client.NetConfig.IsDebug == true)
                    {
                        result.Data = DeviceCommandStatus.Success;
                        result.Msg = "调试模式直接返回！";
                        result.Success = true;
                        client.CmdQueue.TryPop(out _);
                    }
                }
                else if (needResetWidth == -1)
                {
                    result.Data = DeviceCommandStatus.Executing;
                    result.Success = true;
                }
                else if (needResetWidth != 2 && needResetWidth != 0)
                {
                    result.Data = DeviceCommandStatus.Success;
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.SetError(EquipmentErrorCode.UnknownError);
                result.SetError($"AdjustParameter 异常：{ex.Message}\r\n{ex.StackTrace}");
            }
            return result;
        }


        private IResult<ushort> ConvertWidth(EquipmentParameter param)
        {
            var result = new Result<ushort>();
            double dwidth = 0;
            if (double.TryParse(param.Value, out dwidth))
            {
                switch (param.Unit?.ToLower())
                {
                    case "cm":
                        {
                            result.Data = (ushort)(dwidth * 100);
                            return result;
                        }
                    case "m":
                        {
                            result.Data = (ushort)(dwidth * 1000);
                            return result;
                        }
                    default:
                        {
                            result.Data = (ushort)(dwidth * 10);
                            return result;
                        }
                }
            }
            else
            {
                result.SetError(EquipmentErrorCode.CommandError);
                result.SetError($"{param.Key}参数值{param.Value}错误，无法转换宽度毫米单位！");
            }
            return result;
        }

        private IResult<ushort> ConvertUshot(EquipmentParameter param)
        {
            var result = new Result<ushort>();
            ushort dwidth = 0;
            if (ushort.TryParse(param.Value, out dwidth))
            {
                result.Data = dwidth;
            }
            else
            {
                result.SetError($"{param.Key}参数值错误，无法转换为ushort值！");
            }
            return result;
        }


        #region 轨道宽度命令

        /// <summary>
        /// 读取轨道宽度
        /// </summary>
        /// <returns></returns>
        public virtual async ValueTask<IResult> ReadWidth(PlcClient client)
        {
            var result = ReadData(client, PlcBufferRegister.Change);
            if (result.Success)
            {
                result.Success = await SendEquipmentImfoReport(client.NetConfig, new List<EquipmentInformationDataParameter>() {
                                         new EquipmentInformationDataParameter(){
                                              Key="width",
                                              Value=(result.Data/10f).ToString()
                                         }
                                    });
                if (!result.Success)
                {
                    result.SetError("Send EquipmentImfo Report Error");
                }
            }
            return result;
        }

        /// <summary>
        /// 设置轨道1宽度
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> WriteWidth(PlcClient client, ushort width)
        {
            return WriteData(client, PlcBufferRegister.Change, width);
        }

        /// <summary>
        /// 设置轨道2宽度
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> WriteWidth2(PlcClient client, ushort width)
        {
            return WriteData(client, PlcBufferRegister.Change2, width);
        }

        #endregion 轨道宽度命令


        #region 控制轨道启停


        /// <summary>
        /// 设备执行准备
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> SetEquipmentCmdReady(PlcClient plcClient, bool clearErrorCode = true)
        {
            if (plcClient.NetConfig.QtyType != ConveyorQtyType.ScannerAutoChangeConveyor)
            {
                if (clearErrorCode)
                    plcClient.ErrorCode = EquipmentErrorCode.None;
                plcClient.CmdExecStatus = DeviceCommandStatus.Ready;
                return WriteData(plcClient, PlcBufferRegister.CmdExecStatus, 0);
            }
            return new Result<bool>();
        }


        /// <summary>
        /// 复位设备状态
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> ResetStatusEquipment(PlcClient client)
        {
            if (client.NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
            {
                return WriteData(client, ScannerDeviceType.ControlMachine, "2");
            }
            else
            {
                SetEquipmentCmdReady(client);
                return WriteData(client, PlcBufferRegister.Control, 2);
            }
        }

        /// <summary>
        /// 启动设备
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> StartEquipment(PlcClient client)
        {
            var result = new Result<bool>();
            //if (client.EquipmentStatus == EquipmentStatus.Runnling)
            //{
            //    result.SetError(EquipmentErrorCode.CommandRepeat);
            //    result.Success = true;
            //    return result;
            //}
            if (client.NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
            {
                var exeResult = WriteData(client, ScannerDeviceType.ControlMachine, "1");
                result.AddError(exeResult);
                result.Data = exeResult.Data;
            }
            else
            {
                var exeResult = WriteData(client, PlcBufferRegister.Control, 1);
                result.AddError(exeResult);
                result.Data = exeResult.Data;
            }
            return result;
        }


        /// <summary>
        /// 停止设备
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> StopEquipment(PlcClient client, bool checkCmdRepeat = true)
        {
            var result = new Result<bool>();
            //if (client.EquipmentStatus == EquipmentStatus.Shutdown)
            //{
            //    if (checkCmdRepeat)
            //    {
            //        result.SetError(EquipmentErrorCode.CommandRepeat);
            //        result.Success = true;
            //    }
            //    return result;
            //}
            if (client.NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
            {
                var exeResult = WriteData(client, ScannerDeviceType.ControlMachine, "3");
                result.AddError(exeResult);
                result.Data = exeResult.Data;
            }
            else
            {
                var exeResult = WriteData(client, PlcBufferRegister.Control, 3);
                result.AddError(exeResult);
                result.Data = exeResult.Data;
            }
            return result;
        }

        #endregion 控制轨道启停

        #region 读取或重置轨道过板数量


        /// <summary>
        /// 读取轨道过板数量
        /// </summary>
        /// <returns></returns>
        public virtual IResult<int> ReadPcbNum(PlcClient client)
        {
            var result = ReadData(client, PlcBufferRegister.Counter);
            return SendEquipmentImfoReport(client, result, "PcbNum", result.Data.ToString()).Result;
        }

        /// <summary>
        /// 重置轨道过板数量
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> ResetPcbNum(PlcClient client)
        {
            return WriteData(client, PlcBufferRegister.Counter, 0);
        }


        #endregion 读取或重置轨道过板数量

        #region 运行状态寄存器：0运行正常，3报警


        /// <summary>
        /// 运行状态寄存器：0运行正常，3报警
        /// </summary>
        /// <returns></returns>
        public virtual IResult<int> ReadStatus(PlcClient client)
        {
            return ReadData(client, PlcBufferRegister.RunStatus);
        }


        #endregion 运行状态寄存器：0运行正常，3报警


        #region 是否有板


        /// <summary>
        /// 检查是否有板
        /// </summary>
        /// <returns></returns>
        public IResult<int> ReadHasPanel(PlcClient client)
        {
            var result = ReadData(client, PlcBufferRegister.HasPanel);
            return SendEquipmentImfoReport(client, result, "PcbNum", result.Data == 1 ? "有板" : "没板").Result;
        }

        #endregion 是否有板


        #region 传输方向


        /// <summary>
        /// 传输方向
        /// 1左至右，2右至左，3上至下，4下至上, 5左至下，6左至上，7右至下，8右至上，9上至左，10上至右，11下至左，12下至右
        /// </summary>
        /// <returns></returns>
        public virtual IResult<int> ReadRunPath(PlcClient client)
        {
            var result = ReadData(client, PlcBufferRegister.RunPath);
            return SendEquipmentImfoReport(client, result, "PcbNum", result.Data.ToString()).Result;
        }

        /// <summary>
        /// 传输方向
        /// 1左至右，2右至左，3上至下，4下至上, 5左至下，6左至上，7右至下，8右至上，9上至左，10上至右，11下至左，12下至右
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> WriteRunPath(PlcClient client, ushort path)
        {
            return WriteData(client, PlcBufferRegister.RunPath, 0);
        }


        #endregion 传输方向


        #region 轨道传输速度


        /// <summary>
        /// 轨道传输速度，读取和设置调整轨道速度
        /// </summary>
        /// <returns></returns>
        public virtual IResult<int> ReadSpeed(PlcClient client)
        {
            var result = ReadData(client, PlcBufferRegister.Speed);
            return SendEquipmentImfoReport(client, result, "PcbNum", (result.Data / 100f).ToString()).Result;
        }

        /// <summary>
        /// 轨道传输速度，读取和设置调整轨道速度
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> WriteSpeed(PlcClient client, ushort speed)
        {
            return WriteData(client, PlcBufferRegister.Speed, speed);
        }


        #endregion 轨道传输速度

        #region 归位


        /// <summary>
        /// 设置轨道回到原点
        /// </summary>
        /// <returns></returns>
        public IResult<bool> ResetWidth(PlcClient client)
        {
            return WriteData(client, PlcBufferRegister.WidthReset, 1);
        }

        /// <summary>
        /// 设置轨道回到原点
        /// </summary>
        /// <returns></returns>
        public IResult<bool> ResetWidth2(PlcClient client)
        {
            client.CmdExecStatus = 0;
            return WriteData(client, PlcBufferRegister.WidthReset, 2);
        }

        /// <summary>
        /// 直接调宽
        /// </summary>
        /// <returns></returns>
        public IResult<bool> ResetWidth3(PlcClient client)
        {
            return WriteData(client, PlcBufferRegister.WidthReset, 3);
        }

        #endregion 归位

        #region 传输模式


        /// <summary>
        /// 筛选机需要设置模式：1直通模式和2手动模式：
        /// </summary>
        /// <returns></returns>
        public virtual IResult<int> ReadTransferMode(PlcClient client)
        {
            var result = ReadData(client, PlcBufferRegister.TransferMode);
            return SendEquipmentImfoReport(client, result, "PcbNum", result.Data == 1 ? "直通模式" : (result.Data == 2 ? "手动模式" : "读取失败")).Result;
        }

        /// <summary>
        /// 筛选机需要设置模式：1直通模式和2手动模式：
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> WriteTransferMode(PlcClient client, ushort speed)
        {
            return WriteData(client, PlcBufferRegister.TransferMode, speed);
        }

        #endregion 传输模式

        private async ValueTask<IResult<T>> SendEquipmentImfoReport<T>(PlcClient client, IResult<T> result, string key, string value)
        {
            if (result.Success)
            {
                result.Success = await SendEquipmentImfoReport(client.NetConfig, new List<EquipmentInformationDataParameter>() {
                                         new EquipmentInformationDataParameter(){
                                              Key=key,
                                              Value=value
                                         }
                                    });
                if (!result.Success)
                {
                    result.SetError("Send EquipmentImfo Report Error");
                }
            }
            return result;
        }

        private async Task SendReponseAsync(string cmd, string cmdId, string machineId, Result<DeviceCommandStatus> result)
        {
            try
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
            catch (Exception ex)
            {
                Log.LogError($"[mqtt] send report Error:{ex.Message},\n{ex.StackTrace}");
            }
        }

        public IResult<int> ReadData(PlcClient client, string code)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<int>();
            try
            {
                //Log.LogDebug($"设备[{client.NetConfig.Name}]读取指令:{code}, 耗时:{watch.ElapsedMilliseconds}毫秒");
                result.Data = client.ReadUInt16(code);
                //Log.LogDebug($"设备[{client.NetConfig.Name}]读取指令[{code}]返回数据:{result.Data},耗时:{watch.ElapsedMilliseconds}毫秒");
                return result;
            }
            catch (Exception ex)
            {
                Log.LogError($"读取设备[{client.NetConfig.Name}]指令[{code}]状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
                result.SetError($"读取设备[{client.NetConfig.Name}]指令[{code}]状态异常:{ex.Message}");
            }
            finally { watch.Stop(); }
            return result;
        }

        public IResult<bool> WriteData(PlcClient client, string code, ushort data)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<bool>();
            try
            {
                if (client == null)
                {
                    Log.LogWarning($"设备[{client.NetConfig.Name}]的PLCClient初始化异常,耗时:{watch.ElapsedMilliseconds}毫秒");
                    result.SetError(EquipmentErrorCode.PlcClientNullError);
                    result.SetError($"设备[{client.NetConfig.Name}]的PLCClient初始化异常");
                    return result;
                }
                //Log.LogDebug($"设备[{client.NetConfig.Name}]命令:{code}, 耗时:{watch.ElapsedMilliseconds}毫秒");
                if (client.NetConfig.QtyType == ConveyorQtyType.ScannerAutoChangeConveyor)
                {
                    var cmdResult = client.Send(code, data.ToString());
                    result.AddError(cmdResult);
                    result.Data = cmdResult.Success;
                }
                else
                {
                    result.Success = result.Data = client.WriteUInt16(code, data);
                    if (!result.Success)
                    {
                        result.SetError(EquipmentErrorCode.CommandFailed, $"[{code}]");
                        Log.LogError($"[{code}] WriteUInt16 commond failed");
                    }
                }
                //result.Data = true;
                Log.LogDebug($"设备[{client.NetConfig.Name}]发送设置命令{code}结果:{result.Data},耗时:{watch.ElapsedMilliseconds}毫秒");
                //SendPanelNumReport( result.Data);
                //await mqttClient.SendAsync(new CmdResponse() { data = new CmdResponseData { } }, client.NetConfig);
                return result;
            }
            catch (Exception ex)
            {
                Log.LogError($"读取设备[{client.NetConfig.Name}]指令[{code}]状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
                result.SetError(EquipmentErrorCode.UnknownError);
                result.SetError($"读取设备[{client.NetConfig.Name}]指令[{code}]状态异常:{ex.Message}");
            }
            finally { watch.Stop(); }
            return result;
        }


        public IResult<bool> WriteData(PlcClient plcClient, string code, string data)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<bool>();
            try
            {
                Log.LogDebug($"读取设备[{plcClient.NetConfig.Name}]指令[{code}]");
                if (plcClient == null)
                {
                    Log.LogWarning($"设备[{plcClient.NetConfig.Name}]的PLCClient初始化异常,耗时:{watch.ElapsedMilliseconds}毫秒");
                    result.SetError(EquipmentErrorCode.PlcClientNullError);
                    result.SetError($"设备[{plcClient.NetConfig.Name}]的PLCClient初始化异常");
                    //result.Data = DeviceCommandStatus.Failed;
                    return result;
                }
                if (plcClient.NetConfig.QtyType == ConveyorQtyType.ScannerConveyor)
                {
                    var resData = plcClient.ReadAndWriteCore(code, UTF8Encoding.UTF8.GetBytes(data));
                    result.Success = result.Data = ModbusHelper.GetBools(resData, 0, 1)[0];
                    if (!result.Success)
                    {
                        result.SetError(EquipmentErrorCode.CommandFailed);
                    }
                }
                else
                {

                    var resultData = Send(plcClient, code, data);
                    result.AddError(resultData);
                    Log.LogDebug($"[{plcClient.NetConfig.Topic}]接收数据:{resultData.Data}");
                    result.Data = resultData.Success;
                    if (!result.Success)
                    {
                        result.SetError(EquipmentErrorCode.CommandFailed);
                    }
                }
                //if (resultData.Success)
                //{
                //    result.Data = DeviceCommandStatus.Success;
                //    //var informations = new List<EquipmentInformationDataParameter>();
                //    //informations.Add(new EquipmentInformationDataParameter() { Key = "model", Value = resultData.Data });
                //    //if (!await SendEquipmentImfoReport(plcClient.NetConfig, informations))
                //    //{
                //    //    Log.LogError($"mqtt Client send Heart beat fail topic ={plcClient.NetConfig.Topic}");
                //    //}
                //}
                //else
                //{
                //    result.Data = DeviceCommandStatus.Failed;
                //}
                return result;
            }
            catch (Exception ex)
            {
                Log.LogError($"读取设备[{plcClient.NetConfig.Name}]指令[{code}]状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
                result.SetError(EquipmentErrorCode.UnknownError);
                result.SetError($"读取设备[{plcClient.NetConfig.Name}]指令[{code}]状态异常:{ex.Message}");
            }
            finally { watch.Stop(); }
            return result;
        }

        public IResult<string> Send(PlcClient client, params string[] cmds)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            IResult<string> result = new Result<string>();
            try
            {
                if (client == null)
                {
                    Log.LogWarning($"设备[{client.NetConfig.Name}]的PLCClient初始化异常,耗时:{watch.ElapsedMilliseconds}毫秒");
                    result.SetError(EquipmentErrorCode.PlcClientNullError);
                    result.SetError($"设备[{client.NetConfig.Name}]的PLCClient初始化异常");
                    return result;
                }
                return client.Send(cmds);
            }
            catch (Exception ex)
            {
                Log.LogError($"读取设备[{client.NetConfig.Name}]指令[{cmds.JoinAsString("&")}]状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
                result.SetError(EquipmentErrorCode.UnknownError);
                result.SetError($"读取设备[{client.NetConfig.Name}]指令[{cmds.JoinAsString("&")}]状态异常:{ex.Message}");
            }
            finally { watch.Stop(); }
            return result;
        }

        public delegate void EquipmentReportEventHandler(IEquipmentConfig config, IList<EquipmentInformationDataParameter> status);

        #region 设备状态报告
        /// <summary>
        /// 设备状态报告
        /// </summary>
        /// <param name="config">设备配置</param>
        /// <param name="stateInfoList">状态信息集合</param>
        public async ValueTask<bool> SendEquipmentImfoReport(IEquipmentConfig config, List<EquipmentInformationDataParameter> stateInfoList)
        {
            if (stateInfoList == null && stateInfoList.Count == 0) return true;
            var resportInfo = mqttClient.GetMsg<EquipmentInformation>(config);
            resportInfo.Data = new EquipmentInformationData() { Parameters = new List<EquipmentInformationDataParameter>() };
            resportInfo.Data.Parameters.AddRange(stateInfoList);

            if (ReportHandler != null)
            {
                ReportHandler(config, resportInfo.Data.Parameters);
            }
            return await mqttClient.SendAsync(resportInfo, config, config.Retries);
        }

        #endregion 设备状态报告


        #region 设备状态报告：每一个pcb完成任务后，发送本次工作的相关机器参数
        /// <summary>
        /// 设备状态报告：每一个pcb完成任务后，发送本次工作的相关机器参数
        /// </summary>
        public async ValueTask<bool> SendRunStatusReport(PlcClient client, string equipmentStatus)
        {
            /**
             * 
{
    "id":"uuid-1234-uixd-xxid",
    "messageType":"EquipmentImformation",
    "data":{
        "parameters":[
            {
                "key":"Power consumption", //耗电量， 后续所有实时参数在该数组动态增加即可
                "value":"10",
                "unit":"kilowatt-hour"
            }
        ]
    }
}
             * 
             * 
             */
            var resportInfo = mqttClient.GetMsg<EquipmentInformation>(client.NetConfig);
            resportInfo.Data = new EquipmentInformationData() { Parameters = new List<EquipmentInformationDataParameter>() };
            resportInfo.Data.Parameters.Add(new EquipmentInformationDataParameter() { Key = this.GetFieldDescription("EquipmentStatus"), Value = equipmentStatus });

            if (ReportHandler != null)
            {
                ReportHandler(client.NetConfig, resportInfo.Data.Parameters);
            }
            return await mqttClient.SendAsync(resportInfo, client.NetConfig, client.NetConfig.Retries);
        }
        #endregion


        #region 设备报警

        /// <summary>
        /// 设备报警：设备报警和设备故障的区别，设备故障表示机器已经无法正常运作，而设备报警则是第一层次的警报，        
        /// 用于当设备处理危险状态下的的报警使用，例如氮气量低，温度过高等
        /// </summary>
        public async Task<bool> SendWarningReport(PlcClient client, string errorInstanceId, string errorCode, string errorMsg)
        {
            Log.LogError($"[{client.NetConfig.Name}] SendWarningReport [EquipmentWarning]:{errorMsg}");
            return await SendErrorDataReport(client, errorInstanceId, errorCode, errorMsg, "EquipmentWarning");
        }

        /// <summary>
        /// 设备报警清除：当之前发生的故障被处理，机器恢复的时候，发送设备故障清除消息
        /// </summary>
        public async Task<bool> SendWarningCleardReport(PlcClient client, string errorInstanceId, string errorCode, string errorMsg)
        {
            Log.LogError($"[{client.NetConfig.Name}] SendWarningCleardReport [EquipmentWarningCleard]:{errorMsg}");
            return await SendErrorDataReport(client, errorInstanceId, errorCode, errorMsg, "EquipmentWarningCleard");
        }

        #endregion 设备报警


        #region 设备故障报告：设备发生故障无法继续运行的时候，发送设备故障消息
        /// <summary>
        /// 设备故障报告：设备发生故障无法继续运行的时候，发送设备故障消息
        /// </summary>
        public async Task<bool> SendErrorReport(PlcClient client, string errorInstanceId, string errorCode, string errorMsg)
        {
            Log.LogError($"[{client.NetConfig.Name}] SendErrorReport [EquipmentError]:{errorMsg}");
            return await SendErrorDataReport(client, errorInstanceId, errorCode, errorMsg, "EquipmentError");
        }

        /// <summary>
        /// 设备故障清除：当之前发生的故障被处理，机器恢复的时候，发送设备故障清除消息
        /// </summary>
        public async Task<bool> SendErrorCleardReport(PlcClient client, string errorInstanceId, string errorCode, string errorMsg)
        {
            Log.LogError($"[{client.NetConfig.Name}] SendErrorCleardReport [EquipmentErrorCleard]:{errorMsg}");
            return await SendErrorDataReport(client, errorInstanceId, errorCode, errorMsg, "EquipmentErrorCleard");
        }

        #endregion 设备故障报告


        /// <summary>
        /// 发送设备异常或清楚设备异常数据报告
        /// </summary>
        protected async Task<bool> SendErrorDataReport(PlcClient client, string errorInstanceId, string errorCode, string errorMsg, string messageType)
        {
            return await SendErrorDataReport(client, new EquipmentErrorData(errorInstanceId, errorCode, errorMsg), messageType);
        }

        /// <summary>
        /// 发送设备异常或清楚设备异常数据报告
        /// </summary>
        protected async Task<bool> SendErrorDataReport(PlcClient client, EquipmentErrorData errorData, string messageType)
        {
            var reportData = mqttClient.CreateMsg<EquipmentErrorData>(client.NetConfig);
            reportData.Data = errorData;
            reportData.MessageType = messageType;
            return await mqttClient.SendAsync(reportData, client.NetConfig, client.NetConfig.Retries);
        }

    }
}
