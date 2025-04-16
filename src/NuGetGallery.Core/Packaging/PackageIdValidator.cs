// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using NuGet.Services.Entities;

namespace NuGetGallery.Packaging
{
    public static class PackageIdValidator
    {
        private static readonly Regex IdRegex = RegexEx.CreateWithTimeout(
            @"^\w+([.-]\w+)*$",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        private static readonly Regex AllAsciiRegex = RegexEx.CreateWithTimeout(
            @"^[A-Za-z0-9\-_\.]+$",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public static bool IsValidPackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (String.Equals(packageId, "$id$", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IdRegex.IsMatch(packageId);
        }

        public static bool IsAsciiOnlyPackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (String.Equals(packageId, "$id$", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return AllAsciiRegex.IsMatch(packageId);
        }

        public static void ValidatePackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (packageId.Length > Constants.MaxPackageIdLength)
            {
                throw new ArgumentException($"Id must not exceed {Constants.MaxPackageIdLength} characters.");
            }

            if (!IsValidPackageId(packageId))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    "The package ID '{0}' contains invalid characters. Package ID can only contain alphanumeric characters, hyphens, underscores, and periods.",
                    packageId));
            }
        }
    }
}
