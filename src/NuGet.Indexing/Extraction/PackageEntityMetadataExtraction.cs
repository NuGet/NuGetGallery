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

                AddString("id", package.PackageRegistration.Id);
                AddString("version", package.NormalizedVersion);
                AddString("originalVersion", package.Version);
                AddString("title", package.Title);
                AddString("description", package.Description);
                AddString("summary", package.Summary);
                AddString("tags", package.Tags);
                AddString("authors", package.FlattenedAuthors);

                AddString("listed", package.Listed.ToString());
                AddString("created", package.Created.ToString("O"));
                AddString("published", package.Published.ToString("O"));
                AddString("lastEdited", package.LastEdited?.ToString("O"));

                AddString("iconUrl", package.IconUrl);
                AddString("projectUrl", package.ProjectUrl);
                AddString("minClientVersion", package.MinClientVersion);
                AddString("releaseNotes", package.ReleaseNotes);
                AddString("copyright", package.Copyright);
                AddString("language", package.Language);
                AddString("licenseUrl", package.LicenseUrl);
                AddString("packageHash", package.Hash);
                AddString("packageHashAlgorithm", package.HashAlgorithm);
                AddString("packageSize", package.PackageFileSize.ToString());
                AddString("requiresLicenseAcceptance", package.RequiresLicenseAcceptance.ToString());

                AddString("flattenedDependencies", package.FlattenedDependencies);
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
                AddString("supportedFrameworks", flattened);
            }
        }
    }
}