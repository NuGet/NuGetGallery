// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class ExternalIconCopyResult
    {
        public static ExternalIconCopyResult Success(Uri sourceUrl, Uri storageUrl)
        {
            if (sourceUrl == null)
            {
                throw new ArgumentNullException(nameof(sourceUrl));
            }

            if (storageUrl == null)
            {
                throw new ArgumentNullException(nameof(storageUrl));
            }

            return new ExternalIconCopyResult
            {
                SourceUrl = sourceUrl,
                StorageUrl = storageUrl,
                Expiration = null,            // successes don't expire
            };
        }

        public static ExternalIconCopyResult Fail(Uri sourceUrl, TimeSpan validityPeriod)
        {
            if (sourceUrl == null)
            {
                throw new ArgumentNullException(nameof(sourceUrl));
            }

            if (validityPeriod < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(validityPeriod), $"{nameof(validityPeriod)} cannot be negative");
            }

            return new ExternalIconCopyResult
            {
                SourceUrl = sourceUrl,
                StorageUrl = null,
                Expiration = DateTimeOffset.UtcNow.Add(validityPeriod),
            };
        }

        public Uri SourceUrl { get; set; }
        public Uri StorageUrl { get; set; }

        /// <summary>
        /// Expiration time for the fail cache item.
        /// </summary>
        public DateTimeOffset? Expiration { get; set; }
        public bool IsCopySucceeded => StorageUrl != null;
    }
}
