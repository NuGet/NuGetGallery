using Lucene.Net.Store;
using Moq;
using NuGetGallery.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Infrastructure
{
    public class LuceneSearchServiceFacts
    {
        [Fact]
        public void IndexAndSearchAPackage()
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

            var luceneIndexingService = CreateIndexingService(packageSource);
            luceneIndexingService.UpdateIndex(forceRefresh: true);

            var luceneSearchService = CreateSearchService(packageSource);
            int totalHits = 0;
            var searchFilter = new SearchFilter
            {
                Skip = 0,
                Take = 10,
                SearchTerm = "awesome",
            };

            var results = luceneSearchService.Search(
                packageSource.Object.GetPackagesForIndexing(null), 
                searchFilter, 
                out totalHits).ToList();

            Assert.Single(results);
            Assert.Equal(3, results[0].Key);
            Assert.Equal(1, results[0].PackageRegistrationKey);
        }

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
                        PackageRegistrationKey = 12500,
                        PackageRegistration = new PackageRegistration
                        {
                            Id = "DavidTest123",
                            Key = 12500,
                            DownloadCount = 495
                        },
                        Created = DateTime.Parse("2011-07-18 22:29:46.893"),
                        Title = "DavidTest123",
                        Description = "Description",
                        DownloadCount = 469,
                        HashAlgorithm = "SHA512",
                        Hash = "1unECLYoz4z1C5DiIkdnptHvodvNkbLUIev28Y6wOG8EohgPLNp1w7Qa7H1M6upa4tXYlbsenDjFgQIpNHhU3w==",
                        LastUpdated = DateTime.Parse("2012-11-26 04:17:21.723"),
                        Listed = true,
                        IsLatest = true,
                        IsLatestStable = true,
                        Published = DateTime.Parse("1900-01-01 00:00:00.000"),
                        FlattenedAuthors = "",
                        PackageFileSize = 4429,
                    }
                }.AsQueryable());

            var luceneIndexingService = CreateIndexingService(packageSource);
            luceneIndexingService.UpdateIndex();

            var luceneSearchService = CreateSearchService(packageSource);
            int totalHits = 0;
            var searchFilter = new SearchFilter
            {
                Skip = 0,
                Take = 10,
                SearchTerm = "12",
            };

            var results = luceneSearchService.Search(
                packageSource.Object.GetPackagesForIndexing(null),
                searchFilter,
                out totalHits).ToList();

            Assert.Empty(results);
        }

        static Directory d = LuceneCommon.GetRAMDirectory();

        private LuceneIndexingService CreateIndexingService(Mock<IPackageSource> packageSource)
        {
            d = LuceneCommon.GetRAMDirectory();
            return new LuceneIndexingService(
                   packageSource.Object,
                   d);
        }
        
        private LuceneSearchService CreateSearchService(Mock<IPackageSource> packageSource)
        {
            return new LuceneSearchService(
                   d);
        }
    }
}