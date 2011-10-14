using System;
using Ninject;

namespace NuGetGallery
{
    public static class Container
    {
        private static readonly Lazy<IKernel> kernel = new Lazy<IKernel>(() => new StandardKernel(new ContainerBindings()));

        public static IKernel Kernel
        {
            get
            {
                return kernel.Value;
            }
        }
    }
}
