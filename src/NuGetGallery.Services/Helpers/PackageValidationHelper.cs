// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;

namespace NuGetGallery.Helpers
{
    public class PackageValidationHelper
    {
        public static bool HasDuplicatedEntries(PackageArchiveReader nuGetPackage)
        {
            // Normalize paths and ensures case sensitivity is also considered
            var packageFiles = GetNormalizedEntryPaths(nuGetPackage);

            return packageFiles.Count() != packageFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        public static IEnumerable<string> GetNormalizedEntryPaths(PackageArchiveReader nuGetPackage)
        {
            return nuGetPackage.GetFiles().Select(packageFile => FileNameHelper.GetZipEntryPath(packageFile));
        }
    }
}