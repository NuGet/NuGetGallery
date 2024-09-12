// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public sealed class NormalizedPackageIdInfo
    {
        public string OriginalId { get; }
        public string NormalizedId { get; }

        public NormalizedPackageIdInfo(string originalId, string normalizedId)
        {
            OriginalId = originalId ?? throw new ArgumentNullException(nameof(originalId));
            NormalizedId = normalizedId ?? throw new ArgumentNullException(nameof(normalizedId));
        }

    }
}
