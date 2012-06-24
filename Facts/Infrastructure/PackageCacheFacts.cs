using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Infrastructure
{
    public class PackageCacheFacts
    {
        public class TheAddPackageMethod
        {
            [Fact]
            public void WillRemoveTheReleasedPackageIdsListFromCache()
            {
                var packageCache = new TestablePackageCache();

                packageCache.AddPackage(new Package());

                packageCache.StubCache.Verify(stub => stub.Remove("released-package-ids"));
            }

            [Fact]
            public void WillStartATaskToReadAndCacheReleasedPackageIds()
            {
                var packageCache = new TestablePackageCache();

                packageCache.AddPackage(new Package());

                Assert.False(packageCache.ReadAndCachePackageIds_IncludePrereleaseArg.ElementAt(0));
            }

            [Fact]
            public void WillRemoveTheAllPackageIdsListFromCache()
            {
                var packageCache = new TestablePackageCache();

                packageCache.AddPackage(new Package());

                packageCache.StubCache.Verify(stub => stub.Remove("all-package-ids"));
            }

            [Fact]
            public void WillStartATaskToReadAndCacheAllPackageIds()
            {
                var packageCache = new TestablePackageCache();

                packageCache.AddPackage(new Package());

                Assert.True(packageCache.ReadAndCachePackageIds_IncludePrereleaseArg.ElementAt(1));
            }
        }

        public class GetPackageIdsMethod
        {
            [Theory]
            [InlineData(true, "all-package-ids")]
            [InlineData(false, "released-package-ids")]
            public void WillReturnCachedPackageIdsWhenAlreadyCached(
                bool includePrerelease, 
                string cacheKey)
            {
                var packageCache = new TestablePackageCache();
                packageCache.StubCache
                    .Setup(stub => stub.Get<string[]>(cacheKey))
                    .Returns(new[] { "theFirstPackageId", "theSecondPackageId", "theLastPackageId" });

                var result = packageCache.GetPackageIds(includePrerelease);

                Assert.Equal("theFirstPackageId", result.ElementAt(0));
                Assert.Equal("theSecondPackageId", result.ElementAt(1));
                Assert.Equal("theLastPackageId", result.ElementAt(2));
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void WillReadPackageIdsWhenNotAlreadyCached(bool includePrerelease)
            {
                var packageCache = new TestablePackageCache();
                packageCache.StubPackageIdsQuery
                    .Setup(stub => stub.Execute((bool?)includePrerelease))
                    .Returns(new[] { "theFirstPackageId", "theSecondPackageId", "theLastPackageId" });

                var result = packageCache.GetPackageIds(includePrerelease);

                Assert.Equal("theFirstPackageId", result.ElementAt(0));
                Assert.Equal("theSecondPackageId", result.ElementAt(1));
                Assert.Equal("theLastPackageId", result.ElementAt(2));
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void WillCacheTheReadPackageIds(bool includePrerelease)
            {
                var packageCache = new TestablePackageCache();
                packageCache.StubPackageIdsQuery
                    .Setup(stub => stub.Execute(It.IsAny<bool?>()))
                    .Returns(new[] { "theFirstPackageId", "theSecondPackageId", "theLastPackageId" });

                var result = packageCache.GetPackageIds(includePrerelease);

                Assert.Equal(includePrerelease, packageCache.CachePackageIds_IncludesPrereleaseArg);
                Assert.Equal("theFirstPackageId", packageCache.CachePackageIds_PackageIdsArg.ElementAt(0));
                Assert.Equal("theSecondPackageId", packageCache.CachePackageIds_PackageIdsArg.ElementAt(1));
                Assert.Equal("theLastPackageId", packageCache.CachePackageIds_PackageIdsArg.ElementAt(2));
            }
        }

        public class TestablePackageCache : PackageCache
        {
            public TestablePackageCache() : base(null)
            {
                StubCache = new Mock<ICache>();
                StubPackageIdsQuery = new Mock<IPackageIdsQuery>();
                StubTaskFactory = new Mock<ITaskFactory>();

                ReadAndCachePackageIds_IncludePrereleaseArg = new Queue<bool>();

                StubCache
                    .Setup(stub => stub.Get<string[]>(It.IsAny<string>()))
                    .Returns((string[])null);
                StubTaskFactory
                    .Setup(stub => stub.StartNew(It.IsAny<Action>()))
                    .Callback<Action>(action => action())
                    .Returns(new Task(() => {}));

                Cache = StubCache.Object;
            }

            public bool CachePackageIds_IncludesPrereleaseArg { get; private set; }
            public string[] CachePackageIds_PackageIdsArg { get; private set; }
            public Queue<bool> ReadAndCachePackageIds_IncludePrereleaseArg { get; private set; }
            public Mock<ICache> StubCache { get; set; }
            public Mock<IPackageIdsQuery> StubPackageIdsQuery { get; set; }
            public Mock<ITaskFactory> StubTaskFactory { get; set; }

            protected override void CachePackageIds(
                string[] packageIds, 
                bool includesPrerelease = false)
            {
                CachePackageIds_PackageIdsArg = packageIds;
                CachePackageIds_IncludesPrereleaseArg = includesPrerelease;
            }
            
            protected override T GetService<T>()
            {
                if (typeof(T) == typeof(IPackageIdsQuery))
                    return (T)StubPackageIdsQuery.Object;
                
                if (typeof(T) == typeof(ITaskFactory))
                    return (T)StubTaskFactory.Object;

                throw new Exception("Tried to get unexpected service");
            }

            protected override string[] ReadAndCachePackageIds(bool includePrerelease = false)
            {
                ReadAndCachePackageIds_IncludePrereleaseArg.Enqueue(includePrerelease);
                return new string[] { };
            }
        }
    }
}
