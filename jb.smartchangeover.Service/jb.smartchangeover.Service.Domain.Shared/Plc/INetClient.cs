using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using jb.smartchangeover.Service.Domain.Shared.Commons;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public interface INetClient
    {
        System.Collections.Concurrent.ConcurrentStack<CmdCacheItem> CmdQueue { get; set; }
        EquipmentStatus EquipmentStatus { get; set; }
        EquipmentErrorCode ErrorCode { get; set; }
        DeviceCommandStatus CmdExecStatus { get; set; }
        IEquipmentConfig NetConfig { get; }
        IAsyncResult Open(AsyncCallback cb = null);
        IResult<byte[]> Send(byte[] data);
        IResult<byte[]> Receive();
        void Close();
    }
}
