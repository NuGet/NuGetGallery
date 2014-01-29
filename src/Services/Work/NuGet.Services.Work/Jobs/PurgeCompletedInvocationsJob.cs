using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Jobs
{
    [Description("Purges completed invocations older than a provided date or age")]
    public class PurgeCompletedInvocationsJob : JobHandlerBase<PurgeCompletedInvocationsEventSource>
    {
        public DateTimeOffset? Before { get; set; }
        public TimeSpan? MinAge { get; set; }

        protected internal override async Task<InvocationResult> Invoke()
        {
            DateTimeOffset before;
            if (MinAge != null)
            {
                before = DateTimeOffset.UtcNow - MinAge.Value;
            }
            else
            {
                before = Before ?? DateTimeOffset.UtcNow;
            }

            // Get purgable invocations
            Log.FindingPurgableInvocations(before);
            var purgable = (await Context.Queue.GetPurgable(before)).ToList();
            Log.FoundPurgableInvocations(purgable.Count, before);

            // Purge 'em
            Log.PurgingInvocations(purgable.Count);
            await Context.Queue.Purge(purgable.Select(i => i.Id));
            Log.PurgedInvocations(purgable.Count);

            return InvocationResult.Completed();
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-PurgeCompletedInvocations")]
    public class PurgeCompletedInvocationsEventSource : EventSource
    {
        public static readonly PurgeCompletedInvocationsEventSource Log = new PurgeCompletedInvocationsEventSource();

        private PurgeCompletedInvocationsEventSource() { }

        [Event(
            eventId: 1,
            Opcode = EventOpcode.Start,
            Task = Tasks.GetPurgable,
            Message = "Finding invocations that completed before: {0}")]
        private void FindingPurgableInvocations(string before) { WriteEvent(1, before); }
        [NonEvent]
        public void FindingPurgableInvocations(DateTimeOffset before) { FindingPurgableInvocations(before.ToString("O")); }

        [Event(
            eventId: 2,
            Opcode = EventOpcode.Stop,
            Task = Tasks.GetPurgable,
            Message = "Found {0} invocations that completed before {1} for purging")]
        private void FoundPurgableInvocations(int jobCount, string before) { WriteEvent(2, jobCount, before); }
        [NonEvent]
        public void FoundPurgableInvocations(int jobCount, DateTimeOffset before) { FoundPurgableInvocations(jobCount, before.ToString("O")); }

        [Event(
            eventId: 3,
            Opcode = EventOpcode.Start,
            Task = Tasks.PurgingInvocations,
            Message = "Purging {0} invocations from the queue.")]
        public void PurgingInvocations(int count) { WriteEvent(3, count); }
        
        [Event(
            eventId: 4,
            Opcode = EventOpcode.Stop,
            Task = Tasks.PurgingInvocations,
            Message = "Purged {0} invocations from the queue.")]
        public void PurgedInvocations(int count) { WriteEvent(4, count); }
        
        public class Tasks
        {
            public const EventTask GetPurgable = (EventTask)0x1;
            public const EventTask PurgingInvocations = (EventTask)0x2;
        }
    }
}
