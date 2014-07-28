using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client
{
    public static class ResolverClientExtensions
    {
        public static ResolverClient GetResolverClient(this ServiceClient serviceClient)
        {
            serviceClient.Initialized.Wait();
            return new ResolverClient(serviceClient);
        }
    }
}
