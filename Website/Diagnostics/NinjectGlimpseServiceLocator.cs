using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Glimpse.Core.Framework;
using Ninject;

namespace NuGetGallery.Diagnostics
{
    public class NinjectGlimpseServiceLocator : IServiceLocator
    {
        private IKernel _kernel;

        public NinjectGlimpseServiceLocator() : this(Container.Kernel) { }

        public NinjectGlimpseServiceLocator(IKernel kernel)
        {
            _kernel = kernel;
        }

        public ICollection<T> GetAllInstances<T>() where T : class
        {
            return _kernel.GetAll<T>().ToList();
        }

        public T GetInstance<T>() where T : class
        {
            return _kernel.TryGet<T>();
        }
    }
}