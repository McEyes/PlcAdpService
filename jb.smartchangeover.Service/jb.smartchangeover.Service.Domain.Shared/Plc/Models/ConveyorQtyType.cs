using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace jb.smartchangeover.Service.Domain.Shared
{
    /// <summary>
    /// PLC 寄存器定义
    /// </summary>
    public enum ConveyorQtyType
    {
        /// <summary>
        /// 普通轨道(单)
        /// </summary>
        NomalConveyor = 1,
        /// <summary>
        /// 双轨道：D650二轨调宽指令，D658二轨是否有板，D660二轨道运动方向，D664设置轨道回到原点，D666读取读取轨道实际宽度指令
        /// </summary>
        DualConveyor = 2,
        /// <summary>
        /// 换边双轨道：
        /// D700板旋转角度，读取和设置轨道旋转角度，
        /// D702板道旋转角度方向指令，读取和设置轨道旋转方向，旋转方向：1顺时针，2逆时针
        /// D704板道旋转方式指令，读取和设置轨道旋转方式，旋转方式：1窄变宽，2宽变窄
        /// </summary>
        CornerConveyor = 3,
        /// <summary>
        /// 筛选机轨道/转角机轨道/翻板机轨道：D500等于1时直通和2时手动模式配置命令,
        /// </summary>
        ScreeningConveyor = 4,
        /// <summary>
        /// 扫描仪轨道：
        ///     带有上下扫描仪设置，分别是：
        ///     D1450读取或写入扫描轨道程序
        ///     D1552读取或写入扫描枪1(上扫描抢)的y轴宽度
        ///     D1554读取扫描枪1(上扫描抢)的y轴实际宽度
        ///     D1556读取或写入扫描枪2(上扫描抢)的y轴宽度
        ///     D1558读取扫描枪2(上扫描抢)的y轴实际宽度
        /// </summary>
        ScannerConveyor = 5,
        /// <summary>
        /// 翻板机轨道：D500等于1时直通和2时手动模式配置命令
        /// </summary>
        InvertConveyor = 6,
        /// <summary>
        /// 自动扫描仪轨道：传automode调整轨道
        /// </summary>
        ScannerAutoChangeConveyor = 7,
    }
}
