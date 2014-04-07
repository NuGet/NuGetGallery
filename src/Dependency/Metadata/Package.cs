
using System.Collections.Generic;

namespace Resolver.Metadata
{
    public class Package
    {
        public string Id { get; private set; }
        public SemanticVersion Version { get; private set; }
        public IDictionary<string, Group> DependencyGroups { get; private set; }

        public Package(string id, SemanticVersion version)
        {
            Id = id;
            Version = version;
            DependencyGroups = new Dictionary<string, Group>();
        }

        public Package(string id, string version)
            : this(id, SemanticVersion.Parse(version))
        {
        }
    }
}
