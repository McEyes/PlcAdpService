using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static jb.smartchangeover.Service.Domain.Shared.ScannerAsciiClient;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public class CmdCacheItem
    {
        public CmdCacheItem(string cmd, string cmdId)
        {
            Cmd = cmd;
            CmdId = cmdId;
            StartTime = DateTime.Now;
        }

        //public string DeviceId { get; set; }
        public string Cmd { get; set; }

        public string CmdId { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public ReceiveEventArgs ReceiveEventArgs { get; set; }

        private AutoResetEvent resetEvent = new AutoResetEvent(false);
        public AutoResetEvent ResetEvent => resetEvent;

    }
}
