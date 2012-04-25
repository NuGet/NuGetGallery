using System;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery.PackageCurators
{
    public class WebMatrixPackageCuratorFacts
    {
        public class TheCurateMethod
        {
            [Fact]
            public void WillNotIncludeThePackageWhenTheWebMatrixCuratedFeedDoesNotExist()
            {
                var curator = new TestableWebMatrixPackageCurator();
                curator.StubCuratedFeedByNameQry.Setup(stub => stub.Execute(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);

                curator.Curate(CreateStubGalleryPackage(), null);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Never());   
            }

            [Fact]
            public void WillNotIncludeThePackageWhenItIsNotTheLatestStable()
            {
                var curator = new TestableWebMatrixPackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.IsLatestStable = false;

                curator.Curate(stubGalleryPackage, null);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void WillIncludeThePackageWhenItIsTaggedWithAspNetWebPages()
            {
                var curator = new TestableWebMatrixPackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Tags = "aTag aspnetwebpages aThirdTag";

                curator.Curate(stubGalleryPackage, null);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Once());
            }

            [Fact]
            public void WillNotExamineTheNuGetPackageFilesWhenTaggedWithAspNetWebPages()
            {
                var curator = new TestableWebMatrixPackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Tags = "aTag aspnetwebpages aThirdTag";
                var stubNuGetPackage = CreateStubNuGetPackage();

                curator.Curate(stubGalleryPackage, stubNuGetPackage.Object);

                stubNuGetPackage.Verify(stub => stub.GetFiles(), Times.Never());
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTaggedAndThereIsAPowerShellFile()
            {
                var curator = new TestableWebMatrixPackageCurator();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(new[]
                {
                    CreateStubNuGetPackageFile("foo.txt").Object, 
                    CreateStubNuGetPackageFile("foo.ps1").Object, 
                    CreateStubNuGetPackageFile("foo.cs").Object
                });

                curator.Curate(CreateStubGalleryPackage(), stubNuGetPackage.Object);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTaggedAndThereIsT4Template()
            {
                var curator = new TestableWebMatrixPackageCurator();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(new[]
                {
                    CreateStubNuGetPackageFile("foo.txt").Object, 
                    CreateStubNuGetPackageFile("foo.t4").Object, 
                    CreateStubNuGetPackageFile("foo.cs").Object
                });

                curator.Curate(CreateStubGalleryPackage(), stubNuGetPackage.Object);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void WillIncludeThePackageWhenThereIsNotPowerShellOrT4File()
            {
                var curator = new TestableWebMatrixPackageCurator();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(new[]
                {
                    CreateStubNuGetPackageFile("foo.txt").Object, 
                    CreateStubNuGetPackageFile("foo.cs").Object
                });

                curator.Curate(CreateStubGalleryPackage(), stubNuGetPackage.Object);

                curator.StubCreatedCuratedPackageCmd.Verify(stub => stub.Execute(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>()), Times.Once());
            }

            [Fact]
            public void WillIncludeThePackageUsingTheCuratedFeedKey()
            {
                var curator = new TestableWebMatrixPackageCurator();
                curator.StubCuratedFeed.Key = 42;

                curator.Curate(CreateStubGalleryPackage(), CreateStubNuGetPackage().Object);

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
                var curator = new TestableWebMatrixPackageCurator();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.PackageRegistration.Key = 42;

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
                var curator = new TestableWebMatrixPackageCurator();

                curator.Curate(CreateStubGalleryPackage(), CreateStubNuGetPackage().Object);

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

        public class TestableWebMatrixPackageCurator : WebMatrixPackageCurator
        {
            public TestableWebMatrixPackageCurator()
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
