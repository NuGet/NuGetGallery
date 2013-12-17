using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;

namespace NuGet.Services.Composition
{
    internal class AutofacComponentContainer : IComponentContainer, IDisposable
    {
        public ILifetimeScope Container { get; private set; }

        public AutofacComponentContainer(ILifetimeScope container)
        {
            Container = container;
        }

        public object GetService(Type serviceType)
        {
            return Container.Resolve(serviceType);
        }

        public IEnumerable<object> GetServices(Type type)
        {
            return (IEnumerable<object>)Container.Resolve(typeof(IEnumerable<>).MakeGenericType(type));
        }

        public IComponentContainer BeginScope()
        {
            return new AutofacComponentContainer(Container.BeginLifetimeScope());
        }

        public void InjectServices(object instance)
        {
            Container.InjectProperties(instance);
        }

        public void Dispose()
        {
            Container.Dispose();
        }
    }
}