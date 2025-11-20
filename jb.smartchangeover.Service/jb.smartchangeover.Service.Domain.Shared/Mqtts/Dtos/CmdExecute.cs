using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos
{
    public class CmdExecute
    {
        [JsonProperty("cmd")]
        public string Cmd { get; set; }

        [JsonProperty("parameter")]
        public string Parameter { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("wirtercmd")]
        public bool IsWriteCmd { get; set; }=false;
    }
}
