using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Backend.Tracing
{
    public class JobResposeTableEntity : TableEntity
    {
        public string LastInvocationId { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public DateTimeOffset CompletedAt { get; set; }
        public string RequestSource { get; set; }
        public string Status { get; set; }
        public string Exception { get; set; }

        public JobResposeTableEntity(string instanceId, JobResponse response)
            : base(response.Invocation.Request.Name, instanceId)
        {
            LastInvocationId = response.Invocation.Id.ToString("N");
            ReceivedAt = response.Invocation.RecievedAt;
            RequestSource = response.Invocation.Source;
            Status = response.Result.Status.ToString();
            Exception = response.Result.Exception == null ? null : response.Result.Exception.ToString();
        }
    }
}
