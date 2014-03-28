using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GatherMergeRewrite
{
    public class PackageData
    {
        public string OwnerId { get; set; }
        public string RegistrationId { get; set; }
        public DateTime Published { get; set; }
        public XDocument Nuspec { get; set; }
    }
}
