
using System.Collections.Generic;
using System.IO;

namespace Resolver.Metadata
{
    public class Dependency
    {
        public string Id { get; private set; }

        public SemanticVersionRange Range { get; private set; }

        public IDictionary<string, string> Properties { get; private set; }

        public Dependency(string id, SemanticVersionRange range)
        {
            Id = id;
            Range = range;

            Properties = new Dictionary<string, string>();
        }

        public Dependency(string id, string range)
            : this(id, SemanticVersionRange.Parse(range))
        {
        }

        public void WriteTo(TextWriter writer)
        {
            writer.Write("{0} {1}", Id, Range);
        }
    }
}
