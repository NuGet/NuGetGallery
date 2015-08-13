// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using Moq;
using NuGet;
using NuGetGallery.Packaging;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.PackageCurators
{
    public class TestableWindows8PackageCurator : Windows8PackageCurator
    {
        public static TestableWindows8PackageCurator Create(Action<Mock<ICuratedFeedService>> setupCuratedFeedServiceStub)
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

            return new TestableWindows8PackageCurator(stubCuratedFeed, stubCuratedFeedService);
        }

        public TestableWindows8PackageCurator(CuratedFeed stubCuratedFeed, Mock<ICuratedFeedService> stubCuratedFeedService)
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
        public void WillNotIncludeThePackageWhenTheWindows8CuratedFeedDoesNotExist()
        {
            var curator = TestableWindows8PackageCurator.Create(stubCuratedFeedService => {
                stubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);
            });
                
            var package = CreateStubGalleryPackage();
            package.Tags = "winrt";

            curator.Curate(package, null, commitChanges: true);

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
            var curator = TestableWindows8PackageCurator.Create(null);
            var stubGalleryPackage = CreateStubGalleryPackage();
            stubGalleryPackage.Tags = "aTag " + tag + " aThirdTag";

            curator.Curate(stubGalleryPackage, null, commitChanges: true);

            curator.StubCuratedFeedService.Verify(
                stub => stub.CreatedCuratedPackage(
                    It.IsAny<CuratedFeed>(),
                    It.IsAny<PackageRegistration>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()),
                Times.Once());
        }

        [Fact]
        public void WillNotIncludeThePackageWhenNotTagged()
        {
            var curator = TestableWindows8PackageCurator.Create(null);
            var stubGalleryPackage = CreateStubGalleryPackage();
            stubGalleryPackage.Tags = "aTag notforwinrt aThirdTag";

            curator.Curate(stubGalleryPackage, null, commitChanges: true);

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
        public void WillNotIncludeThePackageWhenTagsIsNull()
        {
            var curator = TestableWindows8PackageCurator.Create(null);
            var stubGalleryPackage = CreateStubGalleryPackage();
            stubGalleryPackage.Tags = null;

            curator.Curate(stubGalleryPackage, null, commitChanges: true);

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
        public void WillNotIncludeThePackageWhenItDependsOnAPackageThatIsNotIncluded()
        {
            var curator = TestableWindows8PackageCurator.Create(null);
            var stubNuGetPackage = CreateStubNuGetPackage();

            var stubGalleryPackage = CreateStubGalleryPackage();
            stubGalleryPackage.Tags = "win8";
            stubGalleryPackage.Dependencies.Add(new PackageDependency { Id = "NotACuratedPackage" });

            curator.Curate(stubGalleryPackage, CreateStubNuGetPackage().Object, commitChanges: true);

            curator.StubCuratedFeedService.Verify(
                stub => stub.CreatedCuratedPackage(
                    It.IsAny<CuratedFeed>(),
                    It.IsAny<PackageRegistration>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public void WillNotIncludeThePackageWhenItDependsOnAPackageThatIsExcludedInTheFeed()
        {
            var curator = TestableWindows8PackageCurator.Create(null);
            curator.StubCuratedFeed.Packages.Add(new CuratedPackage { AutomaticallyCurated = false, Included = false, PackageRegistration = new PackageRegistration { Id = "ManuallyExcludedPackage" } });

            var stubGalleryPackage = CreateStubGalleryPackage();
            stubGalleryPackage.Tags = "win8";
            stubGalleryPackage.Dependencies.Add(new PackageDependency { Id = "ManuallyExcludedPackage" });

            curator.Curate(stubGalleryPackage, CreateStubNuGetPackage().Object, commitChanges: true);

            curator.StubCuratedFeedService.Verify(
                stub => stub.CreatedCuratedPackage(
                    It.IsAny<CuratedFeed>(),
                    It.IsAny<PackageRegistration>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public void WillIncludeThePackageUsingTheCuratedFeedKey()
        {
            var curator = TestableWindows8PackageCurator.Create(null);
            curator.StubCuratedFeed.Key = 42;
            var package = CreateStubGalleryPackage();
            package.Tags = "winrt";

            curator.Curate(package, CreateStubNuGetPackage().Object, commitChanges: true);

            curator.StubCuratedFeedService.Verify(
                stub => stub.CreatedCuratedPackage(
                    curator.StubCuratedFeed,
                    It.IsAny<PackageRegistration>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()));
        }

        [Fact]
        public void WillIncludeThePackageUsingThePackageRegistrationKey()
        {
            var curator = TestableWindows8PackageCurator.Create(null);
            var stubGalleryPackage = CreateStubGalleryPackage();
            stubGalleryPackage.PackageRegistration.Key = 42;
            stubGalleryPackage.Tags = "winrt";

            curator.Curate(stubGalleryPackage, CreateStubNuGetPackage().Object, commitChanges: true);

            curator.StubCuratedFeedService.Verify(
                stub => stub.CreatedCuratedPackage(
                    It.IsAny<CuratedFeed>(),
                    stubGalleryPackage.PackageRegistration,
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()));
        }

        [Fact]
        public void WillSetTheAutomaticBitWhenIncludingThePackage()
        {
            var curator = TestableWindows8PackageCurator.Create(null);
            var stubGalleryPackage = CreateStubGalleryPackage();
            stubGalleryPackage.Tags = "winrt";

            curator.Curate(stubGalleryPackage, CreateStubNuGetPackage().Object, commitChanges: true);

            curator.StubCuratedFeedService.Verify(
                stub => stub.CreatedCuratedPackage(
                    It.IsAny<CuratedFeed>(),
                    It.IsAny<PackageRegistration>(),
                    It.IsAny<bool>(),
                    true,
                    It.IsAny<string>(),
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
            stubNuGetPackage.Setup(stub => stub.GetFiles()).Returns(new string[] {});
            return stubNuGetPackage;
        }
    }
}
