using Microsoft.Extensions.Logging;
using MQTTnet.Client;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Collections.Immutable;
using MQTTnet.Packets;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using System.Net;
using jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts
{
    public class MqttClientService : Volo.Abp.DependencyInjection.ISingletonDependency
    {
        protected readonly IConfiguration _config;
        private readonly ILogger<MqttClientService> _logger;
        private ImmutableList<EquipmentConfig> _equipmentList;
        private CancellationTokenSource _cts = null;
        private IMqttClient _client = null;
        JsonSerializerSettings JsonSerializerSettings = null;
        private int oldHash = 0;
        public event EventHandler<string> MessageReceived = null;
        private List<string> subList = new List<string>();
        private int _heartBeat = 50;

        /// <summary>
        /// 是否监听第三方链接
        /// </summary>
        public bool CheckOtherSysConnect { get; set; } = false;
        private MQTTnet.Protocol.MqttQualityOfServiceLevel Qos = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce;



        /// <summary>
        /// 设备配置信息
        /// </summary>
        public ImmutableList<EquipmentConfig> EquipmentList { get { return _equipmentList; } }

        public MqttClientService(IConfiguration config, ILogger<MqttClientService> logger)
        {
            _config = config;
            _logger = logger;
            _equipmentList = ImmutableList<EquipmentConfig>.Empty;
            JsonSerializerSettings = new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() };
            InitMachineList().Wait();
        }
        public async Task InitMachineList()
        {
            var lls = _config.GetSection("Equipments").Get<List<EquipmentConfig>>();
            var rs = JsonConvert.SerializeObject(lls);
            if (string.IsNullOrEmpty(rs))
            {
                _equipmentList = ImmutableList<EquipmentConfig>.Empty;
                return;
            }
            var hash = BKDRHash(rs);
            if (oldHash == hash) return;
            oldHash = hash;
            var list = JsonConvert.DeserializeObject<List<EquipmentConfig>>(rs);
            if (list == null || list.Any() == false) { _equipmentList = ImmutableList<EquipmentConfig>.Empty; }
            else
            {
                _equipmentList = ImmutableList.CreateRange(list.Where(f => f.Enable == true));
                if (_client != null && _client.IsConnected)
                {
                    var mList = _equipmentList;
                    foreach (var m in mList)
                    {
                        if (m.WithSubscribe && subList.Contains(m.Id) == false)
                        {
                            await _client.SubscribeAsync(new MqttTopicFilter { Topic = $"out/{m.Kind}/{m.Id}" });
                            subList.Add(m.Id);
                        }
                    }
                }
            }

            if (!int.TryParse(_config["PlcService: MqttHeartBeat"], out _heartBeat))
            {
                _heartBeat = 50;
            }
            var bData = false;
            if (bool.TryParse(_config["PlcService: CheckOtherSysConnect"], out bData))
            {
                CheckOtherSysConnect = bData;
            }
        }

        public int BKDRHash(string str)
        {
            int seed = 131;
            int hash = 0;
            for (int i = 0; i < str.Length; i++)
            {
                hash = hash * seed + str[i];
            }
            return (hash & 0x7FFFFFFF);
        }


        public async Task StartAsync()
        {
            var mqttConfig = _config.GetSection("Mqtt").Get<MqttConfig>();
            try
            {
                Qos = (MQTTnet.Protocol.MqttQualityOfServiceLevel)mqttConfig.Qos;
                _cts = new CancellationTokenSource();
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(mqttConfig.Host, mqttConfig.Port)
                    .WithClientId(Guid.NewGuid().ToString())
                    .WithWillQualityOfServiceLevel(Qos)
                    // .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithCredentials(mqttConfig.User, mqttConfig.Password).Build();
                _client = new MqttFactory().CreateMqttClient();
                _client.ApplicationMessageReceivedAsync += e =>
                {

                    var receivesBuffer = e.ApplicationMessage?.PayloadSegment.Array ?? new byte[0];
                    var mesBody = System.Text.Encoding.UTF8.GetString(receivesBuffer);
                    if (MessageReceived != null)
                        MessageReceived.Invoke(e, mesBody);
                    return Task.CompletedTask;
                };
                _client.ConnectedAsync += _client_ConnectedAsync;
                _client.DisconnectedAsync += _client_DisconnectedAsync;
                await _client.ConnectAsync(options);
                //订阅
                var mList = _equipmentList;
                subList.Clear();
                foreach (var m in mList)
                {
                    await _client.SubscribeAsync(new MqttTopicFilter { Topic = $"out/{m.Kind}/{m.Id}" });
                    subList.Add(m.Id);
                }

                if (mqttConfig.WithHeartBeat == true)
                    await Task.Factory.StartNew(async () =>
                    {
                        await Task.Run(() => { StartHeartBeat(); });
                    }, TaskCreationOptions.LongRunning);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Start Mqtt Error:{ex.Message}");
            }
        }

        private async Task _client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            await Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("MQTT client disconnect");
                    Thread.Sleep(3000);
                    await RestartAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"reconnect mqtt:{ex.Message}");
                }
            });
        }

        public async Task RestartAsync()
        {
            await InitMachineList();
            await StopAsync();
            await StartAsync();
        }


        private Task _client_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            return Task.Run(() => { _logger.LogInformation("MQTT client connect"); });
        }

        public async Task StopAsync()
        {
            try
            {
                if (_client != null)
                {
                    _client.ConnectedAsync -= _client_ConnectedAsync;
                    _client.DisconnectedAsync -= _client_DisconnectedAsync;
                    if (_client.IsConnected)
                        await _client.DisconnectAsync();
                    _client.Dispose();
                    _client = null;
                }
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation("MQTT client stop");
            }
        }

        public bool IsConnected => _client == null ? false : _client.IsConnected;

        /// <summary>
        /// 服务心跳
        /// </summary>
        private async void StartHeartBeat()
        {
            CancellationToken cts = _cts.Token;
            while (!cts.IsCancellationRequested)
            {
                if (_client != null && _client.IsConnected)
                {
                    await InitMachineList();
                    var curList = _equipmentList;
                    foreach (var cur in curList)
                    {
                        // if (CheckOtherSysConnect && !cur.IsConnected) //需要根据第三方系统的连接性来判断是否需要发送心跳包
                        // {
                        //     continue; //第三方系统没有连接，不发送心跳包
                        // }
                        if (!cur.WithHeartBeat) continue;
                        try
                        {
                            var msg = new EquipmentHeartbeat();
                            FillBaseField(msg, cur);
                            var message = new MqttApplicationMessageBuilder()
                                  .WithTopic(cur.Topic)
                                  //.WithTopic($"out/convoyor/{cur.Id}")
                                  .WithPayload(Newtonsoft.Json.JsonConvert.SerializeObject(msg, JsonSerializerSettings)) //JsonSerializer.Serialize(msg)
                                  .WithQualityOfServiceLevel(Qos)
                                  .WithRetainFlag().Build();
                            var result = await _client.PublishAsync(message, cts);
                            if (!result.IsSuccess)
                            {
                                _logger.LogError($"发送心跳包失败,ReasonCode:{result.ReasonCode}\r\n{result.ReasonString}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"发送心跳包异常:{ex.Message}\r\n{ex.StackTrace}");
                        }
                    }
                    await Task.Delay(_heartBeat * 1000);
                    //Thread.Sleep(_heartBeat * 1000);
                }
            }
        }

        public BaseMsg<T> CreateMsg<T>(IEquipmentConfig equipment, T data = null) where T : class, new()
        {
            var msg = new BaseMsg<T>();
            FillBaseField(msg, equipment);
            msg.Data = data;
            msg.MessageType = typeof(T).Name;
            return msg;
        }
        public BaseMsg<dynamic> CreateMsg(string messageType, IEquipmentConfig equipment, dynamic data)
        {
            var msg = new BaseMsg<dynamic>();
            FillBaseField(msg, equipment);
            msg.Data = data;
            msg.MessageType = messageType;
            return msg;
        }

        public BaseMsg CreateMsg(IEquipmentConfig equipment)
        {
            var msg = new BaseMsg();
            FillBaseField(msg, equipment);
            return msg;
        }

        public T GetMsg<T>(IEquipmentConfig equipment, T msg = null) where T : BaseMsg, new()
        {
            if (msg == null) msg = new T();
            msg.MessageType = typeof(T).Name;
            FillBaseField(msg, equipment);
            return msg;
        }

        private void FillBaseField(BaseMsg msg, IEquipmentConfig equipment)
        {
            msg.Id = Guid.NewGuid().ToString("D");
            msg.TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (string.IsNullOrEmpty(msg.PanelId))
                msg.PanelId = "";
            if (string.IsNullOrWhiteSpace(msg.MessageType))
                msg.MessageType = msg.GetType().Name;
            SetBaseInfo(msg, equipment);
        }

        public void SetBaseInfo(BaseMsg msg, IEquipmentConfig equipment)
        {
            msg.Sender = equipment.Id;
            if (string.IsNullOrWhiteSpace(msg.HostName))
                msg.HostName = equipment.Host;
            if (string.IsNullOrWhiteSpace(msg.HostName))
                msg.HostName = equipment.Ip;
            msg.Kind = equipment.Kind;
        }

        public async ValueTask<bool> SendAsync(BaseMsg msg, IEquipmentConfig equipment, int tryTimes = 3)
        {
            if (!_client.IsConnected)
            {
                //失败等待重连(10秒一次)，重试3次
                _logger.LogError($"[mqtt][{equipment.Topic}][{tryTimes}] IsConnected faild .");
                await Task.Delay(5000);
                if (tryTimes >= 0)
                    return await SendAsync(msg, equipment, --tryTimes);
                _logger.LogError($"[mqtt][{equipment.Topic}][{tryTimes}] 链接超时上报命令失败！");
                return false;
            }
            if (msg == null)
            {
                _logger.LogError($"[mqtt][{equipment.Topic}][{tryTimes}] 发送消息对象为空.");
                return true;
            }
            if (tryTimes == equipment.Retries) FillBaseField(msg, equipment);

            //   var payload = JsonSerializer.Serialize(msg, options);
            var payload = JsonConvert.SerializeObject(msg, JsonSerializerSettings);
            var message = new MqttApplicationMessageBuilder()
                      .WithTopic(equipment.Topic)
                      .WithPayload(payload)
                      .WithQualityOfServiceLevel(Qos)
                      .WithRetainFlag().Build();
            var response = await _client.PublishAsync(message);
            if (response.IsSuccess)
            {
                return true;
            }
            else if (tryTimes > 0)
            {
                _logger.LogError($"[mqtt][{equipment.Topic}][{tryTimes}]send faild :{response.ReasonString}");
                //失败重试3次（延迟N次方秒）
                var time = (equipment.Retries - tryTimes + 1);
                await Task.Delay(1000 * time);
                return await SendAsync(msg, equipment, --tryTimes);
            }
            return false;
        }


        public ImmutableList<EquipmentConfig> MachineList => _equipmentList;
    }

}
