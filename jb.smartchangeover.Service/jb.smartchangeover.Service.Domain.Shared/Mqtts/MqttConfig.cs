using System;
using System.Collections.Generic;
using System.Text;

namespace jb.smartchangeover.Service.Domain.Shared.Mqtts
{
    public class MqttConfig
    {
        public MqttConfig() { }
        public MqttConfig(string host, int port, string user, string password)
        {
            this.Host = host;
            this.Port = port;
            this.User = user;
            this.Password = password;
        }
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public int Qos { get; set; } = 1;
        public bool? WithHeartBeat { get; set; } = true;
    }
}
