﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
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
            public async Task WillNotIncludeThePackageWhenTheWebMatrixCuratedFeedDoesNotExist()
            {
                var curator = TestableWebMatrixPackageCurator.Create(stubCuratedFeedService =>
                {
                    stubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);
                });
                var stubNuGetPackageReader = CreateStubNuGetPackageReader();

                await curator.CurateAsync(CreateStubGalleryPackage(), stubNuGetPackageReader.Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackageAsync(
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
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.IsLatestStable = false;
                var stubNuGetPackageReader = CreateStubNuGetPackageReader();

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubGalleryPackage,
                    stubNuGetPackageReader.Object);

                Assert.False(result);
            }

            [Fact]
            public void WillIncludeThePackageWhenItIsTaggedWithAspNetWebPages()
            {
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackageReader = CreateStubNuGetPackageReader();
                stubGalleryPackage.Tags = "aspnetwebpages";

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubGalleryPackage,
                    stubNuGetPackageReader.Object);

                Assert.True(result);
            }

            [Fact]
            public async Task WillNotExamineTheNuGetPackageFilesWhenTaggedWithAspNetWebPages()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackageReader = CreateStubNuGetPackageReader();
                stubGalleryPackage.Tags = "aTag aspnetwebpages aThirdTag";

                await curator.CurateAsync(stubGalleryPackage, stubNuGetPackageReader.Object, commitChanges: true);

                // at most once - reading the NuSpec will call GetFiles() under the hood
                stubNuGetPackageReader.Verify(stub => stub.GetFiles(), Times.AtMostOnce());
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTaggedAndThereIsAPowerShellFile()
            {
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage(populatePackage: p =>
                {
                    p.CreateEntry("foo.txt", CompressionLevel.Fastest);
                    p.CreateEntry("foo.ps1", CompressionLevel.Fastest);
                    p.CreateEntry("foo.cs", CompressionLevel.Fastest);
                });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubGalleryPackage,
                    new PackageArchiveReader(stubNuGetPackage));

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenNotTaggedAndThereIsT4Template()
            {
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage(populatePackage: p =>
                {
                    p.CreateEntry("foo.txt", CompressionLevel.Fastest);
                    p.CreateEntry("foo.t4", CompressionLevel.Fastest);
                    p.CreateEntry("foo.cs", CompressionLevel.Fastest);
                });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubGalleryPackage,
                    new PackageArchiveReader(stubNuGetPackage));

                Assert.False(result);
            }

            [Fact]
            public async Task WillNotIncludeThePackageWhenItDependsOnAPackageThatIsNotIncluded()
            {
                var stubFeed = new CuratedFeed();
                var stubNuGetPackage = CreateStubNuGetPackageReader();
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.Dependencies.Add(
                    new PackageDependency { Id = "NotACuratedPackage" });

                var curator = TestableWebMatrixPackageCurator.Create(stubCuratedFeedService =>
                {
                    stubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns(stubFeed);
                });

                await curator.CurateAsync(stubGalleryPackage, stubNuGetPackage.Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackageAsync(
                        It.IsAny<CuratedFeed>(),
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>()),
                    Times.Never());
            }

            [Fact]
            public async Task WillNotIncludeThePackageWhenItDependsOnAPackageThatIsExcludedInTheFeed()
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

                var curator = TestableWebMatrixPackageCurator.Create(stubCuratedFeedService =>
                {
                    stubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns(stubFeed);
                });

                await curator.CurateAsync(stubGalleryPackage, stubNuGetPackage.Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackageAsync(
                        It.IsAny<CuratedFeed>(),
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>()),
                    Times.Never());
            }

            [Fact]
            public void WillIncludeThePackageWhenThereIsNotPowerShellOrT4File()
            {
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage(populatePackage: p =>
                {
                    p.CreateEntry("foo.txt", CompressionLevel.Fastest);
                    p.CreateEntry("foo.cs", CompressionLevel.Fastest);
                });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubGalleryPackage,
                    new PackageArchiveReader(stubNuGetPackage));

                Assert.True(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenMinClientVersionIsTooHigh()
            {
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackage(minClientVersion: "3.0.0");

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubGalleryPackage,
                    new PackageArchiveReader(stubNuGetPackage));

                Assert.False(result);
            }

            [Fact]
            public void WillNotIncludeThePackageWhenPackageDoesNotSupportNet40()
            {
                var stubGalleryPackage = CreateStubGalleryPackage();
                var stubNuGetPackage = CreateStubNuGetPackageReader();
                stubGalleryPackage.Tags = "aspnetwebpages";
                stubGalleryPackage.SupportedFrameworks.Add(new PackageFramework()
                {
                    TargetFramework = "net45"
                });

                bool result = WebMatrixPackageCurator.ShouldCuratePackage(
                    stubGalleryPackage,
                    stubNuGetPackage.Object);

                Assert.False(result);
            }

            [Fact]
            public async Task WillIncludeThePackageUsingTheCuratedFeedKey()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);
                curator.StubCuratedFeed.Key = 42;

                await curator.CurateAsync(CreateStubGalleryPackage(), CreateStubNuGetPackageReader().Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackageAsync(
                        curator.StubCuratedFeed,
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        null,
                        It.IsAny<bool>()));
            }

            [Fact]
            public async Task WillIncludeThePackageUsingThePackageRegistrationKey()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);
                var stubGalleryPackage = CreateStubGalleryPackage();
                stubGalleryPackage.PackageRegistration.Key = 42;

                await curator.CurateAsync(stubGalleryPackage, CreateStubNuGetPackageReader().Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackageAsync(
                        It.IsAny<CuratedFeed>(),
                        stubGalleryPackage.PackageRegistration,
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        null,
                        It.IsAny<bool>()));
            }

            [Fact]
            public async Task WillSetTheAutomaticBitWhenIncludingThePackage()
            {
                var curator = TestableWebMatrixPackageCurator.Create(null);

                await curator.CurateAsync(CreateStubGalleryPackage(), CreateStubNuGetPackageReader().Object, commitChanges: true);

                curator.StubCuratedFeedService.Verify(
                    stub => stub.CreatedCuratedPackageAsync(
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
