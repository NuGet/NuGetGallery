using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class Subscription
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public X509Certificate2 ManagementCertificate { get; set; }
    }
}
