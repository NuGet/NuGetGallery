using Newtonsoft.Json.Linq;
using Resolver.Metadata;
using System.Collections.Generic;
using System.IO;

namespace Resolver.Resolver
{
    public class PVNode
    {
        public SemanticVersion Version { get; private set; }

        public IList<string> Properties { get; private set; }
        public Package Package { get; private set; }

        private List<PNode> _children;

        public IReadOnlyList<PNode> Children
        {
            get
            {
                return _children;
            }
        }

        public void AddChild(PNode child)
        {
            lock (_children)
            {
                _children.Add(child);
            }
        }

        public PVNode(SemanticVersion version, Package package)
        {
            Version = version;
            Package = package;
            _children = new List<PNode>();
        }

        public void WriteTo(TextWriter writer, int indent = 0)
        {
            writer.WriteLine("{0}{1}", Utils.Indent(indent), Version);
            foreach (PNode pnode in Children)
            {
                pnode.WriteTo(writer, indent + 1);
            }
        }
    }
}
