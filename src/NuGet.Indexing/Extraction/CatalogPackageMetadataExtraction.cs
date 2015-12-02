// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

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
            private Dictionary<string, string> _metadata;

            public IDictionary<string, string> Extract(JObject catalog)
            {
                _catalog = catalog;
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
                var dependencyGroups = _catalog["dependencyGroups"];
                if (dependencyGroups == null)
                {
                    return;
                }

                if (!(dependencyGroups is JArray))
                {
                    dependencyGroups = new JArray(dependencyGroups);
                }

                var builder = new StringBuilder();
                foreach (var group in dependencyGroups)
                {
                    var dependencies = group["dependencies"];
                    if (dependencies == null)
                    {
                        continue;
                    }

                    var targetFramework = (string)group["targetFramework"];
                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var parsedTargetFramework = VersionUtility.ParseFrameworkName(targetFramework);
                        targetFramework = VersionUtility.GetShortFrameworkName(parsedTargetFramework);
                    }

                    foreach (var dependency in dependencies)
                    {
                        string id = (string)dependency["id"];
                        string range = (string)dependency["range"];
                        if (!string.IsNullOrWhiteSpace(range))
                        {
                            var parsedRange = VersionUtility.ParseVersionSpec(range);
                            range = parsedRange.ToString();
                        }

                        if (builder.Length > 0)
                        {
                            builder.Append("|");
                        }

                        builder.Append(id);
                        builder.Append(":");
                        builder.Append(range);
                        if (!string.IsNullOrWhiteSpace(targetFramework))
                        {
                            builder.Append(":");
                            builder.Append(targetFramework);
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
                var frameworkAssemblyGroups = _catalog["frameworkAssemblyGroup"];
                if (frameworkAssemblyGroups == null)
                {
                    return;
                }

                if (!(frameworkAssemblyGroups is JArray))
                {
                    frameworkAssemblyGroups = new JArray(frameworkAssemblyGroups);
                }

                var supportedFrameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var builder = new StringBuilder();
                foreach (var group in frameworkAssemblyGroups)
                {
                    var frameworkNameList = (string)group["targetFramework"];
                    if (string.IsNullOrWhiteSpace(frameworkNameList))
                    {
                        continue;
                    }

                    var frameworkNames = frameworkNameList
                        .Split(',')
                        .Select(t => t.Trim())
                        .Where(t => t.Length > 0);
                    foreach (var frameworkName in frameworkNames)
                    {
                        var parsedFrameworkName = VersionUtility.ParseFrameworkName(frameworkName);
                        var shortFrameworkName = VersionUtility.GetShortFrameworkName(parsedFrameworkName);
                        if (supportedFrameworks.Add(shortFrameworkName))
                        {
                            if (builder.Length > 0)
                            {
                                builder.Append("|");
                            }

                            builder.Append(shortFrameworkName);
                        }
                    }
                }

                // TODO: this is missing supported frameworks implied by the file paths
                // https://github.com/NuGet/NuGetGallery/blob/0609ea34e75fadec8920d4098814d648594dcfeb/src/NuGetGallery.Core/Packaging/Nupkg.cs#L233-L252

                if (builder.Length > 0)
                {
                    _metadata["supportedFrameworks"] = builder.ToString();
                }
            }
        }
    }
}