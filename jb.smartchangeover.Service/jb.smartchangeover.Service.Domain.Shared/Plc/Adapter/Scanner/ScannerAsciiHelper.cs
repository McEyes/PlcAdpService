using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using jb.smartchangeover.Service.Domain.Shared.Commons;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;

namespace jb.smartchangeover.Service.Domain.Shared.Plc.Adapter.Modbus
{
    public static class ScannerAsciiHelper
    {

        public static IResult<string> CheckResult(string code, string returnStr)
        {
            var result = new Result<string>();
            if (string.IsNullOrWhiteSpace(returnStr))
            {
                result.Success = false;
                result.SetError($"【{code}】结果数据为空！");
                return result;
            }
            var datas = returnStr.Split(new char[] { '&' });
            if (datas.Length < 2)
            {
                result.Success = false;
                result.SetError($"【{code}】结果数据格式不正确！{returnStr}");
            }
            else if ("Success".Equals(datas[1], StringComparison.InvariantCultureIgnoreCase))
            {
                result.Success = true;
                if (datas.Length >= 2)
                {
                    if (code.Equals("GetMachineState", StringComparison.CurrentCultureIgnoreCase)
                        && datas[2].IndexOf('{') >= 0)
                    {
                        result.Data = datas[2].Substring(0, datas[2].IndexOf('}') + 1);
                    }
                    else if (datas.Length == 3) result.Data = datas[datas.Length - 1];
                }
                if (datas.Length >= 4)
                {
                    result.Msg += $", \r\n【{code}】返回数据结果异常：{returnStr}";
                }
            }
            else if ("Waiting".Equals(datas[1], StringComparison.InvariantCultureIgnoreCase))
            {//等待状态
                if (datas[2].IndexOf('{') >= 0) result.SetError(datas[datas.Length - 1]);
                else result.SetError(datas[2]);
                result.Code = (int)EquipmentErrorCode.Executing;
                result.Data = EquipmentErrorCode.Executing.ToIntString();
                result.SetError($"[{code}]");
                result.Success = true;
            }
            else if ("Fail".Equals(datas[1], StringComparison.InvariantCultureIgnoreCase))
            {
                result.Success = false;
                if (datas[2].IndexOf('{') >= 0) result.SetError(datas[datas.Length - 1]);
                else result.SetError(datas[2]);
                result.Code = (int)EquipmentErrorCode.CommandFailed;
                result.SetError($"[{code}]");
            }

            return result;
        }



        public static (List<IResult<string>> data, string cmd) CheckResult(string returnStr)
        {
            var list = new List<IResult<string>>();
            var result = new Result<string>();
            list.Add(result);
            if (string.IsNullOrWhiteSpace(returnStr))
            {
                result.Success = false;
                result.SetError($"返回数据为空！");
                return (list, "");
            }
            var datas = returnStr.Split(new char[] { '&' });
            var code = result.SourceId = datas[0];
            if (datas.Length < 2)
            {
                result.Success = false;
                result.SetError($"【{code}】结果数据格式不正确！{returnStr}");
            }
            else if (datas.Length == 2)
            {
                //result.Success = "Success".Equals(datas[1], StringComparison.InvariantCultureIgnoreCase);
                SetErrorData(result, datas[1], code);
            }
            else if (datas.Length == 3)
            {
                result.Data = datas[2];
                //result.Success = "Success".Equals(datas[1], StringComparison.InvariantCultureIgnoreCase) || "Waiting".Equals(datas[1], StringComparison.InvariantCultureIgnoreCase);
                SetErrorData(result, datas[1], code);
            }
            else
            {
                for (var i = 2; i + 2 < datas.Length; i += 2)
                {
                    var nxtCmd = GetDataAndNextCmd(result, datas[i]);
                    if (string.IsNullOrWhiteSpace(nxtCmd))
                    {
                        nxtCmd = GetDataAndNextCmd(result, datas[i + 1]);
                        i--;
                    }
                    if (!string.IsNullOrWhiteSpace(nxtCmd))
                    {
                        result = new Result<string>();
                        result.SourceId = nxtCmd;
                        result.Data = datas[i + 2];
                        SetErrorData(result, datas[i + 1], nxtCmd);
                        list.Add(result);
                    }
                }
            }
            return (list, code);
        }


        private static void SetErrorData(IResult<string> result, string exeStatus, string code)
        {
            if ("Success".Equals(exeStatus, StringComparison.InvariantCultureIgnoreCase))
            {//执行成功
                result.Success = true;
                //result.Data = DeviceCommandStatus.Success.ToIntString();
                // if ("AutoChange".Equals(code, StringComparison.InvariantCultureIgnoreCase))
            }
            else if ("Waiting".Equals(exeStatus, StringComparison.InvariantCultureIgnoreCase))
            {//等待状态
                //result.SetError(result.Data);
                result.Code = (int)EquipmentErrorCode.Executing;
                //result.Data = EquipmentErrorCode.Executing.ToIntString();
                //result.SetError($"[{result.SourceId}]");
                result.Success = true;
            }
            else if ("Fail".Equals(exeStatus, StringComparison.InvariantCultureIgnoreCase))
            {
                result.Success = false;
                result.SetError(result.Data);
                result.Code = (int)EquipmentErrorCode.CommandFailed;
                result.SetError($"[{result.SourceId}]");
            }
        }


        private static string GetDataAndNextCmd(IResult<string> result, string nextData)
        {
            var nxtcmd = nextData.ToLower();
            var cmd = string.Empty;
            if (nxtcmd.EndsWith(ScannerDeviceType.GetMachineState.ToLower()))
            {
                result.Data += nextData.Replace(ScannerDeviceType.GetMachineState, "");
                // return string.Empty;
                return ScannerDeviceType.GetMachineState;
            }
            else if (nextData.ToLower().EndsWith(ScannerDeviceType.ControlMachine.ToLower()))
            {
                result.Data += nextData.Replace(ScannerDeviceType.ControlMachine, "");
                return ScannerDeviceType.ControlMachine;
            }
            else if (nextData.ToLower().EndsWith(ScannerDeviceType.AutomaticSwitchingOfProducts.ToLower()))
            {
                result.Data += nextData.Replace(ScannerDeviceType.AutomaticSwitchingOfProducts, "");
                return ScannerDeviceType.AutomaticSwitchingOfProducts;
            }
            else if (nextData.ToLower().EndsWith(ScannerDeviceType.CheckProductExistence.ToLower()))
            {
                result.Data += nextData.Replace(ScannerDeviceType.CheckProductExistence, "");
                return ScannerDeviceType.CheckProductExistence;
            }
            else if (nextData.ToLower().EndsWith(ScannerDeviceType.CleanQuantity.ToLower()))
            {
                result.Data += nextData.Replace(ScannerDeviceType.CleanQuantity, "");
                return ScannerDeviceType.CleanQuantity;
            }
            else if (nextData.ToLower().EndsWith(ScannerDeviceType.GetCurrentProducts.ToLower()))
            {
                result.Data += nextData.Replace(ScannerDeviceType.GetCurrentProducts, "");
                return ScannerDeviceType.GetCurrentProducts;

            }
            return string.Empty;
        }
    }
}
