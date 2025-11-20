using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace jb.smartchangeover.Service.Domain.Shared.Plc.Enums
{
    /// <summary>
    /// 设备命令执行状态
    /// </summary>
    public enum DeviceCommandStatus
    {
        ///// <summary>
        ///// 未知
        ///// </summary>
        //[Description("未知")]
        //Unknown = -1,
        /// <summary>
        /// 准备就绪，待机，设备收到命令后准备执行前的状态，收到命令状态
        /// </summary>
        [Description("待命中")]
        Ready = 0,
        /// <summary>
        /// 执行中
        /// </summary>
        [Description("执行中")]
        Executing = 1,
        /// <summary>
        /// 执行完成
        /// </summary>
        [Description("执行完成")]
        Success = 2,
        /// <summary>
        /// 执行失败
        /// </summary>
        [Description("执行失败")]
        Failed = 3,
        /// <summary>
        /// 执行超时
        /// </summary>
        [Description("执行超时")]
        Timeout = 4,

        //4,5,6,7等后面添加具体执行失败原因,不在定义内的，用失败返回

    }
}
