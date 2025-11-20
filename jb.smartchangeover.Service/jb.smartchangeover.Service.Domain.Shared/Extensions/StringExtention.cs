using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public static class StringExtention
    {
        public static string RemoveFist(this string str)
        {
            return str.Substring(1).Trim();
        }

        public static string Join(this List<string> data, string separator = " ")
        {
            return string.Join(separator, data);
        }
        public static string Join(this string separator, params string[] joinStr)
        {
            return string.Join(separator, joinStr);
        }
        public static string Join(this string separator, params byte[] joinStr)
        {
            return string.Join(separator, joinStr);
        }
        public static ushort ToUShort(this string data)
        {
            ushort value = 0;
            if (ushort.TryParse(data, out value))
                return value;
            return 9999;
        }
    }
}
