using System;
using System.Xml.Linq;

namespace Catalog
{
    public class PackageData
    {
        public string OwnerId { get; set; }
        public string RegistrationId { get; set; }
        public DateTime Published { get; set; }
        public XDocument Nuspec { get; set; }
    }
}
