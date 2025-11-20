using System;
using System.Net.NetworkInformation;

namespace jb.smartchangeover.Service.Domain.Shared.Commons
{
    /// <summary>
    /// PLC 服务统一配置
    /// </summary>
    public class PlcServiceConfig
    {
        public int RetryTime { get; set; } = 3;
        public int HeartBeat { get; set; } = 50;
        public bool IsDebug { get; set; } = false;
        public int ResetWidthType { get; set; } = 3;

        public bool CheckOtherSysConnect { get; set; } = false;
    }
}
