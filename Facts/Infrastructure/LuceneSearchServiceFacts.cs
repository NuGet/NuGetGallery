using Moq;
using NuGetGallery.Services;
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
                        IsLatest = true,
                        FlattenedAuthors = "",
                    }
                }.AsQueryable());

            var luceneIndexingService = new LuceneIndexingService(packageSource.Object);
            luceneIndexingService.UpdateIndex();

            var luceneSearchService = new LuceneSearchService();
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
            Assert.Equal(1, results[0].Key);
        }
    }
}