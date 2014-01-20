using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGet.Services.Models
{
    public class LogEvent
    {
        public Guid ProviderId { get; set; }
        public int EventId { get; set; }
        public int Keywords { get; set; }
        public LogEventLevel Level { get; set; }
        public string Message { get; set; }
        public int Opcode { get; set; }
        public int Task { get; set; }
        public int Version { get; set; }
        public Dictionary<string, string> Payload { get; set; }
        public string EventName { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public static IEnumerable<LogEvent> ParseLogEvents(string events)
        {
            return JsonConvert.DeserializeObject<IEnumerable<LogEvent>>(
                "[" + events.Substring(0, events.Length - 1) + "]");
        }
    }

    public enum LogEventLevel
    {
        Critical = 1,
        Error = 2,
        Informational = 4,
        LogAlways = 0,
        Verbose = 5,
        Warning = 3
    }
}
