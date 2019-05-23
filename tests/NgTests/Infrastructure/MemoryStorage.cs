// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Infrastructure
{
    public class MemoryStorage : Storage
    {
        public ConcurrentDictionary<Uri, StorageContent> Content { get; }

        public ConcurrentDictionary<Uri, byte[]> ContentBytes { get; }

        public ConcurrentDictionary<Uri, StorageListItem> ListMock { get; }

        public MemoryStorage()
            : this(new Uri("http://tempuri.org"))
        {
        }

        public MemoryStorage(Uri baseAddress)
          : base(baseAddress)
        {
            Content = new ConcurrentDictionary<Uri, StorageContent>();
            ContentBytes = new ConcurrentDictionary<Uri, byte[]>();
            ListMock = new ConcurrentDictionary<Uri, StorageListItem>();
        }

        protected MemoryStorage(
            Uri baseAddress,
            ConcurrentDictionary<Uri, StorageContent> content,
            ConcurrentDictionary<Uri, byte[]> contentBytes)
          : base(baseAddress)
        {
            Content = content;
            ContentBytes = contentBytes;
            ListMock = new ConcurrentDictionary<Uri, StorageListItem>();

            foreach (var resourceUri in Content.Keys)
            {
                ListMock[resourceUri] = CreateStorageListItem(resourceUri);
            }
        }

        public override Task<OptimisticConcurrencyControlToken> GetOptimisticConcurrencyControlTokenAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            return Task.FromResult(OptimisticConcurrencyControlToken.Null);
        }

        private static StorageListItem CreateStorageListItem(Uri uri)
        {
            return new StorageListItem(uri, DateTime.UtcNow);
        }

        public virtual Storage WithName(string name)
        {
            return new MemoryStorage(new Uri(BaseAddress + name), Content, ContentBytes);
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

        protected override async Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            Content[resourceUri] = content;

            using (var memoryStream = new MemoryStream())
            {
                var contentStream = content.GetContentStream();
                await contentStream.CopyToAsync(memoryStream);

                if (contentStream.CanSeek)
                {
                    contentStream.Position = 0;
                }

                ContentBytes[resourceUri] = memoryStream.ToArray();
            }

            ListMock[resourceUri] = CreateStorageListItem(resourceUri);
        }

        protected override Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            StorageContent content;

            Content.TryGetValue(resourceUri, out content);

            return Task.FromResult(content);
        }

        protected override Task OnDeleteAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            Content.TryRemove(resourceUri, out var content);
            ContentBytes.TryRemove(resourceUri, out var contentBytes);
            ListMock.TryRemove(resourceUri, out var item);

            return Task.FromResult(true);
        }

        public override bool Exists(string fileName)
        {
            return Content.Keys.Any(k => k.PathAndQuery.EndsWith(fileName));
        }

        public override Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Content.Keys.AsEnumerable().Select(x =>
                ListMock.ContainsKey(x) ? ListMock[x] : new StorageListItem(x, DateTime.UtcNow)));
        }
    }
}