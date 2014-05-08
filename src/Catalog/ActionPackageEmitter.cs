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
    public class ActionPackageEmitter : PackageEmitter
    {
        Func<JObject, Task> _emit;
        Func<Task> _close;

        public ActionPackageEmitter(Func<JObject, Task> emit, Func<Task> close)
        {
            _emit = emit;
            _close = close;
        }

        public ActionPackageEmitter(Action<JObject> emit, Action close)
            : this(
                new Func<JObject, Task>((d) => { return Task.Factory.StartNew(() => { emit(d); }); }),
                new Func<Task>(() => { return Task.Factory.StartNew(close); }))
        {
        }

        public ActionPackageEmitter(Action<JObject> emit)
            : this(
                new Func<JObject, Task>((d) => { return Task.Factory.StartNew(() => { emit(d); }); }),
                new Func<Task>(() => { return Task.Factory.StartNew(() => {}); }))
        {
        }

        protected override Task EmitPackage(JObject package)
        {
            return _emit(package);
        }

        public override Task Close()
        {
            return _close();
        }
    }
}
