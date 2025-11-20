using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public enum FX5DeviceType
    {
        /// <summary>
        /// D
        /// </summary>
        DataRegister = 0x2044,
        /// <summary>
        /// M
        /// </summary>
        AuxiliaryRelay = 0x204D,
        /// <summary>
        /// R
        /// </summary>
        ExtensionRegister = 0x2050,
        /// <summary>
        /// X
        /// </summary>
        Input = 0x2058,
        /// <summary>
        /// Y
        /// </summary>
        Output = 0x2059,
        /// <summary>
        /// S
        /// </summary>
        State = 0x2053,
        /// <summary>
        /// TN
        /// </summary>
        TimerCurrentValue = 0x4E54,
        /// <summary>
        /// TS
        /// </summary>
        TimerContact = 0x5354,
        /// <summary>
        /// CN
        /// </summary>
        CounterCurrentValue = 0x4E43,
        /// <summary>
        /// CS
        /// </summary>
        CounterContact = 0x5343,
    }

    public enum Fx3MCCommandType : byte
    {
        ReadByBit = 0x00,
        ReadByByte = 0x01,
        WriteByBit = 0x02,
        WriteByByte = 0x03,
        TestByBit = 0x04,
        TestByByte = 0x05,
        Run = 0x13,
        Stop = 0x14,
        CpuModel = 0x15,
        Loopback = 0x16
    }
}
