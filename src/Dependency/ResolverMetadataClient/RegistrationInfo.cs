using System.Collections.Generic;
using System.Xml;

namespace Resolver
{
    public class RegistrationInfo
    {
        public string Id { get; set; }
        public bool IncludePrerelease { get; set; }
        public IList<PackageInfo> Packages { get; private set; }

        public RegistrationInfo()
        {
            Packages = new List<PackageInfo>();
        }

        public void Write(XmlWriter writer)
        {
            writer.WriteStartElement("RegistrationInfo");
            writer.WriteAttributeString("Id", Id);
            writer.WriteAttributeString("IncludePrerelease", IncludePrerelease.ToString());

            writer.WriteStartElement("Packages");
            foreach (PackageInfo packageInfo in Packages)
            {
                packageInfo.Write(writer);
            }
            writer.WriteEndElement();
            
            writer.WriteEndElement();
        }
    }
}
