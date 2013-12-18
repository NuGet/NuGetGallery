using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs.Api.Models
{
    public class InvocationSummaryModel
    {
        public Guid Id { get; set; }
        public string Job { get; set; }
        public string Source { get; set; }
        public DateTimeOffset? EstimatedNextVisibleAt { get; set; }
        public DateTimeOffset? LastDequeuedAt { get; set; }
        public InvocationStatus Status { get; set; }
        public ExecutionResult Result { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        
        public Uri Detail { get; set; }

        public InvocationSummaryModel(Invocation invocation)
        {
            Id = invocation.Id;
            Job = invocation.Job;
            Source = invocation.Source;
            EstimatedNextVisibleAt = invocation.EstimatedNextVisibleTime;
            LastDequeuedAt = invocation.LastDequeuedAt;
            CompletedAt = invocation.CompletedAt;
            Status = invocation.Status;
            Result = invocation.Result;
        }
    }
}
