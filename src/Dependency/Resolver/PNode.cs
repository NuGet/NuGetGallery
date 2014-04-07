using System.Collections.Generic;
using System.IO;

namespace Resolver.Resolver
{
    public class PNode
    {
        public string Id { get; private set; }

        public List<PVNode> Children { get; private set; }

        public PNode(string id)
        {
            Id = id;
            Children = new List<PVNode>();
        }

        public void WriteTo(TextWriter writer, int indent = 0)
        {
            writer.WriteLine("{0}{1}", Utils.Indent(indent), Id);
            foreach (PVNode pvnode in Children)
            {
                pvnode.WriteTo(writer, indent + 1);
            }
        }
    }
}
