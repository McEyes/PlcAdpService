using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Application.Contracts.Handlers
{
    public class DekConfigDto
    {
        public List<MachineApiDto> MachineApiList { get; set; }
    }

    public class MachineApiDto
    {
        public string MachineId { get; set; }

        public string AdpWebApiUrl { get; set; }
    }
}
