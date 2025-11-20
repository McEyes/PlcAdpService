using System;
using System.Net.NetworkInformation;

namespace jb.smartchangeover.Service.Domain.Shared.Commons
{
    public static class IcmpCheck
    {
        public static bool Check(string ip)
        {
            if (string.IsNullOrEmpty(ip))
            {
                return false;
            }
            try
            {
                Ping ping = new Ping();
                PingReply pr = ping.Send(ip, 2000);
                return (pr.Status == IPStatus.Success);
            }
            catch (Exception)
            {
                return false;
            }

        }
    }
}
