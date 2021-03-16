// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NuGet.Services.AzureSearch
{
    // TODO: Delete this copy of the PackageIdValidator.
    // Tracked by: https://github.com/NuGet/Engineering/issues/3669
    // Forked from: https://github.com/NuGet/NuGet.Client/blob/18863da5be3dc8c7315f4416df1bc9ef96cb7446/src/NuGet.Core/NuGet.Packaging/PackageCreation/Utility/PackageIdValidator.cs
    public static class PackageIdValidator
    {
        public const int MaxPackageIdLength = 100;
        private static readonly Regex IdRegex = new Regex(pattern: @"^\w+([.-]\w+)*$",
            options: RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
            matchTimeout: TimeSpan.FromSeconds(15));

        public static bool IsValidPackageIdWithTimeout(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }
            return IdRegex.IsMatch(packageId);
        }
    }
}
