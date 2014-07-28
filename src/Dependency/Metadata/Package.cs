
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Resolver.Metadata
{
    public class Package
    {
        public string Id { get; private set; }
        public SemanticVersion Version { get; private set; }
        public IList<Group> DependencyGroups { get; private set; }
        public JObject PackageJson { get; private set; }

        public Package(string id, SemanticVersion version, JObject packageJson)
        {
            Id = id;
            Version = version;
            PackageJson = packageJson;
            DependencyGroups = new List<Group>();
        }

        public Package(string id, string version, JObject packageJson)
            : this(id, SemanticVersion.Parse(version),packageJson)
        {
        }
    }
}
