using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    [EventSource(Name = "Outercurve-NuGet-Search-Indexing")]
    public class IndexingEventSource : EventSource
    {
        public static readonly IndexingEventSource Log = new IndexingEventSource();
        private IndexingEventSource() { }

        [Event(
            eventId: 1,
            Message = "Reloading {0} from {1}...",
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.ReloadingData)]
        public void ReloadingData(string data, string path) { WriteEvent(1, data, path); }

        [Event(
            eventId: 2,
            Message = "Reloaded {0}",
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.ReloadingData)]
        public void ReloadedData(string data) { WriteEvent(2, data); }

        [Event(
            eventId: 3,
            Message = "{0} data has expired, starting background reload from {1}",
            Level = EventLevel.Informational)]
        public void DataExpiredReloading(string data, string path) { WriteEvent(3, data, path); }

        [Event(
            eventId: 4,
            Message = "Loading Searcher Manager from data in {0}",
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.LoadingSearcherManager)]
        public void LoadingSearcherManager(string path) { WriteEvent(4, path); }

        [Event(
            eventId: 5,
            Message = "Loaded Searcher Manager",
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.LoadingSearcherManager)]
        public void LoadedSearcherManager() { WriteEvent(5); }

        public static class Tasks {
            public const EventTask ReloadingData = (EventTask)1;
            public const EventTask LoadingSearcherManager = (EventTask)2;
        }
    }
}
