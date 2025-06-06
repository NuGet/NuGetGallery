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
        public static IEnumerable<ValidationResult> Validate(Stream nuspecStream, out NuspecReader nuspecReader, out PackageMetadata packageMetadata)
        {
            packageMetadata = null;

            try
            {
                nuspecReader = new NuspecReader(nuspecStream);
                var rawMetadata = nuspecReader.GetMetadata();
                if (rawMetadata != null && rawMetadata.Any())
                {
                    packageMetadata = PackageMetadata.FromNuspecReader(nuspecReader, strict: true);
                    return ValidateCore(packageMetadata);
                }
            }
            catch (Exception ex)
            {
                nuspecReader = null;
                packageMetadata = null;
                return new[] { new ValidationResult(ex.Message) };
            }

            return Enumerable.Empty<ValidationResult>();
        }

        private static IEnumerable<ValidationResult> ValidateCore(PackageMetadata packageMetadata)
        {
            // Validate the ID
            if (string.IsNullOrEmpty(packageMetadata.Id))
            {
                yield return new ValidationResult(CoreStrings.Manifest_MissingId);
            }
            else
            {
                if (packageMetadata.Id.Length > NuGet.Packaging.PackageIdValidator.MaxPackageIdLength)
                {
                    yield return new ValidationResult(CoreStrings.Manifest_IdTooLong);
                }
                else if (!PackageIdValidator.IsValidPackageId(packageMetadata.Id))
                {
                    yield return new ValidationResult(String.Format(
                        CultureInfo.CurrentCulture,
                        CoreStrings.Manifest_InvalidId,
                        packageMetadata.Id));
                }
            }

            // Check and validate URL properties
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
                    CoreStrings.Manifest_InvalidVersion,
                    version));
            }

            var versionValidationResult = ValidateVersionForLegacyClients(packageMetadata.Version);
            if (versionValidationResult != null)
            {
                yield return versionValidationResult;
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
                            CoreStrings.Manifest_TargetFrameworkNotSupported,
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

                    // Verify frameworks
                    var isUnsupportedFramework = dependencyGroup.TargetFramework?.IsUnsupported;
                    if (isUnsupportedFramework.HasValue && isUnsupportedFramework.Value)
                    {
                        yield return new ValidationResult(String.Format(
                            CultureInfo.CurrentCulture,
                            CoreStrings.Manifest_TargetFrameworkNotSupported,
                            dependencyGroup.TargetFramework?.ToString()));
                    }

                    // Verify package id's and versions
                    foreach (var dependency in dependencyGroup.Packages)
                    {
                        bool duplicate = !dependencyIds.Add(dependency.Id);
                        if (duplicate)
                        {
                            yield return new ValidationResult(String.Format(
                                CultureInfo.CurrentCulture,
                                CoreStrings.Manifest_DuplicateDependency,
                                dependencyGroup.TargetFramework.GetShortFolderName(),
                                dependency.Id));
                        }

                        if (!PackageIdValidator.IsValidPackageId(dependency.Id))
                        {
                            yield return new ValidationResult(String.Format(
                                CultureInfo.CurrentCulture,
                                CoreStrings.Manifest_InvalidDependency,
                                dependency.Id,
                                dependency.VersionRange.OriginalString));
                        }

                        // Versions
                        if (dependency.VersionRange.IsFloating)
                        {
                            yield return new ValidationResult(String.Format(
                                CultureInfo.CurrentCulture,
                                CoreStrings.Manifest_InvalidDependencyVersionRange,
                                dependency.VersionRange.OriginalString));
                        }

                        if (dependency.VersionRange.MinVersion != null)
                        {
                            var versionRangeValidationResult =
                                ValidateDependencyVersion(dependency.VersionRange.MinVersion);
                            if (versionRangeValidationResult != null)
                            {
                                yield return versionRangeValidationResult;
                            }
                        }

                        if (dependency.VersionRange.MaxVersion != null
                            && dependency.VersionRange.MaxVersion != dependency.VersionRange.MinVersion)
                        {
                            var versionRangeValidationResult =
                                ValidateDependencyVersion(dependency.VersionRange.MaxVersion);
                            if (versionRangeValidationResult != null)
                            {
                                yield return versionRangeValidationResult;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks whether the provided version is consumable by legacy 2.x clients,
        /// which do not support a `.` in release labels, or release labels starting with numeric characters.
        /// See also https://github.com/NuGet/NuGetGallery/issues/3226.
        /// </summary>
        /// <param name="version">The <see cref="NuGetVersion"/> to check for 2.x client compatibility.</param>
        /// <returns>Returns a <see cref="ValidationResult"/> when non-compliant; otherwise <c>null</c>.</returns>
        private static ValidationResult ValidateVersionForLegacyClients(NuGetVersion version)
        {
            if (!version.IsSemVer2 && !version.IsValidVersionForLegacyClients())
            {
                return new ValidationResult(string.Format(
                    CultureInfo.CurrentCulture,
                    CoreStrings.Manifest_InvalidVersion,
                    version));
            }

            return null;
        }

        private static ValidationResult ValidateDependencyVersion(NuGetVersion version)
        {
            if (version.HasMetadata)
            {
                return new ValidationResult(string.Format(
                    CultureInfo.CurrentCulture,
                    CoreStrings.Manifest_InvalidDependencyVersion,
                    version.ToFullString()));
            }

            return ValidateVersionForLegacyClients(version);
        }

        private static IEnumerable<ValidationResult> CheckUrls(params string[] urls)
        {
            foreach (var url in urls)
            {
                Uri uri = null;
                if (!string.IsNullOrEmpty(url) && !Uri.TryCreate(url, UriKind.Absolute, out uri))
                {
                    yield return new ValidationResult(CoreStrings.Manifest_InvalidUrl);
                }
                else if (uri != null && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                {
                    yield return new ValidationResult(CoreStrings.Manifest_InvalidUrl);
                }
            }
        }
    }
}
