// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public static class PackageUtility
    {
        public static string GetPackageFileName(string packageId, string packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(packageId));
            }

            if (string.IsNullOrEmpty(packageVersion))
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNullOrEmpty, nameof(packageVersion));
            }

            return $"{packageId}.{packageVersion}.nupkg";
        }
    }
}