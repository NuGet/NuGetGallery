using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Metadata
{
    public class Group
    {
        public ICollection<Dependency> Dependencies { get; private set; }
        public IDictionary<string, string> Properties { get; private set; }
        public string TargetFramework { get; private set; }

        public Group(string targetFramework)
        {
            TargetFramework = targetFramework;
            Dependencies = new List<Dependency>();
            Properties = new Dictionary<string, string>();
        }

        public void WriteTo(TextWriter writer)
        {
            foreach (Dependency dependency in Dependencies)
            {
                dependency.WriteTo(writer);
            }
        }
    }
}
