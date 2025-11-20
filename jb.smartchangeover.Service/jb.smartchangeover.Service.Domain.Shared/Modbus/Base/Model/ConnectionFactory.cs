using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Modbus.Base;
using NModbus;
using System;
using System.IO.Ports;
using System.Net.Sockets;

namespace jb.smartchangeover.Service.Domain.Shared.Modbus.Base.Model
{
    public class ConnectionFactory : IModbusMasterFactory
    {
        private const int DefaultTcpConnectionTimeoutMilliseconds = 5 * 1000;
        private readonly IEquipmentConfig _config;

        public ConnectionFactory(IEquipmentConfig connection)
        {
            _config = connection;
        }

        public string Name => _config.Name;

        public IModbusMaster Create()
        {
            var factory = new ModbusFactory();

            var tcpClient = new TcpClient
            {
                ReceiveTimeout = _config.ReceiveTimeout,
                SendTimeout = _config.SendTimeout
            };

            var effectiveConnectionTimeout = _config.ConnectTimeout;

            if (effectiveConnectionTimeout <= 0)
            {
                effectiveConnectionTimeout = DefaultTcpConnectionTimeoutMilliseconds;
            }

            if (!tcpClient.ConnectAsync(_config.Ip, _config.Port).Wait(effectiveConnectionTimeout))
            {
                tcpClient.Dispose();

                throw new TimeoutException($"Timed out trying to connect to TCP Modbus device at {_config.Ip}:{_config.Port}");
            }

            return factory.CreateMaster(tcpClient);
        }


        public IModbusMaster Create(Socket TCPSocket)
        {
            var factory = new ModbusFactory();
            return factory.CreateMaster(TCPSocket);
        }

        public override string ToString()
        {
            return $"{_config.Name} [TCP {_config.Ip}:{_config.Port}]";
        }
    }
}
