using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared
{

    public class ScannerMachineState
    {
        public int PcbNum { get; set; }
        public int HasPcb { get; set; }
        public int IsNeedPcb { get; set; }
        public int IsDoorOpen { get; set; }
        public decimal dPcbWidth { get; set; }
        public decimal dPcbHeight { get; set; }
        public int Status { get; set; }
        public string model { get; set; }
        public int cmdStatus { get; set; }
    }

}
