using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.IO;

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

        public void Event(TraceEventType type, int id, string message, [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (_source == null)
            {
                throw new ObjectDisposedException(ObjectName);
            }
            if (String.IsNullOrEmpty(message))
            {
                throw new ArgumentException(String.Format(Strings.ParameterCannotBeNullOrEmpty, "message"), "message");
            }
            
            _source.TraceEvent(type, id, FormatMessage(message, member, file, line));
        }

        public void Dispose()
        {
            // If we don't do this, it's not the end of the world, but if consumers do dispose us, flush and close the source
            _source.Flush();
            _source.Close();
            _source = null;
        }

        private string FormatMessage(string message, string member, string file, int line)
        {
            return String.Format("[{0}:{1} in {2}] {3}", file, line, member, message);
        }
    }
}