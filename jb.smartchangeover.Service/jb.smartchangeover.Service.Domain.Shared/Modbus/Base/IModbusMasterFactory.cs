using NModbus;

namespace jb.smartchangeover.Service.Domain.Shared.Modbus.Base
{
    public interface IModbusMasterFactory
    {
        IModbusMaster Create();
    }
}
