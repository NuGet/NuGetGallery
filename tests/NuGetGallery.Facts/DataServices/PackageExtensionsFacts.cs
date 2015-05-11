// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.DataServices
{
    public class PackageExtensionsFacts
    {
        public class TheProjectV2FeedPackageMethod
        {
            [Fact]
            public void MapsBasicPackagePropertiesCorrectly()
            {
                // Arrange
                var packages = new List<Package>()
                {
                    CreateFakeBasePackage()
                }.AsQueryable();

                // Act
                var projected = PackageExtensions.ProjectV2FeedPackage(
                    packages,
                    siteRoot: "http://nuget.org",
                    includeLicenseReport: true).ToList();

                // Assert
                var actual = projected.Single();
                Assert.Equal("Hitchhikers.Guide", actual.Id);
                Assert.Equal(420000, actual.DownloadCount);
                Assert.Equal("04.02-harmless", actual.Version);
                Assert.Equal("4.2.0-harmless", actual.NormalizedVersion);
                Assert.Equal("Life, The Universe, Everything", actual.Authors);
                Assert.Equal("Megadodo Publications", actual.Copyright);
                Assert.Equal(new DateTime(1971, 4, 2), actual.Created);
                Assert.Equal("A|B|C", actual.Dependencies);
                Assert.Equal("The standard repository for all knowledge and wisdom", actual.Description);
                Assert.Equal("http://notreal.example/foo.ico", actual.IconUrl);
                Assert.False(actual.IsLatestVersion);
                Assert.True(actual.IsAbsoluteLatestVersion);
                Assert.True(actual.IsPrerelease);
                Assert.Equal(new DateTime(2002, 4, 30), actual.LastUpdated);
                Assert.Equal("en-GB", actual.Language);
                Assert.Equal("abc123", actual.PackageHash);
                Assert.Equal("ROT13", actual.PackageHashAlgorithm);
                Assert.Equal(4200, actual.PackageSize);
                Assert.Equal("https://en.wikipedia.org/wiki/The_Hitchhiker%27s_Guide_to_the_Galaxy_(fictional)", actual.ProjectUrl);
                Assert.Equal("Mostly Harmless", actual.ReleaseNotes);
                Assert.True(actual.RequireLicenseAcceptance);
                Assert.Equal(new DateTime(1979, 10, 12), actual.Published);
                Assert.Equal("A truely remarkable book", actual.Summary);
                Assert.Equal("Guide, Harmless, Mostly", actual.Tags);
                Assert.Equal("The Hitchhiker's Guide to the Galaxy", actual.Title);
                Assert.Equal(421, actual.VersionDownloadCount);
                Assert.Equal("4.2.8", actual.MinClientVersion);
                Assert.Equal("https://blarg/", actual.LicenseUrl);
                Assert.Equal("Foo Public License, Some Other License", actual.LicenseNames);
                Assert.Equal("https://reporturl", actual.LicenseReportUrl);
            }

            [Fact]
            public void InjectsGalleryUrlsCorrectly()
            {
                // Arrange
                var packages = new List<Package>()
                {
                    CreateFakeBasePackage()
                }.AsQueryable();

                // Act
                var projected = PackageExtensions.ProjectV2FeedPackage(
                    packages,
                    siteRoot: "http://nuget.org",
                    includeLicenseReport: true).ToList();

                // Assert
                var actual = projected.Single();
                Assert.Equal("http://nuget.org/packages/Hitchhikers.Guide/4.2.0-harmless", actual.GalleryDetailsUrl);
                Assert.Equal("http://nuget.org/package/ReportAbuse/Hitchhikers.Guide/4.2.0-harmless", actual.ReportAbuseUrl);
            }

            [Fact]
            public void InjectsDummyDateIfNotListed()
            {
                // Arrange
                var package = CreateFakeBasePackage();
                package.Listed = false;
                var packages = new List<Package>()
                {
                    package
                }.AsQueryable();

                // Act
                var projected = PackageExtensions.ProjectV2FeedPackage(
                    packages,
                    siteRoot: "http://nuget.org",
                    includeLicenseReport: true).ToList();

                // Assert
                var actual = projected.Single();
                Assert.Equal(PackageExtensions.UnpublishedDate, actual.Published);
            }

            [Fact]
            public void ReturnsNullLicenseReportInfoIfFeatureDisabled()
            {
                // Arrange
                var packages = new List<Package>()
                {
                    CreateFakeBasePackage()
                }.AsQueryable();

                // Act
                var projected = PackageExtensions.ProjectV2FeedPackage(
                    packages,
                    siteRoot: "http://nuget.org",
                    includeLicenseReport: false).ToList();

                // Assert
                var actual = projected.Single();
                Assert.Null(actual.LicenseNames);
                Assert.Null(actual.LicenseReportUrl);
            }
        }

        /// <summary>
        /// Creates a fake package with all of the data needed for ProjectV2FeedPackage provided
        /// </summary>
        public static Package CreateFakeBasePackage()
        {
            return new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Id = "Hitchhikers.Guide",
                    DownloadCount = 420000
                },
                Version = "04.02-harmless",
                NormalizedVersion = "4.2.0-harmless",
                FlattenedAuthors = "Life, The Universe, Everything",
                Copyright = "Megadodo Publications",
                Created = new DateTime(1971, 4, 2),
                FlattenedDependencies = "A|B|C",
                Description = "The standard repository for all knowledge and wisdom",
                IconUrl = "http://notreal.example/foo.ico",
                IsLatestStable = false,
                IsLatest = true,
                IsPrerelease = true,
                LastUpdated = new DateTime(2002, 4, 30),
                Language = "en-GB",
                Hash = "abc123",
                HashAlgorithm = "ROT13",
                PackageFileSize = 4200,
                ProjectUrl = "https://en.wikipedia.org/wiki/The_Hitchhiker%27s_Guide_to_the_Galaxy_(fictional)",
                ReleaseNotes = "Mostly Harmless",
                RequiresLicenseAcceptance = true,
                Published = new DateTime(1979, 10, 12),
                Summary = "A truely remarkable book",
                Tags = "Guide, Harmless, Mostly",
                Title = "The Hitchhiker's Guide to the Galaxy",
                DownloadCount = 421,
                MinClientVersion = "4.2.8",
                LicenseUrl = "https://blarg/",
                LicenseNames = "Foo Public License, Some Other License",
                LicenseReportUrl = "https://reporturl"
            };
        }
    }
}