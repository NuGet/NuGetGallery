// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NuGet.Common;
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
            bool? developmentDependency = null,
            IEnumerable<PackageDependencyGroup> packageDependencyGroups = null,
            IEnumerable<ClientPackageType> packageTypes = null,
            bool isSymbolPackage = false,
            RepositoryMetadata repositoryMetadata = null,
            Func<string> getCustomNodes = null,
            string licenseExpression = null,
            string licenseFilename = null,
            string iconFilename = null,
            string readmeFilename = null)
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
                        " + (developmentDependency.HasValue ? ("<developmentDependency>" + developmentDependency.Value + "</developmentDependency>") : string.Empty) +
                        "<authors>" + authors + @"</authors>
                        <owners>" + owners + @"</owners>
                        <language>" + (language ?? string.Empty) + @"</language>
                        <copyright>" + (copyright ?? string.Empty) + @"</copyright>
                        <releaseNotes>" + (releaseNotes ?? string.Empty) + @"</releaseNotes>
                        <licenseUrl>" + (licenseUrl?.AbsoluteUri ?? string.Empty) + @"</licenseUrl>
                        " + WriteLicense(licenseExpression, licenseFilename) + @"
                        " + WriteIcon(iconFilename) + @"
                        " + WriteReadme(readmeFilename) + @"
                        <projectUrl>" + (projectUrl?.ToString() ?? string.Empty) + @"</projectUrl>
                        <iconUrl>" + (iconUrl?.ToString() ?? string.Empty) + @"</iconUrl>
                        <packageTypes>" + WritePackageTypes(packageTypes) + @"</packageTypes>
                        <dependencies>" + WriteDependencies(packageDependencyGroups) + @"</dependencies>
                        " + WriteRepositoryMetadata(repositoryMetadata) + @"
                        " + (getCustomNodes != null ? getCustomNodes() : "") + @"
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

        private static string WriteLicense(string licenseExpression, string licenseFilename)
        {
            var stringBuilder = new StringBuilder();
            if (licenseExpression != null)
            {
                stringBuilder.Append($"<license type='expression'>{licenseExpression}</license>");
            }

            if (licenseFilename != null)
            {
                stringBuilder.Append($"<license type='file'>{licenseFilename}</license>");
            }

            return stringBuilder.ToString();
        }

        private static string WriteIcon(string iconFilename)
        {
            if (iconFilename != null)
            {
                return $"<icon>{iconFilename}</icon>";
            }

            return string.Empty;
        }

        private static string WriteReadme(string readmeFilename)
        {
            if (readmeFilename != null)
            {
                return $"<readme>{readmeFilename}</readme>";
            }

            return string.Empty;
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
            foreach (var packageType in packageTypes)
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
                builder.Append('>');

                foreach (var packageDependency in packageDependencyGroup.Packages)
                {
                    builder.AppendFormat("<dependency id=\"{0}\"", packageDependency.Id);
                    if (packageDependency.VersionRange != null)
                    {
                        builder.AppendFormat(" version=\"{0}\"", packageDependency.VersionRange);
                    }
                    builder.Append('>');
                    builder.Append("</dependency>");
                }

                builder.Append("</group>");
            }

            return builder.ToString();
        }

        public static MemoryStream CreateTestPackageStream(
            string id = "theId",
            string version = "1.0.42",
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
            bool? developmentDependency = null,
            IEnumerable<PackageDependencyGroup> packageDependencyGroups = null,
            IEnumerable<ClientPackageType> packageTypes = null,
            RepositoryMetadata repositoryMetadata = null,
            Action<ZipArchive> populatePackage = null,
            bool isSymbolPackage = false,
            int? desiredTotalEntryCount = null,
            Func<string> getCustomNuspecNodes = null,
            string licenseExpression = null,
            string licenseFilename = null,
            byte[] licenseFileContents = null,
            string iconFilename = null,
            byte[] iconFileContents = null,
            string readmeFilename = null,
            byte[] readmeFileContents = null)
        {
            return CreateTestPackageStream(packageArchive =>
            {
                var nuspecEntry = packageArchive.CreateEntry(id + ".nuspec", CompressionLevel.Fastest);
                using (var stream = nuspecEntry.Open())
                {
                    WriteNuspec(stream, true, id, version, title, summary, authors, owners, description, tags, language,
                        copyright, releaseNotes, minClientVersion, licenseUrl, projectUrl, iconUrl,
                        requireLicenseAcceptance, developmentDependency, packageDependencyGroups, packageTypes, isSymbolPackage, repositoryMetadata,
                        getCustomNuspecNodes, licenseExpression, licenseFilename, iconFilename, readmeFilename);
                }

                licenseFilename = AddBinaryFile(packageArchive, licenseFilename, licenseFileContents);
                iconFilename = AddBinaryFile(packageArchive, iconFilename, iconFileContents);
                readmeFilename = AddBinaryFile(packageArchive, readmeFilename, readmeFileContents);

                if (populatePackage != null)
                {
                    populatePackage(packageArchive);
                }
            }, desiredTotalEntryCount);
        }

        private static string AddBinaryFile(ZipArchive archive, string filename, byte[] fileContents)
        {
            if (fileContents != null && filename != null)
            {
                // enforce directory separators the same way as the client (see PackageArchiveReader.GetStream)
                filename = PathUtility.StripLeadingDirectorySeparators(filename);
                var fileEntry = archive.CreateEntry(filename, CompressionLevel.Fastest);
                using (var fileStream = fileEntry.Open())
                {
                    fileStream.Write(fileContents, 0, fileContents.Length);
                }
            }

            return filename;
        }

        public static MemoryStream CreateTestSymbolPackageStream(string id = "theId", string version = "1.0.42", Action<ZipArchive> populatePackage = null)
        {
            var packageTypes = new List<ClientPackageType>();
            packageTypes.Add(new ClientPackageType(name: "SymbolsPackage", version: ClientPackageType.EmptyVersion));
            return CreateTestPackageStream(id,
                version,
                packageTypes: packageTypes,
                populatePackage: populatePackage,
                isSymbolPackage: true);
        }

        public static MemoryStream CreateTestPackageStreamFromNuspec(string id, string nuspec, Action<ZipArchive> populatePackage = null)
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

        public static MemoryStream CreateTestPackageStream(Action<ZipArchive> populatePackage, int? desiredTotalEntryCount = null)
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