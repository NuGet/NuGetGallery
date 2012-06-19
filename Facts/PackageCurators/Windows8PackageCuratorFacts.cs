using System;
using Moq;
using NuGet;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.PackageCurators
{
    public class Windows8PackageCuratorFacts
    {
        public class TheCurateMethod
        {
            [Fact]
            public void WillNotIncludeThePackageWhenTheWindows8CuratedFeedDoesNotExist()
            {
                var curator = new TestableWindows8PackageCurator();
                curator.StubCuratedFeedByNameQry.Setup(stub => stub.Execute(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);
                var package = CreateStubGalleryPackage();
                package.Tags = "winrt";

                curator.Curate(package, null);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Never());   
            }

            [Theory]
            [InlineData("winrt")]
            [InlineData("win8")]
            [InlineData("windows8")]
            [InlineData("winjs")]
            [InlineData("wInRt")]
            [InlineData("wIn8")]
            [InlineData("wInDows8")]
            [InlineData("wInJs")]
            public void WillIncludeThePackageWhenItHasAcceptedTag(string tag)
            {
                var curator = new TestableWindows8PackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Tags = "aTag " + tag + " aThirdTag";

                curator.Curate(stubGalleryPackage, null);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Once());
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTagged()
            {
                var curator = new TestableWindows8PackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Tags = "aTag notforwinrt aThirdTag";

                curator.Curate(stubGalleryPackage, null);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void WillNotIncludeThePackageWhenTagsIsNull()
            {
                var curator = new TestableWindows8PackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Tags = null;

                curator.Curate(stubGalleryPackage, null);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void WillIncludeThePackageUsingTheCuratedFeedKey()
            {
                var curator = new TestableWindows8PackageCurator();
                curator.StubCuratedFeed.Key = 42;
                var package = CreateStubGalleryPackage();
                package.Tags = "winrt";

                curator.Curate(package, CreateStubNuGetPackage().Object);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    42,
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()));
            }

            [Fact]
            public void WillIncludeThePackageUsingThePackageRegistrationKey()
            {
                var curator = new TestableWindows8PackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.PackageRegistration.Key = 42;
                stubGalleryPackage.Tags = "winrt";

                curator.Curate(stubGalleryPackage, CreateStubNuGetPackage().Object);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    42,
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()));
            }

            [Fact]
            public void WillSetTheAutomaticBitWhenIncludingThePackage()
            {
                var curator = new TestableWindows8PackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Tags = "winrt";

                curator.Curate(stubGalleryPackage, CreateStubNuGetPackage().Object);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    true,
                    It.IsAny<string>()));
            }

            static Package CreateStubGalleryPackage()
            {
                return new Package
                {
                    IsLatestStable = true      ,
                    PackageRegistration = new PackageRegistration
                    {
                        Key = 0,                            
                    },
                };
            }

            static Mock<IPackage> CreateStubNuGetPackage()
            {
                var stubNuGetPackage = new Mock<IPackage>();
                stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(new IPackageFile[] {});
                return stubNuGetPackage;
            }

            static Mock<IPackageFile> CreateStubNuGetPackageFile(string path)
            {
                var stubPackageFile = new Mock<IPackageFile>();
                stubPackageFile.Setup(stub => stub.Path).Returns(path);
                return stubPackageFile;
            }
        }

        public class TestableWindows8PackageCurator : Windows8PackageCurator
        {
            public TestableWindows8PackageCurator()
            {
                StubCreatedCuratedPackageCmd = new Mock<ICreateCuratedPackageCommand>();
                StubCuratedFeed = new CuratedFeed { Key = 0 };
                StubCuratedFeedByNameQry = new Mock<ICuratedFeedByNameQuery>();

                StubCuratedFeedByNameQry
                    .Setup(stub => stub.Execute(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(StubCuratedFeed);
            }

            public Mock<ICreateCuratedPackageCommand> StubCreatedCuratedPackageCmd { get; set; }
            public CuratedFeed StubCuratedFeed { get; private set; }
            public Mock<ICuratedFeedByNameQuery> StubCuratedFeedByNameQry { get; private set; }

            protected override T GetService<T>()
            {
                if (typeof(T) == typeof(ICreateCuratedPackageCommand))
                    return (T)StubCreatedCuratedPackageCmd.Object;
                
                if (typeof(T) == typeof(ICuratedFeedByNameQuery))
                    return (T)StubCuratedFeedByNameQry.Object;
                
                throw new Exception("Tried to get an unexpected service.");
            }
        }
    }
}
