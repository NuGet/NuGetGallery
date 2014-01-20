using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work
{
    internal static class TestHelpers
    {
        internal static InvocationState CreateInvocation(Guid id, string job, string source)
        {
            return CreateInvocation(id, job, source, null, isContinuation: false);
        }

        internal static InvocationState CreateInvocation(Guid id, string job, string source, Dictionary<string, string> payload)
        {
            return CreateInvocation(id, job, source, payload, isContinuation: false);
        }

        internal static InvocationState CreateInvocation(Guid id, string job, string source, Dictionary<string, string> payload, bool isContinuation)
        {
            return new InvocationState(
                new InvocationState.InvocationRow()
                {
                    Version = 0,
                    Id = id,
                    Job = job,
                    Source = source,
                    Payload = payload == null ? null : InvocationPayloadSerializer.Serialize(payload),
                    Status = (int)InvocationStatus.Queued,
                    Result = (int)ExecutionResult.Incomplete,
                    IsContinuation = isContinuation
                });
        }
    }
}
