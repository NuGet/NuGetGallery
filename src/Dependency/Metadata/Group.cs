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

        public Group()
        {
            Dependencies = new List<Dependency>();
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
