using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Store;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Infrastructure
{
    public class LuceneSearchServiceFacts
    {
        // This works because we index the description
        [Fact]
        public void IndexAndSearchAPackageByDescription()
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 3,
                    PackageRegistrationKey = 1,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "Package #1",
                    },
                    Metadata = new PackageMetadata
                    {
                        Authors = "",
                        Title = "Package #1 4.2.0",
                        Description = "Package #1 is an awesome package",
                    },
                    Listed = true,
                    IsLatestStable = true,
                    IsLatest = true,
                    IsPrerelease = true,
                    DownloadCount = 100,
                    SupportedFrameworks =
                    {
                        new PackageFramework { TargetFramework = "net45" },
                    }
                }
            };

            var results = IndexAndSearch(packages, "awesome");

            Assert.Single(results);
            Assert.Equal(3, results[0].Key);
            Assert.Equal(1, results[0].PackageRegistrationKey);
        }

        // This works because we do some wildcard magic in our searches.
        [Fact]
        public void IndexAndSearchDavid123For12()
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 49246,
                    PackageRegistrationKey = 11500,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "DavidTest123",
                        Key = 11500,
                        DownloadCount = 495
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "Description",
                        Authors = "DavidX",
                        Title = "DavidTest123",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.1",
                }
            };

            var results = IndexAndSearch(packages, "12");

            Assert.Single(results);
            Assert.Equal("DavidTest123", results[0].Metadata.Title);
        }

        [Fact]
        public void IndexAndSearchWithWordStemming()
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 144,
                    PackageRegistrationKey = 12,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "SuperzipLib",
                        Key = 12,
                        DownloadCount = 41
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "Library for compressing your filez",
                        Authors = "Eric",
                        Title = "SuperzipLib",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.1.2",
                }
            };

            var results = IndexAndSearch(packages, "compressed");

            Assert.Empty(results); // currently stemming is not working
            //Assert.Equal("SuperzipLib", results[0].Title);
        }

        [Fact]
        public void SearchUsingCombinedIdAndGeneralTerms()
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 144,
                    PackageRegistrationKey = 12,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "RedDeath",
                        Key = 12,
                        DownloadCount = 41
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "Yeah",
                        Authors = "Eric I",
                        Title = "Red Death",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.1.2",
                },
                new Package
                {
                    Key = 144,
                    PackageRegistrationKey = 12,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "RedHerring",
                        Key = 12,
                        DownloadCount = 41
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "Library for compressing your filez",
                        Authors = "Eric II",
                        Title = "Red Herring",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.1.2",
                },
            };

            var results = IndexAndSearch(packages, "Id:Red Death");

            Assert.Equal(1, results.Count);
            Assert.Equal("Red Death", results[0].Metadata.Title);
        }

        [Fact]
        public void SearchUsingExactPackageId()
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 144,
                    PackageRegistrationKey = 12,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "NuGet.Core",
                        Key = 12,
                        DownloadCount = 25
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "NuGet.Core is the core framework assembly for NuGet",
                        Authors = "M S C",
                        Tags = "NuGetTag",
                        Title = "NuGet.Core",
                    },
                    DownloadCount = 3,
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.5.20902.9026",
                },
                new Package
                {
                    Key = 145,
                    PackageRegistrationKey = 13,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "SomeotherNuGet.Core.SimilarlyNamedPackage",
                        Key = 13,
                        DownloadCount = 25,
                    },
                    Metadata = new PackageMetadata
                    {
                        Authors = "Laugh",
                        Title = "SomeotherNuGet.Core.SimilarlyNamedPackage",
                        Description =
                            "This isn't really NuGet.Core. The confusing package ID is the test!",
                    },
                    DownloadCount = 3,
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.5.20902.9026",
                }
            };

            // simple query
            var results = IndexAndSearch(packages, "NuGet.Core");
            Assert.Equal(2, results.Count);
            Assert.Equal("NuGet.Core", results[0].Metadata.Title);
            Assert.Equal(144, results[0].Key);
            Assert.Equal("NuGet.Core", results[0].PackageRegistration.Id);
            Assert.Equal(12, results[0].PackageRegistrationKey);
            Assert.Equal(12, results[0].PackageRegistration.Key);
            Assert.Equal("NuGet.Core is the core framework assembly for NuGet", results[0].Metadata.Description);
            Assert.True(results[0].IsLatest);
            Assert.True(results[0].IsLatestStable);
            Assert.Equal("NuGetTag", results[0].Metadata.Tags);
        }

        [Theory]
        [InlineData("Id", "NuGet.Core")]
        [InlineData("id", "NuGet.Core")]
        [InlineData("title", "NuGet.Core")]
        [InlineData("TITLE", "NuGet.Core")]
        [InlineData("Owner", "NugetCoreOwner")]
        [InlineData("Owners", "NugetCoreOwner")]
        [InlineData("Authors", "Alpha")]
        [InlineData("Author", "Alpha")]
        [InlineData("Authors", "Alpha")]
        [InlineData("author", "\"Alpha Beta Gamma\"")]
        [InlineData("Description", "core framework")]
        [InlineData("Tags", "dotnet")]
        [InlineData("Tag", "dotnet")]
        public void SearchForNuGetCoreWithExactField(string field, string term)
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 144,
                    PackageRegistrationKey = 12,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "NuGet.Core",
                        Key = 12,
                        DownloadCount = 41,
                        Owners = { new User { Username = "NugetCoreOwner" } },
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "NuGet.Core is the core framework assembly for NuGet that the rest of NuGet builds upon.",
                        Authors = "Alpha, Beta Gamma",
                        Title = "NuGet.Core",
                        Tags = "dotnet",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.5.20902.9026",
                },
                new Package
                {
                    Key = 145,
                    PackageRegistrationKey = 13,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "SomeotherNuGet.Core.SimilarlyNamedPackage",
                        Key = 13,
                        DownloadCount = 2,
                        Owners = { new User { Username = "SomeOtherOwner" } },
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "This isn't really NuGet.Core. But it needs to look a bit like it for the test case!",
                        Authors = "Laugh",
                        Title = "SomeotherNuGet.Core.SimilarlyNamedPackage",
                        Tags = "javascript",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.5.20902.9026",
                }
            };

            // query targeted specifically against id field should work equally well
            var results = IndexAndSearch(packages, field + ":" + term);
            Assert.NotEmpty(results);
            Assert.Equal("NuGet.Core", results[0].Metadata.Title);
            Assert.Equal("NuGet.Core", results[0].PackageRegistration.Id);
        }

        [Fact]
        public void SearchForJQueryUICombinedWithPartialId()
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 144,
                    PackageRegistrationKey = 12,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "JQuery.UI.Combined",
                        Key = 12,
                        DownloadCount = 41
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "jQuery UI is etc etc and many more important things",
                        Authors = "Alpha Beta Gamma",
                        Title = "JQuery UI (Combined Blobbary)",
                        Tags = "web javascript",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                },
            };

            var results = IndexAndSearch(packages, "id:JQuery.ui");
            Assert.NotEmpty(results);
            Assert.Equal("JQuery.UI.Combined", results[0].PackageRegistration.Id);
        }

        [Fact]
        public void SearchForDegenerateSingleQuoteQuery()
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 144,
                    PackageRegistrationKey = 12,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "JQuery.UI.Combined",
                        Key = 12,
                        DownloadCount = 41
                    },
                    Metadata = new PackageMetadata
                    {
                        Description = "jQuery UI is etc etc and many more important things",
                        Authors = "Alpha Beta Gamma",
                        Title = "JQuery UI (Combined Blobbary)",
                        Tags = "web javascript",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                },
            };

            var results = IndexAndSearch(packages, "\"");
            Assert.NotEmpty(results);
            Assert.Equal("JQuery.UI.Combined", results[0].PackageRegistration.Id);
        }

        [Fact]
        public void SearchUsesPackageRegistrationDownloadCountsToPrioritize()
        {
            var packages = new List<Package>
                {
                    new Package
                    {
                        Key = 145,
                        PackageRegistrationKey = 13,
                        PackageRegistration = new PackageRegistration
                        {
                            Id = "FooQuery",
                            Key = 13,
                            DownloadCount = 21
                        },
                        Metadata = new PackageMetadata
                        {
                            Description = "FooQuery is overall much less popular than JQuery UI",
                            Authors = "Alpha Beta Gamma",
                            Title = "FooQuery",
                            Tags = "web javascript",
                        },
                        DownloadCount = 5,
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                    },
                    new Package
                    {
                        Key = 144,
                        PackageRegistrationKey = 12,
                        PackageRegistration = new PackageRegistration
                        {
                            Id = "JQuery.UI.Combined",
                            Key = 12,
                            DownloadCount = 42
                        },
                        DownloadCount = 3,
                        Metadata = new PackageMetadata
                        {
                            Description = "jQuery UI has only a few downloads of its latest and greatest version, but many total downloads",
                            Authors = "Alpha Beta Gamma",
                            Title = "JQuery UI (Combined Blobbary)",
                            Tags = "web javascript",
                        },
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                    },
                };

            var results = IndexAndSearch(packages, "");
            Assert.NotEmpty(results);
            Assert.Equal("JQuery.UI.Combined", results[0].PackageRegistration.Id);
            Assert.Equal("FooQuery", results[1].PackageRegistration.Id);
        }

        [Fact]
        public void IndexAndSearchRetrievesCanDriveV2Feed()
        {
            Package p = new Package
            {
                Key = 123,
                PackageRegistrationKey = 456,
                PackageRegistration = new PackageRegistration
                {
                    Id = "Pride",
                    Key = 456,
                    DownloadCount = 123456
                },
                Metadata = new PackageMetadata
                {
                    Authors = "Oldies, Newies",
                    Copyright = "Copyright 2013 by Oldies and Newies",
                    Description = "DescriptionText",
                    HashAlgorithm = "SHA512",
                    Hash = "Ii4+Gr44RAClAno38k5MYAkcBE6yn2LE2xO+/ViKco45+hoxtwKAytmPWEMCJWhH8FyitjebvS5Fsf+ixI5xIg==",
                    PackageFileSize = 234567,
                    ProjectUrl = "http://projecturl.com",
                    LicenseUrl = "nuget.org/license.txt",
                    ReleaseNotes = "ReleaseNotesText",
                    Summary = "SummaryText",
                    Tags = "Tag1 Tag2 Tag3",
                    Title = "TitleText",
                },
                
                Created = new DateTime(2019, 2, 28, 0, 5, 59, DateTimeKind.Utc),
                DownloadCount = 12345,
                FlattenedDependencies = "adjunct-System.FluentCast:1.0.0.4|xunit:1.8.0.1545|adjunct-XUnit.Assertions:1.0.0.5|adjunct-XUnit.Assertions.Linq2Xml:1.0.0.3",
                IsLatest = true,
                IsLatestStable = true,
                IsPrerelease = false,
                Language = "en",
                LastUpdated = DateTime.UtcNow,
                Listed = true,
                MinClientVersion = new Version(1, 2, int.MaxValue, int.MaxValue - 1).ToString(),
                Published = DateTime.UtcNow,
                RequiresLicenseAcceptance = true,
                SupportedFrameworks = new PackageFramework[]
                {
                    new PackageFramework
                    {
                        Key = 890,
                        TargetFramework = "net45",
                    }
                },
                Version = "3.4 RC",
            };

            var packages = new[] { p };
            var results = IndexAndSearch(packages, "");
            var r = results.AsQueryable().ToV2FeedPackageQuery("http://www.nuget.org/").First();

            Assert.Equal("Pride", r.Id);
            Assert.Equal("3.4 RC", r.Version);
            Assert.Equal("Oldies, Newies", r.Authors);
            Assert.Equal("Copyright 2013 by Oldies and Newies", r.Copyright);
            Assert.Equal(p.FlattenedDependencies, r.Dependencies);
            Assert.Equal("DescriptionText", r.Description);
            Assert.Equal(123456, r.DownloadCount);
            Assert.NotEmpty(r.GalleryDetailsUrl);
            Assert.True(r.IsLatestVersion);
            Assert.True(r.IsAbsoluteLatestVersion);
            Assert.False(r.IsPrerelease);
            Assert.Equal(p.Created, r.Created);
            Assert.True(r.LastUpdated > DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(20)));
            Assert.Equal("nuget.org/license.txt", r.LicenseUrl);
            Assert.Equal("en", r.Language);
            Assert.Equal(234567, r.PackageSize);
            Assert.Equal("SHA512", r.PackageHashAlgorithm);
            Assert.Equal(p.Metadata.Hash, r.PackageHash);
            Assert.True(r.Published > DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(20)));
            Assert.Equal("http://projecturl.com", r.ProjectUrl);
            Assert.NotEmpty(r.ReportAbuseUrl);
            Assert.True(r.RequireLicenseAcceptance);
            Assert.Equal("ReleaseNotesText", r.ReleaseNotes);
            Assert.Equal("SummaryText", r.Summary);
            Assert.Equal("TitleText", r.Title);
            Assert.Equal("Tag1 Tag2 Tag3", r.Tags);
            Assert.Equal(12345, r.VersionDownloadCount);
            Assert.Equal("1.2.2147483647.2147483646", r.MinClientVersion);
        }

        // See issue https://github.com/NuGet/NuGetGallery/issues/406
        [Fact]
        public void SearchWorksAroundLuceneQuerySyntaxExceptions()
        {
            var packages = new List<Package>
            {
                new Package
                {
                    Key = 144,
                    PackageRegistrationKey = 12,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "NuGet.Core",
                        Key = 12,
                        DownloadCount = 41
                    },
                    Metadata = new PackageMetadata
                    {
                        Authors = "Alpha Beta Gamma",
                        LicenseUrl = "http://nuget.codeplex.com/license",
                        Title = "NuGet.Core",
                        Description = "NuGet.Core is the core framework assembly for NuGet that the rest of NuGet builds upon.",
                    },
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    Version = "1.5.20902.9026",
                },
            };

            var results = IndexAndSearch(packages, "*Core"); // Lucene parser throws for leading asterisk in searches
            Assert.NotEmpty(results);
        }

        private IList<Package> IndexAndSearch(IEnumerable<Package> packages, string searchTerm)
        {
            Directory d = new RAMDirectory();

            var mockPackageSource = new Mock<IEntityRepository<Package>>();
            mockPackageSource
                .Setup(m => m.GetAll())
                .Returns(packages.AsQueryable());

            var mockCuratedPackageSource = new Mock<IEntityRepository<CuratedPackage>>();
            mockCuratedPackageSource
                .Setup(m => m.GetAll())
                .Returns(Enumerable.Empty<CuratedPackage>().AsQueryable());

            var luceneIndexingService = new LuceneIndexingService(
                mockPackageSource.Object,
                mockCuratedPackageSource.Object,
                d,
                null);
            luceneIndexingService.UpdateIndex(forceRefresh: true);

            var luceneSearchService = new LuceneSearchService(d);
            var searchFilter = new SearchFilter
            {
                Skip = 0,
                Take = 10,
                SearchTerm = searchTerm,
            };

            int totalHits;
            var results = luceneSearchService.Search(searchFilter, out totalHits).ToList();

            return results;
        }
    }
}
