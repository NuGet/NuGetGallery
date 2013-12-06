using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Composition
{
    public interface IComponentContainer : IServiceProvider
    {
        void InjectProperties(object instance);
    }

    public static class ComponentContainerExtensions
    {
        public static T GetService<T>(this IComponentContainer self)
        {
            return (T)self.GetService(typeof(T));
        }
    }
}
