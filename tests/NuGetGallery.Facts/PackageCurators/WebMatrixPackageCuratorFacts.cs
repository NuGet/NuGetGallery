// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using Moq;
using NuGet;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery.PackageCurators
{
    public class WebMatrixPackageCuratorFacts
    {
        public class TestableWebMatrixPackageCurator : WebMatrixPackageCurator
        {
            public static TestableWebMatrixPackageCurator Create(Action<Mock<ICuratedFeedService>> setupCuratedFeedServiceStub)
            {
                var stubCuratedFeed = new CuratedFeed { Key = 0 };
                var stubCuratedFeedService = new Mock<ICuratedFeedService>();

                stubCuratedFeedService
                    .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(stubCuratedFeed);

                if (setupCuratedFeedServiceStub != null)
                {
                    setupCuratedFeedServiceStub(stubCuratedFeedService);
                }

                return new TestableWebMatrixPackageCurator(stubCuratedFeed, stubCuratedFeedService);
            }

            public TestableWebMatrixPackageCurator(CuratedFeed stubCuratedFeed, Mock<ICuratedFeedService> stubCuratedFeedService)
                : base(stubCuratedFeedService.Object)
            {
                StubCuratedFeed = stubCuratedFeed;
                StubCuratedFeedService = stubCuratedFeedService;
            }

            public CuratedFeed StubCuratedFeed { get; private set; }
            public Mock<ICuratedFeedService> StubCuratedFeedService { get; private set; }
        }

        public class TheCurateMethod
        {
            [Fact]
            public void WillNotIncludeThePackageWhenTheWebMatrixCuratedFeedDoesNotExist()
            {
                var curator = TestableWebMatrixPackageCurator.Create(stubCuratedFeedService =>
                {
                    stubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);
                });

                curator.Curate(CreateStubGalleryPackage(), null, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackage(
                        It.IsAny<CuratedFeed>(),
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>()),
                    Times.Never());
            }

            [Fact]
            public void WillNotIncludeThePackageWhenItIsNotTheLatestStable()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.IsLatestStable = false;
                var stubNuGetPackage = CreateStubNuGetPackage();

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.False(result);
            }

            [Fact]
            public void WillIncludeThePackageWhenItIsTaggedWithAspNetWebPages()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubGalleryPackage.Tags = "aspnetwebpages";

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.True(result);
            }

            [Fact]
            public void WillNotExamineTheNuGetPackageFilesWhenTaggedWithAspNetWebPages()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubGalleryPackage.Tags = "aTag aspnetwebpages aThirdTag";
                
                curator.Curate(stubGalleryPackage, stubNuGetPackage.Object, commitChanges: true);

                stubNuGetPackage.Verify(stub => stub.GetFiles(), Times.Never());
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTaggedAndThereIsAPowerShellFile()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(
                    new []
                        {
                            "foo.txt",
                            "foo.ps1",
                            "foo.cs",
                        });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTaggedAndThereIsT4Template()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(
                    new[]
                        {
                            "foo.txt",
                            "foo.t4",
                            "foo.cs",
                        });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenItDependsOnAPackageThatIsNotIncluded()
            {
                var stubFeed = new CuratedFeed();
                var stubNuGetPackage = CreateStubNuGetPackage().Object;
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Dependencies.Add(
                    new PackageDependency { Id = "NotACuratedPackage" });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage);

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenItDependsOnAPackageThatIsExcludedInTheFeed()
            {
                var stubFeed = new CuratedFeed();
                var dependencyPackage = new CuratedPackage
                {
                    AutomaticallyCurated = false, 
                    Included = false, 
                    PackageRegistration = new PackageRegistration { Id = "ManuallyExcludedPackage" }
                };
                stubFeed.Packages.Add(dependencyPackage);
                var stubNuGetPackage = CreateStubNuGetPackage().Object;
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Dependencies.Add(
                    new PackageDependency { Id = "ManuallyExcludedPackage" });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage);

                Assert.False(result);
            }

            [Fact]
            public void WillIncludeThePackageWhenThereIsNotPowerShellOrT4File()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(
                    new[]
                        {
                            "foo.txt",
                            "foo.cs",
                        });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.True(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenMinClientVersionIsTooHigh()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubNuGetPackage.Setup(n => n.Metadata.MinClientVersion).Returns(new Version(3, 0));
                
                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenPackageDoesNotSupportNet40()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage();
                stubGalleryPackage.Tags = "aspnetwebpages";
                stubGalleryPackage.SupportedFrameworks.Add(new PackageFramework()
                {
                    TargetFramework = "net45"
                });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.False(result);
            }

            [Fact]
            public void WillIncludeThePackageUsingTheCuratedFeedKey()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);
                curator.StubCuratedFeed.Key = 42;

                curator.Curate(CreateStubGalleryPackage(), CreateStubNuGetPackage().Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackage(
                        curator.StubCuratedFeed,
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        null,
                        It.IsAny<bool>()));
            }

            [Fact]
            public void WillIncludeThePackageUsingThePackageRegistrationKey()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.PackageRegistration.Key = 42;

                curator.Curate(stubGalleryPackage, CreateStubNuGetPackage().Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackage(
                        It.IsAny<CuratedFeed>(),
                        stubGalleryPackage.PackageRegistration,
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        null,
                        It.IsAny<bool>()));
            }

            [Fact]
            public void WillSetTheAutomaticBitWhenIncludingThePackage()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);

                curator.Curate(CreateStubGalleryPackage(), CreateStubNuGetPackage().Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackage(
                        It.IsAny<CuratedFeed>(),
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<bool>(),
                        true,
                        null,
                        It.IsAny<bool>()));
            }

            private static Package CreateStubGalleryPackage()
            {
                return new Package
                    {
                        IsLatestStable = true,
                        PackageRegistration = new PackageRegistration
                            {
                                Key = 0,
                            },
                    };
            }

            private static Mock<INupkg> CreateStubNuGetPackage()
            {
                var stubNuGetPackage = new Mock<INupkg>();
                stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(new string[] { });
                stubNuGetPackage.Setup(stub => stub.Metadata.Id).Returns("test");
                return stubNuGetPackage;
            }

            private static Mock<IPackageFile> CreateStubNuGetPackageFile(string path)
            {
                var stubPackageFile = new Mock<IPackageFile>();
                stubPackageFile.Setup(stub => stub.Path).Returns(path);
                return stubPackageFile;
            }
        }
    }
}
