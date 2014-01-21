using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Models
{
    public class InvocationRequest : IEquatable<InvocationRequest>
    {
        public string Job { get; set; }
        public string Source { get; set; }
        public string JobInstanceName { get; set; }
        public bool UnlessAlreadyRunning { get; set; }
        public Dictionary<string, string> Payload { get; set; }
        public TimeSpan? VisibilityDelay { get; set; }

        [Obsolete("For serialization only")]
        public InvocationRequest() { }
        public InvocationRequest(string job, string source) : this(job, source, new Dictionary<string, string>(), TimeSpan.Zero) { }
        public InvocationRequest(string job, string source, Dictionary<string, string> payload) : this(job, source, payload, TimeSpan.Zero) { }
        public InvocationRequest(string job, string source, TimeSpan visibilityDelay) : this(job, source, new Dictionary<string, string>(), visibilityDelay) { }
        public InvocationRequest(string job, string source, Dictionary<string, string> payload, TimeSpan visibilityDelay)
        {
            Job = job;
            Source = source;
            Payload = payload;
            VisibilityDelay = visibilityDelay;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as InvocationRequest);
        }

        public bool Equals(InvocationRequest other)
        {
            return other != null &&
                String.Equals(other.Job, Job, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(other.Source, Source, StringComparison.OrdinalIgnoreCase) &&
                Equals(other.VisibilityDelay, VisibilityDelay) &&
                other.Payload.DictionariesEqual(Payload, StringComparer.Ordinal);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Job)
                .Add(Source)
                .Add(Payload)
                .Add(VisibilityDelay)
                .CombinedHash;
        }
    }
}
