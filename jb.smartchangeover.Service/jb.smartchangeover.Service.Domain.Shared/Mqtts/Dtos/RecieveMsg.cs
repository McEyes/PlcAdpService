using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos
{
    public class RecieveMsg
    {
        public string Id { get; set; }

        public string TimeStamp { get; set; }

        public string MessageType { get; set; }

        public string Sender { get; set; }

        public string HostName { get; set; }

        public object Data { get; set; }
    }
}
