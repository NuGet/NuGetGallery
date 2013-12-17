using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Composition;

namespace System
{
    public static class ComponentContainerExtensions
    {
        public static T GetService<T>(this IServiceProvider self)
        {
            return (T)self.GetService(typeof(T));
        }

        public static TBase GetService<TBase>(this IServiceProvider self, Type derived)
        {
            return (TBase)self.GetService(derived);
        }

        public static IEnumerable<T> GetServices<T>(this IComponentContainer self)
        {
            return self.GetServices(typeof(T)).Cast<T>();
        }

        public static IEnumerable<TBase> GetServices<TBase>(this IComponentContainer self, Type derived)
        {
            return self.GetServices(derived).Cast<TBase>();
        }
    }
}
