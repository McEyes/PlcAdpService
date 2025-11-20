using System.Reflection;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using jb.smartchangeover.Service.Domain.Shared.Mqtts;
using Microsoft.Extensions.Logging;

namespace jb.smartchangeover.Service.Domain.Shared.Plc
{
    public class NetClientFactory 
    {

        /// <summary>
        /// get instance
        /// </summary>
        /// <param name="config"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static PlcClient GetPlcInstance(IEquipmentConfig config, MqttClientService mqttClient, ILogger log)
        {
            PlcClient client = null;
            switch (config.Protocol.ToUpper())
            {
                case "FX3":
                    {
                        if (config.ProtocolType == "ascii")
                        {
                            client = new Fx3AsciiClient(config, mqttClient, log);
                        }
                        else
                        {
                            client = new Fx3BinaryClient(config, mqttClient, log);
                        }
                    }
                    break;
                case "FX3D":
                    {
                        if (config.ProtocolType == "ascii")
                        {
                            client = new Fx3AsciiClient(config, mqttClient, log);
                        }
                        else
                        {
                            client = new Fx3dBinaryClient(config, mqttClient, log);
                        }
                    }
                    break;
                case "MODBUS":
                    {
                        client = new ModbusClient(config, mqttClient, log);
                    }
                    break;
                case "MODBUS2000":
                    {
                        client = new ModbusClient2000(config, mqttClient, log);
                    }
                    break;
                case "SCANNER":
                    {
                        client = new ScannerAsciiClient(config, mqttClient, log);
                    }
                    break;
                case "FX5":
                    {
                        if (config.ProtocolType == "ascii")
                        {
                            client = new Fx5AsciiClient(config, mqttClient, log);
                        }
                        else
                        {
                            client = new Fx5BinaryClient(config, mqttClient, log);
                        }
                        break;
                    }
                case "FX5D":
                    {
                        client = new FX5DBinaryClient(config, mqttClient, log);
                        break;
                    }
            }
            return client;
        }



    }
}
