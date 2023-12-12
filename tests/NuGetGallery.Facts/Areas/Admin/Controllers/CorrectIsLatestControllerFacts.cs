// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class CorrectIsLatestControllerFacts
    {
        public class CorrectIsLatestPackagesMethod: FactsBase
        {
            [Fact]
            public void WhenPackagesHaveCorrectIsLatestReturnEmptyArray()
            {
                var result = CorrectIsLatestController.CorrectIsLatestPackages() as JsonResult;
                var packages = result.Data as List<CorrectIsLatestPackage>;

                Assert.Empty(packages);
            }

            [Theory]
            [MemberData(nameof(TestData))]
            public void WhenPackageIsLatestAndIsUnlistedReturnsHasIsLatestUnlistedAsTrue(bool isLatest, bool isLatestStable, bool isLatestSemVer2, bool isLatestStableSemVer2)
            {
                var validPackages = new HashSet<Package>();
                var packageId = "IsLatestAndUnlisted";
                validPackages.Add(new Package()
                {
                    Id = packageId,
                    Version = "1.0.0",
                    Listed = false,
                    IsLatest = isLatest,
                    IsLatestStable = isLatestStable,
                    IsLatestSemVer2 = isLatestSemVer2,
                    IsLatestStableSemVer2 = isLatestStableSemVer2,
                });
                validPackages.Add(new Package()
                {
                    Id = packageId,
                    Version = "1.0.1",
                    Listed = true,
                    IsLatest = false,
                    IsLatestStable = false,
                    IsLatestSemVer2 = false,
                    IsLatestStableSemVer2 = false,
                });

                var packageRegistration = new PackageRegistration()
                {
                    Id = packageId,
                    Packages = validPackages
                };

                var fakeContext = GetFakeContext();
                fakeContext.PackageRegistrations.Add(packageRegistration);

                var result = CorrectIsLatestController.CorrectIsLatestPackages() as JsonResult;
                var correctIsLatestPackages = result.Data as List<CorrectIsLatestPackage>;

                Assert.NotEmpty(correctIsLatestPackages);

                var correctIsLatestPackage = correctIsLatestPackages[0];
                Assert.True(correctIsLatestPackage.HasIsLatestUnlisted);
                Assert.True(correctIsLatestPackage.IsLatestCount > 0 || 
                    correctIsLatestPackage.IsLatestStableCount > 0 || 
                    correctIsLatestPackage.IsLatestSemVer2Count > 0 ||
                    correctIsLatestPackage.IsLatestStableSemVer2Count > 0);
            }

            [Theory]
            [MemberData(nameof(TestData))]
            public void WhenPackageHasMultipleIsLatestReturnSum(bool isLatest, bool isLatestStable, bool isLatestSemVer2, bool isLatestStableSemVer2)
            {
                var packages = new HashSet<Package>();
                var packageId = "MultipleIsLatest";
                packages.Add(new Package()
                {
                    Id = packageId,
                    Version = "1.0.0",
                    Listed = true,
                    IsLatest = isLatest,
                    IsLatestStable = isLatestStable,
                    IsLatestSemVer2 = isLatestSemVer2,
                    IsLatestStableSemVer2 = isLatestStableSemVer2,
                });
                packages.Add(new Package()
                {
                    Id = packageId,
                    Version = "1.0.1",
                    Listed = true,
                    IsLatest = isLatest,
                    IsLatestStable = isLatestStable,
                    IsLatestSemVer2 = isLatestSemVer2,
                    IsLatestStableSemVer2 = isLatestStableSemVer2,
                });

                var packageRegistration = new PackageRegistration()
                {
                    Id = packageId,
                    Packages = packages
                };

                var fakeContext = GetFakeContext();
                fakeContext.PackageRegistrations.Add(packageRegistration);

                var result = CorrectIsLatestController.CorrectIsLatestPackages() as JsonResult;
                var correctIsLatestPackages = result.Data as List<CorrectIsLatestPackage>;

                Assert.NotEmpty(packages);

                var correctIsLatestPackage = correctIsLatestPackages[0];
                Assert.False(correctIsLatestPackage.HasIsLatestUnlisted);

                if (isLatest)
                {
                    Assert.True(correctIsLatestPackage.IsLatestCount == 2);
                }
                if (isLatestStable)
                {
                    Assert.True(correctIsLatestPackage.IsLatestStableCount == 2);
                }
                if (isLatestSemVer2)
                {
                    Assert.True(correctIsLatestPackage.IsLatestSemVer2Count == 2);
                }
                if (isLatestStableSemVer2)
                {
                    Assert.True(correctIsLatestPackage.IsLatestStableSemVer2Count == 2);
                }
            }
        }

        public class ReflowPackagesMethod
        {

        }

        public class FactsBase: TestContainer
        {
            protected CorrectIsLatestController CorrectIsLatestController;

            public FactsBase() {
                var correctPackages = new HashSet<Package>();
                var correctPackageId = "TheCorrectIsLatestPackage";
                correctPackages.Add(new Package()
                {
                    Id = correctPackageId,
                    Version = "1.0.0",
                    Listed = true,
                    IsLatest = false,
                    IsLatestStable = false,
                    IsLatestSemVer2 = false,
                    IsLatestStableSemVer2 = false,
                });
                correctPackages.Add(new Package()
                {
                    Id = correctPackageId,
                    Version = "1.0.1",
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true,
                    IsLatestSemVer2 = true,
                    IsLatestStableSemVer2 = true,
                });

                var packageRegistration = new PackageRegistration()
                {
                    Id = correctPackageId,
                    Packages = correctPackages
                };

                var fakeContext = GetFakeContext();
                fakeContext.PackageRegistrations.Add(packageRegistration);

                CorrectIsLatestController = new CorrectIsLatestController(
                    GetMock<IPackageService>().Object,
                    GetFakeContext(),
                    GetMock<IPackageFileService>().Object,
                    GetMock<ITelemetryService>().Object);
            }

            public static IEnumerable<object[]> TestData
            {
                get
                {
                    yield return new object[] { true, false, false, false };
                    yield return new object[] { false, true, false, false };
                    yield return new object[] { false, false, true, false };
                    yield return new object[] { false, false, false, true };
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (CorrectIsLatestController != null)
                {
                    CorrectIsLatestController.Dispose();
                }

                base.Dispose(disposing);
            }
        }

    }
}
