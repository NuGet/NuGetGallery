using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;

namespace NuGet.Services.Composition
{
    internal class AutofacServiceProvider : IServiceContainer
    {
        public IContainer Container { get; private set; }

        public AutofacServiceProvider(IContainer container)
        {
            Container = container;
        }

        public object GetService(Type serviceType)
        {
            return Container.Resolve(serviceType);
        }
    }
}
