using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;

namespace NuGet.Services
{
    internal class AutofacServiceProvider : IServiceProvider
    {
        public IComponentContext Container { get; private set; }

        public AutofacServiceProvider(IComponentContext container)
        {
            Container = container;
        }

        public object GetService(Type serviceType)
        {
            return Container.Resolve(serviceType);
        }
    }
}