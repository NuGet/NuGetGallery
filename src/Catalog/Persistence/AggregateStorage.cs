// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AggregateStorage : Storage
    {
        public delegate StorageContent WriteSecondaryStorageContentInterceptor(
            Uri primaryStorageBaseUri, 
            Uri primaryResourceUri, 
            Uri secondaryStorageBaseUri,
            Uri secondaryResourceUri, 
            StorageContent content);

        private readonly Storage _primaryStorage;
        private readonly Storage[] _secondaryStorage;
        private readonly WriteSecondaryStorageContentInterceptor _writeSecondaryStorageContentInterceptor;
        
        public AggregateStorage(Uri baseAddress, Storage primaryStorage, Storage[] secondaryStorage,
            WriteSecondaryStorageContentInterceptor writeSecondaryStorageContentInterceptor)
            : base(baseAddress)
        {
            _primaryStorage = primaryStorage;
            _secondaryStorage = secondaryStorage;
            _writeSecondaryStorageContentInterceptor = writeSecondaryStorageContentInterceptor;
        
            BaseAddress = _primaryStorage.BaseAddress;
        }
        
        protected override Task OnSave(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            tasks.Add(_primaryStorage.Save(resourceUri, content, cancellationToken));

            foreach (var storage in _secondaryStorage)
            {
                var secondaryResourceUri = new Uri(resourceUri.ToString()
                    .Replace(_primaryStorage.BaseAddress.ToString(), storage.BaseAddress.ToString()));

                var secondaryContent = content;
                if (_writeSecondaryStorageContentInterceptor != null)
                {
                    secondaryContent = _writeSecondaryStorageContentInterceptor(
                        _primaryStorage.BaseAddress, 
                        resourceUri, 
                        storage.BaseAddress, 
                        secondaryResourceUri, content);
                }

                tasks.Add(storage.Save(secondaryResourceUri, secondaryContent, cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        protected override Task<StorageContent> OnLoad(Uri resourceUri, CancellationToken cancellationToken)
        {
            return _primaryStorage.Load(resourceUri, cancellationToken);
        }

        protected override Task OnDelete(Uri resourceUri, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            tasks.Add(_primaryStorage.Delete(resourceUri, cancellationToken));

            foreach (var storage in _secondaryStorage)
            {
                var secondaryResourceUri = new Uri(resourceUri.ToString()
                    .Replace(_primaryStorage.BaseAddress.ToString(), storage.BaseAddress.ToString()));

                tasks.Add(storage.Delete(secondaryResourceUri, cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        public override bool Exists(string fileName)
        {
            return _primaryStorage.Exists(fileName);
        }

        public override Task<IEnumerable<StorageListItem>> List(CancellationToken cancellationToken)
        {
            return _primaryStorage.List(cancellationToken);
        }
    }
}