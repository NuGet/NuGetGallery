using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs
{
    public enum InvocationStatus
    {
        Unspecified = 0,
        Queuing,
        Queued,
        Executing,
        Completed,
        Faulted,
        Suspended,
        Crashed
    }
}
