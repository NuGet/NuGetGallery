using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class JobAttribute : Attribute
    {
        public string Name { get; private set; }

        public JobAttribute(string name)
        {
            Name = name;
        }
    }
}
