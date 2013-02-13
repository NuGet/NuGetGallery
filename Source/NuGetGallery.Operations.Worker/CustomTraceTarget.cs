using System.Collections.Generic;
using System.Diagnostics;
using NLog;
using NLog.Targets;

namespace NuGetGallery.Operations.Worker
{
    public class CustomTraceTarget : TargetWithLayout
    {
        private Dictionary<LogLevel, TraceEventType> _mappings = new Dictionary<LogLevel, TraceEventType>
        {
            { LogLevel.Trace, TraceEventType.Verbose },
            { LogLevel.Debug, TraceEventType.Verbose },
            { LogLevel.Info, TraceEventType.Information },
            { LogLevel.Warn, TraceEventType.Warning },
            { LogLevel.Error, TraceEventType.Error },
            { LogLevel.Fatal, TraceEventType.Critical }
        };

        private TraceSource _source;

        public CustomTraceTarget()
        {
            _source = new TraceSource("NLog");
        }

        protected override void Write(LogEventInfo logEvent)
        {
            TraceEventType type;
            if (!_mappings.TryGetValue(logEvent.Level, out type))
            {
                type = TraceEventType.Verbose;
            }
            _source.TraceEvent(type, 1001, Layout.Render(logEvent));
        }
    }
}
