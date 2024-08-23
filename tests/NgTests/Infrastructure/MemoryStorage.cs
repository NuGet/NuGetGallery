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
using NuGetGallery;
using Xunit;

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
            if (content is StringStorageContentWithAccessCondition accessConditionContent)
            {
                // Verify the access condition of this request.
                IAccessCondition accessCondition = accessConditionContent.AccessCondition;
                AssertAccessCondition(resourceUri, accessCondition);
            }

            if (content is StringStorageContent stringStorageContent && !(content is StringStorageContentWithETag))
            {
                // Give this content an ETag
                content = new StringStorageContentWithETag(
                    stringStorageContent.Content,
                    Guid.NewGuid().ToString(),
                    stringStorageContent.ContentType,
                    stringStorageContent.CacheControl);
            }

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
            Content.TryGetValue(resourceUri, out StorageContent content);

            return Task.FromResult(content);
        }

        protected override Task OnDeleteAsync(Uri resourceUri, DeleteRequestOptions deleteRequestOptions, CancellationToken cancellationToken)
        {
            if (deleteRequestOptions is DeleteRequestOptionsWithAccessCondition deleteRequestOptionsWithAccessCondition)
            {
                // Verify the access condition of this request.
                var accessCondition = deleteRequestOptionsWithAccessCondition.AccessCondition;
                AssertAccessCondition(resourceUri, accessCondition);
            }

            Content.TryRemove(resourceUri, out _);
            ContentBytes.TryRemove(resourceUri, out _);
            ListMock.TryRemove(resourceUri, out _);

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

        public override Task<bool> UpdateCacheControlAsync(Uri resourceUri, string cacheControl, CancellationToken cancellationToken)
        {
            if (Content.TryGetValue(resourceUri, out StorageContent content))
            {
                if (content.CacheControl != cacheControl)
                {
                    content.CacheControl = cacheControl;
                    return Task.FromResult(true);
                }
                else
                {
                    return Task.FromResult(false);
                }
            }
            else
            {
                throw new InvalidOperationException("Resource does not exist.");
            }
        }

        private void AssertAccessCondition(Uri resourceUri, IAccessCondition accessCondition)
        {
            Content.TryGetValue(resourceUri, out var existingContent);
            if (IsAccessCondition(AccessConditionWrapper.GenerateEmptyCondition(), accessCondition))
            {
                return;
            }

            if (IsAccessCondition(AccessConditionWrapper.GenerateIfNotExistsCondition(), accessCondition))
            {
                Assert.Null(existingContent);
                return;
            }

            if (IsAccessCondition(AccessConditionWrapper.GenerateIfMatchCondition("*"), accessCondition))
            {
                Assert.NotNull(existingContent);
                return;
            }

            if (existingContent is StringStorageContentWithETag eTagContent)
            {
                var eTag = eTagContent.ETag;
                if (IsAccessCondition(AccessConditionWrapper.GenerateIfMatchCondition(eTag), accessCondition))
                {
                    return;
                }
            }

            throw new InvalidOperationException("Could not validate access condition!");
        }

        private static bool IsAccessCondition(IAccessCondition expected, IAccessCondition actual)
        {
            try
            {
                PackageMonitoringStatusTestUtility.AssertAccessCondition(expected, actual);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}