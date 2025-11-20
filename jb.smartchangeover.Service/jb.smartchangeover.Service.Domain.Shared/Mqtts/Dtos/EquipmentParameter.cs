using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos
{
    public class EquipmentParameter
    {
        /// <summary>
        /// widthlane1(调宽D450)、pcbnum(过板数量D454)、turnangle(板旋转角度D700)、turndirection(板道旋转方向D702)、TransferMode（传输模式D500）、ScannerDownSetY(下扫描抢D1552)、ScannerUpSetY(下扫描抢D1556)
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; }
        [JsonProperty("Value")]
        public string Value { get; set; }
        [JsonProperty("unit")]
        public string Unit { get; set; }
    }
}
