using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services
{
    public interface IServiceRegistrar
    {
        void RegisterService(Type nugetServiceType);
        void RegisterInstance(NuGetService instance);
    }

    public static class ComponentRegistrarExtensions
    {
        public static void RegisterService<T>(this IServiceRegistrar self)
            where T : NuGetService
        {
            self.RegisterService(typeof(T));
        }
    }
}
