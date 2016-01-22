// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using Moq;
using NuGet.Packaging;
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
                var stubNuGetPackageReader = CreateStubNuGetPackageReader();

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackageReader.Object);

                Assert.False(result);
            }

            [Fact]
            public void WillIncludeThePackageWhenItIsTaggedWithAspNetWebPages()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackageReader = CreateStubNuGetPackageReader();
                stubGalleryPackage.Tags = "aspnetwebpages";

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackageReader.Object);

                Assert.True(result);
            }

            [Fact]
            public void WillNotExamineTheNuGetPackageFilesWhenTaggedWithAspNetWebPages()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackageReader = CreateStubNuGetPackageReader();
                stubGalleryPackage.Tags = "aTag aspnetwebpages aThirdTag";
                
                curator.Curate(stubGalleryPackage, stubNuGetPackageReader.Object, commitChanges: true);

                // at most once - reading the NuSpec will call GetFiles() under the hood
                stubNuGetPackageReader.Verify(stub => stub.GetFiles(), Times.AtMostOnce());
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTaggedAndThereIsAPowerShellFile()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage(populatePackage: p =>
                {
                    p.CreateEntry("foo.txt", CompressionLevel.Fastest);
                    p.CreateEntry("foo.ps1", CompressionLevel.Fastest);
                    p.CreateEntry("foo.cs", CompressionLevel.Fastest);
                });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    new PackageArchiveReader(stubNuGetPackage));

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTaggedAndThereIsT4Template()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage(populatePackage: p =>
                {
                    p.CreateEntry("foo.txt", CompressionLevel.Fastest);
                    p.CreateEntry("foo.t4", CompressionLevel.Fastest);
                    p.CreateEntry("foo.cs", CompressionLevel.Fastest);
                });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    new PackageArchiveReader(stubNuGetPackage));

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenItDependsOnAPackageThatIsNotIncluded()
            {
                var stubFeed = new CuratedFeed();
                var stubNuGetPackage = CreateStubNuGetPackageReader();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Dependencies.Add(
                    new PackageDependency { Id = "NotACuratedPackage" });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

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
                var stubNuGetPackage = CreateStubNuGetPackageReader();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Dependencies.Add(
                    new PackageDependency { Id = "ManuallyExcludedPackage" });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.False(result);
            }

            [Fact]
            public void WillIncludeThePackageWhenThereIsNotPowerShellOrT4File()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage(populatePackage: p =>
                {
                    p.CreateEntry("foo.txt", CompressionLevel.Fastest);
                    p.CreateEntry("foo.cs", CompressionLevel.Fastest);
                });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    new PackageArchiveReader(stubNuGetPackage));

                Assert.True(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenMinClientVersionIsTooHigh()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage(minClientVersion: "3.0.0");
                
                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubFeed,
                    stubGalleryPackage,
                    new PackageArchiveReader(stubNuGetPackage));

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenPackageDoesNotSupportNet40()
            {
                var stubFeed = new CuratedFeed();
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackageReader();
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

                curator.Curate(CreateStubGalleryPackage(), CreateStubNuGetPackageReader().Object, commitChanges: true);

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

                curator.Curate(stubGalleryPackage, CreateStubNuGetPackageReader().Object, commitChanges: true);

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

                curator.Curate(CreateStubGalleryPackage(), CreateStubNuGetPackageReader().Object, commitChanges: true);

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

            private static Stream CreateStubNuGetPackage(string minClientVersion = null, Action<ZipArchive> populatePackage = null)
            {
                return TestPackage.CreateTestPackageStream("test", "1.0.0", minClientVersion: minClientVersion, populatePackage: populatePackage);
            }

            private static Mock<TestPackageReader> CreateStubNuGetPackageReader()
            {
                var mock = new Mock<TestPackageReader>(CreateStubNuGetPackage());
                mock.CallBase = true;
                return mock;
            }
        }
    }
}
