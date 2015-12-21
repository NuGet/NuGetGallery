﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Packaging;

namespace NuGetGallery.Packaging
{
    public class ManifestValidator
    {
        // Copy-pasta from NuGet: src/Core/Utility/PackageIdValidator.cs because that constant is internal :(
        public static readonly int MaxPackageIdLength = 100;
        
        public static IEnumerable<ValidationResult> Validate(Stream nuspecStream, out NuspecReader nuspecReader)
        {
            try
            {
                nuspecReader = new NuspecReader(nuspecStream);
                var rawMetadata = nuspecReader.GetMetadata();
                if (rawMetadata != null && rawMetadata.Any())
                {
                    return ValidateCore(PackageMetadata.FromNuspecReader(nuspecReader));
                }
            }
            catch (Exception ex)
            {
                nuspecReader = null;
                return new [] { new ValidationResult(ex.Message) };
            }

            return Enumerable.Empty<ValidationResult>();
        }

        private static IEnumerable<ValidationResult> ValidateCore(PackageMetadata packageMetadata)
        {
            // Validate the ID
            if (string.IsNullOrEmpty(packageMetadata.Id))
            {
                yield return new ValidationResult(Strings.Manifest_MissingId);
            }
            else
            {
                if (packageMetadata.Id.Length > MaxPackageIdLength)
                {
                    yield return new ValidationResult(Strings.Manifest_IdTooLong);
                }
                else if (!PackageIdValidator.IsValidPackageId(packageMetadata.Id))
                {
                    yield return new ValidationResult(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Manifest_InvalidId,
                        packageMetadata.Id));
                }
            }

            // Check URL properties
            foreach (var result in CheckUrls(
                packageMetadata.GetValueFromMetadata("IconUrl"),
                packageMetadata.GetValueFromMetadata("ProjectUrl"), 
                packageMetadata.GetValueFromMetadata("LicenseUrl")))
            {
                yield return result;
            }

            // Check version
            if (packageMetadata.Version == null)
            {
                var version = packageMetadata.GetValueFromMetadata("version");

                yield return new ValidationResult(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Manifest_InvalidVersion,
                    version));
            }

            // Check dependency groups
            var dependencyGroups = packageMetadata.GetDependencyGroups();
            if (dependencyGroups != null)
            {
                foreach (var dependency in dependencyGroups.SelectMany(set => set.Packages))
                {
                    if (!PackageIdValidator.IsValidPackageId(dependency.Id))
                    {
                        yield return new ValidationResult(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Manifest_InvalidDependency,
                            dependency.Id,
                            dependency.VersionRange));
                    }
                }
            }
        }

        private static IEnumerable<ValidationResult> CheckUrls(params string[] urls)
        {
            foreach (var url in urls)
            {
                Uri _;
                if (!String.IsNullOrEmpty(url) && !Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    yield return new ValidationResult(Strings.Manifest_InvalidUrl);
                }
            }
        }
    }
}
