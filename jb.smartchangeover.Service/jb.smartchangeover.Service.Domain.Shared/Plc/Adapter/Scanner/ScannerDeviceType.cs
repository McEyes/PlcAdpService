using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared
{

    public class ScannerDeviceType
    {
        /// <summary>
        /// 发送命令
        /// </summary>
        public const string TcpCommand = "TcpCommand";
        /// <summary>
        /// 返回结果
        /// </summary>
        public const string TcpReturned = "TcpReturned";
        /// <summary>
        /// 获取当前正在使用的产品型号:TcpCommand&GetCurrentProducts 
        /// 返回值：TcpReturned&Success&model 成功，TcpReturned&Fail&ExceptionDescription 失败
        /// </summary>
        [Description("获取当前正在使用的产品型号")]
        public const string GetCurrentProducts = "GetCurrentProducts";
        /// <summary>
        /// 自动切换产品:TcpCommand&AutomaticSwitchingOfProducts&Model  
        /// 返回值：TcpReturned&Success 成功，TcpReturned&Fail&ExceptionDescription 失败
        /// </summary>
        [Description("自动切换产品")]
        public const string AutomaticSwitchingOfProducts = "AutomaticSwitchingOfProducts";
        /// <summary>
        ///查询本地数据库是否有产品:TcpCommand&CheckProductExistence&Modle  
        ///返回值：TcpReturned&Success 有，TcpReturned&Fail&ExceptionDescription 没有/失败
        /// </summary>
        [Description("查询本地数据库是否有产品")]
        public const string CheckProductExistence = "CheckProductExistence";
        ///// <summary>
        /////获取产品参数包含轨道等参数 TPCB022-1:TcpCommand&GetProductParameters&Modle  
        /////返回值：TcpReturned&Success&{JSON}，TcpReturned&Fail&ExceptionDescription 失败加异常信息
        ///// </summary>
        //[Description("获取产品参数包含轨道等参数")]
        //public const string GetProductParameters = "GetProductParameters";
        /// <summary>
        /// 获取机器状态，1运行，2停止，3报警:TcpCommand&GetMachineState
        /// 返回值：TcpReturned&Success&1，成功，TcpReturned&Fail&ExceptionDescription 失败
        /// </summary>
        [Description("获取机器状态，1运行，2停止，3报警")]
        public const string GetMachineState = "GetMachineState";
        /// <summary>
        ///操作机器，1启动，2复位，3停止:TcpCommand&ControlMachine&1
        ///返回值：TcpReturned&Success，成功，TcpReturned&Fail&ExceptionDescription 失败
        /// </summary>
        [Description("操作机器，1启动，2复位，3停止")]
        public const string ControlMachine = "ControlMachine";
        ///// <summary>
        /////获取要板状态，1，要板中，0没有要板 ：TcpCommand&GetMachineFeedState
        /////返回值：TcpReturned&Success&1，成功，TcpReturned&Fail&ExceptionDescription 失败
        ///// </summary>
        //[Description("获取要板状态")]
        //public const string GetMachineFeedState = "GetMachineFeedState";
        ///// <summary>
        /////获取轨道是否有板状态，1，有板，0没有：TcpCommand&GetMachineIsBoard
        /////返回值：TcpReturned&Success&1，成功，TcpReturned&Fail&ExceptionDescription 失败
        ///// </summary>
        //[Description("是否有板")]
        //public const string GetMachineIsBoard = "GetMachineIsBoard";
        ///// <summary>
        /////获取出板数量，：TcpCommand&GetQuantity
        /////返回值：TcpReturned&Success&1000，成功，TcpReturned&Fail&ExceptionDescription 失败
        ///// </summary>
        //[Description("出板数量")]
        //public const string GetQuantity = "GetQuantity";
        /// <summary>
        ///出板数量清零 ：TcpCommand&CleanQuantity
        ///返回值：TcpReturned&Success，成功，TcpReturned&Fail&ExceptionDescription 失败
        /// </summary>
        [Description("出板数量清零")]
        public const string CleanQuantity = "CleanQuantity";
    }

}
