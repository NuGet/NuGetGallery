// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Glimpse.Core.Framework;

namespace NuGetGallery.Diagnostics
{
    public class GlimpseServiceLocator : IServiceLocator
    {
        public ICollection<T> GetAllInstances<T>() where T : class
        {
            var instances = DependencyResolver.Current.GetServices<T>().ToList();

            // Glimpse interprets an empty collection to mean: I'm overriding your defaults and telling you NOT to load anythig
            // However, we want an empty collection to indicate to Glimpse that it should use the default. Returning null does that.
            if (!instances.Any())
            {
                return null;
            }
            return instances;
        }

        public T GetInstance<T>() where T : class
        {
            return DependencyResolver.Current.GetService<T>();
        }
    }
}