// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Services.Entities;
using NuGet.Versioning;
using PackageDependency = NuGet.Services.Entities.PackageDependency;

namespace NuGetGallery.TestUtils
{
    public static class PackageServiceUtility
    {
        private const string _packageHashForTests = "NzMzMS1QNENLNEczSDQ1SA==";

        public static Package CreateTestPackage(string id = null)
        {
            var packageRegistration = new PackageRegistration();
            packageRegistration.Id = string.IsNullOrEmpty(id) ? "test" : id;

            var framework = new PackageFramework();
            var author = new PackageAuthor { Name = "maarten" };
            var dependency = new PackageDependency { Id = "other", VersionSpec = "1.0.0" };

            var package = new Package
            {
                Key = 123,
                PackageRegistration = packageRegistration,
                Version = "1.0.0",
                Hash = _packageHashForTests,
                SupportedFrameworks = new List<PackageFramework>
                {
                    framework
                },
                FlattenedAuthors = "maarten",
#pragma warning disable 0618
                Authors = new List<PackageAuthor>
                {
                    author
                },
#pragma warning restore 0618
                Dependencies = new List<PackageDependency>
                {
                    dependency
                },
                User = new User("test")
            };

            packageRegistration.Packages.Add(package);

            return package;
        }

        public static Mock<TestPackageReader> CreateNuGetPackage(
            string id = "theId",
            string version = "01.0.42.0",
            string title = "theTitle",
            string summary = "theSummary",
            string authors = "theFirstAuthor, theSecondAuthor",
            string owners = "Package owners",
            string description = "theDescription",
            string tags = "theTags",
            string language = null,
            string copyright = "theCopyright",
            string releaseNotes = "theReleaseNotes",
            string minClientVersion = null,
            Uri licenseUrl = null,
            Uri projectUrl = null,
            Uri iconUrl = null,
            bool requireLicenseAcceptance = true,
            bool? developmentDependency = true,
            IEnumerable<PackageDependencyGroup> packageDependencyGroups = null,
            IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes = null,
            RepositoryMetadata repositoryMetadata = null,
            bool isSigned = false,
            int? desiredTotalEntryCount = null,
            Func<string> getCustomNuspecNodes = null,
            string licenseExpression = null,
            string licenseFilename = null,
            byte[] licenseFileContents = null,
            string readmeFilename = null,
            byte[] readmeFileContents = null)
        {
            var testPackage = CreateNuGetPackageStream(
                    id:id, 
                    version: version, 
                    title: title,
                    summary:summary,
                    authors:authors,
                    owners: owners,
                    description: description, 
                    tags: tags,
                    language: language,
                    copyright: copyright,
                    releaseNotes: releaseNotes,
                    minClientVersion: minClientVersion,
                    licenseUrl: licenseUrl,
                    projectUrl: projectUrl,
                    iconUrl: iconUrl,
                    requireLicenseAcceptance: requireLicenseAcceptance,
                    developmentDependency: developmentDependency,
                    packageDependencyGroups: packageDependencyGroups,
                    packageTypes: packageTypes,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFilename,
                    licenseFileContents: licenseFileContents,
                    readmeFilename: readmeFilename,
                    readmeFileContents: readmeFileContents);

            return CreateNuGetPackage(testPackage);
        }

        public static Mock<TestPackageReader> CreateNuGetPackage(Stream testPackage)
        {
            var mock = new Mock<TestPackageReader>(testPackage);
            mock.CallBase = true;
            return mock;
        }

        public static MemoryStream CreateNuGetPackageStream(string id = "theId",
            string version = "01.0.42.0",
            string title = "theTitle",
            string summary = "theSummary",
            string authors = "theFirstAuthor, theSecondAuthor",
            string owners = "Package owners",
            string description = "theDescription",
            string tags = "theTags",
            string language = null,
            string copyright = "theCopyright",
            string releaseNotes = "theReleaseNotes",
            string minClientVersion = null,
            Uri licenseUrl = null,
            Uri projectUrl = null,
            Uri iconUrl = null,
            bool requireLicenseAcceptance = true,
            bool? developmentDependency = true,
            IEnumerable<PackageDependencyGroup> packageDependencyGroups = null,
            IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes = null,
            RepositoryMetadata repositoryMetadata = null,
            bool isSigned = false,
            int? desiredTotalEntryCount = null,
            Func<string> getCustomNuspecNodes = null,
            string licenseExpression = null,
            string licenseFilename = null,
            byte[] licenseFileContents = null,
            string iconFilename = null,
            byte[] iconFileBinaryContents = null,
            string readmeFilename = null,
            byte[] readmeFileContents = null,
            IReadOnlyList<string> entryNames = null)
        {
            if (packageDependencyGroups == null)
            {
                packageDependencyGroups = new[]
                {
                    new PackageDependencyGroup(
                        new NuGetFramework("net40"),
                        new[]
                        {
                            new NuGet.Packaging.Core.PackageDependency(
                                "theFirstDependency",
                                VersionRange.Parse("[1.0.0, 2.0.0)")),

                            new NuGet.Packaging.Core.PackageDependency(
                                "theSecondDependency",
                                VersionRange.Parse("[1.0]")),

                            new NuGet.Packaging.Core.PackageDependency(
                                "theThirdDependency")
                        }),

                    new PackageDependencyGroup(
                        new NuGetFramework("net35"),
                        new[]
                        {
                            new NuGet.Packaging.Core.PackageDependency(
                                "theFourthDependency",
                                VersionRange.Parse("[1.0]"))
                        })
                };
            }

            if (packageTypes == null)
            {
                packageTypes = new[]
                {
                    new NuGet.Packaging.Core.PackageType("dependency", new Version("1.0.0")),
                    new NuGet.Packaging.Core.PackageType("DotNetCliTool", new Version("2.1.1"))
                };
            }

            return TestPackage.CreateTestPackageStream(
                id, version, title, summary, authors, owners,
                description, tags, language, copyright, releaseNotes,
                minClientVersion, licenseUrl, projectUrl, iconUrl,
                requireLicenseAcceptance, developmentDependency,
                packageDependencyGroups, packageTypes, repositoryMetadata,
                archive =>
                {
                    if (isSigned)
                    {
                        var entry = archive.CreateEntry(SigningSpecifications.V1.SignaturePath);
                        using (var stream = entry.Open())
                        using (var writer = new StreamWriter(stream))
                        {
                            writer.Write("Fake signature file.");
                        }
                    }

                    if (entryNames != null)
                    {
                        foreach(var entryName in entryNames)
                        {
                            WriteEntry(archive, entryName);
                        }
                    }
                }, 
                desiredTotalEntryCount: desiredTotalEntryCount,
                getCustomNuspecNodes: getCustomNuspecNodes,
                licenseExpression: licenseExpression,
                licenseFilename: licenseFilename,
                licenseFileContents: licenseFileContents,
                iconFilename: iconFilename,
                iconFileContents: iconFileBinaryContents,
                readmeFilename: readmeFilename,
                readmeFileContents: readmeFileContents);
        }

        private static void WriteEntry(ZipArchive archive, string entryName)
        {
            var entry = archive.CreateEntry(entryName);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(entryName);
            }
        }

        public static PackageArchiveReader CreateArchiveReader(Stream stream)
        {
            if (stream == null)
            {
                stream = TestPackage.CreateTestPackageStream("theId", "1.0.42");
            }

            return new PackageArchiveReader(stream, leaveStreamOpen: true);
        }
    }
}
