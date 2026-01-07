// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;

namespace NuGetGallery.Infrastructure
{
    public class DependencyResolverServiceProviderAdapter
        : IServiceProvider
    {
        private readonly IDependencyResolver _dependencyResolver;

        public DependencyResolverServiceProviderAdapter(IDependencyResolver dependencyResolver)
        {
            _dependencyResolver = dependencyResolver;
        }

        public object GetService(Type serviceType)
        {
            return _dependencyResolver.GetService(serviceType);
        }
    }
}