using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace jb.smartchangeover.Service.Domain.Shared.Equipment.Configs
{
    /// <summary>
    /// 
    /// </summary>
    public interface IEquipmentConfig
    {
        /// <summary>
        /// 地址
        /// </summary>
        string Id { get; set; }
        string No { get; set; }
        /// <summary>
        /// 服务名称
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// 设备类型
        /// </summary>
        string Kind { get; set; }
        string Type { get; set; }
        string Host { get; set; }
        string Ip { get; set; }
        int Port { get; set; }
        string Protocol { get; set; }
        /// <summary>
        /// 通信类型：ascii，binary，PLC
        /// </summary>
        string ProtocolType { get; set; }
        //string Workcell { get; set; }
        //string Bay { get; set; }
        /// <summary>
        /// 心率
        /// </summary>
        int? HeartBeat { get; set; }
        /// <summary>
        /// 心率
        /// </summary>
        int ReceiveTimeout { get; set; }
        /// <summary>
        /// 心率
        /// </summary>
        int SendTimeout { get; set; }
        /// <summary>
        /// 心率
        /// </summary>
        int ConnectTimeout { get; set; }

        /// <summary>
        /// 是否订阅消息
        /// </summary>
        bool WithSubscribe { get; set; }
        /// <summary>
        /// 重试次数,默认3次
        /// </summary>
        int Retries { get; set; }
        /// <summary>
        /// 输送带数量类型
        /// </summary>
        ConveyorQtyType QtyType { get; set; }
        /// <summary>
        /// 扩展字段
        /// </summary>
        string Events { get; set; }
        /// <summary>
        /// 是否启用
        /// </summary>
        bool Enable { get; set; }
        //DateTime LastReceiveTime { get; set; }
        string Topic { get; }

        /// <summary>
        /// 
        /// </summary>
        bool? IsDebug { get; }
        /// <summary>
        /// 数据采集频率，默认一秒钟更新一次
        /// </summary>
        int DataReadFreq { get; set; }
        int GetRetriesTime(int retries);

        /// <summary>
        /// 是否链接状态，用于Mqttclient 发送消息判断,
        /// netclient 根据设备是否链接成功修改
        /// </summary>
        [JsonIgnore]
        bool IsConnected { get; set; }
    }
}
