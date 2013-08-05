using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    internal static class NativeMethods
    {        
        [DllImport("dnsapi.dll",EntryPoint="DnsFlushResolverCache")]
        public static extern UInt32 DnsFlushResolverCache();
    }
}
