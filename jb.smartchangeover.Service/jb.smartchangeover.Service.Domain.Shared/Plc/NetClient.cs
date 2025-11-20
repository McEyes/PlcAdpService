using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Eventing.Reader;
using Microsoft.Extensions.Logging;
using jb.smartchangeover.Service.Domain.Shared.Commons;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;
using System.ComponentModel;
using jb.smartchangeover.Service.Domain.Shared.Equipment.Configs;
using System.Collections.Concurrent;

namespace jb.smartchangeover.Service.Domain.Shared
{

    //
    // 摘要:
    //     Represents the method that will handle the System.ComponentModel.INotifyPropertyChanged.PropertyChanged
    //     event raised when a property is changed on a component.
    //
    // 参数:
    //   sender:
    //     The source of the event.
    //
    //   e:
    //     A System.ComponentModel.PropertyChangedEventArgs that contains the event data.
    public delegate void StatusChangedEventHandler(INetClient? netClient, PropertyChangedEventArgs e);

    public abstract class NetClient : INetClient, IDisposable
    {

        protected bool disablePropertyChanged = false;
        public bool hasDataChange = false;
        public virtual event StatusChangedEventHandler PropertyChanged;
        public IEquipmentConfig NetConfig { get; protected set; }
        /// <summary>
        /// 手动关闭系统中
        /// </summary>
        public bool Closing { get; set; } = false;
        protected string ConfigJson { get; set; }
        protected string IP { get; set; }
        protected int Port { get; set; }
        protected Socket TCPSocket { get; set; }
        /// <summary>
        /// 失败次数，没失败一次，重连时间翻倍
        /// </summary>
        private int ConnRetries = 0;
        private DateTime LastConnTime = DateTime.Now;
        public ConcurrentStack<CmdCacheItem> CmdQueue { get; set; }


        private readonly ManualResetEvent timeoutObject;
        protected readonly ILogger Log;

        public virtual bool IsConnected
        {
            get
            {
                if (TCPSocket == null) return false;
                return TCPSocket.Connected;
            }
        }
        protected CancellationTokenSource _cts;


        public EquipmentStatus PreEquipmentStatus = EquipmentStatus.Unknown;
        private EquipmentStatus _EquipmentStatus = EquipmentStatus.Unknown;
        private DeviceCommandStatus _CmdExecStatus = 0;
        /// <summary>
        /// 设备状态，当状态发生改变时才上报状态信息
        /// </summary>
        [Description("EquipmentStatus")]
        public EquipmentStatus EquipmentStatus
        {
            get
            {
                return _EquipmentStatus;
            }
            set
            {
                if (_EquipmentStatus != value)
                {
                    if (_EquipmentStatus != EquipmentStatus.Unknown) PreEquipmentStatus = _EquipmentStatus;
                    _EquipmentStatus = value;
                    hasDataChange = true;
                    if (value == EquipmentStatus.Disconnect)
                        NetConfig.IsConnected = false;
                    else
                        NetConfig.IsConnected = true;
                    Log?.LogInformation($"{NetConfig.Name}状态由[{PreEquipmentStatus}]切换到[{value}]，状态改变，上报状态...");
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("EquipmentStatus"));
                }
            }
        }

        /// <summary>
        /// 设备错误码
        /// </summary>
        public EquipmentErrorCode ErrorCode { get; set; }

        /// <summary>
        ///  命令执行情况:0重置(执行前重置，1状态不能重置)，1执行中，2执行完成，3执行失败
        /// </summary>
        public DeviceCommandStatus CmdExecStatus
        {
            get { return _CmdExecStatus; }
            set
            {
                if (_CmdExecStatus != value)
                {
                    _CmdExecStatus = value;
                    hasDataChange = true;
                    if (!disablePropertyChanged && PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("CmdExecStatus"));

                    Log?.LogError($"[{NetConfig.Name}][{IP}] CmdExecStatus:{_CmdExecStatus}");
                }
            }
        }


        protected NetClient(IEquipmentConfig config, ILogger log)
        {
            this.NetConfig = config;
            this.IP = config.Ip;
            this.Port = config.Port;
            timeoutObject = new ManualResetEvent(false);
            Log = log;
            CmdQueue = new System.Collections.Concurrent.ConcurrentStack<CmdCacheItem>();
        }

        #region network operate
        private void ConnectCallback(IAsyncResult asyncResult)
        {
            try
            {
                Socket client = asyncResult.AsyncState as Socket;
                if (client != null)
                {
                    client.EndConnect(asyncResult);
                }
                EquipmentStatus = EquipmentStatus.Runnling;
                NetConfig.IsConnected = true;
                ErrorCode = EquipmentErrorCode.OK;
                Log.LogInformation($"[{NetConfig.Name}][{IP}] Is Connected: {IsConnected}");
            }
            catch (Exception ex)
            {
                EquipmentStatus = EquipmentStatus.UnknownError;
                ErrorCode = EquipmentErrorCode.UnknownError;
                NetConfig.IsConnected = false;
                Log?.LogError($"[{NetConfig.Name}][{IP}] connectCallback error:{ex.Message}\r\n{ex.StackTrace}");
            }
            finally
            {
                timeoutObject.Set();
            }
        }
        public virtual IAsyncResult Open(AsyncCallback cb = null)
        {
            if (Closing)
            {
                Log?.LogWarning($"[{NetConfig.Name}][{IP}] closing...");
                return new AsyncResult(TCPSocket, null, false, false);
            }
            if (ConnRetries > 0 && LastConnTime.AddSeconds(NetConfig.GetRetriesTime(ConnRetries)) > DateTime.Now)
            {
                Log?.LogWarning($"[{NetConfig.Name}][{IP}] conn Retries {ConnRetries}");
                return new AsyncResult(TCPSocket, null, false, false);
            }
            if (!NetConfig.Enable)
            {
                Log.LogError($"[{NetConfig.Name}][{NetConfig.Ip}:{NetConfig.Port}] TCP Modbus 设备停止监控 ");
                return new AsyncResult(TCPSocket, null, false, false);
            }
            if (this.TCPSocket == null || !this.TCPSocket.Connected)
            {
                Close();
                _cts = new CancellationTokenSource();
                this.TCPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                TCPSocket.ReceiveTimeout = NetConfig.ReceiveTimeout;
                TCPSocket.SendTimeout = NetConfig.SendTimeout;
            }
            if (!this.TCPSocket.Connected && !_cts.Token.IsCancellationRequested)
            {
                if (cb == null)
                {
                    cb = new AsyncCallback(ConnectCallback);
                }
                IAsyncResult iar = TCPSocket.BeginConnect(IP, Port, cb, TCPSocket);
                if (!timeoutObject.WaitOne(NetConfig.ConnectTimeout, false))
                {
                    EquipmentStatus = EquipmentStatus.Disconnect;
                    ErrorCode = EquipmentErrorCode.ConnectTimeout;
                    Log?.LogWarning($"[{NetConfig.Name}][{IP}] Timed out trying to connect.");
                }
                else
                {
                    EquipmentStatus = EquipmentStatus.Runnling;
                    NetConfig.IsConnected = true;
                    ErrorCode = EquipmentErrorCode.None;
                }
                return iar;
            }
            return new AsyncResult(TCPSocket, null, false, false);
        }

        public virtual void Close()
        {
            Log?.LogWarning($"[{NetConfig.Name}][{IP}][{EquipmentStatus}] to close.");
            try
            {
                Closing = true;
                NetConfig.Enable = false;
                if (_cts != null)
                {
                    _cts.Cancel();
                }
                if (TCPSocket != null)
                {
                    TCPSocket.Disconnect(false);
                    TCPSocket.Close();
                    TCPSocket.Dispose();
                    Thread.Sleep(NetConfig.ConnectTimeout + 800);
                    TCPSocket = null;

                    EquipmentStatus = EquipmentStatus.Disabled;
                    NetConfig.IsConnected = false;
                    ErrorCode = EquipmentErrorCode.Disconnect;
                    Log?.LogWarning($"[{NetConfig.Name}][{IP}] TCPSocket dispose.");
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[{NetConfig.Name}][{IP}][{EquipmentStatus}] close error {ex.Message}\r\n{ex.StackTrace}.");
            }
            finally
            {
                Closing = false;
                NetConfig.Enable = true;
            }
        }

        public virtual IResult<byte[]> Send(byte[] data)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<byte[]>();
            result.Data = new byte[0];
            if (!IsConnected && !IcmpCheck.Check(IP))
            {
                EquipmentStatus = EquipmentStatus.Disconnect;
                ErrorCode = EquipmentErrorCode.Disconnect;
                result.SetError(ErrorCode);
                result.SetError($"[{NetConfig.Name}][{IP}] Device is offline.");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Device is offline.");
                return result;
            }
            long wait = 0;
            int waitTick = 50;
            var iar = Open();
            var cts = _cts.Token;
            while (iar.IsCompleted && !IsConnected && wait < NetConfig.ConnectTimeout && !cts.IsCancellationRequested)
            {
                Thread.Sleep(waitTick);
                wait += waitTick;
            }
            if (!IsConnected)
            {
                result.SetError($"[{NetConfig.Name}][{IP}] Device IsConnected :{IsConnected}.");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Device  IsConnected :{IsConnected}.");
                return result;
            }

            try
            {
                var sendbyte = TCPSocket.Send(data);
                //Log?.LogDebug($"[{NetConfig.Name}][{IP}] Send 实际发送字节数量:{result2}, 耗时：{watch.ElapsedMilliseconds}毫秒");
                if (sendbyte == 0)
                {
                    if (CmdExecStatus == DeviceCommandStatus.Executing)
                        CmdExecStatus = DeviceCommandStatus.Failed;
                    ErrorCode = EquipmentErrorCode.CommandFailed;
                    result.SetError(ErrorCode);
                    result.SetError($"[{NetConfig.Name}][{IP}] Send 实际发送字节数量0,发送失败！");
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (CmdExecStatus == DeviceCommandStatus.Executing)
                    CmdExecStatus = DeviceCommandStatus.Failed;
                ErrorCode = EquipmentErrorCode.UnknownError;
                result.SetError(ErrorCode);
                result.SetError($"[{NetConfig.Name}][{IP}] Send data error:{ex.Message}");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Send error:{ex.Message},\r\n{ex.StackTrace}, 耗时：{watch.ElapsedMilliseconds}毫秒");
                if (ex.Message.Contains("你的主机中的软件中止了一个已建立的连接"))
                {
                    result.SetError($"[{NetConfig.Name}][{IP}] tcp close");
                    result.SetError($"[{NetConfig.Name}][{IP}] tcp close");
                    Close();
                }
                return result;
            }
            wait = 0;
            cts = _cts.Token;
            try
            {
                while (wait < NetConfig.ReceiveTimeout && !cts.IsCancellationRequested && TCPSocket != null && TCPSocket.Available == 0)
                {
                    Thread.Sleep(waitTick);
                    wait += waitTick;
                }
                if (TCPSocket?.Available == 0)
                {
                    result.SetError($"[{NetConfig.Name}][{IP}] Receive Available:{TCPSocket.Available}");
                    Log?.LogWarning($"[{NetConfig.Name}][{IP}] Receive Available:{TCPSocket.Available}, 耗时：{watch.ElapsedMilliseconds}毫秒");
                    return result;
                }
                if (TCPSocket != null && TCPSocket.Available > 0)
                {
                    byte[] buffer = new byte[TCPSocket.Available];
                    var reLen = TCPSocket.Receive(buffer);
                    //Log?.LogDebug($"[{NetConfig.Name}][{IP}] Send Receive buffer length:{reLen} .\n Code :{string.Join(" ", buffer)}\n Hex :{ByteToHexString(buffer)}\n String:{Encoding.ASCII.GetString(buffer)}\n 0X :{BitConverter.ToString(buffer).Replace("-", " ")}");
                    result.Data = buffer;
                }
                else
                {
                    result.SetError($"[{NetConfig.Name}][{IP}] Receive data Error:{TCPSocket.Available}");
                    Log?.LogWarning($"[{NetConfig.Name}][{IP}] Receive data Error:{TCPSocket.Available}, 耗时：{watch.ElapsedMilliseconds}毫秒");
                }
                return result;
            }
            catch (Exception ex)
            {
                ErrorCode = EquipmentErrorCode.UnknownError;
                result.SetError(ErrorCode);
                result.SetError($"[{NetConfig.Name}][{IP}] Receive error:{ex.Message}");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Receive error:{ex.Message},\r\n{ex.StackTrace}, 耗时：{watch.ElapsedMilliseconds}毫秒"); ErrorCode = EquipmentErrorCode.UnknownError;
            }
            return result;
        }


        public IResult<byte[]> Receive()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var result = new Result<byte[]>();
            result.Data = new byte[0];

            long wait = 0;
            int waitTick = 50;
            var iar = Open();
            var cts = _cts.Token;
            while (iar.IsCompleted && !IsConnected && wait < NetConfig.ConnectTimeout && !cts.IsCancellationRequested)
            {
                Thread.Sleep(waitTick);
                wait += waitTick;
            }
            if (!IsConnected)
            {
                result.SetError($"[{NetConfig.Name}][{IP}] Device IsConnected :{IsConnected}.");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Device  IsConnected :{IsConnected}.");
                return result;
            }
            try
            {
                while (wait < NetConfig.ReceiveTimeout && !cts.IsCancellationRequested && TCPSocket != null && TCPSocket.Available == 0)
                {
                    Thread.Sleep(waitTick);
                    wait += waitTick;
                }
                if (TCPSocket?.Available == 0)
                {
                    result.SetError($"[{NetConfig.Name}][{IP}] Receive Available:{TCPSocket.Available}");
                    Log?.LogWarning($"[{NetConfig.Name}][{IP}] Receive Available:{TCPSocket.Available}, 耗时：{watch.ElapsedMilliseconds}毫秒");
                    return result;
                }
                if (TCPSocket != null && TCPSocket.Available > 0)
                {
                    byte[] buffer = new byte[TCPSocket.Available];
                    var reLen = TCPSocket.Receive(buffer);
                    //Log?.LogDebug($"[{NetConfig.Name}][{IP}] Send Receive buffer length:{reLen} .\n Code :{string.Join(" ", buffer)}\n Hex :{ByteToHexString(buffer)}\n String:{Encoding.ASCII.GetString(buffer)}\n 0X :{BitConverter.ToString(buffer).Replace("-", " ")}");
                    result.Data = buffer;
                }
                else
                {
                    result.SetError($"[{NetConfig.Name}][{IP}] Receive data Error:{TCPSocket.Available}");
                    Log?.LogWarning($"[{NetConfig.Name}][{IP}] Receive data Error:{TCPSocket.Available}, 耗时：{watch.ElapsedMilliseconds}毫秒");
                }
                return result;
            }
            catch (Exception ex)
            {
                ErrorCode = EquipmentErrorCode.UnknownError;
                result.SetError(ErrorCode);
                result.SetError($"[{NetConfig.Name}][{IP}] Receive error:{ex.Message}");
                Log?.LogError($"[{NetConfig.Name}][{IP}] Receive error:{ex.Message},\r\n{ex.StackTrace}, 耗时：{watch.ElapsedMilliseconds}毫秒"); ErrorCode = EquipmentErrorCode.UnknownError;
            }
            return result;
        }


        #endregion

        #region abstract methods
        ///// <summary>
        ///// 解析命令
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <param name="data"></param>
        ///// <returns></returns>
        //public abstract byte[] getCmd<T>(T data);

        #endregion


        #region listener 


        // private void ReceiveCallback(IAsyncResult ar)
        // {
        //     // 获取StateObject对象和客户端Socket
        //     StateObject state = (StateObject)ar.AsyncState;
        //     Socket clientSocket = state.ClientSocket;

        //     try
        //     {
        //         // 结束异步接收，获取接收到的数据长度
        //         int bytesRead = clientSocket.EndReceive(ar);

        //         if (bytesRead > 0)
        //         {
        //             // 处理接收到的数据
        //             byte[] receivedData = new byte[bytesRead];
        //             //Log.LogDebug($"[{NetConfig.Name}][{IP}] 收到来自客户端的消息 Code：{" ".Join(receivedData)}");
        //             Array.Copy(state.Buffer, receivedData, bytesRead);
        //             string message = System.Text.Encoding.ASCII.GetString(receivedData);
        //             //Log.LogDebug($"[{NetConfig.Name}][{IP}] 收到来自客户端的消息：{message}");

        //             // 继续异步接收数据
        //             clientSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReceiveCallback, state);
        //             if (state.Buffer.Length > 0)
        //             {
        //                 Log?.LogWarning($"[{NetConfig.Name}][{IP}] Receive Callback buffer length:{state.Buffer.Length} .\n Code :{string.Join(" ", state.Buffer)}\n Hex :{ByteToHexString(state.Buffer)}\n String:{Encoding.ASCII.GetString(state.Buffer)}\n 0X :{BitConverter.ToString(state.Buffer).Replace("-", " ")}");
        //             }
        //             else
        //             {
        //                 Log?.LogWarning($"[{NetConfig.Name}][{IP}] Receive Callback buffer length:{state.Buffer.Length} ");
        //             }
        //         }
        //         else
        //         {
        //             // 客户端断开连接
        //             Log.LogError($"[{NetConfig.Name}][{IP}] 客户端已断开连接：{clientSocket.RemoteEndPoint}");
        //             clientSocket.Close();
        //         }
        //     }
        //     catch (SocketException ex)
        //     {
        //         // 客户端连接出现错误
        //         Log.LogError($"[{NetConfig.Name}][{IP}] 客户端连接出现错误：{ex.Message}");
        //         clientSocket.Close();
        //     }
        // }


        #endregion listener 

        #region common methods

        /// <summary>
        /// convert hex string to byte array
        /// example: 81006A31
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public byte[] HexStringToByteArray(string input)
        {
            string pattern = @"^[0-9A-Fa-f]{1,}$";
            bool match = Regex.IsMatch(input, pattern);
            if (string.IsNullOrEmpty(input) || input.Length % 2 > 0 || !match)
            {
                return new byte[0];
            }

            byte[] result = new byte[input.Length / 2];
            for (int i = 0; i < input.Length; i += 2)
            {
                result[i / 2] = Convert.ToByte(input.Substring(i, 2), 16);
            }

            return result;
        }

        public byte[] HexArrayToByteArray(byte[] input)
        {
            if (input == null || input.Length == 0 || input.Length % 2 > 0)
            {
                return new byte[0];
            }

            byte[] result = new byte[input.Length / 2];
            for (int i = 0; i < input.Length; i += 2)
            {
                result[i / 2] = Convert.ToByte(Encoding.ASCII.GetString(input, i, 2), 16);
            }
            return result;
        }

        public string ByteToHexString(byte[] input)
        {
            return ByteToHexString(input, (char)0);
        }

        public string ByteToHexString(byte[] input, char separate)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte inByte in input)
            {
                if (separate == 0)
                {
                    sb.Append(string.Format("{0:X2}", inByte));
                }
                else
                {
                    sb.Append(string.Format("{0:X2}{1}", inByte, separate));
                }
            }

            if (separate != 0 && sb.Length > 1 && sb[sb.Length - 1] == separate)
            {
                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }

        public string FixString(string input, int length)
        {
            return FixString(input, length, '0');
        }

        public string FixString(string input, int length, char prex)
        {
            if (input.Length >= length)
            {
                return input;
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < length - input.Length; i++)
            {
                sb.Append(prex);
            }
            sb.Append(input);
            return sb.ToString();
        }

        /// <summary>
        /// 转换成Ascii编码
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public string ToAsciiString(byte[] buffer)
        {
            return Encoding.ASCII.GetString(buffer);
        }

        /// <summary>
        /// 转换16进制字符串
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public string To0xString(byte[] buffer)
        {
            return BitConverter.ToString(buffer).Replace("-", " ");
        }


        #endregion



        protected virtual void Dispose(bool disposing)
        {
            Close();
            GC.SuppressFinalize(this);
        }


        public void Dispose()
        {
            Dispose(disposing: true);
        }
        ~NetClient()
        {
            Dispose(disposing: false);
        }
    }


    public class StateObject
    {
        public Socket ClientSocket { get; }
        public byte[] Buffer { get; }

        public StateObject(Socket clientSocket, byte[] buffer)
        {
            ClientSocket = clientSocket;
            Buffer = buffer;
        }
    }
}
