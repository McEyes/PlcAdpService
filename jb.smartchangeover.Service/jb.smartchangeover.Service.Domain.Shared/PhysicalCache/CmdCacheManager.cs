using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using jb.smartchangeover.Service.Domain.Shared.Mqtts.Dtos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace jb.smartchangeover.Service.Domain.Shared.PhysicalCache
{
    public class CmdCacheManager
    {
        protected readonly IConfiguration _config = null;
        private const string filePath = "cmdrecord.txt";
        private List<CmdRecordInfo> cmdRecordInfos = null;
        //private bool IsRun = false;
        private object _lock = new object();
        public int CmdRepeatCheckTime = 1;

        public CmdCacheManager(IConfiguration config)
        {
            _config = config;
            InitCache();
        }

        private void InitCache()
        {
            if (File.Exists(filePath) == false)
            {
                cmdRecordInfos = new List<CmdRecordInfo>();
                return;
            }
            using (var reader = new StreamReader(filePath))
            {
                var rs = reader.ReadToEnd();
                if (string.IsNullOrEmpty(rs)) cmdRecordInfos = new List<CmdRecordInfo>();
                else
                {
                    cmdRecordInfos = JsonConvert.DeserializeObject<List<CmdRecordInfo>>(rs);
                }
            }
        }

        //public bool IsExistsSameRecipe<T>(BaseMsg<T> Msg)
        //{
        //    lock (_lock)
        //    {
        //        if (cmdRecordInfos.Count == 0) return false;
        //        return cmdRecordInfos.Exists(p => p.MachineId == Msg.HostName&& p.RecipeName == Msg.ToString());
        //    }
        //}
        /// <summary>
        /// 一分钟之内是否重复发送指令
        /// </summary>
        /// <param name="Msg"></param>
        /// <param name="cmdRepeatCheckTim"></param>
        /// <returns></returns>
        public bool IsExistsSameRecipe(BaseMsg Msg, uint cmdRepeatCheckTim = 30)
        {
            lock (_lock)
            {
                CmdRepeatCheckTime = (int)cmdRepeatCheckTim;
                if (cmdRecordInfos.Count == 0) return false;
                return cmdRecordInfos.Exists(p => p.MachineId == Msg.HostName && p.LastTime > DateTime.Now.AddSeconds(-cmdRepeatCheckTim) && p.RecipeName == Msg.ToString());
            }
        }

        public void UpdateRecord(BaseMsg Msg)
        {
            lock (_lock)
            {
                if (cmdRecordInfos.Count == 0)
                {
                    cmdRecordInfos.Add(new CmdRecordInfo(Msg.ToString(), Msg.HostName));
                }
                else
                {
                    var info = cmdRecordInfos.FirstOrDefault(p => p.MachineId == Msg.HostName);
                    if (info != null)
                    {
                        info.RecipeName = Msg.ToString();
                        info.LastTime = DateTime.Now;
                    }
                    else
                        cmdRecordInfos.Add(new CmdRecordInfo(Msg.ToString(), Msg.HostName));
                }
            }
            FlushData();
        }

        /// <summary>
        /// 一秒钟只执行一次保存,没有那么频繁的命令，正常保存就好了
        /// </summary>
        private async void FlushData()
        {
            //if (IsRun) return;
            ////Task.Factory.StartNew(async () =>
            ////{
            //IsRun = true;
            //await Task.Delay(1000);
            //IsRun = false;
            //try
            //{
            var json = JsonConvert.SerializeObject(cmdRecordInfos);
            using (var write = new StreamWriter(filePath, false))
            {
                await write.WriteAsync(json);
                await write.FlushAsync();
            }
            if (!int.TryParse(_config["PlcService:CmdRepeatCheckTime"], out CmdRepeatCheckTime)) CmdRepeatCheckTime = 10;
            //}
            //catch { }
            //});
        }
    }
}
