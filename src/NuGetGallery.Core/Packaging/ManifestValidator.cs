// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;
using NuGet.Resources;

namespace NuGetGallery.Packaging
{
    public class ManifestValidator
    {
        // Copy-pasta from NuGet: src/Core/Utility/PackageIdValidator.cs because that constant is internal :(
        public static readonly int MaxPackageIdLength = 100;

        public static IEnumerable<ValidationResult> Validate(Manifest manifest)
        {
            return Validate(manifest.Metadata);
        }

        public static IEnumerable<ValidationResult> Validate(INupkg nupkg)
        {
            var metadata = nupkg.Metadata as ManifestMetadata;
            if (metadata != null)
            {
                return Validate(metadata);
            }
            return Enumerable.Empty<ValidationResult>();
        }

        public static IEnumerable<ValidationResult> Validate(ManifestMetadata metadata)
        {
            // Validate the ID
            if (String.IsNullOrEmpty(metadata.Id))
            {
                yield return new ValidationResult(Strings.Manifest_MissingId);
            }
            else
            {
                if (metadata.Id.Length > MaxPackageIdLength)
                {
                    yield return new ValidationResult(Strings.Manifest_IdTooLong);
                }
                else if (!PackageIdValidator.IsValidPackageId(metadata.Id))
                {
                    yield return new ValidationResult(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Manifest_InvalidId,
                        metadata.Id));
                }
            }

            foreach (var result in CheckUrls(metadata.IconUrl, metadata.ProjectUrl, metadata.LicenseUrl))
            {
                yield return result;
            }

            SemanticVersion __;
            if (!SemanticVersion.TryParse(metadata.Version, out __))
            {
                yield return new ValidationResult(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Manifest_InvalidVersion,
                    metadata.Version));
            }

            if (metadata.DependencySets != null)
            {
                foreach (var dependency in metadata.DependencySets.SelectMany(set => set.Dependencies))
                {
                    IVersionSpec ___;
                    if (!PackageIdValidator.IsValidPackageId(dependency.Id) || ( !string.IsNullOrEmpty(dependency.Version) && !VersionUtility.TryParseVersionSpec(dependency.Version, out ___)))
                    {
                        yield return new ValidationResult(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Manifest_InvalidDependency,
                            dependency.Id,
                            dependency.Version));
                    }
                }
            }

            var fxes = Enumerable.Concat(
                metadata.FrameworkAssemblies == null ? 
                    Enumerable.Empty<string>() : 
                    (metadata.FrameworkAssemblies.Select(a => a.TargetFramework)),
                metadata.DependencySets == null ?
                    Enumerable.Empty<string>() :
                    (metadata.DependencySets.Select(s => s.TargetFramework)));
            foreach (var fx in fxes)
            {
                //if target framework is not specified, then continue. Validate only for wrong specification.
                if (string.IsNullOrEmpty(fx))
                    continue;
                ValidationResult result = null;
                try
                {
                    VersionUtility.ParseFrameworkName(fx);
                }
                catch (ArgumentException)
                {
                    // Can't yield in the body of a catch...
                    result = new ValidationResult(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Manifest_InvalidTargetFramework,
                        fx));
                }

                if (result != null)
                {
                    yield return result;
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
