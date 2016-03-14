// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Packaging;
using NuGet.Versioning;

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

            // Check framework reference groups
            var frameworkReferenceGroups = packageMetadata.GetFrameworkReferenceGroups();
            if (frameworkReferenceGroups != null)
            {
                foreach (var frameworkReferenceGroup in frameworkReferenceGroups)
                {
                    var isUnsupportedFramework = frameworkReferenceGroup?.TargetFramework?.IsUnsupported;
                    if (isUnsupportedFramework.HasValue && isUnsupportedFramework.Value)
                    {
                        yield return new ValidationResult(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Manifest_TargetFrameworkNotSupported,
                            frameworkReferenceGroup?.TargetFramework?.ToString()));
                    }
                }
            }

            // Check dependency groups
            var dependencyGroups = packageMetadata.GetDependencyGroups();
            if (dependencyGroups != null)
            {
                foreach (var dependencyGroup in dependencyGroups)
                {
                    // Keep track of duplicates
                    var dependencyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Verify package id's
                    foreach (var dependency in dependencyGroup.Packages)
                    {
                        bool duplicate = !dependencyIds.Add(dependency.Id);
                        if (duplicate)
                        {
                            yield return new ValidationResult(String.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Manifest_DuplicateDependency,
                                dependencyGroup.TargetFramework.GetShortFolderName(),
                                dependency.Id));
                        }

                        if (!PackageIdValidator.IsValidPackageId(dependency.Id))
                        {
                            yield return new ValidationResult(String.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Manifest_InvalidDependency,
                                dependency.Id,
                                dependency.VersionRange.OriginalString));
                        }
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
