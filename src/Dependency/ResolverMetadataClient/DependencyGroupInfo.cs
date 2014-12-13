using System.Collections.Generic;
using System.Xml;

namespace Resolver
{
    public class DependencyGroupInfo
    {
        public string TargetFramework { get; set; }
        public IList<DependencyInfo> Dependencies { get; private set; }

        public DependencyGroupInfo()
        {
            Dependencies = new List<DependencyInfo>();
        }

        public void Write(XmlWriter writer)
        {
            writer.WriteStartElement("DependencyGroupInfo");
            writer.WriteAttributeString("TargetFramework", TargetFramework);

            writer.WriteStartElement("DependencyInfo");
            foreach (DependencyInfo dependencyInfo in Dependencies)
            {
                dependencyInfo.Write(writer);
            }
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
    }
}
