using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuCmd
{
    public class NuGetEnvironment
    {
        public string Name { get; set; }
        public string SubscriptionId { get; set; }
        public string SubscriptionName { get; set; }
        public X509Certificate2 SubscriptionCertificate { get; set; }
        public Dictionary<string, Uri> ServiceMap { get; set; }

        public NuGetEnvironment()
        {
            ServiceMap = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        }

        public Uri GetServiceUri(string name)
        {
            Uri uri;
            if (!ServiceMap.TryGetValue(name, out uri))
            {
                return null;
            }
            return uri;
        }
    }
}
