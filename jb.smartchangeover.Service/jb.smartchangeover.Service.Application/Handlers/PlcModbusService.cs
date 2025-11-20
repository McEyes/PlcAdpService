using AoiAdapterService.Mqtts;
using Confluent.Kafka;
using jb.smartchangeover.Service.Application.Contracts.Mqtts.Dtos;
using jb.smartchangeover.Service.Domain.Shared;
using jb.smartchangeover.Service.Domain.Shared.Plc;
using jb.smartchangeover.Service.Domain.Shared.Plc.Configs;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using Nest;
using Newtonsoft.Json;
using NModbus;
using NModbus.IO;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Application.Handlers
{
    public class PlcModbusService : Volo.Abp.DependencyInjection.ISingletonDependency
    {
        private ImmutableList<PlcConfig> PlcConfigList = null;
        private ImmutableList<IModbusMaster> PlcMasterList = null;
        private MqttClientService mqttClient = null;

        public EquipmentReportEventHandler ReportHandler { get; set; } = null;

        private System.Timers.Timer _timer = null;
        /// <summary>
        /// 单位秒
        /// </summary>
        private int HeartBeat = 1;
        private int MonitorInterval = 1;

        private readonly ILogger<PlcHandler> Log = null;
        protected readonly IConfiguration _config = null;

        private int oldHash = 0;
        private int RetryTime = 3;

        private bool isTick = false;


        /// <summary>
        /// PLC 但前的状态
        /// </summary>
        public int PlcStatus { get; set; }


        private readonly System.Collections.Concurrent.ConcurrentStack<CmdCacheItem> CmdList = null;

        public PlcModbusService(MqttClientService mqttClient, ILogger<PlcHandler> loger, IConfiguration config)
        {
            Log = loger;
            _config = config;
            this.mqttClient = mqttClient;
            this.mqttClient.MessageReceived += MqttClient_MessageReceived;
            InitConfig();
        }

        /// <summary>
        /// 获取服务PLC设备配置，监控哪些设备
        /// 初始化监控配置
        /// </summary>
        private void InitConfig()
        {
            try
            {
                var lls = _config.GetSection("Equipments").Get<List<PlcConfig>>();
                var rs = JsonConvert.SerializeObject(lls);
                if (string.IsNullOrEmpty(rs))
                {
                    PlcConfigList = ImmutableList<PlcConfig>.Empty;
                    PlcMasterList = ImmutableList<IModbusMaster>.Empty;
                    Log.LogWarning($" PLC Init Equipments Config Empty:{rs}");
                    return;
                }
                var hash = mqttClient.BKDRHash(rs);
                if (oldHash == hash) return;
                oldHash = hash;
                var list = JsonConvert.DeserializeObject<List<PlcConfig>>(rs);
                if (list == null || list.Any() == false)
                {
                    PlcConfigList = ImmutableList<PlcConfig>.Empty;
                }
                else
                {
                    PlcConfigList = ImmutableList.CreateRange(list.Where(f => f.Enable));
                }
                InitNetClient();
                if (!int.TryParse(_config["PlcHandler:RetryTime"], out RetryTime))
                {
                    RetryTime = 3;
                }
                if (!int.TryParse(_config["PlcHandler:HeartBeat"], out HeartBeat))
                {
                    HeartBeat = 1;
                }
                if (!int.TryParse(_config["PlcHandler:MonitorInterval"], out MonitorInterval))
                {
                    MonitorInterval = 1;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($" PLC Init Config Error:{ex.Message}\r\n{ex.StackTrace}");
                Log.LogException(ex);
            }
        }



        private void InitNetClient()
        {
            var list = new List<IModbusMaster>();
            foreach (var item in PlcConfigList)
            {
                ModbusFactory.CreateMaster(new ModbusFactory(), new TcpClientAdapter(item.Ip, item.Port));
                list.Add(NetClientFactory.GetPlcInstance(item, Log));
            }
            PlcMasterList = ImmutableList.CreateRange(list);
        }



        public async Task StartAsync()
        {
            await this.mqttClient.StartAsync();

        }



        private async void MqttClient_MessageReceived(object? sender, string msgBody)
        {
            try
            {
                Log.LogDebug($"[Mqtt] Received Message:[{msgBody}]");
                if (sender is MqttApplicationMessageReceivedEventArgs)
                {
                    var client = sender as MqttApplicationMessageReceivedEventArgs;
                    var plcClient = PlcMasterList.FirstOrDefault(f => client.ApplicationMessage.Topic.EndsWith(f.NetConfig.Id));
                    if (plcClient == null)
                    {
                        InitConfig();
                        plcClient = PlcMasterList.FirstOrDefault(f => client.ApplicationMessage.Topic.EndsWith(f.NetConfig.Id));
                        if (plcClient == null)
                        {
                            Log.LogError($"[Mqtt][{client.ApplicationMessage.Topic}]消息PLC对象不存在，当前接收的消息内容为:{msgBody}");
                            return;
                        }
                    }
                    var cmdMsg = msgBody.FromJSON<BaseMsg<CmdExecute>>();

                    if ("CmdExecute".Equals(cmdMsg.MessageType, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (cmdMsg.Data == null || string.IsNullOrWhiteSpace(cmdMsg.Data.Cmd))
                        {
                            Log.LogError($"[Mqtt] [{plcClient.NetConfig.Name}]消息命令格式错误，缺少命令信息,当前接收的消息内容为:{msgBody}");
                            return;
                        }
                        await SendCmd(plcClient, cmdMsg, msgBody);
                    }
                    else
                    {
                        Log.LogDebug($"[Mqtt][{plcClient.NetConfig.Name}] unknown cmd Msg:{msgBody}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[Mqtt] Message Received 异常,\r\n {ex.Message}\r\n {ex.StackTrace},\r\n msgBody: {msgBody} ");
            }
        }


        public async ValueTask<IResult> SendCmd(PlcClient client, BaseMsg<CmdExecute> msg, string cmdJson)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new JbResult();
            try
            {
                switch (msg.Data.Cmd.ToLower())
                {
                    case "readwidth":
                        {
                            return await ReadWidth(client);
                        }
                    case "writewidth":
                        {
                            return WriteWidth(client, msg.Data.Parameter.ToUShort());
                        }
                    //    case "readequipmentrunstate":
                    //        {
                    //            return ReadEquipmentRunState();
                    //        }
                    case "stopequipment":
                        {
                            return StopEquipment(client);
                        }
                    case "startequipment":
                        {
                            return StartEquipment(client);
                        }
                    case "readpcbnum":
                        {
                            return ReadPcbNum(client);
                        }
                    case "resetpcbnum":
                        {
                            return ResetPcbNum(client);
                        }
                    case "readstatus":
                        {
                            return ReadStatus(client);
                        }
                    case "checkhaspanel":
                        {
                            return ReadHasPanel(client);
                        }
                    case "readrunpath":
                        {
                            return ReadRunPath(client);
                        }
                    case "writerunpath":
                        {
                            return WriteRunPath(client, msg.Data.Parameter.ToUShort());
                        }
                    case "readspeed":
                        {
                            return ReadSpeed(client);
                        }
                    case "writespeed":
                        {
                            return WriteSpeed(client, msg.Data.Parameter.ToUShort());
                        }
                    case "resetwidth":
                        {
                            return ResetWidth(client);
                        }
                    case "resetwidth2":
                        {
                            return ResetWidth2(client);
                        }
                    case "readtransfermode":
                        {
                            return ReadTransferMode(client);
                        }
                    case "writetransfermode":
                        {
                            return WriteTransferMode(client, msg.Data.Parameter.ToUShort());
                        }
                    default:
                        {
                            //检查cmd命令格式
                            ushort data = 0;
                            if (ushort.TryParse(msg.Data.Parameter, out data))
                            {
                                if (msg.Data.IsWriteCmd)
                                {
                                    return WriteData(client, msg.Data.Cmd, data);
                                }
                                else
                                {
                                    return ReadData(client, msg.Data.Cmd);
                                }
                            }
                            else
                            {
                                return ReadData(client, msg.Data.Cmd);
                            }
                        }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[{client.NetConfig.Name}][{client.NetConfig.Ip}] SendCmd Errpr:{ex.Message},\r\n{ex.StackTrace},\r\n耗时:{watch.ElapsedMilliseconds}毫秒");
                result.SetError($"[{client.NetConfig.Name}][{client.NetConfig.Ip}] SendCmd Errpr:{ex.Message},\r\n{ex.StackTrace},\r\n耗时:{watch.ElapsedMilliseconds}毫秒");
            }
            finally
            {
                watch.Stop();
            }
            return result;
        }

        //#region 设备状态报告
        ///// <summary>
        ///// 设备状态报告
        ///// </summary>
        ///// <param name="stateInfoList">状态信息集合</param>
        //public void SendDataReport(List<EquipmentImformationDataParameter> stateInfoList)
        //{
        //    if (stateInfoList == null && stateInfoList.Count == 0) return;
        //    var resportInfo = _mqttClient.GetMsg<EquipmentImformation>();
        //    resportInfo.Data = new EquipmentImformationData() { Parameters = new List<EquipmentImformationDataParameter>() };
        //    resportInfo.Data.Parameters.AddRange(stateInfoList);//.Add(new EquipmentImformationDataParameter() { Key = "PanelNumber", Value = num.ToString() });

        //    if (ReportHandler != null)
        //    {
        //        ReportHandler(Config, resportInfo.Data.Parameters);
        //    }
        //    _mqttClient.Send(resportInfo);
        //}
        //#endregion 设备状态报告


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
                                              Value=(result.Data/100f).ToString()
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
        /// 设置轨道宽度
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> WriteWidth(PlcClient client, ushort width)
        {
            return WriteData(client, PlcBufferRegister.Change, width);
        }

        #endregion 轨道宽度命令


        #region 控制轨道启停

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

        private async ValueTask<bool> SendEquipmentHeartbeat(PlcClient client, dynamic data)
        {
            var resportInfo = mqttClient.GetMsg<EquipmentHeartbeat>(client.NetConfig);
            resportInfo.Data = data;
            return await mqttClient.SendAsync(resportInfo, client.NetConfig);
        }

        /// <summary>
        /// 获取当前轨道启停状态
        /// </summary>
        /// <returns></returns>
        public virtual IResult<int> ReadEquipmentRunState(PlcClient client)
        {
            var result = ReadData(client, PlcBufferRegister.Control);
            return SendEquipmentImfoReport(client, result, "RunState", ((ControlCode)result.Data).ToString()).Result;
        }

        /// <summary>
        /// 设置轨道启停
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> WriteEquipmentRunState(PlcClient client, bool stop)
        {
            return stop ? StopEquipment(client) : StartEquipment(client);
        }

        /// <summary>
        /// 停止设备
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> StopEquipment(PlcClient client)
        {
            return WriteData(client, PlcBufferRegister.Control, 0);
        }

        /// <summary>
        /// 启动设备
        /// </summary>
        /// <returns></returns>
        public virtual IResult<bool> StartEquipment(PlcClient client)
        {
            return WriteData(client, PlcBufferRegister.Control, 1);
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
            return SendEquipmentImfoReport(client,result, "PcbNum", result.Data == 1 ? "有板" : "没板").Result;
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
            return WriteData(client, PlcBufferRegister.WidthReset, 2);
            //return SendEquipmentImfoReport(client, result, "PcbNum", result.Data.ToString()).Result;
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

        public virtual void SendEquipmentResponse(PlcClient client, BaseMsg<CmdExecute> msg, IResult errMsg)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            try
            {
                //var data = new EquipmentResponse(msg, errMsg);
                ////命令回复
                //if (!mqttClient.ResponseMsg(data))
                //{
                //    Log.LogError($"[{Config.Name}]ResponseMsg失败:{data.ToJSON()},\n耗时:{watch.ElapsedMilliseconds}毫秒");
                //}
            }
            catch (Exception ex)
            {
                Log.LogError($"[{client.NetConfig.Name}]ResponseMsg失败:{msg.ToJSON()},\n{ex.Message},\n {ex.StackTrace}\n耗时:{watch.ElapsedMilliseconds}毫秒");
            }
            watch.Stop();
        }


        protected T GetMsgData<T>(string msgBody)
        {
            if (!string.IsNullOrWhiteSpace(msgBody))
            {
                var msg = msgBody.FromJSON<BaseMsg<T>>();
                if (msg.Data != null)
                {
                    return msg.Data;
                }
            }
            return default(T);
        }



        private async Task SendReponseAsync(string cmd, string cmdId, bool resultCode, string resultMsg, string machineId)
        {
            var responseMsg = new CmdResponse();
            var cmddata = new CmdResponseData();
            cmddata.cmd = cmd;
            cmddata.cmdID = cmdId;
            cmddata.errCode = resultCode.ToString();
            cmddata.errMsg = resultMsg;
            cmddata.result = resultCode ? "success" : "failed";
            responseMsg.data = cmddata;


            var m = mqttClient.MachineList;
            var machineDto = m.FirstOrDefault(p => p.Id == machineId);
            if (machineDto != null)
            {
                await mqttClient.SendAsync(responseMsg, machineDto);
            }
        }

        public IResult<ushort[]> ReadData(PlcClient client, string code, byte length)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<ushort[]>();
            try
            {
                Log.LogDebug($"设备[{client.NetConfig.Name}]读取指令:{code}, 耗时:{watch.ElapsedMilliseconds}毫秒");
                result.Data = client.ReadUInt16(code, length);
                Log.LogDebug($"设备[{client.NetConfig.Name}]读取数据:{result},耗时:{watch.ElapsedMilliseconds}毫秒");
                return result;
            }
            catch (Exception ex)
            {
                Log.LogError($"读取设备[{client.NetConfig.Name}]运行状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
                result.SetError($"读取设备[{client.NetConfig.Name}]指令[{code}]状态异常:{ex.Message}");
            }
            finally { watch.Stop(); }
            return result;
        }

        public IResult<int> ReadData(PlcClient client, string code)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<int>();
            try
            {
                Log.LogDebug($"设备[{client.NetConfig.Name}]读取指令:{code}, 耗时:{watch.ElapsedMilliseconds}毫秒");
                result.Data = client.ReadUInt16(code);
                Log.LogDebug($"设备[{client.NetConfig.Name}]读取指令[{code}]返回数据:{result.Data},耗时:{watch.ElapsedMilliseconds}毫秒");
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
                var num = false;
                if (client == null)
                {
                    Log.LogDebug($"设备[{client.NetConfig.Name}]的PLCClient初始化异常,耗时:{watch.ElapsedMilliseconds}毫秒");
                    result.SetError($"设备[{client.NetConfig.Name}]的PLCClient初始化异常");
                    return result;
                }
                Log.LogDebug($"设备[{client.NetConfig.Name}]命令:{code}, 耗时:{watch.ElapsedMilliseconds}毫秒");
                num = result.Data = client.WriteUInt16(code, data);
                Log.LogDebug($"设备[{client.NetConfig.Name}]发送设置命令{code}结果:{num},耗时:{watch.ElapsedMilliseconds}毫秒");
                //SendPanelNumReport(num);
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

        public IResult<bool> WriteData(PlcClient client, string code, ushort[] data)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<bool>();
            try
            {
                var num = false;
                if (client == null)
                {
                    Log.LogDebug($"设备[{client.NetConfig.Name}]的PLCClient初始化异常,耗时:{watch.ElapsedMilliseconds}毫秒");
                    result.SetError($"设备[{client.NetConfig.Name}]的PLCClient初始化异常");
                    return result;
                }
                Log.LogDebug($"设备[{client.NetConfig.Name}]命令:{code}, 耗时:{watch.ElapsedMilliseconds}毫秒");
                result.Data = client.WriteUInt16(code, data);
                Log.LogDebug($"设备[{client.NetConfig.Name}]发送设置命令{code}结果:{num},耗时:{watch.ElapsedMilliseconds}毫秒");
                //SendPanelNumReport(num);
                return result;
            }
            catch (Exception ex)
            {
                Log.LogError($"读取设备[{client.NetConfig.Name}]运行指令[{code}]状态异常:{ex.Message}");
            }
            finally { watch.Stop(); }
            return result;
        }



        /// <summary>
        /// 默认读取PLC轨道的状态
        /// </summary>
        /// <param name="needReportFlag"></param>
        /// <returns></returns>
        public virtual async ValueTask<IResult<ControlCode>> ReadEquipmentState(PlcClient client, Boolean needReportFlag = true)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<ControlCode>();
            try
            {
                if (client != null)
                {
                    var hasChange = false;
                    var pcbNum = ReadData(client, PlcBufferRegister.Counter).Data;
                    if (client.PcbNum != pcbNum)
                    {
                        hasChange = true;
                        client.PcbNum = pcbNum;
                    }

                    var hasPcb = ReadData(client, PlcBufferRegister.HasPanel).Data;
                    if (client.HasPcb != hasPcb)
                    {
                        hasChange = true;
                        client.HasPcb = hasPcb;
                    }

                    var runStatus = ReadData(client, PlcBufferRegister.RunStatus).Data;
                    if (client.EquipmentStatus != (ControlCode)runStatus)
                    {
                        hasChange = true;
                        client.EquipmentStatus = (ControlCode)runStatus;
                    }
                    if (hasChange)
                    {
                        if (await SendEquipmentHeartbeat(client, new { PcbNum = pcbNum, HasPcb = hasPcb, RunStatus = runStatus }))
                        {
                            result.SetError($"mqtt Client send Heart beat fail PcbNum ={pcbNum}，HasPcb ={hasPcb}");
                        }
                    }
                }
                else
                {
                    result.SetError("PLCClient is null");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"读取设备[{client.NetConfig.Name}]运行状态异常:{ex.Message},\n{ex.StackTrace},\n耗时:{watch.ElapsedMilliseconds}毫秒");
                result.SetError($"读取设备[{client.NetConfig.Name}]运行状态异常:{ex.Message}");
            }
            finally { watch.Stop(); }
            return result;
        }

        //        //public ushort ReadUInt16()
        //        //{
        //        //    var resualt = _netClient.send(((NxtAsciiClient)_netClient).getCmd(new KeepAliveResponse()));
        //        //    if (resualt != null)
        //        //    {
        //        //        Log.Debug($"exc cmd result :{string.Join(" ", resualt)}.");
        //        //    }
        //        //    int d = new System.Random(1).Next(2);
        //        //    return (ushort)d;
        //        //}

        //        //private ushort SetEvent(string param)
        //        //{
        //        //    var resualt = _netClient.send(((NxtAsciiClient)_netClient).getCmd(new SetEvRequest() { SeqID = NxtAsciiClient.GenSeqID, Name = "SETEV", MachineName = "AE1220", Ack = "1" }));
        //        //    if (resualt != null)
        //        //    {
        //        //        Log.Debug($"exc cmd result :{string.Join(" ", resualt)}.");
        //        //    }
        //        //    int d = new System.Random(1).Next(2);
        //        //    return (ushort)d;
        //        //}

        //        #region 设备上传消息

        //        #region cmd命令消息回复
        //        //public void SendEquipmentResponse(BaseMsg<CmdExecute> msg, IResult errMsg)
        //        //{
        //        //    Stopwatch watch = new Stopwatch();
        //        //    watch.Start();
        //        //    try
        //        //    {
        //        //        var data = new EquipmentResponse(msg, errMsg);
        //        //        //命令回复
        //        //        if (!_mqttClient.ResponseMsg(data))
        //        //        {
        //        //            Log.LogError($"[{Config.Name}]ResponseMsg失败:{data.ToJSON()},\n耗时:{watch.ElapsedMilliseconds}毫秒");
        //        //        }
        //        //    }
        //        //    catch (Exception ex)
        //        //    {
        //        //        Log.LogError($"[{Config.Name}]ResponseMsg失败:{msg.ToJSON()},\n{ex.Message},\n {ex.StackTrace}\n耗时:{watch.ElapsedMilliseconds}毫秒");
        //        //    }
        //        //    watch.Stop();
        //        //}
        //        #endregion cmd命令消息回复

        public delegate void EquipmentReportEventHandler(IEquipmentConfig config, IList<EquipmentInformationDataParameter> status);
        #region 设备状态报告
        /// <summary>
        /// 设备状态报告
        /// </summary>
        /// <param name="config">设备配置</param>
        /// <param name="stateInfoList">状态信息集合</param>
        public async ValueTask<bool> SendEquipmentImfoReport(IEquipmentConfig config, List<EquipmentInformationDataParameter> stateInfoList)
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
            if (stateInfoList == null && stateInfoList.Count == 0) return false;
            var resportInfo = mqttClient.GetMsg<EquipmentInformation>(config);
            resportInfo.Data = new EquipmentInformationData() { Parameters = new List<EquipmentInformationDataParameter>() };
            resportInfo.Data.Parameters.AddRange(stateInfoList);//.Add(new EquipmentImformationDataParameter() { Key = "PanelNumber", Value = num.ToString() });

            if (ReportHandler != null)
            {
                ReportHandler(config, resportInfo.Data.Parameters);
            }
            return await mqttClient.SendAsync(resportInfo, config);
        }

        #endregion 设备状态报告


        //        #region 设备状态报告：每一个pcb完成任务后，发送本次工作的相关机器参数


        //        #endregion 设备状态报告


        //        #region 设备心跳(已经有自动心跳，不需要额外添加)

        //        #endregion 设备心跳


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
            return await mqttClient.SendAsync(resportInfo, client.NetConfig);
        }
        #endregion

        //        /// <summary>
        //        /// 设备状态变更
        //        /// "currentState":"READY-IDLE-BLOCKED" //报警发生时候的编码，EquipmentWarning
        //        /// "previousState":"READY-PROCESSING-ACTIVE",
        //        /// "eventId":"EquipmentBlocked"
        //        /// </summary>
        //        /// <param name="currentState">"READY-IDLE-BLOCKED" //报警发生时候的编码，EquipmentWarning</param>
        //        /// <param name="previousState">"READY-PROCESSING-ACTIVE",</param>
        //        /// <param name="eventId">"EquipmentBlocked"</param>
        //        /// <param name="messageType">报告类型</param>
        //        protected bool SendChangeStateReport(string currentState, string previousState, string eventId)
        //        {
        //            return SendDataReport(new { currentState, previousState, eventId }, "EquipmentChangeState");
        //        }

        //        #endregion 设备状态报告


        //        #region 设备报警

        //        /// <summary>
        //        /// 设备报警：设备报警和设备故障的区别，设备故障表示机器已经无法正常运作，而设备报警则是第一层次的警报，        
        //        /// 用于当设备处理危险状态下的的报警使用，例如氮气量低，温度过高等
        //        /// </summary>
        //        public bool SendWarningReport(string errorInstanceId, string errorCode, string errorMsg)
        //        {
        //            return SendErrorDataReport(errorInstanceId, errorCode, errorMsg, "EquipmentWarning");
        //        }

        //        /// <summary>
        //        /// 设备报警清除：当之前发生的故障被处理，机器恢复的时候，发送设备故障清除消息
        //        /// </summary>
        //        public bool SendWarningCleardReport(string errorInstanceId, string errorCode, string errorMsg)
        //        {
        //            return SendErrorDataReport(errorInstanceId, errorCode, errorMsg, "EquipmentWarningCleard");
        //        }

        //        #endregion 设备报警


        //        #region 设备故障报告：设备发生故障无法继续运行的时候，发送设备故障消息
        //        /// <summary>
        //        /// 设备故障报告：设备发生故障无法继续运行的时候，发送设备故障消息
        //        /// </summary>
        //        public bool SendErrorReport(string errorInstanceId, string errorCode, string errorMsg)
        //        {
        //            return SendErrorDataReport(errorInstanceId, errorCode, errorMsg, "EquipmentError");
        //        }

        //        /// <summary>
        //        /// 设备故障清除：当之前发生的故障被处理，机器恢复的时候，发送设备故障清除消息
        //        /// </summary>
        //        public bool SendErrorCleardReport(string errorInstanceId, string errorCode, string errorMsg)
        //        {
        //            return SendErrorDataReport(errorInstanceId, errorCode, errorMsg, "EquipmentErrorCleard");
        //        }

        //        #endregion 设备故障报告


        //        #region 设备工作报告

        //        /// <summary>
        //        /// 开始工作;当pcb板进入，机器开始该次任务的时候发送该消息
        //        /// </summary>
        //        public bool SendItemWorkStartReport(string instanceId)
        //        {
        //            return SendDataReport(new { instanceId }, "ItemWorkStart");
        //        }


        //        /// <summary>
        //        /// 完成工作,完成本次工作任务发送
        //        /// </summary>
        //        public bool SendItemWorkCompleteReport(string instanceId)
        //        {
        //            return SendDataReport(new { instanceId }, "ItemWorkComplete");
        //        }


        //        /// <summary>
        //        /// 工作中断:当遇到错误，无法完成本次任务，任务中断, 发送该消息，并要填写对应的错误原因
        //        /// </summary>
        //        /// <param name="instanceId">pcb sn编码</param>
        //        /// <param name="pauseMsg">任务未能完成原因</param>
        //        public bool SendItemWorkAbortReport(string instanceId, string pauseMsg)
        //        {
        //            return SendDataReport(new { instanceId, pauseMsg }, "ItemWorkAbort");
        //        }


        //        /// <summary>
        //        /// 工作中断:每一次pcb版的处理结果
        //        /// "instanceId":"22000***",//pcb sn编码
        //        /// "status":"good", //本次任务处理结果，good: 成功，bad：失败
        //        /// "msg":"success",//当失败的时候，需要填写失败原因
        //        /// "extents":{},//处理详细参数，可为空
        //        /// </summary>
        //        /// <param name="instanceId">pcb sn编码</param>
        //        /// <param name="pauseMsg">任务未能完成原因</param>
        //        public bool SendItemProcessStatusReport(string instanceId, string status, string msg, dynamic extents)
        //        {
        //            return SendDataReport(new { instanceId, status, msg, extents }, "ItemProcessStatus");
        //        }

        //        #endregion 设备工作报告



        //        #region 机器参数调整：当系统参数发生变更的时候发送该消息
        ////        /// <summary>
        ////        /// 机器参数调整：当系统参数发生变更的时候发送该消息
        ////        /// </summary>
        ////        public bool SendParameterModifiedPreport(List<ChangeParameterItem> parameterList)
        ////        {
        ////            return SendDataReport(new { parameter = parameterList }, "EquipmentParameterModified");
        ////            /**
        ////             * 
        ////{
        ////    "id":"uuid-1234-uixd-xxid",
        ////    "messageType":"EquipmentParameterModified",
        ////    "data":{
        ////    "parameter":[ //数组形式,不同机器发送不同参数
        ////        {
        ////            "name": "Volume",
        ////            "units": "%",
        ////            "oldValue": "119.7",
        ////            "newValue": "50.0"
        ////        }
        ////    ]
        ////    }
        ////}
        ////             * 
        ////             * 
        ////             */
        ////        }

        //        #endregion 机器参数调整

        //        #region 用户登录

        //        /// <summary>
        //        /// 用户登录:
        //        /// 用户登陆机器的操作软件，无需登录则无需发送该消息
        //        /// "loginId":"****",//id , can be empty
        //        /// "loginName":"jax", //name,can be empty
        //        /// "status":"success" //success or failed
        //        /// </summary>
        //        /// <param name="instanceId"></param>
        //        /// <param name="status"></param>
        //        /// <param name="msg"></param>
        //        /// <param name="extents"></param>
        //        /// <returns></returns>
        //        public bool SendOperatorInformationReport(string loginId, string loginName, string status)
        //        {
        //            return SendDataReport(new { loginId, status, loginName }, "OperatorInformation");
        //        }

        //        #endregion 用户登录

        //        #region 等待工程师处理

        //        /// <summary>
        //        /// 等待工程师处理:
        //        /// 任务无法继续，等待工程师处理
        //        /// "loginId":"****",//id , can be empty
        //        /// "loginName":"jax", //name,can be empty
        //        /// "status":"success" //success or failed
        //        /// </summary>
        //        /// <param name="instanceId"></param>
        //        /// <param name="status"></param>
        //        /// <param name="msg"></param>
        //        /// <param name="extents"></param>
        //        /// <returns></returns>
        //        public bool SendWaitingforOperatorActionReport(string description)
        //        {
        //            return SendDataReport(new { description }, "WaitingforOperatorAction");
        //        }

        //        #endregion 等待工程师处理

        //        #region 设备上报信息接口


        //        /// <summary>
        //        /// 发送设备异常或清楚设备异常数据报告
        //        /// </summary>
        //        protected bool SendErrorDataReport(string errorInstanceId, string errorCode, string errorMsg, string messageType)
        //        {
        //            return SendErrorDataReport(new EquipmentErrorData(errorInstanceId, errorCode, errorMsg), messageType);
        //        }

        //        /// <summary>
        //        /// 发送设备异常或清楚设备异常数据报告
        //        /// </summary>
        //        protected bool SendErrorDataReport(EquipmentErrorData errorData, string messageType)
        //        {
        //            var reportData = _mqttClient.CreateMsg<EquipmentErrorData>();
        //            reportData.Data = errorData;
        //            reportData.MessageType = messageType;
        //            return _mqttClient.Send(reportData);
        //        }

        //        /// <summary>
        //        /// 发送设备数据
        //        /// </summary>
        //        protected bool SendDataReport<T>(T data, string messageType = null) where T : class, new()
        //        {
        //            var reportData = _mqttClient.CreateMsg<T>();
        //            reportData.Data = data;
        //            if (messageType != null)
        //                reportData.MessageType = messageType;
        //            return _mqttClient.Send(reportData);
        //        }
        //        /// <summary>
        //        /// 发送设备数据
        //        /// </summary>
        //        protected bool SendDataReport(dynamic data, string messageType)
        //        {
        //            var reportData = _mqttClient.CreateMsg<dynamic>();
        //            reportData.Data = data;
        //            reportData.MessageType = messageType;
        //            return _mqttClient.Send(reportData);
        //        }

        //        #endregion  设备上报信息接口


        //        #endregion 设备上传消息
    }
}
