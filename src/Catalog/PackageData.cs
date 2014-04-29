using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Catalog
{
    public class PackageData
    {
        public List<string> OwnerIds { get; set; }
        public string RegistrationId { get; set; }
        public DateTime Published { get; set; }
        public XDocument Nuspec { get; set; }
    }
}
