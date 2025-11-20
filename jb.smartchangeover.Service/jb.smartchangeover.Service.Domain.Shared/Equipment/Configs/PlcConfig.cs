using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.Equipment.Configs
{
    public class PlcConfig<T> : AdapterConfig<T>
    {
        /// <summary>
        /// 重发次数，如果为0，无限制，需要配合IsRetry参数使用
        /// </summary>
        public int RetryTime { get; set; }
    }
    public class PlcConfig : PlcConfig<dynamic>
    {
    }
}
