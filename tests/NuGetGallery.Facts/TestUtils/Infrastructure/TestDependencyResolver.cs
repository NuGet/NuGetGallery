// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Dependencies;

namespace NuGetGallery.TestUtils.Infrastructure
{
    public class TestDependencyResolver : IDependencyResolver, IDependencyScope, IDisposable
    {
        private readonly Dictionary<Type, object> _registeredServices = new Dictionary<Type, object>();

        public TestDependencyResolver()
        {
        }

        public TestDependencyResolver(params object[] services)
        {
            foreach (var service in services)
            {
                RegisterService(service.GetType(), service);
            }
        }

        public void RegisterService<T>(T instance)
        {
            RegisterService(typeof(T), instance);
        }

        public void RegisterService(Type type, object instance)
        {
            _registeredServices[type] = instance;
        }

        public IDependencyScope BeginScope()
        {
            return (IDependencyScope)this;
        }

        public object GetService(Type serviceType)
        {
            if (_registeredServices.TryGetValue(serviceType, out var service))
            {
                return service;
            }
            return (object)null;
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            if (_registeredServices.TryGetValue(serviceType, out var service))
            {
                return new[] {service};
            }
            return Enumerable.Empty<object>();
        }

        public void Dispose()
        {
        }
    }
}