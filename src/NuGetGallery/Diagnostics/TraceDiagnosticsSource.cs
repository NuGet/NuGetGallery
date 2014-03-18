using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

namespace NuGetGallery.Diagnostics
{
    public class TraceDiagnosticsSource : IDiagnosticsSource, IDisposable
    {
        private const string ObjectName = "TraceDiagnosticsSource";
        private TraceSource _source;

        public string Name { get; private set; }

        public TraceDiagnosticsSource(string name)
        {
            Name = name;
            _source = new TraceSource(name, SourceLevels.All);
            
            // Make the source's listeners list look like the global list.
            _source.Listeners.Clear();
            _source.Listeners.AddRange(Trace.Listeners);
        }

        public virtual void TraceEvent(TraceEventType type, int id, string message, [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (_source == null)
            {
                throw new ObjectDisposedException(ObjectName);
            }
            if (String.IsNullOrEmpty(message))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, "message"), "message");
            }
            
            _source.TraceEvent(type, id, FormatMessage(message, member, file, line));
        }

        public void PerfEvent(string name, TimeSpan time, IEnumerable<KeyValuePair<string,object>> payload)
        {
            // For now, hard-code the number of samples we track to 1000.
            PerfCounters.AddSample(name, sampleSize: 1000, value: time.TotalMilliseconds);

            // Send the event to the queue
            MessageQueue.Enqueue(Name, new PerfEvent(name, DateTime.UtcNow, time, payload));
        }

        // The "protected virtual Dispose(bool)" pattern is for objects with unmanaged resources. We have none, so a virtual Dispose is enough.
        public virtual void Dispose()
        {
            // If we don't do this, it's not the end of the world, but if consumers do dispose us, flush and close the source
            if (_source != null)
            {
                _source.Flush();
                _source.Close();
                _source = null;
            }

            // We don't have a finalizer, but subclasses might. Those subclasses should have their Finalizer call Dispose, so suppress finalization
            GC.SuppressFinalize(this);
        }

        private static string FormatMessage(string message, string member, string file, int line)
        {
            return String.Format(CultureInfo.CurrentCulture, "[{0}:{1} in {2}] {3}", file, line, member, message);
        }
    }
}