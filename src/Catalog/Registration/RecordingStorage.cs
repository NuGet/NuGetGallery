// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

        public Task Save(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            Task result = _innerStorage.Save(resourceUri, content, cancellationToken);
            Saved.Add(resourceUri);
            return result;
        }

        public Task<StorageContent> Load(Uri resourceUri, CancellationToken cancellationToken)
        {
            Task<StorageContent> result = _innerStorage.Load(resourceUri, cancellationToken);
            Loaded.Add(resourceUri);
            return result;
        }

        public Task Delete(Uri resourceUri, CancellationToken cancellationToken)
        {
            return _innerStorage.Delete(resourceUri, cancellationToken);
        }

        public Task<string> LoadString(Uri resourceUri, CancellationToken cancellationToken)
        {
            Task<string> result = _innerStorage.LoadString(resourceUri, cancellationToken);
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

        public Task<IEnumerable<StorageListItem>> List(CancellationToken cancellationToken)
        {
            return _innerStorage.List(cancellationToken);
        }
    }
}
