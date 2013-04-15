using System;
using System.Collections.Generic;
using System.Linq;
using Ninject;
using Ninject.Modules;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public static class Container
    {
        private static readonly Lazy<IKernel> LazyKernel = new Lazy<IKernel>(() => new StandardKernel(GetModules().ToArray()));

        public static IKernel Kernel
        {
            get { return LazyKernel.Value; }
        }

        private static IEnumerable<NinjectModule> GetModules()
        {
            yield return new ContainerBindings();
            yield return new DiagnosticsNinjectModule();
        }
    }
}