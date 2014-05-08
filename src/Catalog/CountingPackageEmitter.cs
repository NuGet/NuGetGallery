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

        protected override async Task EmitPackage(JObject package)
        {
            await Task.Factory.StartNew(() =>
            {
                Interlocked.Increment(ref _total);
            });
        }

        public override async Task Close()
        {
            await Task.Factory.StartNew(() =>
            {
                Console.WriteLine("total {0} documents emitted", _total);
            });
        }
    }
}
