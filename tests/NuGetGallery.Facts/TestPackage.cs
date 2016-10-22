// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGetGallery
{
    public static class TestPackage
    {
        public static void WriteNuspec(
            Stream stream,
            bool leaveStreamOpen,
            string id,
            string version,
            string title = "Package Id",
            string summary = "Package Summary",
            string authors = "Package author",
            string owners = "Package owners",
            string description = "Package Description",
            string tags = "Package tags",
            string language = null,
            string copyright = null,
            string releaseNotes = null,
            string minClientVersion = null,
            Uri licenseUrl = null,
            Uri projectUrl = null,
            Uri iconUrl = null,
            bool requireLicenseAcceptance = false,
            IEnumerable<PackageDependencyGroup> packageDependencyGroups = null,
            IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes = null)
        {
            using (var streamWriter = new StreamWriter(stream, new UTF8Encoding(false, true), 1024, leaveStreamOpen))
            {
                streamWriter.WriteLine(@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                        <metadata" + (!string.IsNullOrEmpty(minClientVersion) ? @" minClientVersion=""" + minClientVersion + @"""" : string.Empty) + @">
                            <id>" + id + @"</id>
                            <version>" + version + @"</version>
                            <title>" + title + @"</title>
                            <summary>" + summary + @"</summary>
                            <description>" + description + @"</description>
                            <tags>" + tags + @"</tags>
                            <requireLicenseAcceptance>" + requireLicenseAcceptance + @"</requireLicenseAcceptance>
                            <authors>" + authors + @"</authors>
                            <owners>" + owners + @"</owners>
                            <language>" + (language ?? string.Empty) + @"</language>
                            <copyright>" + (copyright ?? string.Empty) + @"</copyright>
                            <releaseNotes>" + (releaseNotes ?? string.Empty) + @"</releaseNotes>
                            <licenseUrl>" + (licenseUrl?.ToString() ?? string.Empty) + @"</licenseUrl>
                            <projectUrl>" + (projectUrl?.ToString() ?? string.Empty) + @"</projectUrl>
                            <iconUrl>" + (iconUrl?.ToString() ?? string.Empty) + @"</iconUrl>
                            <packageTypes>" + WritePackageTypes(packageTypes) + @"</packageTypes>
                            <dependencies>" + WriteDependencies(packageDependencyGroups) + @"</dependencies>
                        </metadata>
                    </package>");
            }
        }

        private static string WritePackageTypes(IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes)
        {
            if (packageTypes == null || !packageTypes.Any())
            {
                return string.Empty;
            }

            var output = new StringBuilder();
            foreach(var packageType in packageTypes)
            {
                output.Append("<packageType");
                if (packageType.Name != null)
                {
                    output.AppendFormat(" name=\"{0}\"", packageType.Name);
                }

                if (packageType.Version != null)
                {
                    output.AppendFormat(" version=\"{0}\"", packageType.Version.ToString());
                }

                output.Append("/>");
            }
            return output.ToString();
        }

        private static string WriteDependencies(IEnumerable<PackageDependencyGroup> packageDependencyGroups)
        {
            if (packageDependencyGroups == null || !packageDependencyGroups.Any())
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var packageDependencyGroup in packageDependencyGroups)
            {
                builder.Append("<group");
                if (packageDependencyGroup.TargetFramework != null)
                {
                    builder.AppendFormat(" targetFramework=\"{0}\"", packageDependencyGroup.TargetFramework.GetShortFolderName());
                }
                builder.Append(">");

                foreach (var packageDependency in packageDependencyGroup.Packages)
                {
                    builder.AppendFormat("<dependency id=\"{0}\"", packageDependency.Id);
                    if (packageDependency.VersionRange != null)
                    {
                        builder.AppendFormat(" version=\"{0}\"", packageDependency.VersionRange);
                    }
                    builder.Append(">");
                    builder.Append("</dependency>");
                }

                builder.Append("</group>");
            }

            return builder.ToString();
        }

        public static Stream CreateTestPackageStream(
            string id,
            string version,
            string title = "Package Id",
            string summary = "Package Summary",
            string authors = "Package author",
            string owners = "Package owners",
            string description = "Package Description",
            string tags = "Package tags",
            string language = null,
            string copyright = null,
            string releaseNotes = null,
            string minClientVersion = null,
            Uri licenseUrl = null,
            Uri projectUrl = null,
            Uri iconUrl = null,
            bool requireLicenseAcceptance = false,
            IEnumerable<PackageDependencyGroup> packageDependencyGroups = null,
            IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes = null,
            Action<ZipArchive> populatePackage = null)
        {
            return CreateTestPackageStream(packageArchive =>
            {
                var nuspecEntry = packageArchive.CreateEntry(id + ".nuspec", CompressionLevel.Fastest);
                using (var stream = nuspecEntry.Open())
                {
                    WriteNuspec(stream, true, id, version, title, summary, authors, owners, description, tags, language,
                        copyright, releaseNotes, minClientVersion, licenseUrl, projectUrl, iconUrl,
                        requireLicenseAcceptance, packageDependencyGroups, packageTypes);
                }

                if (populatePackage != null)
                {
                    populatePackage(packageArchive);
                }
            });
        }

        public static Stream CreateTestPackageStreamFromNuspec(string id, string nuspec, Action<ZipArchive> populatePackage = null)
        {
            return CreateTestPackageStream(packageArchive =>
            {
                var nuspecEntry = packageArchive.CreateEntry(id + ".nuspec", CompressionLevel.Fastest);
                using (var streamWriter = new StreamWriter(nuspecEntry.Open()))
                {
                    streamWriter.WriteLine(nuspec);
                }

                if (populatePackage != null)
                {
                    populatePackage(packageArchive);
                }
            });
        }

        public static Stream CreateTestPackageStream(Action<ZipArchive> populatePackage)
        {
            var packageStream = new MemoryStream();
            using (var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Create, true))
            {
                if (populatePackage != null)
                {
                    populatePackage(packageArchive);
                }
            }

            packageStream.Position = 0;

            return packageStream;
        }
    }
}