using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.ServiceModel
{
    /// <summary>
    /// OPTIONAL attribute that can be applied to a NuGetService subclass to give it a name OTHER than the one
    /// inferred from the type name
    /// </summary>
    /// <remarks>
    /// Normally, a service's name is the Name of the Type without Namespaces. However, if the type name ends in "Service", 
    /// that suffix is removed. For example type service name for "NuGet.Services.FooService" is "Foo". 
    /// Applying this attribute allows the service author to pick a completely different name.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ServiceAttribute : Attribute
    {
        public string Name { get; private set; }

        public ServiceAttribute(string name)
        {
            Name = name;
        }
    }
}
