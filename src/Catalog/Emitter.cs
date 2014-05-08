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
    public abstract class Emitter
    {
        public abstract Task<bool> Emit(JObject obj);
        public abstract Task Close();
    }
}
