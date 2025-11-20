using System;
using System.Collections.Generic;
using System.Text;

namespace AoiAdapterService.Mqtts.Dtos
{
    public class MachineInfoDto
    {
        public MachineInfoDto(string id, string machineType, string hostName)
        {
            Id = id;
            MachineType = machineType;
            HostName = hostName;
        }

        public string Id { get; set; }

        public string MachineType { get; set; }

        public string HostName { get; set; }

        public string Topic => $"in/{MachineType}/{Id}";

        public DateTime LastReceiveTime { get; set; } = DateTime.Now;

        public override string ToString()
        {
            return HostName;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            MachineInfoDto p = obj as MachineInfoDto;
            if (p == null)
            {
                return false;
            }

            return this.Id == p.Id;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
