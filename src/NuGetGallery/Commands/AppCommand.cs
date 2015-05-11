// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Web.Mvc;
using Ninject;

namespace NuGetGallery
{
    public abstract class AppCommand
    {
        protected AppCommand(IEntitiesContext entities)
        {
            Entities = entities;
        }

        protected IEntitiesContext Entities { get; set; }

        protected virtual T GetService<T>()
        {
            return Container.Kernel.TryGet<T>();
        }

        protected virtual IEnumerable<T> GetServices<T>()
        {
            return Container.Kernel.GetAll<T>();
        }
    }
}