using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using jb.smartchangeover.Service.Domain.Shared.Plc.Enums;

namespace jb.smartchangeover.Service.Domain.Shared.Commons
{
    public interface IResult
    {
        string SourceId { get; set; }
        bool Success { get; set; }
        int Code { get; set; }
        string Msg { get; set; }
        void SetError(string msg, bool success = false);
        void SetError(string msg, int code);
        void SetError(EquipmentErrorCode errocCde = EquipmentErrorCode.UnknownError, string errorMsg = "");
        void AddError(IResult result);
    }
    public interface IResult<T> : IResult
    {
        T Data { get; set; }
    }
}
