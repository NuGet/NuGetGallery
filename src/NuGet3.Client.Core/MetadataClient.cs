using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client
{
    public class MetadataClient
    {
        private ServiceClient serviceClient;

        public MetadataClient(ServiceClient serviceClient)
        {
            // TODO: Complete member initialization
            this.serviceClient = serviceClient;
        }

        public PackageMetadata FindPackage(string packageId, string packageVersion)
        {
            return new PackageMetadata();
        }

        public PackageMetadata GetPackageMetadata(string packageUri)
        {
            return new PackageMetadata();
        }

        public string GetPackageUri(string p1, string p2)
        {
            return "";
        }
    }
}
