// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace StatusAggregator.Container
{
    public class ContainerWrapper : IContainerWrapper
    {
        private readonly BlobContainerClient _container;

        public ContainerWrapper(BlobContainerClient container)
        {
            _container = container;
        }

        public Task CreateIfNotExistsAsync()
        {
            return _container.CreateIfNotExistsAsync();
        }

        public Task SaveBlobAsync(string name, string contents)
        {
            var blob = _container.GetBlobClient(name);
            return blob.UploadAsync(contents);
        }
    }
}