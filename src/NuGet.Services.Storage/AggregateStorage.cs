// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Storage
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
            WriteSecondaryStorageContentInterceptor writeSecondaryStorageContentInterceptor,
            ILogger<AggregateStorage> logger)
            : base(baseAddress, logger)
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

        protected override Task OnSave(Uri resourceUri, StorageContent content, bool overwrite, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            tasks.Add(_primaryStorage.Save(resourceUri, content, overwrite, cancellationToken));

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

                tasks.Add(storage.Save(secondaryResourceUri, secondaryContent, overwrite, cancellationToken));
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

        public override Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
            return _primaryStorage.ExistsAsync(fileName, cancellationToken);
        }

        public override IEnumerable<StorageListItem> List(bool getMetadata)
        {
            return _primaryStorage.List(getMetadata);
        }

        public override async Task<IEnumerable<StorageListItem>> ListAsync(bool getMetadata, CancellationToken cancellationToken)
        {
            return await _primaryStorage.ListAsync(getMetadata, cancellationToken);
        }

        public override Task SetMetadataAsync(Uri resourceUri, IDictionary<string, string> metadata)
        {
            throw new NotImplementedException();
        }
    }
}