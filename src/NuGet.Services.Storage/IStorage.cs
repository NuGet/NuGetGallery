// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Storage
{
    public interface IStorage
    {
        bool Exists(string fileName);
        Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken);
        Task Save(Uri resourceUri, StorageContent content, bool overwrite, CancellationToken cancellationToken);
        Task<StorageContent> Load(Uri resourceUri, CancellationToken cancellationToken);
        Task Delete(Uri resourceUri, CancellationToken cancellationToken);
        Task<string> LoadString(Uri resourceUri, CancellationToken cancellationToken);
        Uri BaseAddress { get; }
        Uri ResolveUri(string relativeUri);
        Task<IEnumerable<StorageListItem>> List(bool getMetadata, CancellationToken cancellationToken);
        Task CopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellation);
        Task SetMetadataAsync(Uri resourceUri, IDictionary<string, string> metadata);
    }
}
