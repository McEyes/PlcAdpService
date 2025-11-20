using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;

namespace jb.smartchangeover.Service.Domain.Shared.Modbus.Base.Model
{
    public class SavedConnections
    {
        public IEquipmentConfig[] Connections { get; set; }
    }
}
