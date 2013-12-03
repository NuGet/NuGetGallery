using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;

namespace NuGet.Services.Jobs
{
    public class InvocationRequest
    {
        public Invocation Invocation { get; private set; }
        public CloudQueueMessage Message { get; private set; }

        public InvocationRequest(Invocation invocation, CloudQueueMessage message)
        {
            Invocation = invocation;
            Message = message;
        }
    }
}
