
# PLC服务端监控

轨道设备数据采集和远程控制服务。
	采集轨道状态：是否有板，启停，过板数量，轨道宽度等。
	远程指令支持：启停，调轨等
	支持三菱FX3，FX5系列设备，
	支持所有支持Modbus协议设备

## 硬件基础要求：
	轨道：
		1，支持modbus tcp通信的PLC
		2，适配JP一键转拉指令
		3，内网ip
## NXT：
	所有有fujie接口的nxt程序，启动nxt的一键转拉功能即可

 轨道设备配置说明
```json
    {
          "Id": "3a100f26-9492-84d4-7707-e11cd73b34e1", 设备id
          "No": "AE-4444",  							设备编号
          "Name": "扫描仪AE-4444",						设备名称
          "Kind": "conveyor",							设备类型，此处固定conveyor
          "Workcell": "TEST",							
          "Bay": "Bay011",
          "Host": "AE-4444",							设备编号
          "Ip": "10.114.197.153",						设备IP
          "Port": 502,									通信端口
          "Protocol": "MODBUS",							通信协议FX3,SCANNER,FX5,MODBUS
          "Type": "PLC",								固定 PLC
          "QtyType":"5",								轨道类型： 默认为1单轨道，2双轨道，3筛选机，4翻板机，5扫描仪
          "Enable": true,								是否启用一键转拉
         "IsDebug":false,								是否调试模式，调试模式
          "HeartBeat": 60,								上传心跳
          "WithHeartBeat": true,						是否启用mqtt心跳，不能改为false
          "WithSubscribe": true							是否启用mqtt数据监听，不能改为false
     },
```

