// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace NuGetGallery
{
    public class HijackSearchServiceFactory : IHijackSearchServiceFactory
    {
        /// <summary>
        /// The hasher that maps clients to test buckets. There is a single hasher per thread
        /// to avoid performance issues from creating <see cref="SHA256"/> objects.
        /// </summary>
        [ThreadStatic]
        private static SHA256 Hasher;

        private readonly HttpContextBase _httpContext;
        private readonly IFeatureFlagService _featureFlags;
        private readonly IContentObjectService _contentObjectService;
        private readonly ISearchService _search;
        private readonly ISearchService _previewSearch;

        public HijackSearchServiceFactory(
            HttpContextBase httpContext,
            IFeatureFlagService featureFlags,
            IContentObjectService contentObjectService,
            ISearchService search,
            ISearchService previewSearch)
        {
            _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _previewSearch = previewSearch ?? throw new ArgumentNullException(nameof(previewSearch));
        }

        public ISearchService GetService()
        {
            if (!_featureFlags.IsPreviewHijackEnabled())
            {
                return _search;
            }

            // Initialize this thread's hasher if necessary.
            // See: https://docs.microsoft.com/en-us/dotnet/api/system.threadstaticattribute?view=netframework-4.8#remarks
            if (Hasher == null)
            {
                Hasher = SHA256.Create();
            }

            var testBucket = GetClientBucket();
            var testPercentage = _contentObjectService.ABTestConfiguration.PreviewHijackPercentage;
            var isActive = testBucket <= testPercentage;

            return isActive ? _previewSearch : _search;
        }

        private int GetClientBucket()
        {
            // Use the client's user agent (if present) to generate a value that is constant
            // for this client. This string will be hashed using SHA-256, and the hash's first 8
            // bytes will be used to bucket the client from 1 to 100 (inclusive).
            var userAgent = _httpContext.Request.UserAgent ?? "empty";
            var hashedBytes = Hasher.ComputeHash(Encoding.ASCII.GetBytes(userAgent));
            var value = BitConverter.ToUInt64(hashedBytes, startIndex: 0);

            return (int)(value % 100) + 1;
        }
    }
}