// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Framework;
using NuGetGallery.Helpers;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class CorrectIsLatestControllerFacts
    {
        public class CorrectIsLatestPackagesMethod : FactsBase
        {
            [Fact]
            public void WhenPackagesHaveCorrectIsLatestReturnEmptyArray()
            {
                var result = (JsonResult)CorrectIsLatestController.CorrectIsLatestPackages();
                var packages = (List<CorrectIsLatestPackage>)result.Data;

                Assert.Empty(packages);
            }

            [Theory]
            [MemberData(nameof(IsLatestTestData))]
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

                var result = (JsonResult)CorrectIsLatestController.CorrectIsLatestPackages();
                var correctIsLatestPackages = (List<CorrectIsLatestPackage>)result.Data;

                Assert.NotEmpty(correctIsLatestPackages);

                var correctIsLatestPackage = correctIsLatestPackages[0];
                Assert.True(correctIsLatestPackage.HasIsLatestUnlisted);
                Assert.True(correctIsLatestPackage.IsLatestCount > 0 ||
                    correctIsLatestPackage.IsLatestStableCount > 0 ||
                    correctIsLatestPackage.IsLatestSemVer2Count > 0 ||
                    correctIsLatestPackage.IsLatestStableSemVer2Count > 0);
            }

            [Theory]
            [MemberData(nameof(IsLatestTestData))]
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

                var result = (JsonResult)CorrectIsLatestController.CorrectIsLatestPackages();
                var correctIsLatestPackages = (List<CorrectIsLatestPackage>)result.Data;

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

        public class ReflowPackagesMethod : FactsBase
        {
            [Theory]
            [MemberData(nameof(BadReflowPackagesTestData))]
            public async Task WhenInvalidReturnsBadRequest(CorrectIsLatestRequest request)
            {
                // Act
                var result = (JsonResult)await CorrectIsLatestController.ReflowPackages(request);

                // Assert
                Assert.Equal(((int)HttpStatusCode.BadRequest), CorrectIsLatestController.Response.StatusCode);
                Assert.Equal("Packages cannot be null or empty.", result.Data);
            }

            [Fact]
            public async Task WhenPackageValidReturnsReflowCount()
            {
                // Arrange
                var request = new CorrectIsLatestRequest()
                {
                    Packages = new List<CorrectIsLatestPackageRequest>()
                    {
                        new CorrectIsLatestPackageRequest()
                        {
                            Id = ReflowPackage.Id,
                            Version = ReflowPackage.Version
                        },
                        new CorrectIsLatestPackageRequest()
                        {
                            Id = ReflowPackage2.Id,
                            Version = ReflowPackage2.Version
                        }
                    }
                };

                // Act
                var result = (JsonResult)await CorrectIsLatestController.ReflowPackages(request);

                // Assert
                Assert.Equal(((int)HttpStatusCode.OK), CorrectIsLatestController.Response.StatusCode);
                Assert.Equal("2 packages reflowed, 0 packages fail reflow.", result.Data);
            }

            [Fact]
            public async Task WhenPackageInvalidReturnsReflowCount()
            {
                // Arrange
                var request = new CorrectIsLatestRequest()
                {
                    Packages = new List<CorrectIsLatestPackageRequest>()
                    {
                        new CorrectIsLatestPackageRequest()
                        {
                            Id = FailReflowPackage.Id,
                            Version = FailReflowPackage.Version
                        },
                        new CorrectIsLatestPackageRequest()
                        {
                            Id = FailReflowPackage2.Id,
                            Version = FailReflowPackage2.Version
                        }
                    }
                };

                // Act
                var result = (JsonResult)await CorrectIsLatestController.ReflowPackages(request);

                // Assert
                Assert.Equal(((int)HttpStatusCode.OK), CorrectIsLatestController.Response.StatusCode);
                Assert.Equal("0 packages reflowed, 2 packages fail reflow.", result.Data);
            }

            [Fact]
            public async Task WhenPackageValidAndInvalidReturnsReflowCount()
            {
                // Arrange
                var request = new CorrectIsLatestRequest()
                {
                    Packages = new List<CorrectIsLatestPackageRequest>()
                    {
                        new CorrectIsLatestPackageRequest()
                        {
                            Id = ReflowPackage.Id,
                            Version = ReflowPackage.Version
                        },
                        new CorrectIsLatestPackageRequest()
                        {
                            Id = FailReflowPackage.Id,
                            Version = FailReflowPackage.Version
                        }
                    }
                };

                // Act
                var result = (JsonResult)await CorrectIsLatestController.ReflowPackages(request);

                // Assert
                Assert.Equal(((int)HttpStatusCode.OK), CorrectIsLatestController.Response.StatusCode);
                Assert.Equal("1 package reflowed, 1 package fail reflow.", result.Data);
            }
        }

        public class FactsBase : TestContainer
        {
            protected CorrectIsLatestController CorrectIsLatestController;
            protected Mock<PackageService> PackageServiceMock;
            protected Mock<IPackageFileService> PackageFileServiceMock;

            protected Package ReflowPackage;
            protected Package ReflowPackage2;
            protected Package FailReflowPackage;
            protected Package FailReflowPackage2;

            public FactsBase()
            {
                var entitiesContextMock = ReflowServiceSetupHelper.SetupEntitiesContext();
                var database = new Mock<IDatabase>();
                database.Setup(x => x.BeginTransaction()).Returns(() => new Mock<IDbContextTransaction>().Object);
                entitiesContextMock.Setup(m => m.GetDatabase()).Returns(database.Object);
                
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

                entitiesContextMock
                    .SetupGet(ec => ec.PackageRegistrations)
                    .Returns(fakeContext.PackageRegistrations);

                PackageServiceMock = ReflowServiceSetupHelper.SetupPackageService();
                PackageFileServiceMock = new Mock<IPackageFileService>();

                ReflowPackage = PackageServiceUtility.CreateTestPackage("ReflowPackage");
                ReflowPackage2 = PackageServiceUtility.CreateTestPackage("ReflowPackage2");
                FailReflowPackage = PackageServiceUtility.CreateTestPackage("FailReflowPackage");
                FailReflowPackage2 = PackageServiceUtility.CreateTestPackage("FailReflowPackage2");

                ReflowServiceSetupHelper.SetupPackages(PackageServiceMock, PackageFileServiceMock, new List<Package>() { ReflowPackage, ReflowPackage2 });
                PackageServiceMock.ThrowFindPackageByIdAndVersionStrict(new List<Package>() { FailReflowPackage, FailReflowPackage2 });

                CorrectIsLatestController = new CorrectIsLatestController(
                    PackageServiceMock.Object,
                    entitiesContextMock.Object,
                    PackageFileServiceMock.Object,
                    GetMock<ITelemetryService>().Object);

                TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), CorrectIsLatestController);
            }

            public static IEnumerable<object[]> IsLatestTestData
            {
                get
                {
                    yield return new object[] { true, false, false, false };
                    yield return new object[] { false, true, false, false };
                    yield return new object[] { false, false, true, false };
                    yield return new object[] { false, false, false, true };
                }
            }

            public static IEnumerable<object[]> BadReflowPackagesTestData
            {
                get
                {
                    yield return new object[] { null };
                    yield return new object[] { new CorrectIsLatestRequest() };
                    yield return new object[] { new CorrectIsLatestRequest() { Packages = new List<CorrectIsLatestPackageRequest>() } };
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
