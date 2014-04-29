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
    public abstract class PackageEmitter : Emitter
    {
        public override bool Emit(JObject obj)
        {
            JToken type;
            if (obj.TryGetValue("@type", out type) && type.ToString() == "Package")
            {
                EmitPackage(obj);
                return true;
            }
            return false;
        }

        protected abstract void EmitPackage(JObject package);
    }
}
