using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery.Jobs;
using NuGetGallery.Monitoring.Tables;

namespace NuGetGallery.Monitoring
{
    /// <summary>
    /// Contains all information about the current status of an invocation, in a form
    /// that is ready to be pivoted and published to monitoring tables
    /// </summary>
    public class InvocationStatus : PivotedTableEntity
    {
        public Guid Id { get; private set; }
        public string Job { get; private set; }
        public string Source { get; private set; }
        public string Payload { get; private set; }
        public JobStatus Status { get; private set; }
        public int DequeueCount { get; private set; }
        public string InstanceName { get; private set; }
        public Exception Exception { get; private set; }

        public DateTimeOffset Timestamp { get; private set; }
        public DateTimeOffset QueuedAt { get; private set; }
        public DateTimeOffset ReceivedAt { get; private set; }
        public DateTimeOffset? EndedAt { get; private set; }
        public DateTimeOffset? NextVisibleAt { get; private set; }
        public DateTimeOffset? EstimatedContinueAt { get; private set; }
        public DateTimeOffset? EstimatedReinvokeAt { get; private set; }
        
        public InvocationStatus(string instanceName, JobInvocation invocation, JobStatus status)
        {
            Timestamp = DateTimeOffset.UtcNow; // Timestamp of the record.

            Id = invocation.Id;
            Job = invocation.Request.Name;
            Source = invocation.Request.Source;
            Status = status;
            QueuedAt = invocation.Request.InsertionTime;
            ReceivedAt = invocation.RecievedAt;
            InstanceName = instanceName;

            if (invocation.Request.Message != null)
            {
                Payload = invocation.Request.Message.AsString;
                DequeueCount = invocation.Request.Message.DequeueCount;
                NextVisibleAt = invocation.Request.Message.NextVisibleTime;
            }
        }

        public InvocationStatus(string instanceName, JobInvocation invocation)
            : this(instanceName, invocation, JobStatus.Unspecified)
        {
        }

        public InvocationStatus(string instanceName, JobInvocation invocation, JobResponse response)
            : this(instanceName, invocation, response.Result.Status)
        {
            EndedAt = response.EndedAt;
            Exception = response.Result.Exception;

            if (response.Result.Status == JobStatus.AwaitingContinuation)
            {
                EstimatedContinueAt = DateTimeOffset.UtcNow + response.Result.Continuation.WaitPeriod;
            }
            else if (response.Result.RescheduleIn.HasValue)
            {
                EstimatedReinvokeAt = DateTimeOffset.UtcNow + response.Result.RescheduleIn.Value;
            }
        }

        public override IEnumerable<TablePivot> GetPivots()
        {
            yield return PivotOn(
                name: "InvocationStatus", 
                partition: () => Id.ToString().ToLowerInvariant());

            yield return PivotOn(
                name: "InvocationHistory", 
                partition: () => Id.ToString().ToLowerInvariant(),
                row: () => ReverseChronlogicalRowKey);

            yield return PivotOn(
                name: "InstanceStatus",
                partition: () => InstanceName);

            yield return PivotOn(
                name: "InstanceHistory",
                partition: () => InstanceName,
                row: () => ReverseChronlogicalRowKey);

            yield return PivotOn(
                name: "JobStatus",
                partition: () => Job);

            yield return PivotOn(
                name: "JobHistory",
                partition: () => Job,
                row: () => ReverseChronlogicalRowKey);
        }
    }
}
