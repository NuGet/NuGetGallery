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
    public class DistinctCountingPackageEmitter : PackageEmitter
    {
        int _total;
        HashSet<string> _packageIds = new HashSet<string>();

        protected override async Task EmitPackage(JObject package)
        {
            await Task.Factory.StartNew(() =>
            {
                int progress = Interlocked.Increment(ref _total);

                if (progress % 10000 == 0)
                {
                    Console.WriteLine(progress);
                }

                lock (this)
                {
                    _packageIds.Add(package["id"].ToString().ToLowerInvariant());
                }
            });
        }

        public override async Task Close()
        {
            await Task.Factory.StartNew(() =>
            {
                Console.WriteLine("total {0} documents emitted {1} distinct package ids", _total, _packageIds.Count);
            });
        }
    }
}
