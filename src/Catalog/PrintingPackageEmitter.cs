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

        protected override void EmitPackage(JObject package)
        {
            base.EmitPackage(package);
            _packages.Add(package);
        }

        public override void Close()
        {
            Dump(_packages);
            base.Close();
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
