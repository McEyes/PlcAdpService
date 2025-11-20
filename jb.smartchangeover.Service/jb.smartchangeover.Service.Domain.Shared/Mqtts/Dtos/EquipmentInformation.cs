using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos
{
    public class EquipmentInformation : BaseMsg
    {
        [JsonProperty("data")]
        public EquipmentInformationData Data { get; set; }
    }
    public class EquipmentInformationData
    {
        [JsonProperty("parameters")]
        public List<EquipmentInformationDataParameter> Parameters { get; set; }

    }
    public class EquipmentInformationDataParameter
    {
        [JsonProperty("key")]
        public string Key { get; set; }
        [JsonProperty("Value")]
        public string Value { get; set; }
        [JsonProperty("unit")]
        public string Unit { get; set; }
    }
}
