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
        public ICollection<T> GetAllInstances<T>() where T : class
        {
            var ninjectResources = Container.Kernel.GetAll<T>().ToList();
            if (!ninjectResources.Any())
            {
                return null;
            }
            return ninjectResources;
        }

        public T GetInstance<T>() where T : class
        {
            return Container.Kernel.TryGet<T>();
        }
    }
}