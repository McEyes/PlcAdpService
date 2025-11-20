using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace jb.smartchangeover.Service.Domain.Shared.Plc.Enums
{
    /// <summary>
    /// 设备状态代码,1000以内为预留成功代码，
    /// 1000以后为异常代码
    /// 9999为未知代码
    /// </summary>
    public enum EquipmentStatus
    {
        /// <summary>
        /// 未知状态,设备初始状态
        /// </summary>
		[Description("未知状态")]
        Unknown = -1,
        /// <summary>
        /// 运行中
        /// </summary>
        [Description("运行中")]
        Runnling2 = 0, 
        /// <summary>
        /// 运行中
        /// </summary>
        [Description("运行中")]
        Runnling = 1,
        /// <summary>
        /// 停机，待机中
        /// </summary>
        [Description("停机，待机中")]
        Shutdown = 2,
        /// <summary>
        /// 报警，通用报警
        /// </summary>
        [Description("报警")]
        Alarm = 3,
        /// <summary>
        /// 打开门报警
        /// </summary>
        [Description("打开门报警")]
        OpenDoorAlarm = 21,

        /// <summary>
        /// 设备离线,ip不通
        /// </summary>
        [Description("设备离线,无法访问")]
        OffLine = 1001,
        /// <summary>
        /// 设备掉线,ip通，端口不通
        /// </summary>
        [Description("设备端口异常，无法正常通信")]
        PortOffLine = 1002,
        /// <summary>
        /// 设备链接断开,tcp链接断开
        /// </summary>
        [Description("设备链接断开")]
        Disconnect = 1003,
        /// <summary>
        /// 设备停止监控,设备一键转啦功能被禁用
        /// </summary>
        [Description("设备停止监控")]
        Disabled = 1004,




        //#region 设备异常信息代码，从1000开始

        ///// <summary>
        ///// 设备离线,ip不通
        ///// </summary>
        //[Description("设备离线,无法访问")]
        //OffLine = 1001,
        ///// <summary>
        ///// 设备掉线,ip通，端口不通
        ///// </summary>
        //[Description("设备端口异常，无法正常通信")]
        //PortOffLine = 1002,
        ///// <summary>
        ///// 设备链接断开,tcp链接断开
        ///// </summary>
        //[Description("设备链接断开")]
        //Disconnect = 1003,

        ///// <summary>
        ///// 设备忙碌，当前正在执行程序中
        ///// </summary>
        //[Description("设备忙碌中，正在执行操作")]
        //DeviceBusy = 1004,

        ///// <summary>
        ///// 指令错误，不支持该指令操作
        ///// </summary>
        //[Description(" 指令错误，不支持该指令操作")]
        //CommandError =1005,
        ///// <summary>
        ///// 指令过期，超过时效
        ///// </summary>
        //[Description("指令过期，超过时效")]
        //CommandExpired = 1006,




        //#endregion 设备异常信息代码，从1000开始


        //#region 设备报警代码，从2000开始

        ///// <summary>
        ///// 限宽报警,收到调宽指令，宽度超过容许调宽最大最小范围报警(50-450)
        ///// </summary>
        //[Description("限宽报警")]
        //WidthAlarm = 2001,



        ///// <summary>
        ///// 打开门报警
        ///// </summary>
        //[Description("打开门报警")]
        //HasBoardAlarm = 2002,

        ///// <summary>
        ///// 轨道安全门被打开报警
        ///// </summary>
        //[Description("安全门开门报警")]
        //ConveyorDoorOpenAlarm = 2003,


        ////Conveyor belt safety door
        ///// <summary>
        ///// 主机防护门开门报警，轨道的主机维修的门被打开时报警
        ///// </summary>
        //[Description("主机防护门开门报警")]
        //EquipmentDoorAlarm = 2004,


        ///// <summary>
        ///// 主动上报数据异常报警
        ///// </summary>
        //[Description("主动上报数据异常报警")]
        //ReportFailureAlarm = 2006,

        //#endregion 设备报警代码，从2000开始



        //#region 程序异常，重5000开始

        ///// <summary>
        ///// PLC设备适配器初始化失败
        ///// </summary>
        //[Description("PLC设备适配器初始化失败")]
        //PlcInitError = 5001,


        ///// <summary>
        ///// PLC设备适配器不存在异常
        ///// </summary>
        //[Description("PLC设备适配器不存在异常")]
        //PlcClientNullError = 5002,


        ///// <summary>
        ///// PLC设备适配器不存在异常
        ///// </summary>
        //[Description("PLC设备适配器不存在异常")]
        //Cmd = 5002,


        //#endregion 程序异常，重5000开始



        /// <summary>
        /// 未知异常
        /// </summary>
        [Description("未知异常")]
        UnknownError = 9999
    }
}
