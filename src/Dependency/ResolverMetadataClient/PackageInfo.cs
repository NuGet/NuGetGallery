using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Resolver
{
    public class PackageInfo
    {
        public NuGetVersion Version { get; set; }
        public Uri PackageContent { get; set; }
        public IList<DependencyGroupInfo> DependencyGroups { get; private set; }

        public PackageInfo()
        {
            DependencyGroups = new List<DependencyGroupInfo>();
        }

        public void Write(XmlWriter writer)
        {
            writer.WriteStartElement("PackageInfo");
            writer.WriteAttributeString("Version", (Version != null) ? Version.ToString() : string.Empty);
            writer.WriteAttributeString("PackageContent", PackageContent.AbsoluteUri);

            writer.WriteStartElement("DependencyGroups");
            foreach (DependencyGroupInfo dependencyGroupInfo in DependencyGroups)
            {
                dependencyGroupInfo.Write(writer);
            }
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
    }
}
