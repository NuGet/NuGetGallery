// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Framework;
using Glimpse.Core.Policy;
using NuGetGallery.Configuration;

namespace NuGetGallery.Diagnostics
{
    public class GlimpseServiceLocator : IServiceLocator
    {
        static Dictionary<Type, object[]> _localObjects = new Dictionary<Type, object[]>();

        static GlimpseServiceLocator()
        {
            var configuration = new ConfigurationService();
            _localObjects.Add(typeof(IRuntimePolicy), new object[]
            {
                new GlimpseRuntimePolicy(configuration.Current),
                new GlimpseResourcePolicy(),
                new UriPolicy(new List<Regex> {
                        new Regex(@"^.*/Content/.*$"),
                        new Regex(@"^.*/Scripts/.*$"),
                        new Regex(@"^.*(Web|Script)Resource\.axd.*$")
                    })
            });
            _localObjects.Add(typeof(IPersistenceStore), new object[] { new ConcurrentInMemoryPersistenceStore() });
        }
        
        public ICollection<T> GetAllInstances<T>() where T : class
        {
            var instances = DependencyResolver.Current.GetServices<T>().ToList();

            object[] localObjects = null;
            if (_localObjects.TryGetValue(typeof (T), out localObjects))
            {
                foreach (var localObject in localObjects)
                {
                    instances.Add(localObject as T);
                }
            }

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
            object[] localObjects = null;
            if (_localObjects.TryGetValue(typeof (T), out localObjects))
            {
                return localObjects[0] as T;
            }

            return DependencyResolver.Current.GetService<T>();
        }
    }
}