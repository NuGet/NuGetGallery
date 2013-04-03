using System;
using System.Linq;
using Ninject;

namespace NuGetGallery
{
    public static class Container
    {
        private static readonly Lazy<IKernel> LazyKernel = new Lazy<IKernel>(() => new StandardKernel(ContainerBindings.GetModules().ToArray()));

        public static IKernel Kernel
        {
            get { return LazyKernel.Value; }
        }
    }
}