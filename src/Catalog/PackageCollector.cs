using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Catalog
{
    public class PackageCollector : Collector
    {
        Emitter _emitter;

        public PackageCollector(Emitter emitter)
        {
            _emitter = emitter;
        }

        protected override Emitter CreateEmitter()
        {
            return _emitter;
        }
    }
}
