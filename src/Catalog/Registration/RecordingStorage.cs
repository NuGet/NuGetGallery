// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RecordingStorage : IStorage
    {
        IStorage _innerStorage;

        public RecordingStorage(IStorage storage)
        {
            _innerStorage = storage;

            Loaded = new HashSet<Uri>();
            Saved = new HashSet<Uri>();
        }

        public HashSet<Uri> Loaded { get; private set; }
        public HashSet<Uri> Saved { get; private set; }

        public Task<OptimisticConcurrencyControlToken> GetOptimisticConcurrencyControlTokenAsync(
            Uri resourceUri,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task CopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            Task result = _innerStorage.SaveAsync(resourceUri, content, cancellationToken);
            Saved.Add(resourceUri);
            return result;
        }

        public Task<StorageContent> LoadAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            Task<StorageContent> result = _innerStorage.LoadAsync(resourceUri, cancellationToken);
            Loaded.Add(resourceUri);
            return result;
        }

        public Task DeleteAsync(Uri resourceUri, CancellationToken cancellationToken, DeleteRequestOptions deleteRequestOptions = null)
        {
            return _innerStorage.DeleteAsync(resourceUri, cancellationToken, deleteRequestOptions);
        }

        public Task<string> LoadStringAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            Task<string> result = _innerStorage.LoadStringAsync(resourceUri, cancellationToken);
            Loaded.Add(resourceUri);
            return result;
        }

        public Uri BaseAddress
        {
            get { return _innerStorage.BaseAddress; }
        }

        public Uri ResolveUri(string relativeUri)
        {
            return _innerStorage.ResolveUri(relativeUri);
        }

        public Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
        {
            return _innerStorage.ListAsync(cancellationToken);
        }
    }
}