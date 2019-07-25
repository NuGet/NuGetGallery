// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Options;
using NuGet.Frameworks;
using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Registration;
using NuGet.Versioning;
using NuGetGallery;
using PackageDependency = NuGet.Protocol.Catalog.PackageDependency;

namespace NuGet.Services.AzureSearch
{
    public class BaseDocumentBuilder : IBaseDocumentBuilder
    {
        private static readonly VersionRangeFormatter VersionRangeFormatter = new VersionRangeFormatter();
        private static readonly DateTimeOffset UnlistedPublished = new DateTimeOffset(Metadata.Catalog.Constants.UnpublishedDate);

        private static readonly HashSet<NuGetFramework> SpecialFrameworks = new HashSet<NuGetFramework>
        {
            NuGetFramework.AnyFramework,
            NuGetFramework.AgnosticFramework,
            NuGetFramework.UnsupportedFramework
        };

        private readonly IOptionsSnapshot<AzureSearchJobConfiguration> _options;

        public BaseDocumentBuilder(IOptionsSnapshot<AzureSearchJobConfiguration> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void PopulateUpdated(
            IUpdatedDocument document,
            bool lastUpdatedFromCatalog)
        {
            document.SetLastUpdatedDocumentOnNextRead();
            document.LastDocumentType = document.GetType().FullName;
            document.LastUpdatedFromCatalog = lastUpdatedFromCatalog;
        }

        public void PopulateCommitted(
            ICommittedDocument document,
            bool lastUpdatedFromCatalog,
            DateTimeOffset? lastCommitTimestamp,
            string lastCommitId)
        {
            if (lastUpdatedFromCatalog)
            {
                if (lastCommitTimestamp == null)
                {
                    throw new ArgumentNullException(nameof(lastCommitTimestamp));
                }

                if (lastCommitId == null)
                {
                    throw new ArgumentNullException(nameof(lastCommitId));
                }
            }
            else
            {
                if (lastCommitTimestamp != null)
                {
                    throw new ArgumentException("The last commit timestamp must be null when not updated from the catalog", nameof(lastCommitTimestamp));
                }

                if (lastCommitId != null)
                {
                    throw new ArgumentException("The last commit ID must be null when not updated from the catalog", nameof(lastCommitId));
                }
            }

            PopulateUpdated(document, lastUpdatedFromCatalog);
            document.LastCommitTimestamp = lastCommitTimestamp;
            document.LastCommitId = lastCommitId;
        }

        public void PopulateMetadata(
            IBaseMetadataDocument document,
            string packageId,
            Package package)
        {
            document.Authors = package.FlattenedAuthors;
            document.Copyright = package.Copyright;
            document.Created = AssumeUtc(package.Created);
            document.Description = package.Description;
            document.FileSize = package.PackageFileSize;
            document.FlattenedDependencies = package.FlattenedDependencies;
            document.Hash = package.Hash;
            document.HashAlgorithm = package.HashAlgorithm;
            document.IconUrl = package.IconUrl;
            document.Language = package.Language;
            document.LastEdited = AssumeUtc(package.LastEdited);
            document.MinClientVersion = package.MinClientVersion;
            document.NormalizedVersion = package.NormalizedVersion;
            document.OriginalVersion = package.Version;
            document.PackageId = packageId;
            document.Prerelease = package.IsPrerelease;
            document.ProjectUrl = package.ProjectUrl;
            document.Published = package.Listed ? AssumeUtc(package.Published) : UnlistedPublished;
            document.ReleaseNotes = package.ReleaseNotes;
            document.RequiresLicenseAcceptance = package.RequiresLicenseAcceptance;
            document.SemVerLevel = package.SemVerLevelKey;
            document.SortableTitle = GetSortableTitle(package.Title, packageId);
            document.Summary = package.Summary;
            document.Tags = package.Tags == null ? null : Utils.SplitTags(package.Tags);
            document.Title = package.Title;
            document.TokenizedPackageId = packageId;

            if (package.LicenseExpression != null || package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
            {
                document.LicenseUrl = LicenseHelper.GetGalleryLicenseUrl(
                    packageId,
                    package.NormalizedVersion,
                    _options.Value.ParseGalleryBaseUrl());
            }
            else
            {
                document.LicenseUrl = package.LicenseUrl;
            }

            // TODO: detect whether the package entity has an icon file once there is schema for it.
            // https://github.com/NuGet/NuGetGallery/issues/7261
            // https://github.com/nuget/nugetgallery/issues/7064
        }

        public void PopulateMetadata(
            IBaseMetadataDocument document,
            string normalizedVersion,
            PackageDetailsCatalogLeaf leaf)
        {
            document.Authors = leaf.Authors;
            document.Copyright = leaf.Copyright;
            document.Created = leaf.Created;
            document.Description = leaf.Description;
            document.FileSize = leaf.PackageSize;
            document.FlattenedDependencies = GetFlattenedDependencies(leaf);
            document.Hash = leaf.PackageHash;
            document.HashAlgorithm = leaf.PackageHashAlgorithm;
            document.Language = leaf.Language;
            document.LastEdited = leaf.LastEdited;
            document.MinClientVersion = leaf.MinClientVersion;
            document.NormalizedVersion = normalizedVersion;
            document.OriginalVersion = leaf.VerbatimVersion;
            document.PackageId = leaf.PackageId;
            document.Prerelease = leaf.IsPrerelease;
            document.ProjectUrl = leaf.ProjectUrl;
            document.Published = leaf.Published;
            document.ReleaseNotes = leaf.ReleaseNotes;
            document.RequiresLicenseAcceptance = leaf.RequireLicenseAgreement;
            document.SemVerLevel = leaf.IsSemVer2() ? SemVerLevelKey.SemVer2 : SemVerLevelKey.Unknown;
            document.SortableTitle = GetSortableTitle(leaf.Title, leaf.PackageId);
            document.Summary = leaf.Summary;
            document.Tags = leaf.Tags == null ? null : leaf.Tags.ToArray();
            document.Title = leaf.Title;
            document.TokenizedPackageId = leaf.PackageId;

            if (leaf.LicenseExpression != null || leaf.LicenseFile != null)
            {
                document.LicenseUrl = LicenseHelper.GetGalleryLicenseUrl(
                    document.PackageId,
                    normalizedVersion,
                    _options.Value.ParseGalleryBaseUrl());
            }
            else
            {
                document.LicenseUrl = leaf.LicenseUrl;
            }

            if (leaf.IconFile != null)
            {
                var provider = new FlatContainerPackagePathProvider(_options.Value.FlatContainerContainerName);
                var iconPath = provider.GetIconPath(document.PackageId, normalizedVersion);
                document.IconUrl = new Uri(_options.Value.ParseFlatContainerBaseUrl(), iconPath).AbsoluteUri;
            }
            else
            {
                document.IconUrl = leaf.IconUrl;
            }
        }

        private static string GetSortableTitle(string title, string packageId)
        {
            var output = string.IsNullOrWhiteSpace(title) ? packageId : title;
            return output.Trim().ToLowerInvariant();
        }

        private static DateTimeOffset? AssumeUtc(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
            {
                return null;
            }

            return new DateTimeOffset(dateTime.Value.Ticks, TimeSpan.Zero);
        }

        /// <summary>
        /// This method produces output for official client. The implementation on the client side, at one point of time
        /// was:
        /// https://github.com/NuGet/NuGet.Client/blob/b404acf6eb88c2b6086a9cbb5106104534de2428/src/NuGet.Core/NuGet.Protocol/LegacyFeed/V2FeedPackageInfo.cs#L228-L308
        /// 
        /// The output is a string where each dependency is separated by a "|" character and information about each
        /// dependency is separated by a ":" character. Per dependency, the colon-seperated data is a pair or triple.
        /// The three fields in order are: dependency package ID, version range, and target framework. If third value
        /// (the framework that the dependency targets) is an empty string or excluded, this means the dependency
        /// applies to any framework. If the package ID and range and empty strings but the framework is included, this
        /// means that the package supports the specified framework but has no dependencies specific to that framework.
        /// 
        /// Output:
        ///     [DEPENDENCY[|DEPENDENCY][|...][|DEPENDENCY]]
        /// Each dependency:
        ///     [PACKAGE_ID]:[VERSION_RANGE][:TARGET_FRAMEWORK]
        ///   
        /// Example A (no target frameworks):
        ///     Microsoft.Data.OData:5.0.2|Microsoft.WindowsAzure.ConfigurationManager:1.8.0
        /// Example B (target frameworks):
        ///     NETStandard.Library:1.6.1:netstandard1.0|Newtonsoft.Json:10.0.2:netstandard1.0
        /// Example D (empty target framework):
        ///     Microsoft.Data.OData:5.0.2:
        /// Example D (just target framework):
        ///     ::net20|::net35|::net40|::net45|NETStandard.Library:1.6.1:netstandard1.0
        /// </summary>
        private static string GetFlattenedDependencies(PackageDetailsCatalogLeaf leaf)
        {
            if (leaf.DependencyGroups == null)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (var dependencyGroup in leaf.DependencyGroups)
            {
                var targetFramework = dependencyGroup.ParseTargetFramework();

                if (dependencyGroup.Dependencies != null && dependencyGroup.Dependencies.Any())
                {
                    foreach (var packageDependency in dependencyGroup.Dependencies)
                    {
                        AddFlattenedPackageDependency(targetFramework, packageDependency, builder);
                    }
                }
                else
                {
                    if (builder.Length > 0)
                    {
                        builder.Append("|");
                    }

                    builder.Append(":");
                    AddFlattenedFrameworkDependency(targetFramework, builder);
                }
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }

        private static void AddFlattenedPackageDependency(
            NuGetFramework targetFramework,
            PackageDependency packageDependency,
            StringBuilder builder)
        {
            if (builder.Length > 0)
            {
                builder.Append("|");
            }

            builder.Append(packageDependency.Id);
            builder.Append(":");

            var versionRange = packageDependency.ParseRange();

            if (!VersionRange.All.Equals(versionRange))
            {
                builder.Append(versionRange?.ToString("S", VersionRangeFormatter));
            }

            AddFlattenedFrameworkDependency(targetFramework, builder);
        }

        private static void AddFlattenedFrameworkDependency(NuGetFramework targetFramework, StringBuilder builder)
        {
            if (!SpecialFrameworks.Contains(targetFramework))
            {
                try
                {
                    builder.Append(":");
                    builder.Append(targetFramework?.GetShortFolderName());
                }
                catch (FrameworkException)
                {
                    // ignoring FrameworkException on purpose - we don't want the job crashing
                    // whenever someone uploads an unsupported framework
                }
            }
        }
    }
}
