using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using jb.smartchangeover.Service.Application.Mqtts;
using jb.smartchangeover.Service.Domain.Shared;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos;
using Microsoft.AspNetCore.Mvc;

using Volo.Abp.Application.Services;

namespace jb.smartchangeover.Service.HttpApi
{
    public class PlcAppService : ApplicationService
    {

        IMqttClientService _mqttClient;

        public PlcAppService(IMqttClientService mqttClient)
        {
            _mqttClient = mqttClient;
        }

        /// <summary>
        /// 轨道参数设置指令
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <param name="data">key:指令名称
        /// <br />  1，通用轨道指令(除GE扫描轨道不支持外其他轨道都支持)：widthlane1(调宽D450)、pcbnum(过板数量D454)、
        /// <br />  2，筛选机、翻板机，转角机特殊指令： TransferMode（传输模式D500:1直通模式，2非直通模式(手动模式/翻版模式/转角模式）、
        /// <br />  3，Valoe扫描枪轨道特殊指令：ScannerDownSetY(下扫描抢D1552)、ScannerUpSetY(上扫描抢D1556)
        /// <br />  4，GE扫描轨道指令：autochange/model(扫描轨道程序名)、
        /// <br />  5，换边机特殊指令：widthlane2(调宽D650)、pcbnum2(过板数量D654)、turndirection(板道旋转方向D702：1顺时针，2逆时针)、turnmethod(板道旋转方式D704：1窄变宽，2宽变窄)、
        /// <br />  6，ResetWidthType：调宽方式：2归零调宽，3直接调宽，不传当前默认3直接调宽
        /// </param>
        /// <returns></returns>
        [HttpPost("Conveyor/AdjustParameter/{equipId}")]
        //[SwaggerOperation("Conveyor AdjustParameter", Tags = new string[] { "Conveyor" })]
        public async Task<IResult> ConveyorAdjustParameter(string equipId,[FromBody] AdjustParameter data)
        {
            var config = new TextEquipmentConfig();
            config.Kind = "conveyor";
            config.Id = equipId;
            config.Topic = $"out/conveyor/{equipId}";
            var machine = _mqttClient.MachineList.FirstOrDefault(f => f.Id == equipId);
            if (machine != null)
            {
                config.Host = machine.Host;
                config.Ip = machine.Ip;
                config.Retries = machine.Retries;
            }
            var msg = new BaseMsg<AdjustParameter>();
            msg.MessageType = "AdjustParameter";
            msg.Data = data;
            await _mqttClient.StartAsync();
            return await _mqttClient.SendAsync(msg, config, config.Retries);
        }
        /// <summary>
        /// 轨道启动指令
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <returns></returns>
        [HttpPost("Conveyor/Start/{equipId}")]
        public async Task<IResult> ConveyorStart(string equipId)
        {
            return await SendMqttMsg("conveyor", equipId, "start");
        }

        /// <summary>
        /// 轨道调宽，调到设置值
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <returns></returns>
        [HttpPost("Conveyor/ResetWidth/{equipId}")]
        public async Task<IResult> ConveyorResetWidth(string equipId)
        {
            return await SendMqttMsg("conveyor", equipId, "ResetWidth");
        }

        /// <summary>
        /// 轨道主动上报信息
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <returns></returns>
        [HttpPost("Conveyor/Report/{equipId}")]
        public async Task<IResult> ConveyorReport(string equipId)
        {
            return await SendMqttMsg("conveyor", equipId, "Report");
        }

        /// <summary>
        /// 轨道复位，清除报警
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <returns></returns>
        [HttpPost("Conveyor/Reset/{equipId}")]
        public async Task<IResult> ConveyorReset(string equipId)
        {
            return await SendMqttMsg("conveyor", equipId, "reset");
        }

        /// <summary>
        /// 轨道下发报警代码
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <param name="alermCode">报警代码</param>
        /// <returns></returns>
        [HttpPost("Conveyor/Alarm/{equipId}/{alermCode}")]
        public async Task<IResult> ConveyorAlarm(string equipId, ushort alermCode)
        {
            return await SendMqttMsg("conveyor", equipId, "Alarm", alermCode);
        }


        /// <summary>
        /// 轨道停止
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <returns></returns>
        [HttpPost("Conveyor/Shutdown")]
        public async Task<IResult> ConveyorShutdown(string equipId)
        {
            return await SendMqttMsg("conveyor", equipId, "Shutdown");
        }

        /// <summary>
        /// GetCurrentProducts
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <param name="model">model</param>
        /// <returns></returns>
        [HttpPost("Conveyor/GetCurrentProducts")]
        public async Task<IResult> ConveyorGetCurrentProducts(string equipId,string model)
        {
            return await SendMqttMsg("conveyor", equipId, "GetCurrentProducts", model);
        }


        /// <summary>
        /// AutomaticSwitchingOfProducts
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <param name="model">model</param>
        /// <returns></returns>
        [HttpPost("Conveyor/AutomaticSwitchingOfProducts")]
        public async Task<IResult> ConveyorAutomaticSwitchingOfProducts(string equipId, string model)
        {
            return await SendMqttMsg("conveyor", equipId, "AutomaticSwitchingOfProducts", model);
        }

        /// <summary>
        /// AutomaticSwitchingOfProducts
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <param name="model">model</param>
        /// <returns></returns>
        [HttpPost("Conveyor/CheckProductExistence")]
        public async Task<IResult> ConveyorCheckProductExistence(string equipId, string model)
        {
            return await SendMqttMsg("conveyor", equipId, "CheckProductExistence", model);
        }

        /// <summary>
        /// AutomaticSwitchingOfProducts
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <param name="model">model</param>
        /// <returns></returns>
        [HttpPost("Conveyor/GetProductParameters")]
        public async Task<IResult> ConveyorGetProductParameters(string equipId, string model)
        {
            return await SendMqttMsg("conveyor", equipId, "GetProductParameters", model);
        }

        /// <summary>
        /// AutomaticSwitchingOfProducts
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <param name="model">model</param>
        /// <returns></returns>
        [HttpPost("Conveyor/GetMachineState")]
        public async Task<IResult> ConveyorGetMachineStates(string equipId, string model)
        {
            return await SendMqttMsg("conveyor", equipId, "GetMachineState", model);
        }

        /// <summary>
        /// AutomaticSwitchingOfProducts
        /// </summary>
        /// <param name="equipId">设备id</param>
        /// <param name="model">model</param>
        /// <returns></returns>
        [HttpPost("Conveyor/ControlMachine")]
        public async Task<IResult> ConveyorControlMachine(string equipId, string model)
        {
            return await SendMqttMsg("conveyor", equipId, "ControlMachine", model);
        }

        /// <summary>
        /// mqtt命令发送接口
        /// </summary>
        /// <param name="kind">设备类型：conveyor/nxt/reflow/spi/aoi</param>
        /// <param name="equipId">设备id</param>
        /// <param name="cmd">设备支持的命令</param>
        /// <param name="data">参数</param>
        /// <returns></returns>
        [HttpGet("{kind}/{equipId}/{cmd}")]
        public async Task<IResult> SendMqttMsg(string kind, string equipId, string cmd, string data)
        {
            var config = new TextEquipmentConfig();
            config.Kind = kind ?? "conveyor";
            config.Id = equipId;
            config.Topic = $"out/{config.Kind}/{equipId}";
            var machine = _mqttClient.MachineList.FirstOrDefault(f => f.Id == equipId);
            if (machine != null)
            {
                config.Host = machine.Host;
                config.Ip = machine.Ip;
                config.Retries = machine.Retries;
            }
            var msg = new BaseMsg<string>();
            msg.MessageType = cmd;
            if (data != null)
                msg.Data = data;
            await _mqttClient.StartAsync();
            return await _mqttClient.SendAsync(msg, config, config.Retries);
        }

        /// <summary>
        /// mqtt命令发送接口
        /// </summary>
        /// <param name="kind">设备类型：conveyor/nxt/reflow/spi/aoi</param>
        /// <param name="equipId">设备id</param>
        /// <param name="cmd">设备支持的命令</param>
        /// <returns></returns>
        [HttpGet("{kind}/{equipId}/{cmd}/{data}")]
        public async Task<IResult> SendMqttMsg(string kind, string equipId, string cmd, ushort? data = null)
        {
            var config = new TextEquipmentConfig();
            config.Kind = kind ?? "conveyor";
            config.Id = equipId;
            config.Topic = $"out/{config.Kind}/{equipId}";
            var machine = _mqttClient.MachineList.FirstOrDefault(f => f.Id == equipId);
            if (machine != null)
            {
                config.Host = machine.Host;
                config.Ip = machine.Ip;
                config.Retries = machine.Retries;
            }
            var msg = new BaseMsg<ushort>();
            msg.MessageType = cmd;
            if (data != null)
                msg.Data = data.Value;
            await _mqttClient.StartAsync();
            return await _mqttClient.SendAsync(msg, config, config.Retries);
        }



        /// <summary>
        /// mqtt命令发送接口
        /// </summary>
        /// <param name="kind">设备类型：conveyor/nxt/reflow/spi/aoi</param>
        /// <param name="equipId">设备id</param>
        /// <param name="cmd">设备支持的命令</param>
        /// <returns></returns>
        [HttpPost("{kind}/{equipId}/{cmd}")]
        public async Task<IResult> AdjustParameter(string kind, string equipId, string cmd, [FromBody] AdjustParameter data)
        {
            var config = new TextEquipmentConfig();
            config.Kind = kind ?? "conveyor";
            config.Id = equipId;
            config.Topic = $"out/{config.Kind}/{equipId}";
            var machine = _mqttClient.MachineList.FirstOrDefault(f => f.Id == equipId);
            if (machine != null)
            {
                config.Host = machine.Host;
                config.Ip = machine.Ip;
                config.Retries = machine.Retries;
            }
            var msg = new BaseMsg<AdjustParameter>();
            msg.MessageType = cmd;
            msg.Data = data;
            await _mqttClient.StartAsync();
            return await _mqttClient.SendAsync(msg, config, config.Retries);
        }
    }
}

