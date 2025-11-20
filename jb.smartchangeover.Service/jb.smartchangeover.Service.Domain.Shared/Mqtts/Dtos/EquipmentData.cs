using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos
{
    public class EquipmentData
    {
        [JsonProperty("parameters")]
        public List<EquipmentParameter> Parameters { get; set; }
    }
}
