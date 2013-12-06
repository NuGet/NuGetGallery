using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;

namespace NuGet.Services.Composition
{
    internal class AutofacComponentContainer : IComponentContainer
    {
        public IContainer Container { get; private set; }

        public AutofacComponentContainer(IContainer container)
        {
            Container = container;
        }

        public object GetService(Type serviceType)
        {
            return Container.Resolve(serviceType);
        }

        public void InjectProperties(object instance)
        {
            Container.InjectProperties(instance);
        }
    }
}
