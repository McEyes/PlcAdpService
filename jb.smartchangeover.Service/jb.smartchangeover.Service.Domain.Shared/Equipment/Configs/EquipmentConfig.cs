using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace jb.smartchangeover.Service.Domain.Shared.Equipment.Configs
{
    public class EquipmentConfig : IEquipmentConfig
    {
        /// <summary>
        /// mqtt的id
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        ///设备编号
        /// </summary>
        public string No { get; set; }
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 设备类型
        /// </summary>
        private string _kind;
        public string Kind { get { return _kind; } set { if (value != null) _kind = value.ToLower(); else _kind = value; } }
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enable { get; set; } = true;

        #region stock 配置
        /// <summary>
        /// 心跳频率，单位秒
        /// </summary>
        public int? HeartBeat { get; set; } = 60;

        /// <summary>
        /// 默认类型PLC数据
        /// </summary>
        public string Type { get; set; } = "PLC";
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Host { get; set; }
        /// <summary>
        /// 设备的IP
        /// </summary>
        public string Ip { get; set; }
        /// <summary>
        /// 默认端口10000
        /// </summary>
        public int Port { get; set; } = 10000;
        /// <summary>
        /// PLC协议：FX3，FX5
        /// </summary>
        public string Protocol { get; set; }
        //public string Workcell { get; set; }
        //public string Bay { get; set; }
        /// <summary>
        /// 协议类型：binary,sacii
        /// </summary>
        public string ProtocolType { get; set; } = "binary";
        /// <summary>
        /// tcp超时配置
        /// </summary>
        public int ReceiveTimeout { get; set; } = 900;
        /// <summary>
        /// tcp超时配置
        /// </summary>
        public int SendTimeout { get; set; } = 900;
        /// <summary>
        /// tcp超时配置
        /// </summary>
        public int ConnectTimeout { get; set; } = 3000;
        /// <summary>
        /// 是否链接状态，用于Mqttclient 发送消息判断,
        /// netclient 根据设备是否链接成功修改
        /// </summary>
        [JsonIgnore]
        public bool IsConnected { get; set; } = false;
        #endregion stock 配置

        /// <summary>
        /// NXT配置
        /// </summary>
        public string Events { get; set; }

        #region MQtt config
        /// <summary>
        /// 是否订阅下发事件
        /// </summary>
        public bool WithSubscribe { get; set; } = true;
        /// <summary>
        /// 是否需要心跳上报
        /// </summary>
        public bool WithHeartBeat { get; set; } = true;
        public string Topic => $"in/{Kind}/{Id}";
        ///// <summary>
        ///// mqtt 上报消息时间
        ///// </summary>
        //public DateTime LastReceiveTime { get; set; } = DateTime.Now;
        #endregion MQtt config
        /// <summary>
        /// 重试次数,默认3次
        /// </summary>
        public int Retries { get; set; } = 3;

        /// <summary>
        /// 轨道数量类型
        /// </summary>
        public ConveyorQtyType QtyType { get; set; } = ConveyorQtyType.NomalConveyor;

        public bool? IsDebug { get; set; }

        /// <summary>
        /// 数据采集频率，默认一秒钟更新一次
        /// </summary>
        public int DataReadFreq { get; set; } = 1;

        public override string ToString()
        {
            return Name;
        }
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            if (obj is IEquipmentConfig)
            {
                return this.Id == (obj as IEquipmentConfig).Id;
            }
            return false;
        }
        public int GetRetriesTime(int retries)
        {
            if (retries < 7) return retries * (retries + 2);
            else return 60;
        }

        public override int GetHashCode()
        {
          return base.GetHashCode();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class TextEquipmentConfig : IEquipmentConfig
    {
        /// <summary>
        /// mqtt的id
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        ///设备编号
        /// </summary>
        public string No { get; set; }
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 设备类型
        /// </summary>
        private string _kind;
        public string Kind { get { return _kind; } set { if (value != null) _kind = value.ToLower(); else _kind = value; } }
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enable { get; set; } = true;

        #region stock 配置
        /// <summary>
        /// 心跳频率，单位秒
        /// </summary>
        public int? HeartBeat { get; set; } = 60;

        /// <summary>
        /// 默认类型PLC数据
        /// </summary>
        public string Type { get; set; } = "PLC";
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Host { get; set; }
        /// <summary>
        /// 设备的IP
        /// </summary>
        public string Ip { get; set; }
        /// <summary>
        /// 默认端口10000
        /// </summary>
        public int Port { get; set; } = 10000;
        /// <summary>
        /// PLC协议：FX3，FX5
        /// </summary>
        public string Protocol { get; set; }
        //public string Workcell { get; set; }
        //public string Bay { get; set; }
        /// <summary>
        /// 协议类型：binary,sacii
        /// </summary>
        public string ProtocolType { get; set; } = "binary";
        /// <summary>
        /// tcp超时配置
        /// </summary>
        public int ReceiveTimeout { get; set; } = 900;
        /// <summary>
        /// tcp超时配置
        /// </summary>
        public int SendTimeout { get; set; } = 900;
        /// <summary>
        /// tcp超时配置
        /// </summary>
        public int ConnectTimeout { get; set; } = 3000;
        /// <summary>
        /// 是否链接状态，用于Mqttclient 发送消息判断,
        /// netclient 根据设备是否链接成功修改
        /// </summary>
        [JsonIgnore]
        public bool IsConnected { get; set; } = false;
        #endregion stock 配置

        /// <summary>
        /// NXT配置
        /// </summary>
        public string Events { get; set; }

        #region MQtt config
        /// <summary>
        /// 是否订阅下发事件
        /// </summary>
        public bool WithSubscribe { get; set; } = true;
        /// <summary>
        /// 是否需要心跳上报
        /// </summary>
        public bool WithHeartBeat { get; set; } = true;
        public string Topic { get; set; }
        ///// <summary>
        ///// mqtt 上报消息时间
        ///// </summary>
        //public DateTime LastReceiveTime { get; set; } = DateTime.Now;
        #endregion MQtt config
        /// <summary>
        /// 重试次数,默认3次
        /// </summary>
        public int Retries { get; set; } = 3;

        /// <summary>
        /// 轨道数量类型
        /// </summary>
        public ConveyorQtyType QtyType { get; set; } = ConveyorQtyType.NomalConveyor;

        public bool? IsDebug { get; set; }
        /// <summary>
        /// 数据采集频率，默认一秒钟更新一次
        /// </summary>
        public int DataReadFreq { get; set; } = 1;

        public override string ToString()
        {
            return Name;
        }
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            if (obj is IEquipmentConfig)
            {
                return this.Id == (obj as IEquipmentConfig).Id;
            }
            return false;
        }
        public int GetRetriesTime(int retries)
        {
            if (retries < 7) return retries * (retries + 2);
            else return 60;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
