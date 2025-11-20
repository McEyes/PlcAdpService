using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;

namespace jb.smartchangeover.Service.Domain.Shared.Commons
{
    public class JbResult : IResult
    {
        public JbResult()
        {

        }
        public JbResult(string msg, int code = 1001)
        {
            if (!string.IsNullOrWhiteSpace(msg))
            {
                Success = false;
                Code = code;
                Msg = $"[{Code}]{msg}。 ";
            }
        }
        public JbResult(EquipmentErrorCode errorCode = EquipmentErrorCode.None)
        {
            if (errorCode != EquipmentErrorCode.None)
            {
                Success = false;
                Code = errorCode.IntValue();
                Msg = $"[{Code}]{errorCode.GetDescription()}。 ";
            }
        }

        public string SourceId { get; set; }
        public bool Success { get; set; } = true;
        public int Code { get; set; } = EquipmentErrorCode.OK.IntValue();
        private string _Msg;
        public string Msg
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_Msg) && Code != 0) _Msg = $"[{Code}]{((EquipmentErrorCode)Code).GetDescription()}";
                return _Msg;
            }
            set { _Msg = value; }
        }

        public void SetError(string msg, bool success = false)
        {
            if (!string.IsNullOrWhiteSpace(msg))
            {
                Success = success;
                _Msg += (_Msg?.Length > 0 ? "\r\n" : "") + msg;
            }
        }
        public void SetError(string msg, int code)
        {
            if (!string.IsNullOrWhiteSpace(msg))
            {
                Success = false;
                Code = code;
                _Msg += $"{(_Msg?.Length > 0 ? "\r\n" : "")}[{Code}]{msg}。 ";
            }
        }

        public virtual void SetError(EquipmentErrorCode errorCode = EquipmentErrorCode.UnknownError, string errorMsg = "")
        {
            var oldSuccess = Success;
            Success = false;
            Code = errorCode.IntValue();
            if (errorCode != EquipmentErrorCode.None)
            {
                if (oldSuccess)
                    _Msg = $"[{Code}]{errorCode.GetDescription()}。 ";
                else
                    _Msg += $"{(_Msg?.Length > 0 ? "\r\n" : "")}[{Code}]{errorCode.GetDescription()}。";
            }
            if (!errorMsg.IsNullOrWhiteSpace())
                _Msg += $"{(_Msg.Length > 0 ? "\r\n" : "")}{errorMsg}";
        }

        public virtual void AddError(IResult result)
        {
            if (result == null) return;
            Success = result.Success;
            Code = result.Code;
            if (result.Code != EquipmentErrorCode.OK.IntValue() && result.Code != EquipmentErrorCode.Executing.IntValue())
            {
                Code = result.Code;
                if (!string.IsNullOrWhiteSpace(result.Msg))
                    _Msg += $"{(_Msg?.Length > 0 ? "\r\n" : "")}{result.Msg}";
            }
        }

        public static JbResult GetResult(string msg, int code = 1001)
        {
            return new JbResult(msg, code);
        }

        public static JbResult GetResult(EquipmentErrorCode errorCode = EquipmentErrorCode.None)
        {
            return new JbResult(errorCode);
        }
    }

    public class Result<T> : JbResult, IResult<T>
    {
        public T Data { get; set; }

        public override void AddError(IResult result)
        {
            base.AddError(result);
            if (!result.Success && result is IResult<string>) this.SetError((result as IResult<string>).Data);
            if (result is Result<T>) Data = (result as Result<T>).Data;
        }
    }

}
