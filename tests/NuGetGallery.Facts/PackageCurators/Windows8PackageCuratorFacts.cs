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
    public class Windows8PackageCuratorFacts
    {
        public class TestableWindows8PackageCurator : Windows8PackageCurator
        {
            public TestableWindows8PackageCurator()
            {
                StubCuratedFeed = new CuratedFeed { Key = 0 };
                StubCuratedFeedService = new Mock<ICuratedFeedService>();

                StubCuratedFeedService
                    .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(StubCuratedFeed);
            }

            public CuratedFeed StubCuratedFeed { get; private set; }
            public Mock<ICuratedFeedService> StubCuratedFeedService { get; private set; }

            protected override T GetService<T>()
            {
                if (typeof(T) == typeof(ICuratedFeedService))
                {
                    return (T)StubCuratedFeedService.Object;
                }

                throw new Exception("Tried to get an unexpected service.");
            }
        }

        public class TheCurateMethod
        {
            [Fact]
            public void WillNotIncludeThePackageWhenTheWindows8CuratedFeedDoesNotExist()
            {
                var curator = new TestableWindows8PackageCurator();
                curator.StubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);
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
                var curator = new TestableWindows8PackageCurator();
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
                var curator = new TestableWindows8PackageCurator();
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
                var curator = new TestableWindows8PackageCurator();
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
                var curator = new TestableWindows8PackageCurator();
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
                var curator = new TestableWindows8PackageCurator();
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
                var curator = new TestableWindows8PackageCurator();
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
                var curator = new TestableWindows8PackageCurator();
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
                var curator = new TestableWindows8PackageCurator();
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
}
