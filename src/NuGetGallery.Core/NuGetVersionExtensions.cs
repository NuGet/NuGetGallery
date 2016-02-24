// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Versioning;

namespace NuGetGallery
{
    public static class NuGetVersionExtensions
    {
        public static string ToNormalizedStringSafe(this NuGetVersion self)
        {
            return self != null ? self.ToNormalizedString() : string.Empty;
        }

        public static bool IsSemVer200(this NuGetVersion self)
        {
            return self.ReleaseLabels.Count() > 1 || self.HasMetadata;
        }
    }
}