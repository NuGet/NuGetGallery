using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using NuGet.Services.Storage;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work
{
    /// <summary>
    /// Contains all information about the current status of an invocation, serves as the central
    /// status record of an invocation
    /// </summary>
    public class InvocationState
    {
        public int CurrentVersion { get { return CurrentRow.Version; } }
        public Guid Id { get { return CurrentRow.Id; } }
        public string Job { get { return CurrentRow.Job; } }
        public string Source { get { return CurrentRow.Source; } }

        public Dictionary<string, string> Payload { get; private set; }

        public InvocationStatus Status { get; private set; }
        public ExecutionResult Result { get; private set; }
        public string ResultMessage { get { return CurrentRow.ResultMessage; } }
        public string LastUpdatedBy { get { return CurrentRow.UpdatedBy; } }
        public string LogUrl { get { return CurrentRow.LogUrl; } }
        public string JobInstanceName { get { return CurrentRow.JobInstanceName; } }

        public int DequeueCount { get { return CurrentRow.DequeueCount; } }
        public bool IsContinuation { get { return CurrentRow.IsContinuation; } }

        public DateTimeOffset? LastDequeuedAt { get; private set; }
        public DateTimeOffset? LastSuspendedAt { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public DateTimeOffset QueuedAt { get; private set; }
        public DateTimeOffset NextVisibleAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }

        internal InvocationRow CurrentRow { get; private set; }

        internal InvocationState(InvocationRow latest)
        {
            Update(latest);
        }

        internal void Update(InvocationRow newVersion)
        {
            // Cast here so that failures occur during the update.
            Status = (InvocationStatus)newVersion.Status;
            Result = (ExecutionResult)newVersion.Result;

            // Set up dates
            LastDequeuedAt = LoadUtcDateTime(newVersion.LastDequeuedAt);
            LastSuspendedAt = LoadUtcDateTime(newVersion.LastSuspendedAt);
            CompletedAt = LoadUtcDateTime(newVersion.CompletedAt);
            QueuedAt = new DateTimeOffset(newVersion.QueuedAt, TimeSpan.Zero);
            NextVisibleAt = new DateTimeOffset(newVersion.NextVisibleAt, TimeSpan.Zero);
            UpdatedAt = new DateTimeOffset(newVersion.UpdatedAt, TimeSpan.Zero);

            if (String.IsNullOrEmpty(newVersion.Payload))
            {
                Payload = new Dictionary<string, string>();
            }
            else if (CurrentRow == null || !String.Equals(CurrentRow.Payload, newVersion.Payload, StringComparison.Ordinal))
            {
                Payload = InvocationPayloadSerializer.Deserialize(newVersion.Payload);
            }
            CurrentRow = newVersion;
        }

        private DateTimeOffset? LoadUtcDateTime(DateTime? dateTime)
        {
            return dateTime.HasValue ? new DateTimeOffset(dateTime.Value, TimeSpan.Zero) : (DateTimeOffset?)null;
        }

        /// <summary>
        /// Simple Data Transfer Object for the Invocations Table
        /// </summary>
        internal class InvocationRow
        {
            public int Version { get; set; }
            public Guid Id { get; set; }
            public string Job { get; set; }
            public string Source { get; set; }
            public string Payload { get; set; }
            public int Status { get; set; }
            public int Result { get; set; }
            public string UpdatedBy { get; set; }
            public string ResultMessage { get; set; }
            public string LogUrl { get; set; }
            public string JobInstanceName { get; set; }
            public int DequeueCount { get; set; }

            public bool IsContinuation { get; set; }
            public bool Dequeued { get; set; }
            public bool Complete { get; set; }

            public DateTime? LastDequeuedAt { get; set; }
            public DateTime? LastSuspendedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public DateTime QueuedAt { get; set; }
            public DateTime NextVisibleAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }
}
