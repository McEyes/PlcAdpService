using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared
{
    public class AsyncResult : IAsyncResult
    {
        public AsyncResult() { }

        public AsyncResult(object state) { }

        public AsyncResult(object asyncState, WaitHandle asyncWaitHandle, bool completedSynchronously, bool isCompleted) : this(asyncState)
        {
            AsyncWaitHandle = asyncWaitHandle;
            CompletedSynchronously = completedSynchronously;
            IsCompleted = isCompleted;
        }

        public object AsyncState { get; set; }

        public WaitHandle AsyncWaitHandle { get; set; }

        public bool CompletedSynchronously { get; set; }

        public bool IsCompleted { get; set; }
    }
}
