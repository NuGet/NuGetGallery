using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Catalog
{
    public class CountingPackageEmitter : PackageEmitter
    {
        int _total;

        protected override void EmitPackage(JObject package)
        {
            Interlocked.Increment(ref _total);
        }

        public override void Close()
        {
            Console.WriteLine("total {0} documents emitted", _total);
        }
    }
}
