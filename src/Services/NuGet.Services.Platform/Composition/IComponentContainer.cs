using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Composition
{
    public interface IComponentContainer : IServiceProvider
    {
        IComponentContainer BeginScope();
        IEnumerable<object> GetServices(Type type);
        void InjectServices(object instance);
    }
}
