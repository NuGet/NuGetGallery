using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Http.Dependencies;
using Autofac;
using NuGet.Services.Composition;

namespace NuGet.Services.Http
{
    internal class ComponentDependencyScope : IDependencyScope
    {
        public IComponentContainer Container { get; set; }

        public ComponentDependencyScope(IComponentContainer container)
        {
            Container = container;
        }

        public object GetService(Type serviceType)
        {
            return Container.GetService(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            return Container.GetServices(serviceType);
        }

        public void Dispose()
        {
            var disp = Container as IDisposable;
            if (disp != null)
            {
                disp.Dispose();
            }
        }
    }

    internal class ComponentDependencyResolver : ComponentDependencyScope, IDependencyResolver
    {
        public ComponentDependencyResolver(IComponentContainer container)
            : base(container)
        {
        }

        public IDependencyScope BeginScope()
        {
            return new ComponentDependencyScope(Container.BeginScope(_ => { }));
        }
    }
}
