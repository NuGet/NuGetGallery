// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    /// <summary>
    /// A set of tools for some simple perf counting
    /// </summary>
    public class PerfEventTracker
    {
        private ConcurrentDictionary<string, ConcurrentBag<PerfEvent>> _data = new ConcurrentDictionary<string, ConcurrentBag<PerfEvent>>();
        public static readonly PerfEventTracker Null = new NullPerfEventTracker();

        public virtual void AddEvent(string name, string payload, TimeSpan duration)
        {
            var bag = _data.GetOrAdd(name, new ConcurrentBag<PerfEvent>());
            bag.Add(new PerfEvent(payload, duration));
        }

        public virtual IEnumerable<string> GetEvents()
        {
            return _data.Keys.ToList();
        }

        public virtual IEnumerable<PerfEvent> GetEventData(string name)
        {
            ConcurrentBag<PerfEvent> bag;
            if (!_data.TryGetValue(name, out bag))
            {
                return Enumerable.Empty<PerfEvent>();
            }
            return bag.ToArray();
        }

        public virtual PerfEventSummary GetSummary(string name)
        {
            var values = GetEventData(name);
            return new PerfEventSummary(
                values.Max(),
                values.Min(),
                TimeSpan.FromMilliseconds(values.Average(p => p.Duration.TotalMilliseconds)));
        }

        public virtual IDisposable TrackEvent(string name, string payloadFormat, params object[] args)
        {
            return TrackEvent(name, String.Format(payloadFormat, args));
        }
        public virtual IDisposable TrackEvent(string name, string payload)
        {
            DateTime start = DateTime.UtcNow;
            return new DisposableAction(() =>
            {
                TimeSpan duration = DateTime.UtcNow - start;
                AddEvent(name, payload, duration);
            });
        }

        private class NullPerfEventTracker : PerfEventTracker
        {
            public override void AddEvent(string name, string payload, TimeSpan duration)
            {
                // No-op
            }

            public override IEnumerable<PerfEvent> GetEventData(string name)
            {
                return Enumerable.Empty<PerfEvent>();
            }
        }

        public void Clear()
        {
            _data.Clear();
        }
    }

    public struct PerfEvent : IComparable, IComparable<PerfEvent>
    {
        public string Payload { get; private set; }
        public TimeSpan Duration { get; private set; }

        public PerfEvent(string payload, TimeSpan duration)
            : this()
        {
            Payload = payload;
            Duration = duration;
        }

        public int CompareTo(object obj)
        {
            return CompareTo((PerfEvent)obj);
        }

        public int CompareTo(PerfEvent other)
        {
            return Duration.CompareTo(other.Duration);
        }
    }

    public class PerfEventSummary
    {
        public PerfEvent Max { get; private set; }
        public PerfEvent Min { get; private set; }
        public TimeSpan Average { get; private set; }

        public PerfEventSummary(PerfEvent max, PerfEvent min, TimeSpan average)
        {
            Max = max;
            Min = min;
            Average = average;
        }
    }
}
