using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client
{
    public static class MetadataClientExtensions
    {
        public static MetadataClient GetMetadataClient(this ServiceClient serviceClient)
        {
            serviceClient.Initialized.Wait();
            return new MetadataClient(serviceClient);
        }
    }
}
