using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Storage
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class PropertySerializerAttribute : Attribute
    {
        public Type Type { get; private set; }

        public PropertySerializerAttribute(Type type)
        {
            Type = type;
        }
    }
}
