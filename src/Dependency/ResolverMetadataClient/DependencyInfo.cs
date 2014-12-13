using NuGet.Versioning;
using System;
using System.Xml;

namespace Resolver
{
    public class DependencyInfo
    {
        public string Id { get; set; }
        public VersionRange Range { get; set; }
        public Uri RegistrationUri { get; set; }
        public RegistrationInfo RegistrationInfo { get; set; }

        public void Write(XmlWriter writer)
        {
            writer.WriteStartElement("DependencyInfo");

            writer.WriteAttributeString("Id", Id);
            writer.WriteAttributeString("Range", (Range != null) ? Range.ToString() : string.Empty);
            writer.WriteAttributeString("RegistrationUri", RegistrationUri.AbsoluteUri);

            RegistrationInfo.Write(writer);

            writer.WriteEndElement();
        }
    }
}
