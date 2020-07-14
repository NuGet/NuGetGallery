// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Jobs.Catalog2Registration
{
    public class EntityBuilder : IEntityBuilder
    {
        private readonly RegistrationUrlBuilder _urlBuilder;
        private readonly IOptionsSnapshot<Catalog2RegistrationConfiguration> _options;
        private readonly FlatContainerPackagePathProvider _flatContainerPathProvider;
        private readonly Uri _galleryBaseUrl;

        public EntityBuilder(
            RegistrationUrlBuilder urlBuilder,
            IOptionsSnapshot<Catalog2RegistrationConfiguration> options)
        {
            _urlBuilder = urlBuilder ?? throw new ArgumentNullException(nameof(urlBuilder));
            _options = options ?? throw new ArgumentNullException(nameof(urlBuilder));

            var flatContainerBaseUrl = _options.Value.FlatContainerBaseUrl.TrimEnd('/');
            _flatContainerPathProvider = new FlatContainerPackagePathProvider(flatContainerBaseUrl);
            _galleryBaseUrl = new Uri(_options.Value.GalleryBaseUrl);
        }

        public void UpdateLeafItem(RegistrationLeafItem leafItem, HiveType hive, string id, PackageDetailsCatalogLeaf packageDetails)
        {
            var parsedVersion = packageDetails.ParsePackageVersion();

            leafItem.Url = _urlBuilder.GetLeafUrl(hive, id, parsedVersion);
            leafItem.Type = JsonLdConstants.RegistrationLeafItemType;
            leafItem.PackageContent = GetPackageContentUrl(id, packageDetails);
            leafItem.Registration = _urlBuilder.GetIndexUrl(hive, id);

            if (leafItem.CatalogEntry == null)
            {
                leafItem.CatalogEntry = new Protocol.Registration.RegistrationCatalogEntry();
            }

            UpdateCatalogEntry(hive, id, leafItem.CatalogEntry, packageDetails, parsedVersion);
        }

        private void UpdateCatalogEntry(
            HiveType hive,
            string id,
            Protocol.Registration.RegistrationCatalogEntry catalogEntry,
            PackageDetailsCatalogLeaf packageDetails,
            NuGetVersion parsedVersion)
        {
            catalogEntry.Url = packageDetails.Url;
            catalogEntry.Type = JsonLdConstants.RegistrationLeafItemCatalogEntryType;
            catalogEntry.Authors = packageDetails.Authors ?? string.Empty;
            
            // Add the "registration" property to each package dependency.
            if (packageDetails.DependencyGroups != null)
            {
                catalogEntry.DependencyGroups = new List<RegistrationPackageDependencyGroup>();
                foreach (var group in packageDetails.DependencyGroups)
                {
                    var registrationGroup = new RegistrationPackageDependencyGroup
                    {
                        Url = group.Url,
                        Type = group.Type,
                        TargetFramework = group.TargetFramework,
                    };

                    catalogEntry.DependencyGroups.Add(registrationGroup);

                    if (group.Dependencies == null)
                    {
                        continue;
                    }

                    registrationGroup.Dependencies = new List<RegistrationPackageDependency>();

                    for (int i = 0; i < group.Dependencies.Count; i++)
                    {
                        var catalogDependency = group.Dependencies[i];
                        var registrationDependency = new RegistrationPackageDependency
                        {
                            Url = catalogDependency.Url,
                            Type = catalogDependency.Type,
                            Id = catalogDependency.Id,
                            Range = catalogDependency.Range,
                            Registration = _urlBuilder.GetIndexUrl(hive, catalogDependency.Id),
                        };
                        registrationGroup.Dependencies.Add(registrationDependency);
                    }
                }
            }
            else
            {
                catalogEntry.DependencyGroups = null;
            }

            // Add the types to the deprecation.
            if (hive == HiveType.Legacy || hive == HiveType.Gzipped)
            {
                catalogEntry.Deprecation = null;
            }
            else
            {
                catalogEntry.Deprecation = packageDetails.Deprecation;
                if (catalogEntry.Deprecation != null)
                {
                    catalogEntry.Deprecation.Type = JsonLdConstants.PackageDeprecationType;
                    if (catalogEntry.Deprecation.AlternatePackage != null)
                    {
                        catalogEntry.Deprecation.AlternatePackage.Type = JsonLdConstants.AlternatePackageType;
                    }
                }
            }

            catalogEntry.Description = packageDetails.Description ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(packageDetails.IconUrl)
                || !string.IsNullOrWhiteSpace(packageDetails.IconFile))
            {
                catalogEntry.IconUrl = GetPackageIconUrl(id, packageDetails);
            }
            else
            {
                catalogEntry.IconUrl = string.Empty;
            }

            catalogEntry.PackageId = packageDetails.PackageId ?? id;
            catalogEntry.Language = packageDetails.Language ?? string.Empty;
            catalogEntry.LicenseExpression = packageDetails.LicenseExpression ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(packageDetails.LicenseFile)
                || !string.IsNullOrWhiteSpace(packageDetails.LicenseExpression))
            {
                // Use the package ID casing from this specific version, since license URLs do not exclusively use
                // lowercase package ID like icon URL. This is legacy behavior that can be revisited later. Gallery
                // supports case insensitive package IDs so it doesn't matter too much.
                catalogEntry.LicenseUrl = LicenseHelper.GetGalleryLicenseUrl(
                    catalogEntry.PackageId,
                    parsedVersion.ToNormalizedString(), _galleryBaseUrl);
            }
            else
            {
                catalogEntry.LicenseUrl = packageDetails.LicenseUrl ?? string.Empty;
            }

            catalogEntry.Listed = packageDetails.IsListed();
            catalogEntry.MinClientVersion = packageDetails.MinClientVersion ?? string.Empty;
            catalogEntry.PackageContent = GetPackageContentUrl(id, packageDetails);
            catalogEntry.ProjectUrl = packageDetails.ProjectUrl ?? string.Empty;
            catalogEntry.Published = packageDetails.Published;
            catalogEntry.RequireLicenseAcceptance = packageDetails.RequireLicenseAcceptance ?? false;
            catalogEntry.Summary = packageDetails.Summary ?? string.Empty;

            if (packageDetails.Tags != null && packageDetails.Tags.Count > 0)
            {
                catalogEntry.Tags = packageDetails.Tags;
            }
            else
            {
                catalogEntry.Tags = new List<string> { string.Empty };
            }

            catalogEntry.Title = packageDetails.Title ?? string.Empty;
            catalogEntry.Version = parsedVersion.ToFullString();

            if (hive == HiveType.SemVer2 &&
                packageDetails.Vulnerabilities != null && 
                packageDetails.Vulnerabilities.Count > 0)
            {
                catalogEntry.Vulnerabilities = packageDetails.Vulnerabilities.Select(v => 
                    new RegistrationPackageVulnerability()
                    {
                        AdvisoryUrl = v.AdvisoryUrl,
                        Severity = v.Severity
                    }
                ).ToList();
            }
            else
            {
                catalogEntry.Vulnerabilities = null;
            }
        }

        public RegistrationLeaf NewLeaf(RegistrationLeafItem leafItem)
        {
            return new RegistrationLeaf
            {
                Url = leafItem.Url,
                Types = JsonLdConstants.RegistrationLeafTypes,
                CatalogEntry = leafItem.CatalogEntry.Url,
                Listed = leafItem.CatalogEntry.Listed,
                PackageContent = leafItem.PackageContent,
                Published = leafItem.CatalogEntry.Published,
                Registration = leafItem.Registration,
                Context = JsonLdConstants.RegistrationLeafContext,
            };
        }

        public void UpdateInlinedPageItem(RegistrationPage pageItem, HiveType hive, string id, int count, NuGetVersion lower, NuGetVersion upper)
        {
            Guard.Assert(pageItem.Items != null, "The provided page item must have inlined leaf items.");
            Guard.Assert(pageItem.Items.Count == count, "The provided count must equal the number of leaf items.");
            UpdatePage(pageItem, count, lower, upper);
            pageItem.Url = _urlBuilder.GetInlinedPageUrl(hive, id, lower, upper);
            pageItem.Parent = _urlBuilder.GetIndexUrl(hive, id);
            pageItem.Context = null;
        }

        public void UpdateNonInlinedPageItem(RegistrationPage pageItem, HiveType hive, string id, int count, NuGetVersion lower, NuGetVersion upper)
        {
            Guard.Assert(pageItem.Items == null, "The provided page item must not have inlined leaf items.");
            UpdatePage(pageItem, count, lower, upper);
            pageItem.Url = _urlBuilder.GetPageUrl(hive, id, lower, upper);
            pageItem.Parent = null;
            pageItem.Context = null;
        }

        public void UpdatePage(RegistrationPage page, HiveType hive, string id, int count, NuGetVersion lower, NuGetVersion upper)
        {
            Guard.Assert(page.Items != null, "Pages must have leaf items.");
            Guard.Assert(page.Items.Count == count, "The provided count must equal the number of leaf items.");
            UpdatePage(page, count, lower, upper);
            page.Url = _urlBuilder.GetPageUrl(hive, id, lower, upper);
            page.Parent = _urlBuilder.GetIndexUrl(hive, id);
            page.Context = JsonLdConstants.RegistrationContainerContext;
        }

        private static void UpdatePage(RegistrationPage page, int count, NuGetVersion lower, NuGetVersion upper)
        {
            Guard.Assert(count > 0, "Page count must be greater than zero.");
            Guard.Assert(lower <= upper, "The lower bound on a page must be less than or equal to the upper bound.");
            page.Type = JsonLdConstants.RegistrationPageType;
            page.Lower = lower.ToNormalizedString();
            page.Upper = upper.ToNormalizedString();
            page.Count = count;
        }

        public void UpdateIndex(RegistrationIndex index, HiveType hive, string id, int count)
        {
            Guard.Assert(count > 0, "Indexes must have at least one page item.");
            Guard.Assert(index.Items.Count == count, "The provided count must equal the number of page items.");
            index.Url = _urlBuilder.GetIndexUrl(hive, id);
            index.Types = JsonLdConstants.RegistrationIndexTypes;
            index.Count = count;
            index.Context = JsonLdConstants.RegistrationContainerContext;
        }

        public void UpdateIndexUrls(RegistrationIndex index, HiveType fromHive, HiveType toHive)
        {
            index.Url = _urlBuilder.ConvertHive(fromHive, toHive, index.Url);

            foreach (var pageItem in index.Items)
            {
                UpdatePageUrls(pageItem, fromHive, toHive);
            }
        }

        public void UpdatePageUrls(RegistrationPage page, HiveType fromHive, HiveType toHive)
        {
            page.Url = _urlBuilder.ConvertHive(fromHive, toHive, page.Url);

            if (page.Parent != null)
            {
                page.Parent = _urlBuilder.ConvertHive(fromHive, toHive, page.Parent);
            }

            if (page.Items != null)
            {
                foreach (var item in page.Items)
                {
                    UpdateLeafItemUrls(item, fromHive, toHive);
                }
            }
        }

        private void UpdateLeafItemUrls(RegistrationLeafItem leafItem, HiveType fromHive, HiveType toHive)
        {
            leafItem.Url = _urlBuilder.ConvertHive(fromHive, toHive, leafItem.Url);
            leafItem.Registration = _urlBuilder.ConvertHive(fromHive, toHive, leafItem.Registration);

            if (leafItem.CatalogEntry.DependencyGroups != null)
            {
                foreach (var dependencyGroup in leafItem.CatalogEntry.DependencyGroups)
                {
                    if (dependencyGroup.Dependencies == null)
                    {
                        continue;
                    }

                    foreach (var dependency in dependencyGroup.Dependencies)
                    {
                        dependency.Registration = _urlBuilder.ConvertHive(fromHive, toHive, dependency.Registration);
                    }
                }
            }
        }

        public void UpdateLeafUrls(RegistrationLeaf leaf, HiveType fromHive, HiveType toHive)
        {
            leaf.Url = _urlBuilder.ConvertHive(fromHive, toHive, leaf.Url);
            leaf.Registration = _urlBuilder.ConvertHive(fromHive, toHive, leaf.Registration);
        }

        public void UpdateCommit(ICommitted committed, CatalogCommit commit)
        {
            committed.CommitId = commit.Id;
            committed.CommitTimestamp = commit.Timestamp;
        }

        private string GetPackageIconUrl(string id, PackageDetailsCatalogLeaf packageDetails)
        {
            return new Uri(_flatContainerPathProvider.GetIconPath(id, packageDetails.PackageVersion)).AbsoluteUri;
        }

        private string GetPackageContentUrl(string id, PackageDetailsCatalogLeaf packageDetails)
        {
            return new Uri(_flatContainerPathProvider.GetPackagePath(id, packageDetails.PackageVersion)).AbsoluteUri;
        }
    }
}
