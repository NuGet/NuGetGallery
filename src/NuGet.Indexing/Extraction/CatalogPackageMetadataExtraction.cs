// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public static class CatalogPackageMetadataExtraction
    {
        public static IDictionary<string, string> MakePackageMetadata(JObject catalogItem)
        {
            var extractor = new Extractor();
            return extractor.Extract(catalogItem);
        }

        private class Extractor
        {
            private JObject _catalog;
            private CatalogPackageReader _reader;
            private Dictionary<string, string> _metadata;

            public IDictionary<string, string> Extract(JObject catalog)
            {
                _catalog = catalog;
                _reader = new CatalogPackageReader(_catalog);
                _metadata = new Dictionary<string, string>();

                AddString("id");
                AddString("version");
                AddString("verbatimVersion", "originalVersion");
                AddString("title");
                AddString("description");
                AddString("summary");
                AddString("authors");
                AddStringArray("tags");

                AddListed();
                AddString("created");
                AddString("published");
                AddString("lastEdited");

                AddString("iconUrl");
                AddString("projectUrl");
                AddString("minClientVersion");
                AddString("releaseNotes");
                AddString("copyright");
                AddString("language");
                AddString("licenseUrl");
                AddString("packageHash");
                AddString("packageHashAlgorithm");
                AddString("packageSize");
                AddString("requiresLicenseAcceptance");

                
                AddFlattenedDependencies();
                AddSupportedFrameworks();

                return _metadata;
            }

            private void AddString(string source, string destination = null)
            {
                var value = _catalog[source];
                if (value == null)
                {
                    return;
                }

                _metadata[destination ?? source] = (string)value;
            }

            private void AddStringArray(string source, string destination = null)
            {
                var value = _catalog[source];
                if (value == null)
                {
                    return;
                }

                string joined = string.Join(" ", value.Select(t => (string)t));
                _metadata[destination ?? source] = joined;
            }

            private void AddListed()
            {
                var listed = (string)_catalog["listed"];
                var published = _catalog["published"];
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

                _metadata["listed"] = listed;
            }

            private void AddFlattenedDependencies()
            {
                var dependencies = _reader
                    .GetPackageDependencies()
                    .SelectMany(g => g.Packages.Select(p => new { g.TargetFramework, Package = p }));
                
                var builder = new StringBuilder();
                foreach (var dependency in dependencies)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append("|");
                    }

                    builder.Append(dependency.Package.Id);
                    builder.Append(":");
                    if (!dependency.Package.VersionRange.Equals(VersionRange.All))
                    {
                        builder.Append(dependency.Package.VersionRange?.ToString("S", new VersionRangeFormatter()));
                    }
                    
                    if (!SpecialFrameworks.Contains(dependency.TargetFramework))
                    {
                        try
                        {
                            builder.Append(":");
                            builder.Append(dependency.TargetFramework?.GetShortFolderName());
                        }
                        catch (FrameworkException)
                        {
                            // ignoring FrameworkException on purpose - we don't want the job crashing
                            // whenever someone uploads an unsupported framework
                        }
                    }
                }

                if (builder.Length > 0)
                {
                    _metadata["flattenedDependencies"] = builder.ToString();
                }
            }

            private void AddSupportedFrameworks()
            {
                var supportedFrameworks = _reader
                    .GetSupportedFrameworks()
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
                    _metadata["supportedFrameworks"] = string.Join("|", supportedFrameworks);
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