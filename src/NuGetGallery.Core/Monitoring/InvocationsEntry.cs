using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Jobs;
using NuGetGallery.Storage;

namespace NuGetGallery.Monitoring
{
    /// <summary>
    /// Contains all information about the current status of an invocation, in a form
    /// that is ready to be pivoted and published to monitoring tables
    /// </summary>
    public class InvocationsEntry : PivotedTableEntity
    {
        public Guid Id { get; private set; }
        public string Job { get; private set; }
        public string Source { get; private set; }
        public string Payload { get; private set; }
        public JobStatus Status { get; private set; }
        public int DequeueCount { get; private set; }
        public string InstanceName { get; private set; }
        public Exception Exception { get; private set; }
        public string LogUrl { get; private set; }

        public DateTimeOffset QueuedAt { get; private set; }
        public DateTimeOffset ReceivedAt { get; private set; }
        public DateTimeOffset? EndedAt { get; private set; }
        public DateTimeOffset? NextVisibleAt { get; private set; }
        public DateTimeOffset? EstimatedContinueAt { get; private set; }
        public DateTimeOffset? EstimatedReinvokeAt { get; private set; }

        public InvocationsEntry(string instanceName, JobInvocation invocation, JobStatus status)
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

        public InvocationsEntry(string instanceName, JobInvocation invocation, JobResponse response, string logUrl)
            : this(instanceName, invocation, response == null ? JobStatus.Crashed : response.Result.Status)
        {
            LogUrl = logUrl;

            if (response != null)
            {
                EndedAt = response.EndedAt;
                Exception = response.Result.Exception;
                if (response.Result.Status == JobStatus.Suspended)
                {
                    EstimatedContinueAt = DateTimeOffset.UtcNow + response.Result.Continuation.WaitPeriod;
                }
                else if (response.Result.RescheduleIn.HasValue)
                {
                    EstimatedReinvokeAt = DateTimeOffset.UtcNow + response.Result.RescheduleIn.Value;
                }
            }
            else
            {
                EndedAt = DateTimeOffset.UtcNow;
            }
        }

        public override IEnumerable<TablePivot> GetPivots()
        {
            yield return PivotOn(
                partition: () => Id.ToString("N").ToLowerInvariant());

            yield return PivotOn(
                name: "History",
                partition: () => Id.ToString("N").ToLowerInvariant(),
                row: () => ReverseChronlogicalRowKey);

            yield return PivotOn(
                name: "StatusByInstance",
                partition: () => InstanceName);

            yield return PivotOn(
                name: "HistoryByInstance",
                partition: () => InstanceName,
                row: () => ReverseChronlogicalRowKey);

            yield return PivotOn(
                name: "StatusByJob",
                partition: () => Job);

            yield return PivotOn(
                name: "HistoryByJob",
                partition: () => Job,
                row: () => ReverseChronlogicalRowKey);
        }
    }
}
