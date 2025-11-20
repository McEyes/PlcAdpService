using AoiAdapterService.Mqtts;
using jb.smartchangeover.Service.Application.Contracts.Mqtts.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Application.Handlers
{
    public class DekExecuteHandler : Volo.Abp.DependencyInjection.ISingletonDependency
    {
        private MqttClientService mqttClient = null;
        private readonly ILogger<DekExecuteHandler> _loger = null;
        protected readonly IConfiguration _config;
        private HttpClient httpClient = null;

        public DekExecuteHandler(MqttClientService mqttClient, ILogger<DekExecuteHandler> loger, IConfiguration config)
        {
            httpClient = new HttpClient();
            this.mqttClient = mqttClient;
            this.mqttClient.MessageReceived += MqttClient_MessageReceived;
            _loger = loger;
            _config = config;
          
        }

        public async Task StartAsync()
        {
            await this.mqttClient.StartAsync();
        }

        private async void MqttClient_MessageReceived(object sender, string e)
        {
            _loger.LogDebug($"收到消息：{e}");
            RecieveMsg recieveMsg = null;
            try
            {
                recieveMsg = JsonSerializer.Deserialize<RecieveMsg>(e);
            }
            catch (Exception ex)
            {
                _loger.LogError(ex.Message);
                _loger.LogWarning("收到命令，解析发生出错");
                return;
            }

            if (recieveMsg == null)
            {
                _loger.LogWarning("收到命令，解析发生出错"); return;
            }
            if (recieveMsg.MessageType != "CmdExecute")
            {
                _loger.LogDebug("不是CmdExecute命令，不处理"); return;
            }
            JsonElement root = (JsonElement)recieveMsg.Data;
            var eleRs = GetJsonElement(root, "cmdID");
            if (eleRs.Result == false)
            {
                _loger.LogDebug("查找不到cmdId不处理，不处理"); return;
            }
            var cmdId = GetJsonStringValue(eleRs.Elemnet);
            eleRs = GetJsonElement(root, "cmd");
            if (eleRs.Result == false)
            {
                _loger.LogDebug("查找不到cmd不处理，不处理"); return;
            }
            var cmd = GetJsonStringValue(eleRs.Elemnet);

            eleRs = GetJsonElement(root, "linefullpath");
            if (eleRs.Result == false)
            {
                _loger.LogDebug("查找不到linefullpath不处理，不处理"); return;
            }
            var linefullpath = GetJsonStringValue(eleRs.Elemnet);

            eleRs = GetJsonElement(root, "recipename");
            if (eleRs.Result == false)
            {
                _loger.LogDebug("查找不到recipename不处理，不处理"); return;
            }
            var recipename = GetJsonStringValue(eleRs.Elemnet);

            eleRs = GetJsonElement(root, "productionschedule");
            if (eleRs.Result == false)
            {
                _loger.LogDebug("查找不到productionschedule不处理，不处理"); return;
            }
            var productionschedule = GetJsonStringValue(eleRs.Elemnet);

            //第一个是线体名称，第二个是程序名称，第三个也是线体名称
            var dict = new Dictionary<string, string>();
            dict.Add("linefullpath", linefullpath);
            dict.Add("recipename", recipename);
            dict.Add("productionschedule", productionschedule);
            var lls = mqttClient.MachineList;
            var apiHost = lls.FirstOrDefault(p => p.Id == recieveMsg.Sender);
            //if (apiHost == null || string.IsNullOrEmpty(apiHost.AdpWebApiUrl))
            //{
            //    _loger.LogDebug($"{recieveMsg.HostName}没有配置好设备的api，不处理"); return;
            //}
            //try
            //{
            //    var reponseStr = "";
            //    using (var req = new HttpRequestMessage(HttpMethod.Post, apiHost.AdpWebApiUrl) { Content = new FormUrlEncodedContent(dict) })
            //    {
            //        using (var res = await httpClient.SendAsync(req))
            //        {
            //            if (res.IsSuccessStatusCode)
            //            {
            //                reponseStr = await res.Content.ReadAsStringAsync();
            //            }
            //        }
            //    }
            //    if (string.IsNullOrWhiteSpace(reponseStr) == false)
            //    {
            //        JsonSerializerOptions options= new JsonSerializerOptions();
            //        options.PropertyNameCaseInsensitive = true;
            //        var response = JsonSerializer.Deserialize<WebapiReponse>(reponseStr, options);
            //        await SendReponseAsync(cmd, cmdId, response.ResultCode, response.ResultMsg, recieveMsg.Sender);
            //        _loger.LogDebug("命令处理完成");
            //    }
            //    else
            //    {
            //        await SendReponseAsync(cmd, cmdId, false, "调用webapi未知错误", recieveMsg.Sender);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    await SendReponseAsync(cmd, cmdId, false, "调用webapi接口异常", recieveMsg.Sender);
            //    _loger.LogDebug($"调用webapi接口异常：{ex.Message}");
            //}

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

        private string GetJsonStringValue(JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                return je.GetString();
            }
            return "";
        }

        private (bool Result, JsonElement Elemnet) GetJsonElement(JsonElement root, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName)) return (false, default(JsonElement));
            string[] temp = propertyName.Split(new char[] { '.' });
            JsonElement element;
            JsonElement parent;
            parent = root;
            for (int i = 0; i < temp.Length; i++)
            {
                if (parent.TryGetProperty(temp[i], out element) == false) return (false, default(JsonElement));
                if (i == temp.Length - 1)
                {
                    return (true, element);
                }
                parent = element;
            }
            return (false, default(JsonElement));
        }
  
    }
}
