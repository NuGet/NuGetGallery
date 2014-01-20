using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;

namespace NuGet.Services.Composition
{
    /// <summary>
    /// Thin abstraction on Autofac. Doesn't try to hide autofac, just reduce direct use to component registration only.
    /// </summary>
    public interface IComponentContainer : IServiceProvider
    {
        IComponentContainer BeginScope(Action<ContainerBuilder> configuration);
        IEnumerable<object> GetServices(Type type);
        void InjectServices(object instance);
    }
}
