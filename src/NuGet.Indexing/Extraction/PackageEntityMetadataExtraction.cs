// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGetGallery;

namespace NuGet.Indexing
{
    public static class PackageEntityMetadataExtraction
    {
        public static IDictionary<string, string> MakePackageMetadata(Package package)
        {
            var extractor = new Extractor();
            return extractor.Extract(package);
        }

        private class Extractor
        {
            private Package _package;

            private Dictionary<string, string> _metadata;

            public IDictionary<string, string> Extract(Package package)
            {
                _package = package;
                _metadata = new Dictionary<string, string>();

                AddString(MetadataConstants.IdPropertyName, package.PackageRegistration.Id);
                AddString(MetadataConstants.NormalizedVersionPropertyName, package.NormalizedVersion);
                AddString(MetadataConstants.VersionPropertyName, package.Version);
                AddString(MetadataConstants.TitlePropertyName, package.Title);
                AddString(MetadataConstants.DescriptionPropertyName, package.Description);
                AddString(MetadataConstants.SummaryPropertyName, package.Summary);
                AddString(MetadataConstants.TagsPropertyName, package.Tags);
                AddString(MetadataConstants.AuthorsPropertyName, package.FlattenedAuthors);

                AddString(MetadataConstants.ListedPropertyName, package.Listed.ToString());
                AddString(MetadataConstants.CreatedPropertyName, package.Created.ToString("O"));
                AddString(MetadataConstants.PublishedPropertyName, package.Published.ToString("O"));
                AddString(MetadataConstants.LastEditedPropertyName, package.LastEdited?.ToString("O"));

                AddString(MetadataConstants.IconUrlPropertyName, package.IconUrl);
                AddString(MetadataConstants.ProjectUrlPropertyName, package.ProjectUrl);
                AddString(MetadataConstants.MinClientVersionPropertyName, package.MinClientVersion);
                AddString(MetadataConstants.ReleaseNotesPropertyName, package.ReleaseNotes);
                AddString(MetadataConstants.CopyrightPropertyName, package.Copyright);
                AddString(MetadataConstants.LanguagePropertyName, package.Language);
                AddString(MetadataConstants.LicenseUrlPropertyName, package.LicenseUrl);
                AddString(MetadataConstants.PackageHashPropertyName, package.Hash);
                AddString(MetadataConstants.PackageHashAlgorithmPropertyName, package.HashAlgorithm);
                AddString(MetadataConstants.PackageSizePropertyName, package.PackageFileSize.ToString());
                AddString(MetadataConstants.RequiresLicenseAcceptancePropertyName, package.RequiresLicenseAcceptance.ToString());

                AddString(MetadataConstants.FlattenedDependenciesPropertyName, package.FlattenedDependencies);
                AddSupportedFrameworks();

                return _metadata;
            }

            private void AddString(string destination, string value)
            {
                if (value == null)
                {
                    return;
                }

                _metadata[destination] = value;
            }

            private void AddSupportedFrameworks()
            {
                if (_package.SupportedFrameworks == null)
                {
                    return;
                }

                var supportedFrameworks = _package.SupportedFrameworks.Select(f => f.TargetFramework).ToArray();
                var flattened = string.Join("|", supportedFrameworks);
                AddString(MetadataConstants.SupportedFrameworksPropertyName, flattened);
            }
        }
    }
}