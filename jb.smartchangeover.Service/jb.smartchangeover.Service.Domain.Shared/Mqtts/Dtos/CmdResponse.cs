using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos
{
    public class CmdResponse : BaseMsg
    {
        public CmdResponse()
        {
            this.MessageType = "CmdResponse";
        }

        public CmdResponseData data { get; set; }
    }

    public class CmdResponseData
    {
        public string cmdID { get; set; }

        public string cmd { get; set; }

        public string result { get; set; }

        public string errCode { get; set; }

        public string errMsg { get; set; }
    }
}
