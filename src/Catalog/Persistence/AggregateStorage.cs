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

        protected override Task OnCopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            tasks.Add(_primaryStorage.SaveAsync(resourceUri, content, cancellationToken));

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

                tasks.Add(storage.SaveAsync(secondaryResourceUri, secondaryContent, cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        protected override Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            return _primaryStorage.LoadAsync(resourceUri, cancellationToken);
        }

        protected override Task OnDeleteAsync(Uri resourceUri, DeleteRequestOptions deleteRequestOptions, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            tasks.Add(_primaryStorage.DeleteAsync(resourceUri, cancellationToken, deleteRequestOptions));

            foreach (var storage in _secondaryStorage)
            {
                var secondaryResourceUri = new Uri(resourceUri.ToString()
                    .Replace(_primaryStorage.BaseAddress.ToString(), storage.BaseAddress.ToString()));

                tasks.Add(storage.DeleteAsync(secondaryResourceUri, cancellationToken, deleteRequestOptions));
            }

            return Task.WhenAll(tasks);
        }

        public override bool Exists(string fileName)
        {
            return _primaryStorage.Exists(fileName);
        }

        public override Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
        {
            return _primaryStorage.ListAsync(cancellationToken);
        }

        public override Uri GetUri(string name)
        {
            return this._primaryStorage.GetUri(name);
        }
    }
}