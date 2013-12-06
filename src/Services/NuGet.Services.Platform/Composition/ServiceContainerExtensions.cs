using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Composition
{
    public static class ServiceContainerExtensions
    {
        public static T GetService<T>(this IServiceContainer self)
        {
            return (T)self.GetService(typeof(T));
        }
    }
}
