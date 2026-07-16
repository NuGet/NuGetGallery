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
            @"^[A-Za-z0-9_]+([.-][A-Za-z0-9_]+)*$",
            RegexOptions.ExplicitCapture);

        private static readonly Regex IdRegexForRead = RegexEx.CreateWithTimeout(
            @"^\w+([.-]\w+)*$",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        // Block new packages with invalid characters
        public static bool IsValidPackageId(string packageId)
        {
            return IsValid(packageId, IdRegex);
        }

        // 1. Support downloading existing packages possibly with invalid characters that do not match IdRegex
        // 2. Support new packages that require to depend on existing packages possibly with invalid characters that do not match IdRegex
        public static bool IsValidPackageIdForRead(string packageId)
        {
            return IsValid(packageId, IdRegexForRead);
        }

        private static bool IsValid(string packageId, Regex regex)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (string.Equals(packageId, "$id$", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!regex.IsMatch(packageId))
            {
                return false;
            }

            return true;
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
                    "The package ID '{0}' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.",
                    packageId));
            }
        }
    }
}
