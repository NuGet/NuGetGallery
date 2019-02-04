// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Features
{
    /// <summary>
    /// A snapshot of the flags' content and ETag.
    /// </summary>
    public class FeatureFlagReference
    {
        public FeatureFlagReference(string flags, string contentId)
        {
            if (string.IsNullOrEmpty(flags))
            {
                throw new ArgumentException(nameof(flags));
            }

            if (string.IsNullOrEmpty(contentId))
            {
                throw new ArgumentException(nameof(contentId));
            }

            Flags = flags;
            ContentId = contentId;
        }

        /// <summary>
        /// The feature flag's content, serialized as JSON.
        /// </summary>
        public string Flags { get; }

        /// <summary>
        /// The feature flag's ETag.
        /// </summary>
        public string ContentId { get; }
    }
}
