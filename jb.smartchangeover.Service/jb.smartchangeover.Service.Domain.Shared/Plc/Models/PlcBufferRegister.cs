using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace jb.smartchangeover.Service.Domain.Shared
{
    /// <summary>
    /// PLC 寄存器定义
    /// </summary>
    public class PlcBufferRegister
    {
        #region 单轨道(一轨)相关指令
        /// <summary>
        /// 变轨寄存器D450：轨道设备需要自己检查是否可以调宽，直接发送轨道信息，返回调宽成功与否：ng表示调宽失败，可能有板，没法调整，
        /// 需要一秒后重新执行
        /// </summary>
        [Description("变轨寄存器")]
        public const string Change = "D450";

        /// <summary>
        /// 二轨设置宽度D650：
        /// </summary>
        [Description("实际宽度")]
        public const string SetWidth2 = "D650";
        /// <summary>
        /// 控制（启停）寄存器D452：1启动，2复位，3停止
        /// </summary>
        [Description("控制（启停）寄存器")]
        public const string Control = "D452";
        /// <summary>
        /// 过板数量寄存器D454：过板数量当前设备总共过了多少块，后续需要提供换model时能清空过板数量，重置为0
        /// </summary>
        [Description("过板数量寄存器")]
        public const string Counter = "D454";
        /// <summary>
        /// 运行状态寄存器D456：1运行正常，3报警
        /// </summary>
        [Description("运行状态寄存器")]
        public const string RunStatus = "D456";

        /// <summary>
        /// 读取本机有板状态D458,判断当前轨道是否存在板(板停在中间状态是无法判断是否有板，所以轨道需要自检一遍)
        /// 1有，0没有
        /// </summary>
        [Description("是否有板")]
        public const string HasPanel = "D458";


        /// <summary>
        /// 当前轨道传输方向D460：
        /// 1左至右，2右至左，3上至下，4下至上, 5左至下，6左至上，7右至下，8右至上，9上至左，10上至右，11下至左，12下至右
        /// </summary>
        [Description("传输方向")]
        public const string RunPath = "D460";

        /// <summary>
        /// 轨道传输速度，读取和设置调整轨道速度D462
        /// </summary>
        [Description("传输速度")]
        public const string Speed = "D462";
        /// <summary>
        /// 设置轨道回到原点D464(确保不弄坏板，调规之前一定要自检一遍)	1，归位(回到原点)，2，先归位在回到设定位置（并回到设定位置）,3直接调轨
        /// </summary>
        [Description("归位")]
        public const string WidthReset = "D464";


        /// <summary>
        /// 读取实际宽度D466：
        /// </summary>
        [Description("实际宽度")]
        public const string RealWidth= "D466";

        /// <summary>
        /// 读取实际宽度D666：
        /// </summary>
        [Description("实际宽度")]
        public const string RealWidth2 = "D666";
        /// <summary>
        /// 开盖D468：读取是否有人打开盖子,只读,
        /// 1，打开，0没打开
        /// </summary>
        [Description("是否开盖")]
        public const string IsDoorOpen = "D468";

        /// <summary>
        /// 本机要板D470：
        /// </summary>
        [Description("本机要板")]
        public const string NeedPanel = "D470";


        #endregion 单轨道(一轨)相关指令

        #region  筛选机
        /// <summary>
        /// 筛选机需要设置模式D500：1直通模式和2手动模式：
        /// </summary>
        [Description("传输模式")]
        public const string TransferMode = "D500";
        #endregion  筛选机


        #region 多轨道(二轨)相关指令

        /// <summary>
        /// 变轨寄存器D650：轨道设备需要自己检查是否可以调宽，直接发送轨道信息，返回调宽成功与否：ng表示调宽失败，可能有板，没法调整，
        /// 需要一秒后重新执行
        /// </summary>
        [Description("变轨寄存器")]
        public const string Change2 = "D650";
        /// <summary>
        /// 控制（启停）寄存器D652：1启动，0停止
        /// </summary>
        [Description("控制（启停）寄存器")]
        public const string Control2 = "D652";
        /// <summary>
        /// 过板数量寄存器D654：过板数量当前设备总共过了多少块，后续需要提供换model时能清空过板数量，重置为0
        /// </summary>
        [Description("过板数量寄存器")]
        public const string Counter2 = "D654";
        /// <summary>
        /// 运行状态寄存器D656：0运行正常，3报警
        /// </summary>
        [Description("运行状态寄存器")]
        public const string RunStatus2 = "D656";

        /// <summary>
        /// 读取本机有板状态,判断当前轨道是否存在板(板停在中间状态是无法判断是否有板，所以轨道需要自检一遍)
        /// 1有，0没有
        /// </summary>
        [Description("是否有板")]
        public const string HasPanel2 = "D658";


        /// <summary>
        /// 当前轨道传输方向：D660，
        /// 1左至右，2右至左，3上至下，4下至上, 5左至下，6左至上，7右至下，8右至上，9上至左，10上至右，11下至左，12下至右
        /// </summary>
        [Description("传输方向")]
        public const string RunPath2 = "D660";

        /// <summary>
        /// 轨道传输速度，读取和设置调整轨道速度D662
        /// </summary>
        [Description("传输速度")]
        public const string Speed2 = "D662";
        /// <summary>
        /// 设置轨道回到原点：D664
        /// </summary>
        [Description("归位")]
        public const string WidthReset2 = "D664";


        /// <summary>
        /// 本机要板：D666
        /// </summary>
        [Description("本机要板")]
        public const string NeedPanel2 = "D670";



        #endregion 多轨道(二轨)相关指令


        #region 转角双轨道


        /// <summary>
        /// 板旋转角度：D700，读取和设置轨道旋转角度，
        /// </summary>
        [Description("板旋转角度")]
        public const string TurnAngle = "D700";


        /// <summary>
        /// 板道旋转方向：D702,
        /// 读取和设置轨道旋转方向，旋转方向：1顺时针，2逆时针
        /// </summary>
        [Description("板道旋转方向")]
        public const string TurnDirection = "D702";

        /// <summary>
        /// 板道旋转方向：D704,
        /// 读取和设置轨道旋转方式，旋转方式：1窄变宽，2宽变窄
        /// </summary>
        [Description("板道旋转方式")]
        public const string TurnMethod = "D704";


        #endregion 转角双轨道


        /// <summary>
        /// 执行状态：D900
        /// </summary>
        [Description("执行状态")]
        public const string CmdExecStatus = "D900";




        #region 三菱PLC FX3系列通用指令

        /// <summary>
        /// 版本的确认方法,获取PLC型号及版本
        /// </summary>
        [Description("获取PLC型号及版本")]
        public const string Ver = "D8001";//D8101
        /// <summary>
        /// 版本的确认方法,获取PLC型号及版本
        /// </summary>
        [Description("获取PLC型号及版本")]
        public const string Ver2 = "D8101";//D8101


        #endregion  三菱PLC FX3系列通用指令


        #region 扫描器指令

        /// <summary>
        /// 切换程序：传送切换需要切换的程序文件，字符串，长度64以内
        /// </summary>
        [Description("切换程序")]
        public const string ScannerSwitchingOfProducts = "D1450";


        /// <summary>
        /// 下扫描仪Y轴设定位置：
        /// </summary>
        [Description("下扫描仪设定位置")]
        public const string ScannerDownSetY = "D1552";



        /// <summary>
        /// 下扫描仪Y轴实际位置：
        /// </summary>
        [Description("下扫描仪实际位置")]
        public const string ScannerDownRealY = "D1554";



        /// <summary>
        /// 上扫描仪2Y轴设定位置：
        /// </summary>
        [Description("上扫描仪设定位置")]
        public const string ScannerUpSetY = "D1556";
        /// <summary>
        /// 上扫描仪2Y轴实际位置：
        /// </summary>
        [Description("上扫描仪实际位置")]
        public const string ScannerUpRealY = "D1558";


        /// <summary>
        /// 切换程序：传送切换需要切换的程序文件，字符串，长度64以内
        /// 预留长度100
        /// </summary>
        [Description("切换程序")]
        public const string ScannerReadOfProducts = "D1560";



        #endregion 扫描器指令
    }
}
