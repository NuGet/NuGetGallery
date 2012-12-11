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
            var packageSource = new Mock<IPackageSource>();
            packageSource.Setup(x => x.GetPackagesForIndexing(null)).Returns(
                new List<Package>
                {
                    new Package
                    {
                        Key = 3,
                        PackageRegistrationKey = 1,
                        PackageRegistration = new PackageRegistration
                        {
                            Id = "Package #1",
                        },
                        Title = "Package #1 4.2.0",
                        Description = "Package #1 is an awesome package",
                        Listed = true,
                        IsLatestStable = true,
                        IsLatest = true,
                        IsPrerelease = true,
                        DownloadCount = 100,
                        FlattenedAuthors = "",
                    }
                }.AsQueryable());

            var results = IndexAndSearch(packageSource, "awesome");

            Assert.Single(results);
            Assert.Equal(3, results[0].Key);
            Assert.Equal(1, results[0].PackageRegistrationKey);
        }

        // This works because we do some wildcard magic in our searches.
        [Fact]
        public void IndexAndSearchDavid123For12()
        {
            var packageSource = new Mock<IPackageSource>();
            packageSource.Setup(x => x.GetPackagesForIndexing(null)).Returns(
                new List<Package>
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
                        Description = "Description",
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                        FlattenedAuthors = "DavidX",
                        Title = "DavidTest123",
                        Version = "1.1",
                    }
                }.AsQueryable());

            var results = IndexAndSearch(packageSource, "12");

            Assert.Single(results);
            Assert.Equal("DavidTest123", results[0].Title);
        }

        [Fact]
        public void IndexAndSearchWithWordStemming()
        {
            var packageSource = new Mock<IPackageSource>();
            packageSource.Setup(x => x.GetPackagesForIndexing(null)).Returns(
                new List<Package>
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
                        Description = "Library for compressing your filez",
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                        FlattenedAuthors = "Eric",
                        Title = "SuperzipLib",
                        Version = "1.1.2",
                    }
                }.AsQueryable());

            var results = IndexAndSearch(packageSource, "compressed");

            Assert.Empty(results); // currently stemming is not working
            //Assert.Equal("SuperzipLib", results[0].Title);
        }

        [Fact]
        public void SearchUsingExactPackageId()
        {
            var packageSource = new Mock<IPackageSource>();
            packageSource.Setup(x => x.GetPackagesForIndexing(null)).Returns(
                new List<Package>
                    {
                        new Package
                        {
                            Key = 144,
                            PackageRegistrationKey = 12,
                            PackageRegistration = new PackageRegistration
                            {
                                Id = "NuGet.Core",
                                Key = 12,
                                DownloadCount = 3
                            },
                            Description = "NuGet.Core is the core framework assembly for NuGet",
                            DownloadCount = 3,
                            Listed = true,
                            IsLatest = true,
                            IsLatestStable = true,
                            FlattenedAuthors = "M S C",
                            Tags = "NuGetTag",
                            Title = "NuGet.Core",
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
                            Description =
                                "This isn't really NuGet.Core. The confusing package ID is the test!",
                            DownloadCount = 30,
                            Listed = true,
                            IsLatest = true,
                            IsLatestStable = true,
                            FlattenedAuthors = "Laugh",
                            Title = "SomeotherNuGet.Core.SimilarlyNamedPackage",
                            Version = "1.5.20902.9026",
                        }
                    }.AsQueryable());

            // simple query
            var results = IndexAndSearch(packageSource, "NuGet.Core");
            Assert.Equal(2, results.Count);
            Assert.Equal("NuGet.Core", results[0].Title);
            Assert.Equal(144, results[0].Key);
            Assert.Equal("NuGet.Core", results[0].PackageRegistration.Id);
            Assert.Equal(12, results[0].PackageRegistrationKey);
            Assert.Equal(12, results[0].PackageRegistration.Key);
            Assert.Equal("NuGet.Core is the core framework assembly for NuGet", results[0].Description);
            Assert.True(results[0].IsLatest);
            Assert.True(results[0].IsLatestStable);
            Assert.Equal("NuGetTag", results[0].Tags);
        }

        [Theory]
        [InlineData("Id", "NuGet.Core")]
        [InlineData("id", "NuGet.Core")]
        [InlineData("title", "NuGet.Core")]
        [InlineData("TITLE", "NuGet.Core")]
        [InlineData("Author", "Alpha")]
        [InlineData("author", "\"Alpha Beta Gamma\"")]
        [InlineData("Description", "core framework")]
        [InlineData("Tags", "dotnet")]
        public void SearchForNuGetCoreWithExactField(string field, string term)
        {
            var packageSource = new Mock<IPackageSource>();
            packageSource.Setup(x => x.GetPackagesForIndexing(null)).Returns(
                new List<Package>
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
                        Description = "NuGet.Core is the core framework assembly for NuGet that the rest of NuGet builds upon.",
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                        FlattenedAuthors = "Alpha Beta Gamma",
                        Title = "NuGet.Core",
                        Version = "1.5.20902.9026",
                        Tags = "dotnet",
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
                        },
                        Description = "This isn't really NuGet.Core. Sorry for the confusing name - But its needed for a test case!",
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                        FlattenedAuthors = "Laugh",
                        Title = "SomeotherNuGet.Core.SimilarlyNamedPackage",
                        Version = "1.5.20902.9026",
                        Tags = "javascript"
                    }
                }.AsQueryable());

            // query targeted specifically against id field should work equally well
            var results = IndexAndSearch(packageSource, field + ":" + term);
            Assert.NotEmpty(results);
            Assert.Equal("NuGet.Core", results[0].Title);
            Assert.Equal("NuGet.Core", results[0].PackageRegistration.Id);
        }

        [Fact]
        public void SearchForJQueryUICombinedWithPartialId()
        {
            var packageSource = new Mock<IPackageSource>();
            packageSource.Setup(x => x.GetPackagesForIndexing(null)).Returns(
                new List<Package>
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
                        Description = "jQuery UI is etc etc and many more important things",
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                        FlattenedAuthors = "Alpha Beta Gamma",
                        Title = "JQuery UI (Combined Blobbary)",
                        Tags = "web javascript",
                    },
                }.AsQueryable());

            var results = IndexAndSearch(packageSource, "id:JQuery.ui");
            Assert.NotEmpty(results);
            Assert.Equal("JQuery.UI.Combined", results[0].PackageRegistration.Id);
        }

        // See issue https://github.com/NuGet/NuGetGallery/issues/406
        [Fact]
        public void SearchWorksAroundLuceneQuerySyntaxExceptions()
        {
            var packageSource = new Mock<IPackageSource>();
            packageSource.Setup(x => x.GetPackagesForIndexing(null)).Returns(
                new List<Package>
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
                        Description = "NuGet.Core is the core framework assembly for NuGet that the rest of NuGet builds upon.",
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                        FlattenedAuthors = "Alpha Beta Gamma",
                        LicenseUrl = "http://nuget.codeplex.com/license",
                        Title = "NuGet.Core",
                        Version = "1.5.20902.9026",
                    },
                }.AsQueryable());

            var results = IndexAndSearch(packageSource, "*Core"); // Lucene parser throws for leading asterisk in searches
            Assert.NotEmpty(results);
        }

        private IList<Package> IndexAndSearch(Mock<IPackageSource> packageSource, string searchTerm)
        {
            Directory d = new RAMDirectory();
            var luceneIndexingService = new LuceneIndexingService(packageSource.Object, d);
            luceneIndexingService.UpdateIndex(forceRefresh: true);

            var luceneSearchService = new LuceneSearchService(d);
            int totalHits = 0;
            var searchFilter = new SearchFilter
            {
                Skip = 0,
                Take = 10,
                SearchTerm = searchTerm,
            };

            var results = luceneSearchService.Search(
                packageSource.Object.GetPackagesForIndexing(null),
                searchFilter,
                out totalHits).ToList();

            return results;
        }
    }
}