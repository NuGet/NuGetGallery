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
        public FeatureFlagReference(string flagsJson, string contentId)
        {
            if (string.IsNullOrEmpty(flagsJson))
            {
                throw new ArgumentException(nameof(flagsJson));
            }

            if (string.IsNullOrEmpty(contentId))
            {
                throw new ArgumentException(nameof(contentId));
            }

            FlagsJson = flagsJson;
            ContentId = contentId;
        }

        /// <summary>
        /// The feature flag's content, serialized as JSON.
        /// </summary>
        public string FlagsJson { get; }

        /// <summary>
        /// The feature flag's ETag.
        /// </summary>
        public string ContentId { get; }
    }
}
