using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Pipelines.Sockets.Unofficial.Arenas;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos
{
    public class BaseMsg
    {
        public BaseMsg()
        {

        }
        public string Id { get; set; }

        public string Kind { get; set; }

        public string HostName { get; set; }

        public string MessageType { get; set; }

        /// <summary>
        /// 机器的唯一id，注册的时候分配
        /// </summary>
        /// <value></value>
        public string Sender { get; set; }

        public string TimeStamp { get; set; }

        public string PanelId { get; set; }

        /// <summary>
        /// 来源系统，可自定义
        /// </summary>
        /// <value></value>
        public string Source { get; set; } = "PlcAdapterServer";
        public override string ToString()
        {
            return $"{Kind}@{HostName}@{MessageType}";
        }
    }

    public class BaseMsg<T> : BaseMsg
    {
        public T? Data { get; set; }
        public override string ToString()
        {
            return $"{Kind}@{HostName}@{MessageType}{Data.ToJSONIgnoreNullValue()}";
        }
    }
    //public class EquipmentHeartbeat : BaseMsg
    //{

    //}
}
