// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class IconCopyResultCache : IIconCopyResultCache, IIconCopyResultCachePersistence
    {
        private const string CacheFilename = "c2i_cache.json";

        private ConcurrentDictionary<Uri, ExternalIconCopyResult> _externalIconCopyResults = null;

        private readonly IStorage _auxStorage;

        public IconCopyResultCache(
            IStorage auxStorage)
        {
            _auxStorage = auxStorage ?? throw new ArgumentNullException(nameof(auxStorage));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (_externalIconCopyResults != null)
            {
                return;
            }

            var cacheUrl = _auxStorage.ResolveUri(CacheFilename);
            var content = await _auxStorage.LoadAsync(cacheUrl, cancellationToken);
            if (content == null)
            {
                _externalIconCopyResults = new ConcurrentDictionary<Uri, ExternalIconCopyResult>();
                return;
            }
            using (var contentStream = content.GetContentStream())
            using (var reader = new StreamReader(contentStream))
            {
                var serializer = new JsonSerializer();
                var dictionary = (Dictionary<Uri, ExternalIconCopyResult>)serializer.Deserialize(reader, typeof(Dictionary<Uri, ExternalIconCopyResult>));
                _externalIconCopyResults = new ConcurrentDictionary<Uri, ExternalIconCopyResult>(dictionary);
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            var cacheUrl = _auxStorage.ResolveUri(CacheFilename);
            var serialized = JsonConvert.SerializeObject(_externalIconCopyResults);
            var content = new StringStorageContent(serialized, contentType: "application/json");
            await _auxStorage.SaveAsync(cacheUrl, content, cancellationToken);
        }

        public ExternalIconCopyResult Get(Uri iconUrl)
        {
            if (_externalIconCopyResults == null)
            {
                throw new InvalidOperationException("Object was not initialized");
            }

            if (_externalIconCopyResults.TryGetValue(iconUrl, out var result))
            {
                return result;
            }

            return null;
        }

        public async Task<Uri> SaveExternalIcon(Uri originalIconUrl, Uri storageUrl, IStorage cacheStorage, CancellationToken cancellationToken)
        {
            if (_externalIconCopyResults == null)
            {
                throw new InvalidOperationException("Object was not initialized");
            }

            if (_externalIconCopyResults.TryGetValue(originalIconUrl, out var copyResult))
            {
                if (copyResult.IsCopySucceeded)
                {
                    return copyResult.StorageUrl;
                }

                // if we have failure stored, we'll try to replace it with success,
                // now that we've seen one.
            }

            var cacheStoragePath = GetCachePath(originalIconUrl);
            var cacheUrl = cacheStorage.ResolveUri(cacheStoragePath);

            await cacheStorage.CopyAsync(storageUrl, cacheStorage, cacheUrl, null, cancellationToken);
            // Technically, we could get away without storing the success in the dictionary,
            // but then each get attempt from the cache would result in HTTP request to cache
            // storage that drastically reduces usefulness of the cache (we trade one HTTP request
            // for another).
            Set(originalIconUrl, ExternalIconCopyResult.Success(originalIconUrl, cacheUrl));
            return cacheUrl;
        }

        public void SaveExternalCopyFailure(Uri iconUrl)
        {
            if (_externalIconCopyResults == null)
            {
                throw new InvalidOperationException("Object was not initialized");
            }

            Set(iconUrl, ExternalIconCopyResult.Fail(iconUrl));
        }

        private void Set(Uri iconUrl, ExternalIconCopyResult newItem)
        {
            _externalIconCopyResults.AddOrUpdate(iconUrl, newItem, (_, v) => v.IsCopySucceeded ? v : newItem); // will only overwrite failure results
        }

        public void Clear(Uri externalIconUrl)
        {
            if (_externalIconCopyResults == null)
            {
                throw new InvalidOperationException("Object was not initialized");
            }

            // Cache results are immutable, so we'll remove looking only at the key
            _externalIconCopyResults.TryRemove(externalIconUrl, out var _);
        }

        private string GetCachePath(Uri iconUrl)
        {
            var hash = (byte[])null;
            using (var sha512 = new SHA512Managed())
            {
                var absoluteUriBytes = Encoding.UTF8.GetBytes(iconUrl.AbsoluteUri);
                hash = sha512.ComputeHash(absoluteUriBytes);
            }
            return "icon-cache/" + BitConverter.ToString(hash).Replace("-", "");
        }
    }
}
