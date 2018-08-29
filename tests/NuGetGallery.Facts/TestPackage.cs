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
using ClientPackageType = NuGet.Packaging.Core.PackageType;

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
            IEnumerable<ClientPackageType> packageTypes = null,
            bool isSymbolPackage = false,
            RepositoryMetadata repositoryMetadata = null)
        {
            var fullNuspec = (@"<?xml version=""1.0""?>
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
                        " + WriteRepositoryMetadata(repositoryMetadata) + @"
                    </metadata>
                </package>");

            var symbolNuspec = (@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                        <metadata" + (!string.IsNullOrEmpty(minClientVersion) ? @" minClientVersion=""" + minClientVersion + @"""" : string.Empty) + @">
                            <id>" + id + @"</id>
                            <version>" + version + @"</version>
                            <description>" + description + @"</description>
                            <requireLicenseAcceptance>" + requireLicenseAcceptance + @"</requireLicenseAcceptance>
                            <packageTypes>" + WritePackageTypes(packageTypes) + @"</packageTypes>
                        </metadata>
                    </package>");

            using (var streamWriter = new StreamWriter(stream, new UTF8Encoding(false, true), 1024, leaveStreamOpen))
            {
                streamWriter.WriteLine(isSymbolPackage ? symbolNuspec : fullNuspec);
            }
        }

        private static string WriteRepositoryMetadata(RepositoryMetadata repositoryMetadata)
        {
            return repositoryMetadata == null
                ? string.Empty
                : "<repository type=\"" + repositoryMetadata.Type + "\" " + 
                                "url =\"" + repositoryMetadata.Url + "\" " + 
                                "commit=\"" + repositoryMetadata.Commit + "\" " + 
                                "branch=\"" + repositoryMetadata.Branch + "\"/>";
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
            IEnumerable<ClientPackageType> packageTypes = null,
            RepositoryMetadata repositoryMetadata = null,
            Action<ZipArchive> populatePackage = null,
            bool isSymbolPackage = false,
            int? desiredTotalEntryCount = null)
        {
            return CreateTestPackageStream(packageArchive =>
            {
                var nuspecEntry = packageArchive.CreateEntry(id + ".nuspec", CompressionLevel.Fastest);
                using (var stream = nuspecEntry.Open())
                {
                    WriteNuspec(stream, true, id, version, title, summary, authors, owners, description, tags, language,
                        copyright, releaseNotes, minClientVersion, licenseUrl, projectUrl, iconUrl,
                        requireLicenseAcceptance, packageDependencyGroups, packageTypes, isSymbolPackage, repositoryMetadata);
                }

                if (populatePackage != null)
                {
                    populatePackage(packageArchive);
                }
            }, desiredTotalEntryCount);
        }

        public static Stream CreateTestSymbolPackageStream(string id, string version, Action<ZipArchive> populatePackage = null)
        {
            var packageTypes = new List<ClientPackageType>();
            packageTypes.Add(new ClientPackageType(name: "SymbolsPackage", version: ClientPackageType.EmptyVersion));
            return CreateTestPackageStream(id, 
                version, 
                packageTypes: packageTypes, 
                populatePackage: populatePackage, 
                isSymbolPackage: true);
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

        public static Stream CreateTestPackageStream(Action<ZipArchive> populatePackage, int? desiredTotalEntryCount = null)
        {
            var packageStream = new MemoryStream();
            using (var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                if (populatePackage != null)
                {
                    populatePackage(packageArchive);
                }
            }

            if (desiredTotalEntryCount.HasValue)
            {
                int packageEntryCount;

                using (var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    packageEntryCount = packageArchive.Entries.Count;
                }

                if (desiredTotalEntryCount.Value < packageEntryCount)
                {
                    throw new ArgumentException(
                        $"The desired count ({desiredTotalEntryCount.Value}) of package entries is less than the actual count ({packageEntryCount}) of package entries.",
                        nameof(desiredTotalEntryCount));
                }

                using (var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                {
                    while (packageEntryCount < desiredTotalEntryCount.Value)
                    {
                        packageArchive.CreateEntry(Guid.NewGuid().ToString());

                        ++packageEntryCount;
                    }
                }
            }

            packageStream.Position = 0;

            return packageStream;
        }
    }
}