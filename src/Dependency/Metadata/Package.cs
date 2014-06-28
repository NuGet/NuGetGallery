
using System.Collections.Generic;

namespace Resolver.Metadata
{
    public class Package
    {
        public string Id { get; private set; }
        public SemanticVersion Version { get; private set; }
        public IList<Group> DependencyGroups { get; private set; }

        public Package(string id, SemanticVersion version)
        {
            Id = id;
            Version = version;
            DependencyGroups = new List<Group>();
        }

        public Package(string id, string version)
            : this(id, SemanticVersion.Parse(version))
        {
        }
    }
}
