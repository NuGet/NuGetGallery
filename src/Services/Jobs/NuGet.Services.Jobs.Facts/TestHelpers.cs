using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs
{
    internal static class TestHelpers
    {
        internal static Invocation CreateInvocation(Guid id, string job, string source)
        {
            return CreateInvocation(id, job, source, null, isContinuation: false);
        }

        internal static Invocation CreateInvocation(Guid id, string job, string source, Dictionary<string, string> payload)
        {
            return CreateInvocation(id, job, source, payload, isContinuation: false);
        }

        internal static Invocation CreateInvocation(Guid id, string job, string source, Dictionary<string, string> payload, bool isContinuation)
        {
            return new Invocation(
                new Invocation.InvocationRow()
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
