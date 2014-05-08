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
    public class PrintingPackageEmitter : CountingPackageEmitter
    {
        ConcurrentBag<JToken> _packages = new ConcurrentBag<JToken>();

        protected override async Task EmitPackage(JObject package)
        {
            await base.EmitPackage(package);
            _packages.Add(package);
        }

        public override async Task Close()
        {
            Dump(_packages);
            await base.Close();
        }

        static void Dump(IEnumerable<JToken> packages)
        {
            foreach (JObject package in packages)
            {
                Console.WriteLine("{0}/{1}", package["id"], package["version"]);
            }
        }
    }
}
