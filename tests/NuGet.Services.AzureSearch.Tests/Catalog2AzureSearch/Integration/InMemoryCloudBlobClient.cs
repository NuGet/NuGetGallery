// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch.Integration
{
    public class InMemoryCloudBlobClient : ICloudBlobClient
    {
        private readonly object _lock = new object();

        public Dictionary<string, InMemoryCloudBlobContainer> Containers { get; } = new Dictionary<string, InMemoryCloudBlobContainer>();

        public ISimpleCloudBlob GetBlobFromUri(Uri uri)
        {
            throw new NotImplementedException();
        }

        public ICloudBlobContainer GetContainerReference(string containerAddress)
        {
            lock (_lock)
            {
                InMemoryCloudBlobContainer container;
                if (!Containers.TryGetValue(containerAddress, out container))
                {
                    container = new InMemoryCloudBlobContainer();
                    Containers[containerAddress] = container;
                }

                return container;
            }
        }
    }
}
