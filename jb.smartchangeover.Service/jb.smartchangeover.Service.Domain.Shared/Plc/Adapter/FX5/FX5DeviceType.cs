using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public static class Fx5UmcCommandType
    {
        public static readonly byte[] ReadByBit = new byte[] { 0x01, 0x04, 0x01, 0x00 };
        public static readonly byte[] ReadByWord = new byte[] { 0x01, 0x04, 0x00, 0x00 };
        public static readonly byte[] WriteByBit = new byte[] { 0x01, 0x14, 0x01, 0x00 };
        public static readonly byte[] WriteByWord = new byte[] { 0x01, 0x14, 0x00, 0x00 };
    }

}
