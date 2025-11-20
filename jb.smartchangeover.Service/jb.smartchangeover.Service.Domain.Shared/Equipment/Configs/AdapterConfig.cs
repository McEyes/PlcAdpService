using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.Equipment.Configs
{
    public class AdapterConfig<T> : EquipmentConfig
    {
        /// <summary>
        /// 消息体
        /// </summary>
        public T Data { get; set; }
    }

    public class AdapterConfig : AdapterConfig<dynamic>
    {
    }
}
