using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    public static class ServiceProviderExtensions
    {
        public static T GetService<T>(this IServiceProvider self)
        {
            return (T)self.GetService(typeof(T));
        }

        public static TBase GetService<TBase>(this IServiceProvider self, Type derived)
        {
            return (TBase)self.GetService(derived);
        }
    }
}
