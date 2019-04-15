// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Indexing
{
    public static class CatalogPackageMetadataExtraction
    {
        public static IDictionary<string, string> MakePackageMetadata(JObject catalogItem, Uri galleryBaseAddress)
        {
            var extractor = new Extractor();
            return extractor.Extract(catalogItem, galleryBaseAddress);
        }

        private class Extractor
        {
            private JObject _catalog;
            private CatalogPackageReader _reader;
            private Dictionary<string, string> _metadata;
            private Uri _galleryBaseAddress;

            public IDictionary<string, string> Extract(JObject catalog, Uri galleryBaseAddress)
            {
                _catalog = catalog;
                _reader = new CatalogPackageReader(_catalog);
                _metadata = new Dictionary<string, string>();
                _galleryBaseAddress = galleryBaseAddress;

                AddString(MetadataConstants.IdPropertyName);
                AddString(MetadataConstants.NormalizedVersionPropertyName);
                AddString(MetadataConstants.VerbatimVersionPropertyName);
                AddString(MetadataConstants.TitlePropertyName);
                AddString(MetadataConstants.DescriptionPropertyName);
                AddString(MetadataConstants.SummaryPropertyName);
                AddString(MetadataConstants.AuthorsPropertyName);
                AddStringArray(MetadataConstants.TagsPropertyName);

                AddListed();
                AddSemVerLevelKey();
                AddString(MetadataConstants.CreatedPropertyName);
                AddString(MetadataConstants.PublishedPropertyName);
                AddString(MetadataConstants.LastEditedPropertyName);

                AddString(MetadataConstants.IconUrlPropertyName);
                AddString(MetadataConstants.ProjectUrlPropertyName);
                AddString(MetadataConstants.MinClientVersionPropertyName);
                AddString(MetadataConstants.ReleaseNotesPropertyName);
                AddString(MetadataConstants.CopyrightPropertyName);
                AddString(MetadataConstants.LanguagePropertyName);
                AddString(MetadataConstants.PackageHashPropertyName);
                AddString(MetadataConstants.PackageHashAlgorithmPropertyName);
                AddString(MetadataConstants.PackageSizePropertyName);
                AddString(MetadataConstants.CatalogMetadata.RequiresLicenseAcceptancePropertyName, MetadataConstants.RequiresLicenseAcceptancePropertyName);

                AddLicenseUrl();
                AddFlattenedDependencies();
                AddSupportedFrameworks();

                return _metadata;
            }

            private void AddLicenseUrl()
            {
                var packageId = JTokenToString(_catalog[MetadataConstants.IdPropertyName]);
                var packageVersion = JTokenToString(_catalog[MetadataConstants.NormalizedVersionPropertyName]);

                if (_galleryBaseAddress != null &&
                    !string.IsNullOrWhiteSpace(packageId) &&
                    !string.IsNullOrWhiteSpace(packageVersion) &&
                    (_catalog.Value<string>(MetadataConstants.LicenseExpressionPropertyName) != null ||
                      _catalog.Value<string>(MetadataConstants.LicenseFilePropertyName) != null))
                {
                    _metadata[MetadataConstants.LicenseUrlPropertyName] = LicenseHelper.GetGalleryLicenseUrl(packageId, packageVersion, _galleryBaseAddress);
                }
                else
                {
                    AddString(MetadataConstants.LicenseUrlPropertyName);
                }
            }

            private void AddString(string source, string destination = null)
            {
                var value = _catalog[source];
                if (value == null)
                {
                    return;
                }

                _metadata[destination ?? source] = JTokenToString(value);
            }

            private string JTokenToString(JToken value)
            {
                if (value == null)
                {
                    return null;
                }

                if (value.Type == JTokenType.Date)
                {
                    return value.Value<DateTimeOffset>().ToString("o");
                }
                else
                {
                    return (string)value;
                }
            }

            private void AddStringArray(string source, string destination = null)
            {
                var value = _catalog[source];
                if (value == null)
                {
                    return;
                }

                string joined = string.Join(" ", value.Select(JTokenToString));
                _metadata[destination ?? source] = joined;
            }

            private void AddListed()
            {
                var listed = (string)_catalog[MetadataConstants.ListedPropertyName];
                var published = _catalog[MetadataConstants.PublishedPropertyName];
                if (listed == null)
                {
                    if (published != null && ((DateTime)published).ToString("yyyyMMdd") == "19000101")
                    {
                        listed = "false";
                    }
                    else
                    {
                        listed = "true";
                    }
                }

                _metadata[MetadataConstants.ListedPropertyName] = listed;
            }

            private void AddSemVerLevelKey()
            {
                var version = (string)_catalog[MetadataConstants.VerbatimVersionPropertyName];
                if (version != null)
                {
                    NuGetVersion packageOriginalVersion;
                    if (NuGetVersion.TryParse(version, out packageOriginalVersion))
                    {
                        if (packageOriginalVersion.IsSemVer2)
                        {
                            _metadata[MetadataConstants.SemVerLevelKeyPropertyName] = MetadataConstants.SemVerLevel2Value;
                            return;
                        }
                    }
                }

                var dependencyGroups = _reader.GetPackageDependencies().ToList();
                foreach (var dependencyGroup in dependencyGroups)
                {
                    foreach (var packageDependency in dependencyGroup.Packages)
                    {
                        var versionRange = packageDependency.VersionRange;
                        if ((versionRange.MaxVersion != null && versionRange.MaxVersion.IsSemVer2)
                            || (versionRange.MinVersion != null && versionRange.MinVersion.IsSemVer2))
                        {
                            _metadata[MetadataConstants.SemVerLevelKeyPropertyName] = MetadataConstants.SemVerLevel2Value;
                            return;
                        }
                    }
                }
            }

            private void AddFlattenedDependencies()
            {
                var dependencyGroups = _reader.GetPackageDependencies().ToList();

                var builder = new StringBuilder();
                foreach (var dependencyGroup in dependencyGroups)
                {
                    if (dependencyGroup.Packages.Any())
                    {
                        // Add packages list
                        foreach (var packageDependency in dependencyGroup.Packages)
                        {
                            AddFlattenedPackageDependency(dependencyGroup, packageDependency, builder);
                        }
                    }
                    else
                    {
                        // Add empty framework dependency
                        if (builder.Length > 0)
                        {
                            builder.Append("|");
                        }

                        builder.Append(":");
                        AddFlattenedFrameworkDependency(dependencyGroup, builder);
                    }
                }

                if (builder.Length > 0)
                {
                    _metadata[MetadataConstants.FlattenedDependenciesPropertyName] = builder.ToString();
                }
            }

            private void AddFlattenedPackageDependency(
                PackageDependencyGroup dependencyGroup,
                Packaging.Core.PackageDependency packageDependency,
                StringBuilder builder)
            {
                if (builder.Length > 0)
                {
                    builder.Append("|");
                }

                builder.Append(packageDependency.Id);
                builder.Append(":");
                if (!packageDependency.VersionRange.Equals(VersionRange.All))
                {
                    builder.Append(packageDependency.VersionRange?.ToString("S", new VersionRangeFormatter()));
                }

                AddFlattenedFrameworkDependency(dependencyGroup, builder);
            }

            private void AddFlattenedFrameworkDependency(PackageDependencyGroup dependencyGroup, StringBuilder builder)
            {
                if (!SpecialFrameworks.Contains(dependencyGroup.TargetFramework))
                {
                    try
                    {
                        builder.Append(":");
                        builder.Append(dependencyGroup.TargetFramework?.GetShortFolderName());
                    }
                    catch (FrameworkException)
                    {
                        // ignoring FrameworkException on purpose - we don't want the job crashing
                        // whenever someone uploads an unsupported framework
                    }
                }
            }

            private void AddSupportedFrameworks()
            {
                // Parse files for framework names
                List<NuGetFramework> supportedFrameworksFromReader = null;
                try
                {
                    supportedFrameworksFromReader = _reader
                        .GetSupportedFrameworks()
                        .ToList();
                }
                catch (Exception ex) when (
                    (ex is ArgumentException && ex.Message.ToLowerInvariant().StartsWith("invalid portable"))
                    || ex is FrameworkException
                    || ex is PackagingException)
                {
                    // Ignore exceptions indicating invalid frameworks. Since the package is already accepted, it's
                    // better to have no supported frameworks than to block the pipeline.
                    Trace.TraceWarning($"{nameof(CatalogPackageMetadataExtraction)}.{nameof(AddSupportedFrameworks)} exception: " + ex);
                    return;
                }

                // Filter out special frameworks + get short framework names
                var supportedFrameworks = supportedFrameworksFromReader
                    .Except(SpecialFrameworks)
                    .Select(f =>
                    {
                        try
                        {
                            return f.GetShortFolderName();
                        }
                        catch (FrameworkException)
                        {
                            // ignoring FrameworkException on purpose - we don't want the job crashing
                            // whenever someone uploads an unsupported framework
                            return null;
                        }
                    })
                    .Where(f => !String.IsNullOrEmpty(f))
                    .ToArray();

                if (supportedFrameworks.Any())
                {
                    _metadata[MetadataConstants.SupportedFrameworksPropertyName] = string.Join("|", supportedFrameworks);
                }
            }

            private IEnumerable<NuGetFramework> SpecialFrameworks => new[]
            {
                NuGetFramework.AnyFramework,
                NuGetFramework.AgnosticFramework,
                NuGetFramework.UnsupportedFramework
            };
        }
    }
}