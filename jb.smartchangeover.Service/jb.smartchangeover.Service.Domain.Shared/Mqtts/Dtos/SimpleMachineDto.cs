using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos
{
    public class SimpleMachineDto
    {
        public string Id { get; set; }

        public string HostName { get; set; }

        public string MachineType { get; set; }

        public string AdpWebApiUrl { get; set; }
    }
}
