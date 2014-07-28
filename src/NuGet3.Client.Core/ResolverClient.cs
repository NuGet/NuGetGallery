using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Resolver.Metadata;

namespace NuGet3.Client
{
    public class ResolverClient
    {
        ServiceClient _serviceClient;

        public IGallery Gallery
        {
            get
            {
                return new RemoteGallery(_serviceClient.ResolverBaseUrl);
            }
        }
        public ResolverClient(ServiceClient serviceClient)
        {
            _serviceClient = serviceClient;
        }
    }
}
