// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;
using NuGet.Services.Messaging.Email;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.AsyncFileUpload;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using NuGetGallery.Frameworks;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Infrastructure.Mail.Requests;
using NuGetGallery.Infrastructure.Search;
using NuGetGallery.Infrastructure.Search.Models;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using NuGetGallery.Services;
using NuGetGallery.Services.Helpers;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class PackagesControllerFacts
        : TestContainer
    {
        private static PackagesController CreateController(
            IGalleryConfigurationService configurationService,
            IPackageFilter packageFilter = null,
            Mock<IPackageService> packageService = null,
            Mock<IPackageUpdateService> packageUpdateService = null,
            Mock<IUploadFileService> uploadFileService = null,
            Mock<IUserService> userService = null,
            Mock<IMessageService> messageService = null,
            Mock<HttpContextBase> httpContext = null,
            Stream fakeNuGetPackage = null,
            Mock<ISearchService> searchService = null,
            Mock<ISearchService> previewSearchService = null,
            Exception readPackageException = null,
            Mock<IPackageFileService> packageFileService = null,
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IIndexingService> indexingService = null,
            Mock<ICacheService> cacheService = null,
            Mock<IPackageDeleteService> packageDeleteService = null,
            Mock<ISupportRequestService> supportRequestService = null,
            IAuditingService auditingService = null,
            Mock<ITelemetryService> telemetryService = null,
            Mock<ISecurityPolicyService> securityPolicyService = null,
            Mock<IReservedNamespaceService> reservedNamespaceService = null,
            Mock<IPackageUploadService> packageUploadService = null,
            Mock<IValidationService> validationService = null,
            Mock<IPackageOwnershipManagementService> packageOwnershipManagementService = null,
            IReadMeService readMeService = null,
            Mock<IContentObjectService> contentObjectService = null,
            Mock<ISymbolPackageUploadService> symbolPackageUploadService = null,
            Mock<ICoreLicenseFileService> coreLicenseFileService = null,
            Mock<ILicenseExpressionSplitter> licenseExpressionSplitter = null,
            Mock<IFeatureFlagService> featureFlagService = null,
            Mock<IPackageDeprecationService> deprecationService = null,
            Mock<IPackageVulnerabilitiesService> vulnerabilitiesService = null,
            Mock<IPackageRenameService> renameService = null,
            Mock<IABTestService> abTestService = null,
            Mock<IIconUrlProvider> iconUrlProvider = null,
            Mock<IMarkdownService> markdownService = null,
            Mock<IPackageFrameworkCompatibilityFactory> compatibilityFactory = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            PackageDependents packageDependents = new PackageDependents();
            packageService.Setup(x => x.GetPackageDependents(It.IsAny<string>())).Returns(packageDependents);

            packageUpdateService = packageUpdateService ?? new Mock<IPackageUpdateService>();
            if (uploadFileService == null)
            {
                uploadFileService = new Mock<IUploadFileService>();
                uploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                uploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult<Stream>(null));
                uploadFileService.Setup(x => x.SaveUploadFileAsync(It.IsAny<int>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }
            userService = userService ?? new Mock<IUserService>();
            messageService = messageService ?? new Mock<IMessageService>();

            searchService = searchService ?? new Mock<ISearchService>();
            previewSearchService = previewSearchService ?? new Mock<ISearchService>();

            var searchServiceFactory = new Mock<ISearchServiceFactory>();
            searchServiceFactory.Setup(x => x.GetService()).Returns(() => searchService.Object);
            searchServiceFactory.Setup(x => x.GetPreviewService()).Returns(() => previewSearchService.Object);

            if (packageFileService == null)
            {
                packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }

            packageFilter = packageFilter ?? new PackageFilter(packageService.Object);

            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();

            indexingService = indexingService ?? new Mock<IIndexingService>();

            cacheService = cacheService ?? new Mock<ICacheService>();

            packageDeleteService = packageDeleteService ?? new Mock<IPackageDeleteService>();

            supportRequestService = supportRequestService ?? new Mock<ISupportRequestService>();

            auditingService = auditingService ?? new TestAuditingService();

            telemetryService = telemetryService ?? new Mock<ITelemetryService>();

            if (securityPolicyService == null)
            {
                securityPolicyService = new Mock<ISecurityPolicyService>();
                securityPolicyService
                    .Setup(m => m.EvaluatePackagePoliciesAsync(SecurityPolicyAction.PackagePush, It.IsAny<Package>(), It.IsAny<User>(), It.IsAny<User>(), It.IsAny<HttpContextBase>()))
                    .ReturnsAsync(SecurityPolicyResult.SuccessResult);
            }

            if (reservedNamespaceService == null)
            {
                reservedNamespaceService = new Mock<IReservedNamespaceService>();
                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = new List<ReservedNamespace>();
                reservedNamespaceService.Setup(s => s.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(Array.Empty<ReservedNamespace>());
            }

            if (packageUploadService == null)
            {
                packageUploadService = new Mock<IPackageUploadService>();

                packageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageMetadata>(),
                        It.IsAny<User>()))
                    .ReturnsAsync(PackageValidationResult.Accepted());

                packageUploadService
                    .Setup(x => x.ValidateAfterGeneratePackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<User>(),
                        It.IsAny<User>(),
                        It.IsAny<bool>()))
                    .ReturnsAsync(PackageValidationResult.Accepted());
            }

            packageUploadService = packageUploadService ?? new Mock<IPackageUploadService>();

            validationService = validationService ?? new Mock<IValidationService>();

            packageOwnershipManagementService = packageOwnershipManagementService ?? new Mock<IPackageOwnershipManagementService>();

            if (markdownService == null)
            {
                var mockMarkdownService = new Mock<IMarkdownService>();

                mockMarkdownService.Setup(x => x.GetHtmlFromMarkdown(It.IsAny<string>()))
                    .Returns((string markdown) => new RenderedMarkdownResult { Content = mockGetHtml(markdown) });

                mockMarkdownService.Setup(x => x.GetHtmlFromMarkdown(It.IsAny<string>(), It.IsAny<int>()))
                    .Returns((string markdown, int h) => new RenderedMarkdownResult { Content = mockGetHtml(markdown) });

                markdownService = mockMarkdownService;
            }

            readMeService = readMeService ?? new ReadMeService(
                packageFileService.Object, entitiesContext.Object, markdownService.Object, new Mock<ICoreReadmeFileService>().Object);

            if (contentObjectService == null)
            {
                contentObjectService = new Mock<IContentObjectService>();
                contentObjectService
                    .Setup(x => x.SymbolsConfiguration.IsSymbolsUploadEnabledForUser(It.IsAny<User>()))
                    .Returns(false);
                contentObjectService
                    .SetupGet(c => c.GitHubUsageConfiguration)
                    .Returns(new GitHubUsageConfiguration(Array.Empty<RepositoryInformation>()));
                contentObjectService
                    .SetupGet(c => c.CacheConfiguration)
                    .Returns(new CacheConfiguration());
            }

            if (symbolPackageUploadService == null)
            {
                symbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                symbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(new Package()
                    {
                        PackageRegistration = new PackageRegistration() { Id = "thePackageId" },
                        Version = "1.0.42",
                        NormalizedVersion = "1.0.42"
                    }));
                symbolPackageUploadService
                    .Setup(x => x.DeleteSymbolsPackageAsync(It.IsAny<SymbolPackage>()))
                    .Completes();
            }

            if (coreLicenseFileService == null)
            {
                coreLicenseFileService = new Mock<ICoreLicenseFileService>();
                coreLicenseFileService
                    .Setup(clfs => clfs.DownloadLicenseFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(() => new MemoryStream());
            }

            licenseExpressionSplitter = licenseExpressionSplitter ?? new Mock<ILicenseExpressionSplitter>();

            if (featureFlagService == null)
            {
                featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService.SetReturnsDefault<bool>(true);
            }

            renameService = renameService ?? new Mock<IPackageRenameService>();
            if (deprecationService == null)
            {
                deprecationService = new Mock<IPackageDeprecationService>();
                deprecationService
                    .Setup(x => x.GetDeprecationsById(It.IsAny<string>()))
                    .Returns(new List<PackageDeprecation>());
            }

            if (vulnerabilitiesService == null)
            {
                vulnerabilitiesService = new Mock<IPackageVulnerabilitiesService>();
                vulnerabilitiesService
                    .Setup(x => x.GetVulnerabilitiesById(It.IsAny<string>()))
                    .Returns(new Dictionary<int, IReadOnlyList<PackageVulnerability>>());
            }

            if(compatibilityFactory == null)
            {
                compatibilityFactory = new Mock<IPackageFrameworkCompatibilityFactory>();
                compatibilityFactory
                    .Setup(x => x.Create(It.IsAny<ICollection<PackageFramework>>(), string.Empty, string.Empty, false))
                    .Returns(new PackageFrameworkCompatibility());
            }

            iconUrlProvider = iconUrlProvider ?? new Mock<IIconUrlProvider>();

            abTestService = abTestService ?? new Mock<IABTestService>();

            var diagnosticsService = new Mock<IDiagnosticsService>();

            var controller = new Mock<PackagesController>(
                packageFilter,
                packageService.Object,
                packageUpdateService.Object,
                uploadFileService.Object,
                userService.Object,
                messageService.Object,
                searchServiceFactory.Object,
                packageFileService.Object,
                entitiesContext.Object,
                configurationService.Current,
                indexingService.Object,
                cacheService.Object,
                packageDeleteService.Object,
                supportRequestService.Object,
                auditingService,
                telemetryService.Object,
                securityPolicyService.Object,
                reservedNamespaceService.Object,
                packageUploadService.Object,
                readMeService,
                validationService.Object,
                packageOwnershipManagementService.Object,
                contentObjectService.Object,
                symbolPackageUploadService.Object,
                diagnosticsService.Object,
                coreLicenseFileService.Object,
                licenseExpressionSplitter.Object,
                featureFlagService.Object,
                deprecationService.Object,
                vulnerabilitiesService.Object,
                renameService.Object,
                abTestService.Object,
                iconUrlProvider.Object,
                markdownService.Object,
                compatibilityFactory.Object);

            controller.CallBase = true;
            controller.Object.SetOwinContextOverride(Fakes.CreateOwinContext());

            httpContext = httpContext ?? new Mock<HttpContextBase>();
            httpContext.Setup(c => c.Cache).Returns(new Cache());
            TestUtility.SetupHttpContextMockForUrlGeneration(httpContext, controller.Object);

            if (readPackageException != null)
            {
                controller.Setup(x => x.CreatePackage(It.IsAny<Stream>())).Throws(readPackageException);
            }
            else
            {
                if (fakeNuGetPackage == null)
                {
                    fakeNuGetPackage = TestPackage.CreateTestPackageStream("thePackageId", "1.0.0");
                }

                controller.Setup(x => x.CreatePackage(It.IsAny<Stream>())).Returns(() => new PackageArchiveReader(fakeNuGetPackage, true));
            }

            return controller.Object;
        }

        private static string mockGetHtml(string markdown)
        {
            if (markdown == null) 
            {
                return null;
            }

            if (markdown.StartsWith("#"))
            {
                return "<h2>" + markdown.Trim('#', ' ') + "</h2>";
            }

            return markdown;
        }

        private static Mock<ISymbolPackageUploadService> GetValidSymbolPackageUploadService(string packageId,
            string packageVersion,
            User owner,
            PackageValidationResultType type = PackageValidationResultType.Accepted,
            PackageCommitResult commit = PackageCommitResult.Success)
        {
            var package = new Package()
            {
                PackageRegistration = new PackageRegistration()
                {
                    Id = packageId,
                    Owners = new List<User>() { owner }
                },
                Version = packageVersion,
                NormalizedVersion = packageVersion
            };

            var fakeSymbolsPackageUploadService = new Mock<ISymbolPackageUploadService>();
            fakeSymbolsPackageUploadService
                .Setup(x => x.ValidateUploadedSymbolsPackage(
                    It.IsAny<Stream>(),
                    It.IsAny<User>()))
                .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(package));

            fakeSymbolsPackageUploadService
                .Setup(x => x.CreateAndUploadSymbolsPackage(
                    It.IsAny<Package>(),
                    It.IsAny<Stream>()))
                .ReturnsAsync(commit);

            return fakeSymbolsPackageUploadService;
        }

        private static Mock<IPackageUploadService> GetValidPackageUploadService(string packageId, string packageVersion, PackageValidationResultType type = PackageValidationResultType.Accepted)
        {
            var fakePackageUploadService = new Mock<IPackageUploadService>();

            fakePackageUploadService
                .Setup(x => x.ValidateBeforeGeneratePackageAsync(
                    It.IsAny<PackageArchiveReader>(),
                    It.IsAny<PackageMetadata>(),
                        It.IsAny<User>()))
                .ReturnsAsync(PackageValidationResult.Accepted());

            fakePackageUploadService
                .Setup(x => x.GeneratePackageAsync(
                    It.IsAny<string>(),
                    It.IsAny<PackageArchiveReader>(),
                    It.IsAny<PackageStreamMetadata>(),
                    It.IsAny<User>(),
                    It.IsAny<User>()))
                .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = packageId }, Version = packageVersion }));

            fakePackageUploadService
                .Setup(x => x.ValidateAfterGeneratePackageAsync(
                    It.IsAny<Package>(),
                    It.IsAny<PackageArchiveReader>(),
                    It.IsAny<User>(),
                    It.IsAny<User>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(new PackageValidationResult(
                    type, message: new PlainTextOnlyValidationMessage("Something"), warnings: null));

            return fakePackageUploadService;
        }

        public class TheCancelVerifyPackageAction
            : TestContainer
        {
            [Fact]
            public async Task DeletesTheInProgressPackageUpload()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.CancelUpload();

                fakeUploadFileService.Verify(x => x.DeleteUploadFileAsync(42));
            }

            [Fact]
            public async Task RedirectsToUploadPageAfterDelete()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.CancelUpload();

                Assert.IsType<JsonResult>(result);
                Assert.Null(result.Data);
            }
        }

        public class TheDisplayPackageMethod
            : TestContainer, IDisposable
        {
            private Cache _cache;

            public TheDisplayPackageMethod()
            {
                _cache = new Cache();
            }

            public static IEnumerable<object[]> PackageIsIndexedTestData => new[]
            {
                new object[] { DateTime.UtcNow, null, 1 },
                new object[] { DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, 1 },
                new object[] { DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), 1 },
                new object[] { DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1), 0 },
                new object[] { DateTime.UtcNow.AddDays(-1), null, 0 },
            };

            [Theory]
            [MemberData(nameof(PackageIsIndexedTestData))]
            public async Task IsIndexCheckOnlyHappensForRecentlyChangedPackages(DateTime created, DateTime? lastEdited, int searchTimes)
            {
                // Arrange
                var id = "Test" + Guid.NewGuid().ToString();
                var packageService = new Mock<IPackageService>();
                var diagnosticsService = new Mock<IDiagnosticsService>();
                var searchClient = new Mock<ISearchClient>();
                var searchService = new Mock<ExternalSearchService>(diagnosticsService.Object, searchClient.Object)
                {
                    CallBase = true,
                };
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Cache).Returns(new Cache());
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    searchService: searchService.As<ISearchService>(),
                    httpContext: httpContext);
                controller.SetCurrentUser(TestUtility.FakeUser);

                searchService
                    .Setup(x => x.RawSearch(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(() => new SearchResults(0, indexTimestampUtc: null));

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>(),
                    },
                    Created = created,
                    LastEdited = lastEdited,
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                };

                var packages = new List<Package> { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);
                packageService
                    .Setup(p => p.FilterExactPackage(It.IsAny<IReadOnlyCollection<Package>>(), It.IsAny<string>()))
                    .Returns(package);

                // Act
                var result = await controller.DisplayPackage(package.Id, package.Version);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(id, model.Id);
                searchService.Verify(x => x.RawSearch(It.IsAny<SearchFilter>()), Times.Exactly(searchTimes));
            }

            [Fact]
            public async Task GivenANonNormalizedVersionIt302sToTheNormalizedVersion()
            {
                // Arrange
                var controller = CreateController(GetConfigurationService());

                // Act
                var result = await controller.DisplayPackage("Foo", "01.01.01");

                // Assert
                ResultAssert.IsRedirectToRoute(result, new
                {
                    action = "DisplayPackage",
                    id = "Foo",
                    version = "1.1.1"
                }, permanent: true);
            }

            [Fact]
            public async Task GivenANonExistentPackageIt404s()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                var version = "1.1.1";
                var packages = Array.Empty<Package>();
                packageService.Setup(p => p.FindPackagesById("Foo", PackageDeprecationFieldsToInclude.Deprecation))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterExactPackage(packages, version))
                    .Returns((Package)null);

                // Act
                var result = await controller.DisplayPackage("Foo", version);

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Fact]
            public void GivenADeleteMethodIt405sWithAllowHeader()
            {
                // Arrange
                var controller = CreateController(GetConfigurationService());

                // Act
                var result = controller.DisplayPackage();

                // Assert
                ResultAssert.IsStatusCodeWithHeaders(result, HttpStatusCode.MethodNotAllowed, new NameValueCollection() { { "allow", "GET" } });
            }

            public static IEnumerable<PackageStatus> ValidatingPackageStatuses =
                new[] { PackageStatus.Validating, PackageStatus.FailedValidation };

            public static IEnumerable<object[]> GivenAValidatingPackage_Data => ValidatingPackageStatuses.Select(s => new object[] { s });

            [Theory]
            [MemberData(nameof(GivenAValidatingPackage_Data))]
            public async Task GivenAValidatingPackageThatTheCurrentUserOwnsThenShowIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    TestUtility.FakeUser,
                    TestUtility.FakeUser,
                    true);

                // Assert
                Assert.IsType<ViewResult>(result);
            }

            [Theory]
            [MemberData(nameof(GivenAValidatingPackage_Data))]
            public async Task GivenAValidatingPackageAsAdminThenShowIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    TestUtility.FakeAdminUser,
                    new User { Key = 132114 },
                    true);

                // Assert
                Assert.IsType<ViewResult>(result);
            }

            public static IEnumerable<object[]> GivenAValidatingPackageThatCurrentUsersOrganizationOwnsThenShowIt_Data
            {
                get
                {
                    foreach (var isAdmin in new[] { true, false })
                    {
                        foreach (var status in ValidatingPackageStatuses)
                        {
                            yield return new object[]
                            {
                                status,
                                isAdmin
                            };
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(GivenAValidatingPackageThatCurrentUsersOrganizationOwnsThenShowIt_Data))]
            public async Task GivenAValidatingPackageThatCurrentUsersOrganizationOwnsThenShowIt(PackageStatus packageStatus, bool isAdmin)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    isAdmin ? TestUtility.FakeOrganizationAdmin : TestUtility.FakeOrganizationCollaborator,
                    TestUtility.FakeOrganization,
                    true);

                // Assert
                Assert.IsType<ViewResult>(result);
            }

            [Theory]
            [MemberData(nameof(GivenAValidatingPackage_Data))]
            public async Task GivenAValidatingPackageWhileLoggedOutThenHideIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    null,
                    new User { Key = 132114 },
                    false);

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Theory]
            [MemberData(nameof(GivenAValidatingPackage_Data))]
            public async Task GivenAValidatingPackageThatTheCurrentUserDoesNotOwnThenHideIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    TestUtility.FakeUser,
                    new User { Key = 132114 },
                    false);

                // Assert
                ResultAssert.IsNotFound(result);
            }

            private async Task<ActionResult> GetActionResultForPackageStatusAsync(
                PackageStatus packageStatus,
                User currentUser,
                User owner,
                bool expectSuccess)
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    httpContext: httpContext,
                    deprecationService: deprecationService);
                controller.SetCurrentUser(currentUser);

                httpContext.Setup(c => c.Response.Cache).Returns(httpCachePolicy.Object);

                var id = "NuGet.Versioning";
                var version = "3.4.0";
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new[] { owner }
                    },
                    Version = version,
                    NormalizedVersion = version,
                    PackageStatusKey = packageStatus,
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterExactPackage(packages, version))
                    .Returns(package);

                var getDeprecationsByIdSetup = deprecationService
                    .Setup(x => x.GetDeprecationsById(id));

                if (expectSuccess)
                {
                    getDeprecationsByIdSetup
                        .Returns(new List<PackageDeprecation>())
                        .Verifiable();
                }
                else
                {
                    getDeprecationsByIdSetup.Throws(new InvalidOperationException());
                }

                // Act
                var result = await controller.DisplayPackage(
                    id,
                    version);

                if (expectSuccess)
                {
                    deprecationService.Verify();
                }

                return result;
            }

            public static IEnumerable<object[]> PackageOwners
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        new User { Key = 12414 }
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            public static IEnumerable<object[]> PackageNonOwners
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 12414 }
                    };

                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };
                }
            }

            [Theory]
            [MemberData(nameof(PackageOwners))]
            public Task GivenAnOwnedValidPackageWithNoEditsItDisplaysCurrentMetadata(User currentUser, User owner)
            {
                return CheckValidPackage(currentUser, owner);
            }

            [Theory]
            [MemberData(nameof(PackageNonOwners))]
            public Task GivenAnUnownedValidPackageWithNoEditsItDisplaysCurrentMetadata(User currentUser, User owner)
            {
                return CheckValidPackage(currentUser, owner);
            }

            private async Task CheckValidPackage(User currentUser, User owner)
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService,
                    httpContext: httpContext);
                controller.SetCurrentUser(currentUser);
                httpContext.Setup(c => c.Response.Cache).Returns(httpCachePolicy.Object);
                var title = "A test package!";
                var id = "Foo";
                var normalizedVersion = "1.1.1";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>() { owner }
                    },
                    Version = "01.1.01",
                    NormalizedVersion = normalizedVersion,
                    Title = title
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterExactPackage(packages, normalizedVersion))
                    .Returns(package);

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage(id, normalizedVersion);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
            }

            [Fact]
            public async Task GivenAnAbsoluteLatestVersionItReturnsTheFirstLatestSemVer2()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var notLatestPackage = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "3.0.0",
                    NormalizedVersion = "3.0.0",
                    Title = "An old test package!"
                };

                var latestPackage = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                    IsLatestSemVer2 = true,
                    Title = "A test package!"
                };

                var latestButNotPackage = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "4.0.0",
                    NormalizedVersion = "4.0.0",
                    IsLatest = true,
                    IsLatestSemVer2 = true,
                    Title = "A newer test package!"
                };

                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(new[] { notLatestPackage, latestPackage, latestButNotPackage });

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", LatestPackageRouteVerifier.SupportedRoutes.AbsoluteLatestUrlString);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                Assert.Equal(id, model.Id);
                // The page should select the first package that is IsLatestSemVer2
                Assert.Equal(latestPackage.NormalizedVersion, model.Version);
                Assert.True(model.LatestVersionSemVer2);
                Assert.False(model.VersionRequestedWasNotFound);
            }

            [Fact]
            public async Task GivenAnAbsoluteLatestVersionAndNoLatestSemVer2ItFiltersTheList()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var notLatestPackage = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "3.0.0",
                    NormalizedVersion = "3.0.0",
                    Title = "An old test package!"
                };

                var packages = new[] { notLatestPackage };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(notLatestPackage);

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", LatestPackageRouteVerifier.SupportedRoutes.AbsoluteLatestUrlString);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                Assert.Equal(id, model.Id);
                Assert.Equal(notLatestPackage.NormalizedVersion, model.Version);
                Assert.False(model.LatestVersionSemVer2);
            }

            [Fact]
            public async Task GivenAValidPackageWithNoVersionThatTheCurrentUserDoesNotOwnItDisplaysCurrentMetadata()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo",
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById("Foo",
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Null(model.ReadMeHtml);
            }

            [Fact]
            public async Task WhenHasReadMeAndMarkdownExists_ReturnsContent()
            {
                // Arrange
                var readMeMd = "# Hello World!";

                // Act
                var result = await GetResultWithReadMe(readMeMd, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("<h2>Hello World!</h2>", model.ReadMeHtml);
            }

            [Fact]
            public async Task WhenHasReadMeAndLongMarkdownExists_ReturnsClampedContent()
            {
                // Arrange
                var readMeMd = string.Concat(Enumerable.Repeat($"---{Environment.NewLine}", 20));

                // Act
                var result = await GetResultWithReadMe(readMeMd, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                var htmlCount = model.ReadMeHtml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.Equal(20, htmlCount);
            }

            [Fact]
            public async Task WhenHasReadMeAndFileNotFound_ReturnsNull()
            {
                // Arrange & Act
                var result = await GetResultWithReadMe(null, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Null(model.ReadMeHtml);
            }

            [Fact]
            public async Task WhenHasReadMeFalse_ReturnsNull()
            {
                // Arrange and Act
                var result = await GetResultWithReadMe(null, false);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Null(model.ReadMeHtml);
            }

            private async Task<ActionResult> GetResultWithReadMe(string readMeHtml, bool hasReadMe)
            {
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var fileService = new Mock<IPackageFileService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService,
                    packageFileService: fileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!",
                    HasReadMe = hasReadMe
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                if (hasReadMe)
                {
                    fileService.Setup(f => f.DownloadReadMeMdFileAsync(It.IsAny<Package>())).Returns(Task.FromResult(readMeHtml));
                }

                return await controller.DisplayPackage(id, /*version*/null);
            }

            [Fact]
            public async Task GetsValidationIssues()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var fileService = new Mock<IPackageFileService>();
                var validationService = new Mock<IValidationService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService,
                    packageFileService: fileService,
                    validationService: validationService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo",
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!",
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById("Foo",
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService.Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                var expectedIssues = new[]
                {
                    new TestIssue("This should not be deduplicated by the controller layer"),
                    new TestIssue("I'm a Teapot"),
                    new TestIssue("This should not be deduplicated by the controller layer"),
                };

                validationService.Setup(v => v.GetLatestPackageValidationIssues(It.IsAny<Package>()))
                    .Returns(expectedIssues);

                // Act
                var result = await controller.DisplayPackage("Foo", version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(model.PackageValidationIssues, expectedIssues);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ShowsAtomFeedIfEnabled(bool isAtomFeedEnabled)
            {
                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    deprecationService: deprecationService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsPackagesAtomFeedEnabled())
                    .Returns(isAtomFeedEnabled);

                deprecationService
                    .Setup(x => x.GetDeprecationsById(id))
                    .Returns(new List<PackageDeprecation>());

                // Arrange and Act
                var result = await controller.DisplayPackage(id, version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(isAtomFeedEnabled, model.IsAtomFeedEnabled);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotShowDeprecationToLoggedOutUsers(bool isDeprecationEnabled)
            {
                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    deprecationService: deprecationService);

                var id = "Foo";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(It.IsAny<User>(), packages))
                    .Returns(isDeprecationEnabled);

                deprecationService
                    .Setup(x => x.GetDeprecationsById(id))
                    .Returns(new List<PackageDeprecation>())
                    .Verifiable();

                // Arrange and Act
                var result = await controller.DisplayPackage(id, version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(isDeprecationEnabled, model.IsPackageDeprecationEnabled);

                if (isDeprecationEnabled)
                {
                    deprecationService.Verify();
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ShowsDeprecationIfEnabled(bool isDeprecationEnabled)
            {
                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    deprecationService: deprecationService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(TestUtility.FakeUser, packages))
                    .Returns(isDeprecationEnabled);

                deprecationService
                    .Setup(x => x.GetDeprecationsById(id))
                    .Returns(new List<PackageDeprecation>())
                    .Verifiable();

                // Arrange and Act
                var result = await controller.DisplayPackage(id, version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(isDeprecationEnabled, model.IsPackageDeprecationEnabled);

                if (isDeprecationEnabled)
                {
                    deprecationService.Verify();
                }
            }

            [Fact]
            public async Task ShowsFirstDeprecationPerPackage()
            {
                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    deprecationService: deprecationService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var package = new Package()
                {
                    Key = 123,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                var deprecation1 = new PackageDeprecation
                {
                    PackageKey = 456,
                    CustomMessage = "BAD"
                };
                var deprecation2 = new PackageDeprecation
                {
                    PackageKey = 123,
                    CustomMessage = "Hello"
                };
                var deprecation3 = new PackageDeprecation
                {
                    PackageKey = 123,
                    CustomMessage = "World"
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(TestUtility.FakeUser, packages))
                    .Returns(true);

                var deprecations = new List<PackageDeprecation> { deprecation1, deprecation2, deprecation3 };
                deprecationService
                    .Setup(x => x.GetDeprecationsById(id))
                    .Returns(deprecations)
                    .Verifiable();

                // Arrange and Act
                var result = await controller.DisplayPackage(id, version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                Assert.Equal("Hello", model.CustomMessage);

                deprecationService.Verify();
            }

            [Theory]
            [InlineData(PackageDeprecationStatus.NotDeprecated, PackageDeprecationStatus.NotDeprecated, "")]
            [InlineData(PackageDeprecationStatus.CriticalBugs, PackageDeprecationStatus.NotDeprecated, 
                "{0} is deprecated because it has critical bugs.")]
            [InlineData(PackageDeprecationStatus.Legacy, PackageDeprecationStatus.NotDeprecated,
                "{0} is deprecated because it is no longer maintained.")]
            [InlineData(PackageDeprecationStatus.Legacy, PackageDeprecationStatus.CriticalBugs, 
                "{0} is deprecated because it is no longer maintained and has critical bugs.")]
            [InlineData(PackageDeprecationStatus.Other, PackageDeprecationStatus.NotDeprecated, "{0} is deprecated.")]
            public async Task ShowsCorrectDeprecationIconTitle(
                PackageDeprecationStatus deprecationStatus,
                PackageDeprecationStatus deprecationStatusSecondFlag,
                string expectedIconTitle)
            {
                deprecationStatus |= deprecationStatusSecondFlag; // this is to address a bug in xunit, where or'ing in the inlinedata returns 0

                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();
                var vulnerabilitiesService = new Mock<IPackageVulnerabilitiesService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    deprecationService: deprecationService,
                    vulnerabilitiesService: vulnerabilitiesService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var version = "1.1.1";
                var package = new Package()
                {
                    Key = 1,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = version,
                    Title = "A test package!"
                };

                List<PackageDeprecation> deprecations = default;
                if (deprecationStatus != PackageDeprecationStatus.NotDeprecated)
                {
                    var deprecation = new PackageDeprecation
                    {
                        PackageKey = 1,
                        Status = deprecationStatus
                    };

                    deprecations = new List<PackageDeprecation> {deprecation};

                }
                else
                {
                    deprecations = new List<PackageDeprecation>();
                }

                package.Deprecations = deprecations;


                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(TestUtility.FakeUser, packages))
                    .Returns(true);

                featureFlagService
                    .Setup(x => x.IsDisplayVulnerabilitiesEnabled())
                    .Returns(false);

                deprecationService
                    .Setup(x => x.GetDeprecationsById(id))
                    .Returns(deprecations)
                    .Verifiable();

                // Arrange and Act
                var result = await controller.DisplayPackage(id, version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(string.Format(expectedIconTitle, version), model.PackageWarningIconTitle);

                deprecationService.Verify();
            }

            [Theory]
            [InlineData(false, false, "")]
            [InlineData(true, false, "{0} is deprecated because it is no longer maintained.")]
            [InlineData(false, true, "{0} has at least one vulnerability with {1} severity.")]
            [InlineData(true, true, "{0} is deprecated because it is no longer maintained; {0} has at least one vulnerability with {1} severity.")]
            public async Task ShowsCombinedDeprecationAndVulnerabilitiesIconTitle(
                bool isDeprecationEnabled,
                bool isVulnerabilitiesEnabled,
                string expectedIconTitle)
            {
                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();
                var vulnerabilitiesService = new Mock<IPackageVulnerabilitiesService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    deprecationService: deprecationService,
                    vulnerabilitiesService: vulnerabilitiesService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var vulnerabilityModerate = new PackageVulnerability
                {
                    AdvisoryUrl = "https://theurl/advisory01",
                    GitHubDatabaseKey = 1,
                    Severity = PackageVulnerabilitySeverity.Moderate
                };
                var vulnerabilityLow = new PackageVulnerability
                {
                    AdvisoryUrl = "https://theurl/advisory05",
                    GitHubDatabaseKey = 5,
                    Severity = PackageVulnerabilitySeverity.Low
                };
                var version = "1.1.1";

                var deprecation = new PackageDeprecation
                {
                    PackageKey = 1,
                    Status = PackageDeprecationStatus.Legacy
                };

                var package = new Package()
                {
                    Key = 1,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                    {
                        new VulnerablePackageVersionRange
                        {
                            PackageVersionRange = "1.1.1",
                            FirstPatchedPackageVersion = "1.1.2",
                            PackageId = id,
                            Vulnerability = vulnerabilityModerate
                        },
                        new VulnerablePackageVersionRange
                        {
                            PackageVersionRange = "<=1.1.1",
                            FirstPatchedPackageVersion = "1.1.2",
                            PackageId = id,
                            Vulnerability = vulnerabilityLow
                        }
                    },
                    Deprecations = new List<PackageDeprecation> { deprecation },
                    Version = "01.1.01",
                    NormalizedVersion = version,
                    Title = "A test package!"
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(TestUtility.FakeUser, packages))
                    .Returns(isDeprecationEnabled);

                featureFlagService
                    .Setup(x => x.IsDisplayVulnerabilitiesEnabled())
                    .Returns(isVulnerabilitiesEnabled);

                deprecationService
                    .Setup(x => x.GetDeprecationsById(id))
                    .Returns(new List<PackageDeprecation> { deprecation })
                    .Verifiable();

                vulnerabilitiesService
                    .Setup(x => x.GetVulnerabilitiesById(id))
                    .Returns(new Dictionary<int, IReadOnlyList<PackageVulnerability>>
                    {
                        { 1, new List<PackageVulnerability> { vulnerabilityModerate, vulnerabilityLow } }
                    })
                    .Verifiable();

                // Arrange and Act
                var result = await controller.DisplayPackage(id, version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(isDeprecationEnabled, model.IsPackageDeprecationEnabled);
                Assert.Equal(isVulnerabilitiesEnabled, model.IsPackageVulnerabilitiesEnabled);
                Assert.Equal(string.Format(expectedIconTitle, version, "moderate"), model.PackageWarningIconTitle);

                if (isDeprecationEnabled)
                {
                    deprecationService.Verify();
                }

                if (isVulnerabilitiesEnabled)
                {
                    vulnerabilitiesService.Verify();
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ShowRenamesToEnabledUser(bool isRenamesEnabledForThisUser)
            {
                // Arrange
                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();
                var renameService = new Mock<IPackageRenameService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    deprecationService: deprecationService,
                    renameService: renameService);

                var id = "Foo";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>(),
                        RenamedMessage = "TestMessage"
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                var packageRenames = new List<PackageRename> { new PackageRename() };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsPackageRenamesEnabled(It.IsAny<User>()))
                    .Returns(isRenamesEnabledForThisUser);

                deprecationService
                    .Setup(x => x.GetDeprecationsById(id))
                    .Returns(new List<PackageDeprecation>());

                renameService
                    .Setup(x => x.GetPackageRenames(package.PackageRegistration))
                    .Returns(packageRenames)
                    .Verifiable();

                // Act
                var result = await controller.DisplayPackage(id, version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(isRenamesEnabledForThisUser, model.IsPackageRenamesEnabled);
                if (isRenamesEnabledForThisUser)
                {
                    Assert.Equal(packageRenames, model.PackageRenames);
                    renameService.Verify(x => x.GetPackageRenames(package.PackageRegistration), Times.Once);
                }
                else
                {
                    Assert.Equal(null, model.PackageRenames);
                    renameService.Verify(x => x.GetPackageRenames(package.PackageRegistration), Times.Never);
                }
            }

            [Fact]
            public async Task SplitsLicenseExpressionWhenProvided()
            {
                const string expression = "some expression";
                var splitterMock = new Mock<ILicenseExpressionSplitter>();
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();

                var segments = new List<CompositeLicenseExpressionSegment>();
                splitterMock
                    .Setup(les => les.SplitExpression(expression))
                    .Returns(segments);

                var id = "Foo";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!",
                    LicenseExpression = expression,
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService,
                    licenseExpressionSplitter: splitterMock);

                var result = await controller.DisplayPackage(id, version: null);

                splitterMock
                    .Verify(les => les.SplitExpression(expression), Times.Once);
                splitterMock
                    .Verify(les => les.SplitExpression(It.IsAny<string>()), Times.Once);

                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Same(segments, model.LicenseExpressionSegments);
            }

            [Fact]
            public async Task UsesProperIconUrl()
            {
                var iconUrlProvider = new Mock<IIconUrlProvider>();
                const string iconUrl = "https://some.test/icon";
                iconUrlProvider
                    .Setup(iup => iup.GetIconUrlString(It.IsAny<Package>()))
                    .Returns(iconUrl);
                var packageService = new Mock<IPackageService>();
                var deprecationService = new Mock<IPackageDeprecationService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    iconUrlProvider: iconUrlProvider,
                    deprecationService: deprecationService);

                var id = "Foo";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!",
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                deprecationService
                    .Setup(x => x.GetDeprecationsById(id))
                    .Returns(new List<PackageDeprecation>())
                    .Verifiable();

                var result = await controller.DisplayPackage(id, version: null);
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                iconUrlProvider
                    .Verify(iup => iup.GetIconUrlString(package), Times.AtLeastOnce);
                Assert.Equal(iconUrl, model.IconUrl);
            }

            [Fact]
            public async Task CheckFeatureFlagIsOff()
            {
                var id = "foo";
                var cacheKey = "CacheDependents_" + id.ToLowerInvariant();
                var packageService = new Mock<IPackageService>();
                var featureFlagService = new Mock<IFeatureFlagService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>(),
                    },
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                };

                var packages = new List<Package> { package };
                featureFlagService
                    .Setup(f => f.IsPackageDependentsEnabled(It.IsAny<User>()))
                    .Returns(false);
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);
                packageService
                    .Setup(p => p.FilterLatestPackage(It.IsAny<IReadOnlyCollection<Package>>(), It.IsAny<int?>(), true))
                    .Returns(package);

                var result = await controller.DisplayPackage(id, version: null);
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                Assert.Null(model.PackageDependents);
                packageService
                    .Verify(iup => iup.GetPackageDependents(It.IsAny<string>()), Times.Never());
                Assert.False(model.IsPackageDependentsEnabled);
                Assert.Empty(_cache);
            }

            [Fact]
            public async Task CheckPackageDependentsFeatureFlagIsOff()
            {
                var id = "foo";
                var cacheKey = "CacheDependents_" + id.ToLowerInvariant();
                var packageService = new Mock<IPackageService>();
                var featureFlagService = new Mock<IFeatureFlagService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService);

                featureFlagService
                    .Setup(x => x.IsPackageDependentsEnabled(It.IsAny<User>()))
                    .Returns(false);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>(),
                    },
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                };

                featureFlagService
                   .Setup(f => f.IsPackageDependentsEnabled(It.IsAny<User>()))
                   .Returns(false);
                var packages = new List<Package> { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);
                packageService
                    .Setup(p => p.FilterLatestPackage(It.IsAny<IReadOnlyCollection<Package>>(), It.IsAny<int?>(), true))
                    .Returns(package);

                var result = await controller.DisplayPackage(id, version: null);
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                Assert.Null(model.PackageDependents);
                packageService
                    .Verify(iup => iup.GetPackageDependents(It.IsAny<string>()), Times.Never());
                Assert.False(model.IsPackageDependentsEnabled);
                Assert.Empty(_cache);
            }

            [Fact]
            public async Task WhenCacheIsOccupiedGetProperPackageDependent()
            {
                var id = "foo";
                var cacheKey = "CacheDependents_" + id.ToLowerInvariant();
                var packageService = new Mock<IPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                PackageDependents pd = new PackageDependents();

                httpContext
                    .Setup(c => c.Cache)
                    .Returns(_cache);

                _cache.Add(cacheKey,
                        pd,
                        null,
                        DateTime.UtcNow.AddMinutes(5),
                        Cache.NoSlidingExpiration,
                        CacheItemPriority.Default, null);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    httpContext: httpContext);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>(),
                    },
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                };

                var packages = new List<Package> { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(It.IsAny<IReadOnlyCollection<Package>>(), It.IsAny<int?>(), true))
                    .Returns(package);

                var result = await controller.DisplayPackage(id, version: null);
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                Assert.Same(pd, model.PackageDependents);
                packageService
                    .Verify(iup => iup.GetPackageDependents(It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public async Task OccupyEmptyCache()
            {
                var id = "foo";
                var cacheKey = "CacheDependents_" + id.ToLowerInvariant();
                var packageService = new Mock<IPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var contentObjectService = new Mock<IContentObjectService>();

                var cacheConfiguration = new CacheConfiguration
                {
                    PackageDependentsCacheTimeInSeconds = 60
                };
                PackageDependents pd = new PackageDependents();

                httpContext
                    .Setup(c => c.Cache)
                    .Returns(_cache);
                contentObjectService
                    .Setup(c => c.CacheConfiguration)
                    .Returns(cacheConfiguration);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    contentObjectService: contentObjectService,
                    httpContext: httpContext);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>(),
                    },
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                };
                var packages = new List<Package> { package };
                var gitHubInformation = new NuGetPackageGitHubInformation(new List<RepositoryInformation>());

                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);
                packageService
                    .Setup(p => p.FilterLatestPackage(It.IsAny<IReadOnlyCollection<Package>>(), It.IsAny<int?>(), true))
                    .Returns(package);
                packageService
                    .Setup(p => p.GetPackageDependents(It.IsAny<string>()))
                    .Returns(pd);
                contentObjectService
                    .Setup(c => c.GitHubUsageConfiguration.GetPackageInformation(It.IsAny<string>()))
                    .Returns(gitHubInformation);

                var result = await controller.DisplayPackage(id, version: null);
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                packageService
                    .Verify(iup => iup.GetPackageDependents(It.IsAny<string>()), Times.Once());
                Assert.Same(pd, model.PackageDependents);
                Assert.Same(pd, _cache.Get(cacheKey));
            }

            [Fact]
            public async Task CheckThatCacheKeyIsNotCaseSensitive()
            {
                var id1 = "fooBAr";
                var id2 = "FOObAr";
                var cacheKey = "CacheDependents_foobar";
                var packageService = new Mock<IPackageService>();
                var contentObjectService = new Mock<IContentObjectService>();
                var httpContext = new Mock<HttpContextBase>();

                var cacheConfiguration = new CacheConfiguration
                {
                    PackageDependentsCacheTimeInSeconds = 60
                };
                PackageDependents pd = new PackageDependents();

                contentObjectService
                    .Setup(c => c.CacheConfiguration)
                    .Returns(cacheConfiguration);
                httpContext
                    .Setup(c => c.Cache)
                    .Returns(_cache);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    contentObjectService: contentObjectService,
                    httpContext: httpContext);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id1,
                        Owners = new List<User>(),
                    },
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                };
                var packages = new List<Package> { package };
                var gitHubInformation = new NuGetPackageGitHubInformation(new List<RepositoryInformation>());

                packageService
                    .Setup(p => p.FindPackagesById(It.IsAny<string>(),
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);
                packageService
                    .Setup(p => p.FilterLatestPackage(It.IsAny<IReadOnlyCollection<Package>>(), It.IsAny<int?>(), true))
                    .Returns(package);
                packageService
                    .Setup(p => p.GetPackageDependents(It.IsAny<string>()))
                    .Returns(pd);
                contentObjectService
                    .Setup(c => c.GitHubUsageConfiguration.GetPackageInformation(It.IsAny<string>()))
                    .Returns(gitHubInformation);

                var result1 = await controller.DisplayPackage(id1, version: null);
                var model1 = ResultAssert.IsView<DisplayPackageViewModel>(result1);
                var result2 = await controller.DisplayPackage(id2, version: null);
                var model2 = ResultAssert.IsView<DisplayPackageViewModel>(result2);

                Assert.Same(pd, model1.PackageDependents);
                Assert.Same(pd, model2.PackageDependents);
                packageService
                    .Verify(iup => iup.GetPackageDependents(It.IsAny<String>()), Times.Once());
                Assert.Same(pd, _cache.Get(cacheKey));
            }

            [Fact(Skip = "Flaky test, tracked by issue https://github.com/NuGet/NuGetGallery/issues/8231")]
            public async Task OneSecondCacheTime()
            {
                var id = "foo";
                var cacheKey = "CacheDependents_" + id.ToLowerInvariant();
                var packageService = new Mock<IPackageService>();
                var contentObjectService = new Mock<IContentObjectService>();
                var httpContext = new Mock<HttpContextBase>();
                var pd = new PackageDependents();

                httpContext
                    .Setup(c => c.Cache)
                    .Returns(_cache);

                var cacheConfiguration = new CacheConfiguration
                {
                    PackageDependentsCacheTimeInSeconds = 1
                };

                contentObjectService
                    .Setup(c => c.CacheConfiguration)
                    .Returns(cacheConfiguration);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    contentObjectService: contentObjectService);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>(),
                    },
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                };
                var packages = new List<Package> { package };
                var gitHubInformation = new NuGetPackageGitHubInformation(new List<RepositoryInformation>());

                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);
                packageService
                    .Setup(p => p.FilterLatestPackage(It.IsAny<IReadOnlyCollection<Package>>(), It.IsAny<int?>(), true))
                    .Returns(package);
                packageService
                    .Setup(p => p.GetPackageDependents(It.IsAny<string>()))
                    .Returns(pd);
                contentObjectService
                    .Setup(c => c.GitHubUsageConfiguration.GetPackageInformation(It.IsAny<string>()))
                    .Returns(gitHubInformation);

                var result = await controller.DisplayPackage(id, version: null);
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                Assert.Same(pd, model.PackageDependents);
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.Empty(_cache);
                packageService
                    .Verify(iup => iup.GetPackageDependents(It.IsAny<string>()), Times.Once());
            }

            [Fact]
            public async Task IfDisplayAndComputeFrameworkFlagsAreFalseShouldNotCompute()
            {
                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var compatibilityFactory = new Mock<IPackageFrameworkCompatibilityFactory>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    compatibilityFactory: compatibilityFactory);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var version = "1.0.0";
                var packageFramework = new PackageFramework { TargetFramework = "net5.0" };
                var supportedFrameworks = new HashSet<PackageFramework> { packageFramework };
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    SupportedFrameworks = supportedFrameworks,
                    Version = "1.1.1",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsComputeTargetFrameworkEnabled())
                    .Returns(false);

                featureFlagService
                    .Setup(x => x.IsDisplayTargetFrameworkEnabled(TestUtility.FakeUser))
                    .Returns(false);

                compatibilityFactory
                    .Setup(x => x.Create(supportedFrameworks, id, version, false))
                    .Returns(new PackageFrameworkCompatibility());

                // Arrange and Act
                var result = await controller.DisplayPackage(id, version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Null(model.PackageFrameworkCompatibility);
            }

            [Theory]
            [InlineData(true, false)]
            [InlineData(false, true)]
            [InlineData(true, true)]
            public async Task IfAtLeastOneDisplayOrComputeFrameworkFlagsAreTrueShouldCompute(bool computeFlag, bool displayFlag)
            {
                var featureFlagService = new Mock<IFeatureFlagService>();
                var packageService = new Mock<IPackageService>();
                var compatibilityFactory = new Mock<IPackageFrameworkCompatibilityFactory>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    compatibilityFactory: compatibilityFactory);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var id = "Foo";
                var version = "1.1.1";
                var packageFramework = new PackageFramework { TargetFramework = "net5.0" };
                var supportedFrameworks = new HashSet<PackageFramework> { packageFramework };
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = id,
                        Owners = new List<User>()
                    },
                    SupportedFrameworks = supportedFrameworks,
                    Version = version,
                    NormalizedVersion = version,
                    Title = "A test package!"
                };

                var packages = new[] { package };
                packageService
                    .Setup(p => p.FindPackagesById(id,
                    /*includePackageRegistration:*/ true,
                    /*includeDeprecations:*/ true,
                    /*includeSupportedFrameworks:*/ true))
                    .Returns(packages);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                featureFlagService
                    .Setup(x => x.IsComputeTargetFrameworkEnabled())
                    .Returns(computeFlag);

                featureFlagService
                    .Setup(x => x.IsDisplayTargetFrameworkEnabled(TestUtility.FakeUser))
                    .Returns(displayFlag);

                compatibilityFactory
                    .Setup(x => x.Create(supportedFrameworks, id, version, false))
                    .Returns(new PackageFrameworkCompatibility());

                // Arrange and Act
                var result = await controller.DisplayPackage(id, version);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.NotNull(model.PackageFrameworkCompatibility);
            }

            protected override void Dispose(bool disposing)
            {
                // Clear the cache to avoid test interaction.
                foreach (DictionaryEntry entry in _cache)
                {
                    _cache.Remove((string)entry.Key);
                }

                base.Dispose(disposing);
            }

            private class TestIssue : ValidationIssue
            {
                private readonly string _message;

                public TestIssue(string message) => _message = message;

                public override ValidationIssueCode IssueCode => throw new NotImplementedException();
            }
        }

        public class TheAtomFeedPackageMethod
            : TestContainer
        {
            [Fact]
            public void GivenANonExistentPackageIt404s()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                packageService.Setup(p => p.FindPackageRegistrationById("Foo"))
                              .ReturnsNull();

                // Act
                var result = controller.AtomFeed("Foo");

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Fact]
            public void GivenAExistentPackageWithNoVersionsIt404s()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);


                packageService.Setup(p => p.FindPackageRegistrationById("Foo"))
                              .Returns(new PackageRegistration());

                // Act
                var result = controller.AtomFeed("Foo");

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Fact]
            public void GivenAExistentPackageWithUnlistedAvailablePackagesIt404s()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                var packageRegistration = new PackageRegistration();
                var package = new Package
                {
                    Listed = false,
                    PackageStatusKey = PackageStatus.Available
                };
                packageRegistration.Packages.Add(package);

                packageService.Setup(p => p.FindPackageRegistrationById("Foo"))
                              .Returns(packageRegistration);

                // Act
                var result = controller.AtomFeed("Foo");

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void GivenAnExistentPackageTheFeatureFlagHidesTheFeed(bool enabled)
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>();
                var packageService = new Mock<IPackageService>();
                var configurationService = GetConfigurationService();
                configurationService.Current.Brand = "Test Gallery";
                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackagesAtomFeedEnabled())
                    .Returns(enabled);

                var controller = CreateController(
                    configurationService,
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    httpContext: httpContext);

                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "Foo";

                var onlyVersion = new Package
                {
                    Listed = true,
                    PackageStatusKey = PackageStatus.Available,
                    NormalizedVersion = "2.0.0",
                    Version = "2.0.0",
                    IsPrerelease = true,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo"
                    },
                    Created = new DateTime(2019, 9, 7),
                };
                packageRegistration.Packages.Add(onlyVersion);

                packageService.Setup(p => p.FindPackageRegistrationById("Foo"))
                              .Returns(packageRegistration);

                // Act
                var result = controller.AtomFeed("Foo");

                // Assert
                if (enabled)
                {
                    Assert.IsType<SyndicationAtomActionResult>(result);
                }
                else
                {
                    ResultAssert.IsNotFound(result);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void GivenAExistentPackagePrereleaseVersionsCanBeFilteredOut(bool includePrerelease)
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>();
                var packageService = new Mock<IPackageService>();
                var configurationService = GetConfigurationService();
                configurationService.Current.Brand = "Test Gallery";

                var controller = CreateController(
                    configurationService,
                    packageService: packageService,
                    httpContext: httpContext);

                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "Foo";

                var onlyVersion = new Package
                {
                    Listed = true,
                    PackageStatusKey = PackageStatus.Available,
                    NormalizedVersion = "2.0.0-beta",
                    Version = "2.0.0-beta",
                    IsPrerelease = true,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo"
                    },
                    Created = new DateTime(2019, 9, 7),
                };
                packageRegistration.Packages.Add(onlyVersion);

                packageService.Setup(p => p.FindPackageRegistrationById("Foo"))
                              .Returns(packageRegistration);

                // Act
                var result = controller.AtomFeed("Foo", includePrerelease);

                // Assert
                if (includePrerelease)
                {
                    Assert.IsType<SyndicationAtomActionResult>(result);
                }
                else
                {
                    ResultAssert.IsNotFound(result);
                }
            }

            [Fact]
            public void GivenAExistentPackageWithListedUnavailablePackagesIt404s()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                var packageRegistration = new PackageRegistration();
                var package = new Package
                {
                    Listed = true,
                    PackageStatusKey = PackageStatus.Validating
                };
                packageRegistration.Packages.Add(package);

                packageService.Setup(p => p.FindPackageRegistrationById("Foo"))
                              .Returns(packageRegistration);

                // Act
                var result = controller.AtomFeed("Foo");

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Fact]
            public void GivenAExistentPackageWithListedAvailablePackagesItReturnsSyndicationFeed()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>();
                var packageService = new Mock<IPackageService>();
                var configurationService = GetConfigurationService();
                configurationService.Current.Brand = "Test Gallery";

                var controller = CreateController(
                    configurationService,
                    packageService: packageService,
                    httpContext: httpContext);

                var dateTimeNow = DateTime.Now;
                var dateTimeYesterDay = dateTimeNow.AddDays(-1);
                var dateTimeTwoDaysAgo = dateTimeNow.AddDays(-2);

                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "Foo";

                var oldestPackage = new Package
                {
                    Listed = true,
                    PackageStatusKey = PackageStatus.Available,
                    NormalizedVersion = "1.0.0",
                    Version = "1.0.0",
                    Title = "Foo",
                    Description = "Test Package",
                    Created = dateTimeTwoDaysAgo,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo"
                    }
                };
                packageRegistration.Packages.Add(oldestPackage);

                var highestVersionPackage = new Package
                {
                    Listed = true,
                    PackageStatusKey = PackageStatus.Available,
                    NormalizedVersion = "2.0.0-beta",
                    Version = "2.0.0-beta",
                    IsPrerelease = true,
                    Description = "Most recent version: Test Package",
                    Created = dateTimeYesterDay,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo"
                    }
                };
                packageRegistration.Packages.Add(highestVersionPackage);

                var newestPackageButNotHighestVersion = new Package
                {
                    Listed = true,
                    PackageStatusKey = PackageStatus.Available,
                    NormalizedVersion = "1.1.0",
                    Version = "1.1.0",
                    Description = "Fix for older version: Test Package",
                    Created = dateTimeNow,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo"
                    }
                };
                packageRegistration.Packages.Add(newestPackageButNotHighestVersion);
                packageService.Setup(p => p.FindPackageRegistrationById("Foo"))
                              .Returns(packageRegistration);

                // Act
                var result = controller.AtomFeed("Foo");
                var syndicationResult = result as SyndicationAtomActionResult;

                // Assert
                Assert.NotNull(syndicationResult);

                Assert.Equal("https://localhost/packages/Foo/", syndicationResult.SyndicationFeed.Id);
                Assert.Equal("Test Gallery Feed for Foo", syndicationResult.SyndicationFeed.Title.Text);
                Assert.Equal("Most recent version: Test Package", syndicationResult.SyndicationFeed.Description.Text);
                Assert.Equal(dateTimeNow, syndicationResult.SyndicationFeed.LastUpdatedTime);

                var syndicationFeedItems = new List<System.ServiceModel.Syndication.SyndicationItem>(syndicationResult.SyndicationFeed.Items);

                Assert.Equal(3, syndicationFeedItems.Count);

                Assert.Equal("https://localhost/packages/Foo/2.0.0-beta", syndicationFeedItems[0].Id);
                Assert.Equal("Foo 2.0.0-beta", syndicationFeedItems[0].Title.Text);
                Assert.Equal("Most recent version: Test Package", ((System.ServiceModel.Syndication.TextSyndicationContent) syndicationFeedItems[0].Content).Text);
                Assert.Equal(dateTimeYesterDay, syndicationFeedItems[0].PublishDate);
                Assert.Equal(dateTimeYesterDay, syndicationFeedItems[0].LastUpdatedTime);

                Assert.Equal("https://localhost/packages/Foo/1.1.0", syndicationFeedItems[1].Id);
                Assert.Equal("Foo 1.1.0", syndicationFeedItems[1].Title.Text);
                Assert.Equal("Fix for older version: Test Package", ((System.ServiceModel.Syndication.TextSyndicationContent) syndicationFeedItems[1].Content).Text);
                Assert.Equal(dateTimeNow, syndicationFeedItems[1].PublishDate);
                Assert.Equal(dateTimeNow, syndicationFeedItems[1].LastUpdatedTime);

                Assert.Equal("https://localhost/packages/Foo/1.0.0", syndicationFeedItems[2].Id);
                Assert.Equal("Foo 1.0.0", syndicationFeedItems[2].Title.Text);
                Assert.Equal("Test Package", ((System.ServiceModel.Syndication.TextSyndicationContent) syndicationFeedItems[2].Content).Text);
                Assert.Equal(dateTimeTwoDaysAgo, syndicationFeedItems[2].PublishDate);
                Assert.Equal(dateTimeTwoDaysAgo, syndicationFeedItems[2].LastUpdatedTime);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void UsesProperIconUrl(bool prerel)
            {
                var iconUrlProvider = new Mock<IIconUrlProvider>();
                const string iconUrl = "https://some.test/icon";
                iconUrlProvider
                    .Setup(iup => iup.GetIconUrl(It.IsAny<Package>()))
                    .Returns(new Uri(iconUrl));
                var packageService = new Mock<IPackageService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    iconUrlProvider: iconUrlProvider);

                var packageId = "someId";
                var packageVersion = "1.2.3-someVersion";
                var packageRegistration = new PackageRegistration { Id = packageId };
                var package = new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = packageVersion,
                    NormalizedVersion = packageVersion,
                    Created = new DateTime(2019, 9, 7),
                };
                packageRegistration.Packages.Add(package);

                packageService
                    .Setup(p => p.FindPackageRegistrationById(packageId))
                    .Returns(packageRegistration);

                var result = controller.AtomFeed(packageId, prerel);
                var model = Assert.IsType<SyndicationAtomActionResult>(result);
                iconUrlProvider
                    .Verify(iup => iup.GetIconUrl(package), Times.AtLeastOnce);
                Assert.Equal(iconUrl, model.SyndicationFeed.ImageUrl.AbsoluteUri);
            }
        }

        public class TheOwnershipRequestMethods : TestContainer
        {
            private int _key = 0;

            public delegate Task<ActionResult> InvokeOwnershipRequest(PackagesController packagesController, string id, string username, string token);

            private static Task<ActionResult> ConfirmOwnershipRequest(PackagesController packagesController, string id, string username, string token)
            {
                return packagesController.ConfirmPendingOwnershipRequest(id, username, token);
            }

            private static Task<ActionResult> RejectOwnershipRequest(PackagesController packagesController, string id, string username, string token)
            {
                return packagesController.RejectPendingOwnershipRequest(id, username, token);
            }

            private static Task<ActionResult> ConfirmOwnershipRequestRedirect(PackagesController packagesController, string id, string username, string token)
            {
                return packagesController.ConfirmPendingOwnershipRequestRedirect(id, username, token);
            }

            private static Task<ActionResult> RejectOwnershipRequestRedirect(PackagesController packagesController, string id, string username, string token)
            {
                return packagesController.RejectPendingOwnershipRequestRedirect(id, username, token);
            }

            public static IEnumerable<object[]> TheOwnershipRequestMethods_Data
            {
                get
                {
                    yield return new object[] { new InvokeOwnershipRequest(ConfirmOwnershipRequestRedirect) };
                    yield return new object[] { new InvokeOwnershipRequest(ConfirmOwnershipRequest) };
                    yield return new object[] { new InvokeOwnershipRequest(RejectOwnershipRequestRedirect) };
                    yield return new object[] { new InvokeOwnershipRequest(RejectOwnershipRequest) };
                }
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public async Task WithEmptyTokenReturnsHttpNotFound(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(new PackageRegistration());
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(new User { Username = "username" });

                // Act
                var result = await invokeOwnershipRequest(controller, "foo", "username", "");

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public async Task WithIdentityNotMatchingUserInRequestReturnsNotYourRequest(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var requestedUser = new User { Username = "userA", Key = _key++ };
                var currentUser = new User { Username = "userB", Key = _key++ };

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(requestedUser.Username, false)).Returns(requestedUser);

                var controller = CreateController(GetConfigurationService(), userService: userService);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await invokeOwnershipRequest(controller, "foo", requestedUser.Username, "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                Assert.Equal(requestedUser.Username, model.Username);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public Task WithSiteAdminReturnsNotYourRequest(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                return ReturnsNotYourRequest(TestUtility.FakeAdminUser, TestUtility.FakeUser, invokeOwnershipRequest);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public Task WithOrganizationCollaboratorReturnsNotYourRequest(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                return ReturnsNotYourRequest(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, invokeOwnershipRequest);
            }

            private async Task ReturnsNotYourRequest(User currentUser, User owner, InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var package = new PackageRegistration { Id = "foo" };

                var mockHttpContext = new Mock<HttpContextBase>();

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(package.Id)).Returns(package);

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(owner.Username, false)).Returns(owner);

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: mockHttpContext,
                    packageService: packageService,
                    userService: userService);
                controller.SetCurrentUser(currentUser);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await invokeOwnershipRequest(controller, package.Id, owner.Username, "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                Assert.Equal(owner.Username, model.Username);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public async Task WithNonExistentPackageIdReturnsHttpNotFound(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var currentUser = new User { Username = "username", Key = _key++ };

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(currentUser.Username, false)).Returns(currentUser);

                var controller = CreateController(GetConfigurationService(), userService: userService);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await invokeOwnershipRequest(controller, "foo", "username", "token");

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public async Task WithOwnerReturnsAlreadyOwnerResult(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var package = new PackageRegistration { Id = "foo" };
                var user = new User { Username = "username" };
                package.Owners.Add(user);
                var mockHttpContext = new Mock<HttpContextBase>();
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(package.Id)).Returns(package);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(user.Username, false)).Returns(user);
                var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: mockHttpContext,
                    packageService: packageService,
                    userService: userService,
                    packageOwnershipManagementService: packageOwnershipManagementService);
                controller.SetCurrentUser(user);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await invokeOwnershipRequest(controller, package.Id, user.Username, "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                Assert.Equal(ConfirmOwnershipResult.AlreadyOwner, model.Result);
                packageOwnershipManagementService.Verify(x => x.DeletePackageOwnershipRequestAsync(package, user, true));
            }

            public delegate Expression<Func<IPackageOwnershipManagementService, Task>> PackageOwnershipManagementServiceRequestExpression(PackageRegistration package, User requestingOwner, User newOwner);

            private static Expression<Func<IPackageOwnershipManagementService, Task>> PackagesServiceForConfirmOwnershipRequestExpression(PackageRegistration package, User requestingOwner, User newOwner)
            {
                return packageOwnershipManagementService => packageOwnershipManagementService.AddPackageOwnerWithMessagesAsync(package, newOwner);
            }

            private static Expression<Func<IPackageOwnershipManagementService, Task>> PackagesServiceForRejectOwnershipRequestExpression(PackageRegistration package, User requestingOwner, User newOwner)
            {
                return packageOwnershipManagementService => packageOwnershipManagementService.DeclinePackageOwnershipRequestWithMessagesAsync(package, requestingOwner, newOwner);
            }

            public static IEnumerable<object[]> ReturnsRedirectIfTokenIsValid_Data
            {
                get
                {
                    foreach (var tokenValid in new bool[] { true, false })
                    {
                        foreach (var isOrganizationAdministrator in new bool[] { true, false })
                        {
                            yield return new object[]
                            {
                                new InvokeOwnershipRequest(ConfirmOwnershipRequestRedirect),
                                new PackageOwnershipManagementServiceRequestExpression(PackagesServiceForConfirmOwnershipRequestExpression),
                                tokenValid,
                                isOrganizationAdministrator
                            };
                            yield return new object[]
                            {
                                new InvokeOwnershipRequest(RejectOwnershipRequestRedirect),
                                new PackageOwnershipManagementServiceRequestExpression(PackagesServiceForRejectOwnershipRequestExpression),
                                tokenValid,
                                isOrganizationAdministrator
                            };
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsRedirectIfTokenIsValid_Data))]
            public async Task ReturnsRedirectIfTokenIsValid(
                InvokeOwnershipRequest invokeOwnershipRequest,
                PackageOwnershipManagementServiceRequestExpression packageOwnershipManagementServiceExpression,
                bool tokenValid,
                bool isOrganizationAdministrator)
            {
                // Arrange
                var token = "token";
                var requestingOwner = new User { Key = _key++, Username = "owner", EmailAllowed = true };
                var package = new PackageRegistration { Id = "foo", Owners = new[] { requestingOwner } };

                var currentUser = new User { Key = _key++, Username = "username" };

                User newOwner;
                if (isOrganizationAdministrator)
                {
                    newOwner = new Organization { Key = _key++, Username = "organization", Members = new[] { new Membership { Member = currentUser, IsAdmin = true } } };
                }
                else
                {
                    newOwner = currentUser;
                }

                var mockHttpContext = new Mock<HttpContextBase>();

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(package.Id)).Returns(package);

                var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                
                var request = new PackageOwnerRequest
                {
                    PackageRegistration = package,
                    RequestingOwner = requestingOwner,
                    NewOwner = newOwner,
                    ConfirmationCode = token
                };
                packageOwnershipManagementService.Setup(p => p.GetPackageOwnershipRequest(package, newOwner, token))
                    .Returns(tokenValid ? request : null);

                var configurationService = GetConfigurationService();

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(newOwner.Username, false)).Returns(newOwner);

                var controller = CreateController(
                    configurationService,
                    httpContext: mockHttpContext,
                    packageService: packageService,
                    packageOwnershipManagementService: packageOwnershipManagementService,
                    userService: userService);

                controller.SetCurrentUser(currentUser);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await invokeOwnershipRequest(controller, package.Id, newOwner.Username, token);

                // Assert
                if (tokenValid)
                {
                    ResultAssert.IsRedirectTo(result, "/account/Packages#show-requests-received-container");
                }
                else
                {
                    var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                    Assert.Equal(ConfirmOwnershipResult.Failure, model.Result);
                    Assert.Equal(package.Id, model.PackageId);
                }
                packageOwnershipManagementService.Verify(
                    packageOwnershipManagementServiceExpression(package, currentUser, newOwner),
                    Times.Never);
            }

            public static IEnumerable<object[]> ReturnsSuccessIfTokenIsValid_Data
            {
                get
                {
                    foreach (var tokenValid in new bool[] { true, false })
                    {
                        foreach (var isOrganizationAdministrator in new bool[] { true, false })
                        {
                            yield return new object[]
                            {
                                new InvokeOwnershipRequest(ConfirmOwnershipRequest),
                                new PackageOwnershipManagementServiceRequestExpression(PackagesServiceForConfirmOwnershipRequestExpression),
                                ConfirmOwnershipResult.Success,
                                tokenValid,
                                isOrganizationAdministrator,
                            };
                            yield return new object[]
                            {
                                new InvokeOwnershipRequest(RejectOwnershipRequest),
                                new PackageOwnershipManagementServiceRequestExpression(PackagesServiceForRejectOwnershipRequestExpression),
                                ConfirmOwnershipResult.Rejected,
                                tokenValid,
                                isOrganizationAdministrator,
                            };
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsSuccessIfTokenIsValid_Data))]
            public async Task ReturnsSuccessIfTokenIsValid(
                InvokeOwnershipRequest invokeOwnershipRequest,
                PackageOwnershipManagementServiceRequestExpression packageOwnershipManagementServiceExpression,
                ConfirmOwnershipResult successState,
                bool tokenValid,
                bool isOrganizationAdministrator)
            {
                // Arrange
                var token = "token";
                var requestingOwner = new User { Key = _key++, Username = "owner", EmailAllowed = true };
                var package = new PackageRegistration { Id = "foo", Owners = new[] { requestingOwner } };

                var currentUser = new User { Key = _key++, Username = "username" };

                User newOwner;
                if (isOrganizationAdministrator)
                {
                    newOwner = new Organization { Key = _key++, Username = "organization", Members = new[] { new Membership { Member = currentUser, IsAdmin = true } } };
                }
                else
                {
                    newOwner = currentUser;
                }

                var mockHttpContext = new Mock<HttpContextBase>();

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(package.Id)).Returns(package);

                var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                
                var request = new PackageOwnerRequest
                {
                    PackageRegistration = package,
                    RequestingOwner = requestingOwner,
                    NewOwner = newOwner,
                    ConfirmationCode = token
                };
                packageOwnershipManagementService.Setup(p => p.GetPackageOwnershipRequest(package, newOwner, token))
                    .Returns(tokenValid ? request : null);

                var configurationService = GetConfigurationService();

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(newOwner.Username, false)).Returns(newOwner);

                var controller = CreateController(
                    configurationService,
                    httpContext: mockHttpContext,
                    packageService: packageService,
                    packageOwnershipManagementService: packageOwnershipManagementService,
                    userService: userService);

                controller.SetCurrentUser(currentUser);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await invokeOwnershipRequest(controller, package.Id, newOwner.Username, token);

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                var expectedResult = tokenValid ? successState : ConfirmOwnershipResult.Failure;
                Assert.Equal(expectedResult, model.Result);
                Assert.Equal(package.Id, model.PackageId);
                packageOwnershipManagementService.Verify(packageOwnershipManagementServiceExpression(package, requestingOwner, newOwner), tokenValid ? Times.Once() : Times.Never());
            }

            public class TheCancelPendingOwnershipRequestMethod : TestContainer
            {

                public static IEnumerable<object[]> NotOwner_Data
                {
                    get
                    {
                        yield return MemberDataHelper.AsData((User)null, TestUtility.FakeUser);
                        yield return MemberDataHelper.AsData(TestUtility.FakeUser, new User { Key = 1553 });
                        yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization);
                    }
                }

                public static IEnumerable<object[]> Owner_Data
                {
                    get
                    {
                        yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                        yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser);
                        yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                    }
                }

                [Fact]
                public void WithNonExistentPackageIdReturnsHttpNotFound()
                {
                    // Arrange
                    var controller = CreateController(GetConfigurationService());
                    controller.SetCurrentUser(new User { Username = "userA" });

                    // Act
                    var result = controller.CancelPendingOwnershipRequest("foo", "userA", "userB");

                    // Assert
                    Assert.IsType<HttpNotFoundResult>(result);
                }

                [Theory]
                [MemberData(nameof(NotOwner_Data))]
                public void WithNonOwningCurrentUserReturnsNotYourRequest(User currentUser, User owner)
                {
                    // Arrange
                    var package = new PackageRegistration { Id = "foo", Owners = new[] { owner } };
                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(package);
                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: packageService);
                    controller.SetCurrentUser(currentUser);

                    // Act
                    var result = controller.CancelPendingOwnershipRequest("foo", "userA", "userB");

                    // Assert
                    var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                    Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                    Assert.Equal("userA", model.Username);
                }

                [Theory]
                [MemberData(nameof(Owner_Data))]
                public void WithNonExistentPendingUserReturnsHttpNotFound(User currentUser, User owner)
                {
                    // Arrange
                    var package = new PackageRegistration { Id = "foo", Owners = new[] { owner } };
                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(package);
                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: packageService);
                    controller.SetCurrentUser(currentUser);

                    // Act
                    var result = controller.CancelPendingOwnershipRequest("foo", "userA", "userB");

                    // Assert
                    Assert.IsType<HttpNotFoundResult>(result);
                }

                [Theory]
                [MemberData(nameof(Owner_Data))]
                public void WithNonExistentPackageOwnershipRequestReturnsHttpNotFound(User currentUser, User owner)
                {
                    // Arrange
                    var packageId = "foo";
                    var package = new PackageRegistration { Id = packageId, Owners = new[] { owner } };

                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById(packageId)).Returns(package);

                    var userAName = "userA";
                    var userA = new User { Username = userAName };

                    var userBName = "userB";
                    var userB = new User { Username = userBName };

                    var userService = new Mock<IUserService>();
                    userService.Setup(u => u.FindByUsername(userAName, false)).Returns(userA);
                    userService.Setup(u => u.FindByUsername(userBName, false)).Returns(userB);

                    var controller = CreateController(
                        GetConfigurationService(),
                        userService: userService,
                        packageService: packageService);
                    controller.SetCurrentUser(owner);

                    // Act
                    var result = controller.CancelPendingOwnershipRequest(packageId, userAName, userBName);

                    // Assert
                    Assert.IsType<HttpNotFoundResult>(result);
                }

                [Theory]
                [MemberData(nameof(Owner_Data))]
                public void ReturnsRedirectIfPackageOwnershipRequestExists(User currentUser, User owner)
                {
                    // Arrange
                    var userAName = "userA";
                    var userA = new User { Username = userAName };

                    var userBName = "userB";
                    var userB = new User { Username = userBName };

                    var packageId = "foo";
                    var package = new PackageRegistration { Id = packageId, Owners = new[] { owner } };

                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById(packageId)).Returns(package);

                    var userService = new Mock<IUserService>();
                    userService.Setup(u => u.FindByUsername(userAName, false)).Returns(userA);
                    userService.Setup(u => u.FindByUsername(userBName, false)).Returns(userB);

                    var request = new PackageOwnerRequest() { RequestingOwner = userA, NewOwner = userB };
                    var packageOwnershipManagementRequestService = new Mock<IPackageOwnershipManagementService>();
                    packageOwnershipManagementRequestService.Setup(p => p.GetPackageOwnershipRequests(package, userA, userB)).Returns(new[] { request });

                    var controller = CreateController(
                        GetConfigurationService(),
                        userService: userService,
                        packageService: packageService,
                        packageOwnershipManagementService: packageOwnershipManagementRequestService);
                    controller.SetCurrentUser(currentUser);

                    // Act
                    var result = controller.CancelPendingOwnershipRequest(packageId, userAName, userBName);

                    // Assert
                    var model = ResultAssert.IsRedirectTo(result, "/packages/foo/Manage#show-Owners-container");

                    packageOwnershipManagementRequestService.Verify(
                        x => x.DeletePackageOwnershipRequestAsync(
                            It.IsAny<PackageRegistration>(),
                            It.IsAny<User>(),
                            It.IsAny<bool>()),
                        Times.Never);
                }
            }
        }

        public class TheContactOwnersMethod
            : TestContainer
        {
            [Fact]
            public void ReturnsNotFoundIfPackageIsNull()
            {
                // arrange
                var packageId = "pkgid";
                var packageVersion = "1.0.0";

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(packageId, packageVersion)).Returns<Package>(null);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // act
                var result = controller.ContactOwners(packageId, packageVersion);

                // assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void ReturnsNotFoundIfPackageRegistrationIsNull()
            {
                // arrange
                var packageId = "pkgid";
                var packageVersion = "1.0.0";

                var package = new Package
                {
                    PackageRegistration = null,
                    Version = packageVersion
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(packageId, packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // act
                var result = controller.ContactOwners(packageId, packageVersion);

                // assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void SetsModelParametersFromPackage()
            {
                // arrange
                var packageId = "pkgid";
                var packageVersion = "1.0.0";
                var projectUrl = "http://someurl/";
                var allowedUser = "helpful";

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = packageId,
                        Owners = new[]
                        {
                            new User { Username = allowedUser, EmailAllowed = true }
                        }
                    },
                    ProjectUrl = projectUrl,
                    Version = packageVersion
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(packageId, packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // act
                var model = (ContactOwnersViewModel) ((ViewResult) controller.ContactOwners(packageId, packageVersion)).Model;

                // assert
                Assert.Equal(packageId, model.PackageId);
                Assert.Equal(packageVersion, model.PackageVersion);
                Assert.Equal(projectUrl, model.ProjectUrl);
                Assert.Single(model.Owners);
                Assert.True(model.HasOwners);
            }

            [Fact]
            public void SetsModelHasOwnersTrueIfAllOwnersDisallow()
            {
                // arrange
                var packageId = "pkgid";
                var packageVersion = "1.0.0";
                var notAllowedUser = "grinch";

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = packageId,
                        Owners = new[]
                        {
                            new User { Username = notAllowedUser, EmailAllowed = false }
                        }
                    },
                    Version = packageVersion
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(packageId, packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // act
                var model = (ContactOwnersViewModel)((ViewResult)controller.ContactOwners(packageId, packageVersion)).Model;

                // assert
                Assert.Empty(model.Owners);
                Assert.True(model.HasOwners);
            }

            [Fact]
            public void SetsModelHasOwnersFalseIfNoOwners()
            {
                // arrange
                var packageId = "pkgid";
                var packageVersion = "1.0.0";

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = packageId,
                        Owners = new User[] { }
                    },
                    Version = packageVersion
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(packageId, packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // act
                var model = (ContactOwnersViewModel) ((ViewResult) controller.ContactOwners(packageId, packageVersion)).Model;

                // assert
                Assert.Empty(model.Owners);
                Assert.False(model.HasOwners);
            }

            [Fact]
            public void OnlyShowsOwnersWhoAllowReceivingEmails()
            {
                // arrange
                var packageId = "pkgid";
                var packageVersion = "1.0.0";
                var allowedUser = "helpful";
                var allowedUser2 = "helpful2";
                var notAllowedUser = "grinch";

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = packageId,
                        Owners = new[]
                            {
                                new User { Username = allowedUser, EmailAllowed = true },
                                new User { Username = notAllowedUser, EmailAllowed = false },
                                new User { Username = allowedUser2, EmailAllowed = true }
                            }
                    },
                    Version = packageVersion
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(packageId, packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // act
                var model = (ContactOwnersViewModel) ((ViewResult) controller.ContactOwners(packageId, packageVersion)).Model;

                // assert
                Assert.Equal(2, model.Owners.Count());
                Assert.Empty(model.Owners.Where(u => u == notAllowedUser));
            }

            [Fact]
            public async Task HtmlEncodesMessageContent()
            {
                // arrange
                var packageId = "factory";
                var packageVersion = "1.0.0";
                var message = "I like the cut of your jib. It's <b>bold</b>.";
                var encodedMessage = "I like the cut of your jib. It&#39;s &lt;b&gt;bold&lt;/b&gt;.";

                var sentPackageUrl = string.Empty;
                var messageService = new Mock<IMessageService>();
                string sentMessage = null;
                messageService.Setup(
                    s => s.SendMessageAsync(It.IsAny<ContactOwnersMessage>(), It.IsAny<bool>(), false))
                    .Callback<IEmailBuilder, bool, bool>((msg, copySender, discloseSenderAddress) =>
                    {
                        var contactOwnersMessage = (ContactOwnersMessage) msg;
                        sentPackageUrl = contactOwnersMessage.PackageUrl;
                        sentMessage = contactOwnersMessage.HtmlEncodedMessage;
                    })
                    .Returns(Task.CompletedTask);
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = packageId },
                    Version = packageVersion
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(packageId, packageVersion)).Returns(package);
                var userService = new Mock<IUserService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService);
                controller.SetCurrentUser(new User { EmailAddress = "montgomery@burns.example.com", Username = "Montgomery" });
                var model = new ContactOwnersViewModel
                {
                    Message = message,
                };

                // act
                var result = await controller.ContactOwners(packageId, packageVersion, model) as RedirectToRouteResult;

                Assert.Equal(encodedMessage, sentMessage);
                Assert.Equal(controller.Url.Package(package, false), sentPackageUrl);
            }

            [Fact]
            public async Task CallsSendContactOwnersMessageWithUserInfo()
            {
                // arrange
                var packageId = "factory";
                var packageVersion = "1.0.0";
                var message = "I like the cut of your jib";

                var messageService = new Mock<IMessageService>();
                messageService
                    .Setup(s => s.SendMessageAsync(It.IsAny<ContactOwnersMessage>(), It.IsAny<bool>(), false))
                    .Returns(Task.CompletedTask);
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = packageId },
                    Version = packageVersion
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(packageId, packageVersion)).Returns(package);
                var userService = new Mock<IUserService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService);
                controller.SetCurrentUser(new User { EmailAddress = "montgomery@burns.example.com", Username = "Montgomery" });
                var model = new ContactOwnersViewModel
                {
                    Message = message,
                };

                // act
                var result = await controller.ContactOwners(packageId, packageVersion, model) as RedirectToRouteResult;

                // assert
                Assert.NotNull(result);
            }
        }

        public abstract class TheManageMethod
            : TestContainer
        {
            protected PackageRegistration PackageRegistration;
            protected Package Package;

            public TheManageMethod()
            {
                PackageRegistration = new PackageRegistration { Id = "CrestedGecko" };

                Package = new Package
                {
                    Key = 2,
                    PackageRegistration = PackageRegistration,
                    Version = "1.0.0+metadata",
                    NormalizedVersion = "1.0.0",
                    Listed = true,
                    IsLatestSemVer2 = true,
                    HasReadMe = false
                };
                var olderPackageVersion = new Package
                {
                    Key = 1,
                    PackageRegistration = PackageRegistration,
                    Version = "1.0.0-alpha",
                    IsLatest = true,
                    IsLatestSemVer2 = true,
                    Listed = true,
                    HasReadMe = false
                };

                PackageRegistration.Packages.Add(Package);
                PackageRegistration.Packages.Add(olderPackageVersion);
            }

            protected abstract Task<ActionResult> GetManageResult(PackagesController controller);
            protected abstract Mock<IPackageService> SetupPackageService(bool isPackageMissing = false);

            [Fact]
            public async Task Returns404IfPackageNotFound()
            {
                var packageService = SetupPackageService(isPackageMissing: true);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                var result = await GetManageResult(controller);

                packageService.Verify();

                Assert.IsType<HttpNotFoundResult>(result);
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns403IfNotOwner(User currentUser, User owner)
            {
                PackageRegistration.Owners.Add(owner);

                var packageService = SetupPackageService();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                var result = await GetManageResult(controller);

                packageService.Verify();

                var httpStatusCodeResult = Assert.IsType<HttpStatusCodeResult>(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task FormatsSelectVersionListProperly(User currentUser, User owner)
            {
                PackageRegistration.Owners.Add(owner);

                var packageService = SetupPackageService();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                var result = await GetManageResult(controller);

                packageService.Verify();

                var viewResult = Assert.IsType<ViewResult>(result);
                var model = viewResult.Model as ManagePackageViewModel;
                Assert.NotNull(model);
                Assert.False(model.IsLocked);

                // Verify version select list
                Assert.Equal(PackageRegistration.Packages.Count, model.VersionSelectList.Count());

                foreach (var pkg in PackageRegistration.Packages)
                {
                    var version = NuGetVersion.Parse(pkg.Version);
                    var valueField = version.ToNormalizedString();
                    var textField = version.ToFullString() + (pkg.IsLatestSemVer2 ? " (Latest)" : string.Empty);

                    var selectListItem = model.VersionSelectList
                        .SingleOrDefault(i => string.Equals(i.Text, textField) && string.Equals(i.Value, valueField));

                    Assert.NotNull(selectListItem);
                    Assert.Equal(valueField, selectListItem.Value);
                    Assert.Equal(textField, selectListItem.Text);
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenPackageRegistrationIsLockedReturnsLockedState(User currentUser, User owner)
            {
                // Arrange
                PackageRegistration.Owners.Add(owner);
                PackageRegistration.IsLocked = true;

                var packageService = SetupPackageService();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                // Act
                var result = await GetManageResult(controller);

                // Assert
                var model = ResultAssert.IsView<ManagePackageViewModel>(result);
                Assert.True(model.IsLocked);

                packageService.Verify();
            }


            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenNoReadMeEditPending_ReturnsActive(User currentUser, User owner)
            {
                // Arrange
                PackageRegistration.Owners.Add(owner);
                Package.HasReadMe = true;

                var packageService = SetupPackageService();
                var readMe = "markdown";
                var readMeService = new Mock<IReadMeService>();
                readMeService
                    .Setup(s => s.GetReadMeMdAsync(Package))
                    .ReturnsAsync(readMe);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    readMeService: readMeService.Object);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                // Act
                var result = await GetManageResult(controller);

                // Assert
                var model = ResultAssert.IsView<ManagePackageViewModel>(result);

                packageService.Verify();

                Assert.NotNull(model?.ReadMe?.ReadMe);
                Assert.Equal(ReadMeService.TypeWritten, model.ReadMe.ReadMe.SourceType);
                Assert.Equal(readMe, model.ReadMe.ReadMe.SourceText);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenNoReadMe_ReturnsNull(User currentUser, User owner)
            {
                // Arrange
                PackageRegistration.Owners.Add(owner);
                Package.HasReadMe = false;

                var packageService = SetupPackageService();
                var readMeService = new Mock<IReadMeService>(MockBehavior.Strict);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                // Act
                var result = await GetManageResult(controller);

                // Assert
                var model = ResultAssert.IsView<ManagePackageViewModel>(result);

                packageService.Verify();

                Assert.NotNull(model?.ReadMe?.ReadMe);
                Assert.Null(model.ReadMe.ReadMe.SourceType);
                Assert.Null(model.ReadMe.ReadMe.SourceText);
            }

            public static IEnumerable<object[]> ManageDeprecationFeatureFlagIsSetInModel_Data =
                MemberDataHelper.Combine(
                    Owner_Data,
                    MemberDataHelper.BooleanDataSet());

            [Theory]
            [MemberData(nameof(ManageDeprecationFeatureFlagIsSetInModel_Data))]
            public async Task ManageDeprecationFeatureFlagIsSetInModel(User currentUser, User owner, bool isManageDeprecationEnabled)
            {
                // Arrange
                PackageRegistration.Owners.Add(owner);
                Package.HasReadMe = false;

                var packageService = SetupPackageService();

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, PackageRegistration))
                    .Returns(isManageDeprecationEnabled)
                    .Verifiable();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                // Act
                var result = await GetManageResult(controller);

                // Assert
                var model = ResultAssert.IsView<ManagePackageViewModel>(result);

                packageService.Verify();
                featureFlagService.Verify();

                Assert.Equal(isManageDeprecationEnabled, model.IsManageDeprecationEnabled);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task UsesProperIconUrl(User currentUser, User owner)
            {
                PackageRegistration.Owners.Add(owner);
                var iconUrlProvider = new Mock<IIconUrlProvider>();
                const string iconUrl = "https://some.test/icon";
                iconUrlProvider
                    .Setup(iup => iup.GetIconUrlString(It.IsAny<Package>()))
                    .Returns(iconUrl);
                var packageService = SetupPackageService();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    iconUrlProvider: iconUrlProvider);
                controller.SetCurrentUser(currentUser);

                var result = await GetManageResult(controller);
                var model = ResultAssert.IsView<ManagePackageViewModel>(result);
                iconUrlProvider
                    .Verify(iup => iup.GetIconUrlString(Package), Times.AtLeastOnce);
                Assert.Equal(iconUrl, model.IconUrl);
            }
        }

        public class TheManageMethodWithExactVersion : TheManageMethod
        {
            protected override Task<ActionResult> GetManageResult(
                PackagesController controller)
            {
                return controller.Manage(PackageRegistration.Id, Package.Version);
            }

            protected override Mock<IPackageService> SetupPackageService(bool isPackageMissing = false)
            {
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                var packages = isPackageMissing ? Array.Empty<Package>() : new[] { Package };
                packageService
                    .Setup(p => p.FindPackagesById(PackageRegistration.Id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(packages)
                    .Verifiable();

                packageService
                    .Setup(p => p.FilterExactPackage(packages, It.Is<string>(s => s == Package.Version || s == Package.NormalizedVersion)))
                    .Returns(isPackageMissing ? null : Package)
                    .Verifiable();

                packageService
                    .Setup(svc => svc.FilterExactPackage(packages, It.Is<string>(s => s != Package.Version && s != Package.NormalizedVersion)))
                    .Returns((Package)null);

                packageService
                    .Setup(svc => svc.FilterLatestPackage(packages, It.IsAny<int>(), It.IsAny<bool>()))
                    .Returns((Package)null);

                return packageService;
            }
        }

        public abstract class TheManageMethodWithLatestVersion : TheManageMethod
        {
            protected override Mock<IPackageService> SetupPackageService(bool isPackageMissing = false)
            {
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                var packages = new[] { Package };
                packageService
                    .Setup(p => p.FindPackagesById(PackageRegistration.Id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(packages)
                    .Verifiable();

                packageService
                    .Setup(p => p.FilterExactPackage(packages, It.Is<string>(s => s == Package.Version || s == Package.NormalizedVersion)))
                    .Returns(isPackageMissing ? null : Package);

                packageService
                    .Setup(svc => svc.FilterExactPackage(packages, It.Is<string>(s => s != Package.Version && s != Package.NormalizedVersion)))
                    .Returns((Package)null);

                packageService
                    .Setup(p => p.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(isPackageMissing ? null : Package)
                    .Verifiable();

                return packageService;
            }
        }

        public class TheManageMethodWithNullVersion : TheManageMethodWithLatestVersion
        {
            protected override Task<ActionResult> GetManageResult(
                PackagesController controller)
            {
                return controller.Manage(PackageRegistration.Id, null);
            }
        }

        public class TheManageMethodWithMissingVersion : TheManageMethodWithLatestVersion
        {
            protected override Task<ActionResult> GetManageResult(
                PackagesController controller)
            {
                return controller.Manage(PackageRegistration.Id, "notarealversion");
            }
        }

        public abstract class TheDeleteSymbolsMethod : TestContainer
        {
            protected string _packageId = "CrestedGecko";
            protected PackageRegistration _packageRegistration;
            protected Package _package;

            public TheDeleteSymbolsMethod()
            {
                var symbolPackage1 = new SymbolPackage() { StatusKey = PackageStatus.Available };
                var symbolPackage2 = new SymbolPackage() { StatusKey = PackageStatus.Available };
                _packageRegistration = new PackageRegistration { Id = _packageId };

                _package = new Package
                {
                    Key = 2,
                    PackageRegistration = _packageRegistration,
                    Version = "1.0.0+metadata",
                    NormalizedVersion = "1.0.0",
                    Listed = true,
                    IsLatestSemVer2 = true,
                    HasReadMe = false,
                    SymbolPackages = new List<SymbolPackage>() { symbolPackage1 }
                };
                var olderPackageVersion = new Package
                {
                    Key = 1,
                    PackageRegistration = _packageRegistration,
                    Version = "1.0.0-alpha",
                    NormalizedVersion = "1.0.0-alpha",
                    IsLatest = true,
                    IsLatestSemVer2 = true,
                    Listed = true,
                    HasReadMe = false,
                    SymbolPackages = new List<SymbolPackage>() { symbolPackage2 }
                };

                _packageRegistration.Packages.Add(_package);
                _packageRegistration.Packages.Add(olderPackageVersion);
                symbolPackage1.Package = _package;
                symbolPackage2.Package = olderPackageVersion;
            }

            [Fact]
            public void Returns404IfPackageNotFound()
            {
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(_packageRegistration.Id, PackageDeprecationFieldsToInclude.None))
                    .Returns(Array.Empty<Package>());

                packageService
                    .Setup(p => p.FilterExactPackage(It.IsAny<IReadOnlyCollection<Package>>(), It.IsAny<string>()))
                    .Returns((Package)null)
                    .Verifiable();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                var result = controller.DeleteSymbols(_packageRegistration.Id, _package.Version);

                Assert.IsType<HttpNotFoundResult>(result);
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public void Returns403IfNotOwner(User currentUser, User owner)
            {
                var result = GetDeleteSymbolsResult(currentUser, owner, out var controller);

                var httpStatusCodeResult = Assert.IsType<HttpStatusCodeResult>(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public void DisplaysFullVersionStringAndUsesNormalizedVersionsInUrlsInSelectList(User currentUser, User owner)
            {
                var result = GetDeleteSymbolsResult(currentUser, owner, out var controller);

                var viewResult = Assert.IsType<ViewResult>(result);
                var model = viewResult.Model as DeletePackageViewModel;
                Assert.NotNull(model);
                Assert.False(model.IsLocked);

                // Verify version select list
                Assert.Equal(_packageRegistration.Packages.Count, model.VersionSelectList.Count());

                foreach (var pkg in _packageRegistration.Packages)
                {
                    var valueField = controller.Url.DeleteSymbolsPackage(new TrivialPackageVersionModel(pkg));
                    var textField = PackageHelper.GetSelectListText(pkg);

                    var selectListItem = model.VersionSelectList
                        .SingleOrDefault(i => string.Equals(i.Text, textField) && string.Equals(i.Value, valueField));

                    Assert.NotNull(selectListItem);
                    Assert.Equal(valueField, selectListItem.Value);
                    Assert.Equal(textField, selectListItem.Text);
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public void WhenPackageRegistrationIsLockedReturnsLockedState(User currentUser, User owner)
            {
                _packageRegistration.IsLocked = true;

                var result = GetDeleteSymbolsResult(currentUser, owner, out var controller);

                // Assert
                var model = ResultAssert.IsView<DeletePackageViewModel>(result);
                Assert.True(model.IsLocked);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public void UsesProperIconUrl(User currentUser, User owner)
            {
                var iconUrlProvider = new Mock<IIconUrlProvider>();
                const string iconUrl = "https://some.test/icon";
                iconUrlProvider
                    .Setup(iup => iup.GetIconUrlString(It.IsAny<Package>()))
                    .Returns(iconUrl);
                var packageService = CreatePackageService();

                var result = GetDeleteSymbolsResult(currentUser, owner, out var controller, iconUrlProvider);
                var model = ResultAssert.IsView<DeletePackageViewModel>(result);
                iconUrlProvider
                    .Verify(iup => iup.GetIconUrlString(_package), Times.AtLeastOnce);
                Assert.Equal(iconUrl, model.IconUrl);
            }


            private ActionResult GetDeleteSymbolsResult(User currentUser, User owner, out PackagesController controller, Mock<IIconUrlProvider> iconUrlProvider = null)
            {
                _packageRegistration.Owners.Add(owner);

                var packageService = CreatePackageService();
                controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    iconUrlProvider: iconUrlProvider);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                var result = InvokeDeleteSymbols(controller);

                packageService.Verify();
                return result;
            }

            protected abstract Mock<IPackageService> CreatePackageService();

            protected abstract ActionResult InvokeDeleteSymbols(PackagesController controller);
        }

        public class TheDeleteSymbolsMethodWithExactVersion : TheDeleteSymbolsMethod
        {
            protected override Mock<IPackageService> CreatePackageService()
            {
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                var packages = _packageRegistration.Packages.ToList();
                packageService
                    .Setup(svc => svc.FindPackagesById(_packageId, PackageDeprecationFieldsToInclude.None))
                    .Returns(packages)
                    .Verifiable();

                packageService
                    .Setup(p => p.FilterExactPackage(packages, It.Is<string>(s => s == _package.Version || s == _package.NormalizedVersion)))
                    .Returns(_package);

                packageService
                    .Setup(svc => svc.FilterExactPackage(packages, It.Is<string>(s => s != _package.Version && s != _package.NormalizedVersion)))
                    .Returns((Package)null);

                packageService
                    .Setup(svc => svc.FilterLatestPackage(packages, It.IsAny<int>(), It.IsAny<bool>()))
                    .Returns((Package)null);

                return packageService;
            }

            protected override ActionResult InvokeDeleteSymbols(PackagesController controller)
            {
                return controller.DeleteSymbols(_packageId, _package.Version);
            }
        }

        public abstract class TheDeleteSymbolsMethodThatFilters : TheDeleteSymbolsMethod
        {
            protected override Mock<IPackageService> CreatePackageService()
            {
                var packages = _packageRegistration.Packages.ToList();
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService
                    .Setup(svc => svc.FindPackagesById(_packageId, PackageDeprecationFieldsToInclude.None))
                    .Returns(packages)
                    .Verifiable();

                packageService
                    .Setup(p => p.FilterExactPackage(packages, It.Is<string>(s => s == _package.Version || s == _package.NormalizedVersion)))
                    .Returns(_package);

                packageService
                    .Setup(svc => svc.FilterExactPackage(packages, It.Is<string>(s => s != _package.Version && s != _package.NormalizedVersion)))
                    .Returns((Package)null);

                packageService
                    .Setup(svc => svc.FilterLatestPackage(packages, SemVerLevelKey.SemVer2, true))
                    .Returns(_package)
                    .Verifiable();

                return packageService;
            }
        }

        public class TheDeleteSymbolsMethodWithMissingVersion : TheDeleteSymbolsMethodThatFilters
        {
            protected override ActionResult InvokeDeleteSymbols(PackagesController controller)
            {
                return controller.DeleteSymbols(_packageId, "missing");
            }
        }

        public class TheDeleteSymbolsMethodWithNullVersion : TheDeleteSymbolsMethodThatFilters
        {
            protected override ActionResult InvokeDeleteSymbols(PackagesController controller)
            {
                return controller.DeleteSymbols(_packageId, null);
            }
        }

        public class TheDeleteSymbolsPackageMethod : TestContainer
        {
            [Fact]
            public async Task WhenPackageNotFoundReturns404()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns((Package)null);

                var controller = CreateController(GetConfigurationService(), packageService: packageService);

                // Act
                var result = await controller.DeleteSymbolsPackage("Foo", "1.0");

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.NotFound);
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns403IfNotOwner(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package)
                    .Verifiable();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.DeleteSymbolsPackage("Foo", "1.0");

                // Assert
                var httpStatusCodeResult = Assert.IsType<HttpStatusCodeResult>(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task Returns400IfThereAreNoSymbolsPackage(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.DeleteSymbolsPackage("Foo", "1.0");

                // Assert
                var httpStatusCodeResult = Assert.IsType<HttpStatusCodeResult>(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, httpStatusCodeResult.StatusCode);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task RedirectsToPackagePageAfterSymbolsPackageDeletion(User currentUser, User owner)
            {
                // Arrange
                var symbolPackage = new SymbolPackage()
                {
                    StatusKey = PackageStatus.Available
                };

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    NormalizedVersion = "1.0.0",
                    SymbolPackages = new List<SymbolPackage>() { symbolPackage }
                };
                package.PackageRegistration.Owners.Add(owner);
                symbolPackage.Package = package;

                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package);
                var auditingService = new TestAuditingService();
                var telemetryService = new Mock<ITelemetryService>();
                telemetryService
                    .Setup(x => x.TrackSymbolPackageDeleteEvent(It.IsAny<string>(), It.IsAny<string>()));

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    auditingService: auditingService,
                    telemetryService: telemetryService);

                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.DeleteSymbolsPackage("Foo", "1.0");

                // Assert
                var redirectResult = Assert.IsType<RedirectResult>(result);
                Assert.Equal($"/packages/{package.Id}/{package.NormalizedVersion}", redirectResult.Url);
                Assert.True(auditingService.WroteRecord<PackageAuditRecord>(ar =>
                    ar.Action == AuditedPackageAction.SymbolsDelete
                    && ar.Id == package.PackageRegistration.Id
                    && ar.Version == package.Version));
                telemetryService
                    .Verify(x => x.TrackSymbolPackageDeleteEvent(package.Id, package.Version), Times.Once);
            }

            [Fact]
            public async Task WhenPackageRegistrationIsLockedReturns403()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo", IsLocked = true },
                    Version = "1.0",
                };
                package.PackageRegistration.Owners.Add(new User("Frodo"));

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);

                var controller = CreateController(GetConfigurationService(), packageService: packageService);

                controller.SetCurrentUser(new User("Frodo"));

                // Act
                var result = await controller.DeleteSymbolsPackage("Foo", "1.0");

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.Forbidden);
            }
        }

        public class TheUpdateListedMethod : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task Returns404IfNotFound(bool listed)
            {
                // Arrange
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns((Package)null);
                // Note: this Mock must be strict because it guarantees that MarkPackageListedAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.UpdateListed("Foo", "1.0", listed);

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsErrorIfUserIsLocked(bool listed)
            {
                // Arrange
                var owner = new User { UserStatusKey = UserStatus.Locked };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = !listed,
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);
                // Note: this Mock must be strict because it guarantees that MarkPackageListedAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(owner);
                TestUtility.SetupUrlHelperForUrlGeneration(controller);

                // Act
                var result = await controller.UpdateListed("Foo", "1.0", listed);

                // Assert
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(ServicesStrings.UserAccountIsLocked, controller.TempData["ErrorMessage"]);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task Returns404IfDeleted(bool listed)
            {
                // Arrange
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(new Package { PackageStatusKey = PackageStatus.Deleted });
                // Note: this Mock must be strict because it guarantees that MarkPackageListedAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.UpdateListed("Foo", "1.0", listed);

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        new User { Key = 5535 },
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns403IfNotOwner(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);
                // Note: this Mock must be strict because it guarantees that MarkPackageListedAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.UpdateListed("Foo", "1.0", false);

                // Assert
                var httpStatusCodeResult = Assert.IsType<HttpStatusCodeResult>(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task UpdatesUnlistedIfSelected(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageUpdateService = new Mock<IPackageUpdateService>(MockBehavior.Strict);
                packageUpdateService
                    .Setup(svc => svc.MarkPackageUnlistedAsync(package, true, true))
                    .Returns(Task.CompletedTask).Verifiable();

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package).Verifiable();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                TestUtility.SetupUrlHelperForUrlGeneration(controller);

                // Act
                var result = await controller.UpdateListed("Foo", "1.0", false);

                // Assert
                packageService.Verify();
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(
                    "The package has been unlisted. It may take several hours for this change to propagate through our system.",
                    controller.TempData["Message"]);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task UpdatesUnlistedIfNotSelected(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    NormalizedVersion = "1.0.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageUpdateService = new Mock<IPackageUpdateService>(MockBehavior.Strict);
                packageUpdateService
                    .Setup(svc => svc.MarkPackageUnlistedAsync(package, true, true))
                    .Returns(Task.FromResult(0)).Verifiable();

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package).Verifiable();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                TestUtility.SetupUrlHelperForUrlGeneration(controller);

                // Act
                var result = await controller.UpdateListed("Foo", "1.0", true);

                // Assert
                packageService.Verify();
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(
                    "The package has been listed. It may take several hours for this change to propagate through our system.",
                    controller.TempData["Message"]);
            }

            [Fact]
            public async Task WhenPackageRegistrationIsLockedReturns400()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo", IsLocked = true },
                    Version = "1.0",
                };
                package.PackageRegistration.Owners.Add(new User("Frodo"));

                var packageUpdateService = new Mock<IPackageUpdateService>(MockBehavior.Strict);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    packageUpdateService: packageUpdateService);

                controller.SetCurrentUser(new User("Frodo"));
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.UpdateListed("Foo", "1.0", true);

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
            }
        }

        public class TheEditPostMethod : TestContainer
        {
            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        new User { Key = 5535 },
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            protected PackagesController SetupController(
                User currentUser,
                User owner,
                bool hasReadMe = false,
                bool isPackageLocked = false,
                Mock<IPackageFileService> packageFileService = null,
                IReadMeService readMeService = null,
                PackageStatus packageStatus = PackageStatus.Available)
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "packageId", IsLocked = isPackageLocked },
                    Version = "1.0",
                    Listed = true,
                    HasReadMe = hasReadMe,
                    PackageStatusKey = packageStatus,
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>();
                packageService.Setup(s => s.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package);
                packageService.Setup(s => s.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(package.PackageRegistration);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    packageFileService: packageFileService,
                    readMeService: readMeService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                return controller;
            }

            public static IEnumerable<object[]> OnPostBackWithReadMe_Saves_Data
            {
                get
                {
                    foreach (var ownerData in Owner_Data)
                    {
                        foreach (var hasReadMe in new[] { false, true })
                        {
                            yield return ownerData.Concat(new object[] { hasReadMe }).ToArray();
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns403IfNotOwner(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);
                // Note: this Mock must be strict because it guarantees that MarkPackageListedAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.UpdateListed("Foo", "1.0", false);

                // Assert
                var httpStatusCodeResult = Assert.IsType<HttpStatusCodeResult>(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, httpStatusCodeResult.StatusCode);
            }

            [Theory]
            [MemberData(nameof(OnPostBackWithReadMe_Saves_Data))]
            public async Task OnPostBackWithReadMe_Saves(User currentUser, User owner, bool hasReadMe)
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();
                packageFileService.Setup(s => s.SaveReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var controller = SetupController(currentUser, owner, hasReadMe: hasReadMe, packageFileService: packageFileService);

                var formData = new VerifyPackageRequest
                {
                    Edit = new EditPackageVersionReadMeRequest
                    {
                        ReadMe = new ReadMeRequest
                        {
                            SourceType = "written",
                            SourceText = "markdown2"
                        }
                    }
                };

                // Act.
                var result = await controller.Edit("packageId", "1.0", formData, "returnUrl");

                // Assert.
                packageFileService.Verify(s => s.SaveReadMeMdFileAsync(It.IsAny<Package>(), "markdown2"));

                // Verify that a comparison was done against the active readme.
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()), Times.Exactly(hasReadMe ? 1 : 0));
            }

            [Fact]
            public async Task AlwaysCommitsChangesToReadMeService()
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();
                packageFileService.Setup(s => s.SaveReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var readMeService = new Mock<IReadMeService>();

                var controller = SetupController(
                    TestUtility.FakeUser,
                    TestUtility.FakeUser,
                    hasReadMe: true,
                    packageFileService: packageFileService,
                    readMeService: readMeService.Object);

                var formData = new VerifyPackageRequest
                {
                    Edit = new EditPackageVersionReadMeRequest
                    {
                        ReadMe = new ReadMeRequest
                        {
                            SourceType = "written",
                            SourceText = "markdown2"
                        }
                    }
                };

                // Act.
                var result = await controller.Edit("packageId", "1.0", formData, "returnUrl");

                // Assert.
                readMeService.Verify(
                    x => x.SaveReadMeMdIfChanged(
                        It.IsAny<Package>(),
                        formData.Edit,
                        controller.Request.ContentEncoding,
                        true),
                    Times.Once);
                readMeService.Verify(
                    x => x.SaveReadMeMdIfChanged(
                        It.IsAny<Package>(),
                        It.IsAny<EditPackageVersionReadMeRequest>(),
                        It.IsAny<Encoding>(),
                        It.IsAny<bool>()),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task OnPostBackWithNoReadMe_Deletes(User currentUser, User owner)
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();
                packageFileService.Setup(s => s.DeleteReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var controller = SetupController(currentUser, owner, hasReadMe: true, packageFileService: packageFileService);

                var formData = new VerifyPackageRequest
                {
                    Edit = new EditPackageVersionReadMeRequest()
                };

                // Act.
                var result = await controller.Edit("packageId", "1.0", formData, "returnUrl");

                // Assert.
                packageFileService.Verify(s => s.DeleteReadMeMdFileAsync(It.IsAny<Package>()));

                // Verify that a comparison was done against the active readme.
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()), Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenPackageRegistrationIsLockedReturns403(User currentUser, User owner)
            {
                // Arrange
                var controller = SetupController(currentUser, owner, isPackageLocked: true);

                // Act
                var result = await controller.Edit("packageId", "1.0.0", new VerifyPackageRequest(), string.Empty);

                // Assert
                Assert.IsType<JsonResult>(result);
                Assert.Equal(403, controller.Response.StatusCode);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenPackageIsDeletedReturns404(User currentUser, User owner)
            {
                // Arrange
                var controller = SetupController(currentUser, owner, packageStatus: PackageStatus.Deleted);

                // Act
                var result = await controller.Edit("packageId", "1.0.0", new VerifyPackageRequest(), string.Empty);

                // Assert
                Assert.IsType<JsonResult>(result);
                Assert.Equal(404, controller.Response.StatusCode);
            }
        }

        public class TheListPackagesMethod
            : TestContainer
        {
            private readonly Cache _cache;

            public TheListPackagesMethod()
            {
                _cache = new Cache();
            }

            [Fact]
            public async Task TrimsSearchTerm()
            {
                var searchService = new Mock<ISearchService>();
                searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns(
                    Task.FromResult(new SearchResults(0, DateTime.UtcNow)));
                var controller = CreateController(
                    GetConfigurationService(),
                    searchService: searchService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = (ViewResult) (await controller.ListPackages(new PackageListSearchViewModel() { Q = " test " }));

                var model = (PackageListViewModel) result.Model;
                Assert.Equal("test", model.SearchTerm);
            }

            [Fact]
            public async Task DefaultsToFirstPageAndIncludingPrerelease()
            {
                var searchService = new Mock<ISearchService>();
                searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns(
                    Task.FromResult(new SearchResults(0, DateTime.UtcNow)));
                var controller = CreateController(
                    GetConfigurationService(),
                    searchService: searchService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = (ViewResult) (await controller.ListPackages(new PackageListSearchViewModel { Q = "test" }));

                var model = (PackageListViewModel) result.Model;
                Assert.True(model.IncludePrerelease);
                Assert.Equal(0, model.PageIndex);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task PassesPrerelParameterOnToSearchFilter(bool prerel)
            {
                var searchService = new Mock<ISearchService>();
                searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns(
                    Task.FromResult(new SearchResults(0, DateTime.UtcNow)));
                var controller = CreateController(
                    GetConfigurationService(),
                    searchService: searchService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = (ViewResult) (await controller.ListPackages(new PackageListSearchViewModel { Q = "test", Prerel = prerel }));

                var model = (PackageListViewModel) result.Model;
                Assert.Equal(prerel, model.IncludePrerelease);
                searchService.Verify(x => x.Search(It.Is<SearchFilter>(f => f.IncludePrerelease == prerel)));
            }

            [Fact]
            public async Task UsesPreviewSearchServiceWhenABTestServiceSaysSo()
            {
                var abTestService = new Mock<IABTestService>();
                abTestService
                    .Setup(x => x.IsPreviewSearchEnabled(It.IsAny<User>()))
                    .Returns(true);

                var searchService = new Mock<ISearchService>();
                var previewSearchService = new Mock<ISearchService>();
                previewSearchService
                    .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(() => new SearchResults(0, DateTime.UtcNow));
                var controller = CreateController(
                    GetConfigurationService(),
                    searchService: searchService,
                    previewSearchService: previewSearchService,
                    abTestService: abTestService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.ListPackages(new PackageListSearchViewModel { Q = "test", Prerel = true });

                searchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Never);
                previewSearchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Once);
                abTestService.Verify(
                    x => x.IsPreviewSearchEnabled(TestUtility.FakeUser),
                    Times.Once);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public async Task UsesPreviewSearchServiceForEmptyQuery(string q)
            {
                var abTestService = new Mock<IABTestService>();
                abTestService
                    .Setup(x => x.IsPreviewSearchEnabled(It.IsAny<User>()))
                    .Returns(true);

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Cache).Returns(_cache);

                var searchService = new Mock<ISearchService>();
                var previewSearchService = new Mock<ISearchService>();
                previewSearchService
                    .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(() => new SearchResults(0, DateTime.UtcNow));

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: httpContext,
                    searchService: searchService,
                    previewSearchService: previewSearchService,
                    abTestService: abTestService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.ListPackages(new PackageListSearchViewModel { Q = q, Prerel = true });

                searchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Never);
                previewSearchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Once);
                abTestService.Verify(
                    x => x.IsPreviewSearchEnabled(TestUtility.FakeUser),
                    Times.Once);
            }

            [Theory]
            [InlineData(GalleryConstants.SearchSortNames.Relevance, null, true)]
            [InlineData(null, "", true)]
            [InlineData(null, null, true)]
            [InlineData(GalleryConstants.SearchSortNames.CreatedAsc, null, true)]
            [InlineData(GalleryConstants.SearchSortNames.CreatedDesc, null, false)]
            [InlineData(GalleryConstants.SearchSortNames.LastEdited, null, true)]
            [InlineData(GalleryConstants.SearchSortNames.Published, null, true)]
            [InlineData(GalleryConstants.SearchSortNames.TitleAsc, null, true)]
            [InlineData(GalleryConstants.SearchSortNames.TitleDesc, null, true)]
            [InlineData(GalleryConstants.SearchSortNames.TotalDownloadsAsc, null, true)]
            [InlineData(GalleryConstants.SearchSortNames.TotalDownloadsDesc, null, false)]
            [InlineData(GalleryConstants.SearchSortNames.Relevance, "Dependency", false)]
            [InlineData(null, "Dependency", false)]
            [InlineData(null, "DotNetTool", false)]
            [InlineData(null, "Template", false)]
            [InlineData(null, "SomeRandomPackageType", false)]
            [InlineData(GalleryConstants.SearchSortNames.CreatedAsc, "Dependency", false, true)]
            [InlineData(GalleryConstants.SearchSortNames.CreatedAsc, "Dependency", true, false)]
            public async Task DoesNotCacheAdvancedSearch(string sortBy, string packageType, bool expectCached, bool flightStatus = true)
            {
                var httpContext = new Mock<HttpContextBase>();
                httpContext
                    .Setup(c => c.Cache)
                    .Returns(_cache);

                var searchService = new Mock<ISearchService>();
                searchService
                    .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(new SearchResults(0, DateTime.UtcNow));
                searchService
                    .Setup(s => s.SupportsAdvancedSearch)
                    .Returns(true);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsAdvancedSearchEnabled(It.IsAny<User>()))
                    .Returns(flightStatus);

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: httpContext,
                    searchService: searchService,
                    featureFlagService: featureFlagService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.ListPackages(new PackageListSearchViewModel { Q = string.Empty, SortBy = sortBy, PackageType = packageType});
                if (expectCached)
                {
                    Assert.NotNull(_cache.Get("DefaultSearchResults"));
                }
                else
                {
                    Assert.Null(_cache.Get("DefaultSearchResults"));
                }

                searchService.Verify(x => x.Search(It.IsAny<SearchFilter>()), Times.Once);
            }

            [Theory]
            [InlineData(null, SortOrder.Relevance, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.CreatedAsc, SortOrder.Relevance, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.LastEdited, SortOrder.Relevance, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.Published, SortOrder.Relevance, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.TitleAsc, SortOrder.Relevance, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.TitleDesc, SortOrder.Relevance, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.TotalDownloadsAsc, SortOrder.Relevance, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.CreatedDesc, SortOrder.CreatedDescending, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.TotalDownloadsDesc, SortOrder.TotalDownloadsDescending, null, "")]
            [InlineData(GalleryConstants.SearchSortNames.Relevance, SortOrder.Relevance, null, "")]
            [InlineData(null, SortOrder.Relevance, "Dependency", "Dependency")]
            [InlineData(null, SortOrder.Relevance, "DotNetTool", "DotNetTool")]
            [InlineData(null, SortOrder.Relevance, "Template", "Template")]
            [InlineData(null, SortOrder.Relevance, "IsNotARealpackageType", "IsNotARealpackageType")]
            public async Task RedirectsToDefaultWhenInvalidAdvancedSearch(string sortBy, SortOrder expectedSortBy, string packageType, string expectedPackageType)
            {
                var httpContext = new Mock<HttpContextBase>();
                httpContext
                    .Setup(c => c.Cache)
                    .Returns(_cache);

                var searchService = new Mock<ISearchService>();
                searchService
                    .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(new SearchResults(0, DateTime.UtcNow));
                searchService
                    .Setup(s => s.SupportsAdvancedSearch)
                    .Returns(true);

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: httpContext,
                    searchService: searchService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.ListPackages(new PackageListSearchViewModel { Q = string.Empty, SortBy = sortBy, PackageType = packageType });

                searchService.Verify(x => x.Search(It.Is<SearchFilter>(f => f.SortOrder == expectedSortBy && f.PackageType == expectedPackageType)), Times.Once);
            }

            [Theory]
            [InlineData(true, true, true)]
            [InlineData(true, false, false)]
            [InlineData(false, true, false)]
            [InlineData(false, false, false)]
            public async Task AdvancedSearchOnlyWorksWhenSupported(bool searchServiceSupport, bool flightStatus, bool expectedSupport)
            {
                var httpContext = new Mock<HttpContextBase>();
                httpContext
                    .Setup(c => c.Cache)
                    .Returns(_cache);

                var searchService = new Mock<ISearchService>();
                searchService
                    .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(new SearchResults(0, DateTime.UtcNow));
                searchService
                    .Setup(s => s.SupportsAdvancedSearch)
                    .Returns(searchServiceSupport);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsAdvancedSearchEnabled(It.IsAny<User>()))
                    .Returns(flightStatus);

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: httpContext,
                    searchService: searchService,
                    featureFlagService: featureFlagService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller
                    .ListPackages(new PackageListSearchViewModel 
                    { 
                        Q = string.Empty,
                        SortBy = GalleryConstants.SearchSortNames.TotalDownloadsDesc, 
                        PackageType = "dotnettool"
                    });

                // If it's not supported, it should cache the result since it's a default search query (empty string)
                if (!expectedSupport)
                {
                    Assert.NotNull(_cache.Get("DefaultSearchResults"));
                }
                else
                {
                    Assert.Null(_cache.Get("DefaultSearchResults"));
                }

                var expectedSortBy = expectedSupport ? SortOrder.TotalDownloadsDescending : SortOrder.Relevance ;
                var expectedPackageType = expectedSupport ? "dotnettool" : string.Empty;

                searchService.Verify(x => x.Search(It.Is<SearchFilter>(f => f.SortOrder == expectedSortBy && f.PackageType == expectedPackageType)), Times.Once);

            }

            [Theory]
            [InlineData(false, "DefaultSearchResults")]
            [InlineData(true, "DefaultPreviewSearchResults")]
            public async Task CachesDefaultSearch(bool preview, string key)
            {
                var abTestService = new Mock<IABTestService>();
                abTestService
                    .Setup(x => x.IsPreviewSearchEnabled(It.IsAny<User>()))
                    .Returns(preview);

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Cache).Returns(_cache);

                var results = new SearchResults(0, DateTime.UtcNow);
                var searchService = new Mock<ISearchService>();
                searchService
                    .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(results);
                var previewResults = new SearchResults(0, DateTime.UtcNow);
                var previewSearchService = new Mock<ISearchService>();
                previewSearchService
                    .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(previewResults);

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: httpContext,
                    searchService: searchService,
                    previewSearchService: previewSearchService,
                    abTestService: abTestService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.ListPackages(new PackageListSearchViewModel { Q = string.Empty, Prerel = true });

                var cachedValue = _cache.Get(key);
                Assert.NotNull(cachedValue);
                Assert.Same(preview ? previewResults : results, cachedValue);
            }

            protected override void Dispose(bool disposing)
            {
                // Clear the cache to avoid test interaction.
                foreach (DictionaryEntry entry in _cache)
                {
                    _cache.Remove((string)entry.Key);
                }

                base.Dispose(disposing);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task UsesProperIconUrl(bool prerel)
            {
                var iconUrlProvider = new Mock<IIconUrlProvider>();
                const string iconUrl = "https://some.test/icon";
                iconUrlProvider
                    .Setup(iup => iup.GetIconUrlString(It.IsAny<Package>()))
                    .Returns(iconUrl);
                var searchService = new Mock<ISearchService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    searchService: searchService,
                    iconUrlProvider: iconUrlProvider);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "packageId" },
                    Version = "1.2.3"
                };

                searchService
                    .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                    .ReturnsAsync(new SearchResults(1, DateTime.UtcNow, new[] { package }.AsQueryable()));

                var result = await controller.ListPackages(new PackageListSearchViewModel { Q = "test", Prerel = prerel });
                var model = ResultAssert.IsView<PackageListViewModel>(result);
                iconUrlProvider
                    .Verify(iup => iup.GetIconUrlString(package), Times.AtLeastOnce);
                var packageViewModel = Assert.Single(model.Items);
                Assert.Equal(iconUrl, packageViewModel.IconUrl);
            }
        }

        public class TheReportAbuseMethod
            : TestContainer
        {
            public static string PackageId = "gollum";
            public static string PackageVersion = "2.0.1";
            public static string UnencodedMessage = "Gollum took my <b>finger</bold>";
            public static string EncodedMessage = "Gollum took my &lt;b&gt;finger&lt;/bold&gt;";
            public static string ReporterEmailAddress = "frodo@hobbiton.example.com";
            public static string Signature = "Frodo";
            public static User Owner = new User { Key = 313, Username = "Gollum", EmailAddress = "gollum@mordor.com" };

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        Owner
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        Owner
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        Owner
                    };
                }
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            public static IEnumerable<object[]> Credential_Data
            {
                get
                {
                    // Format:
                    // 1. bool - true -> expecting to see safety categories
                    // 2. First array is direct credentials (owners)
                    // 3. Second array is an array of indirect credentials through owner organization members (first array contains credentials of the org itself)

                    yield return new object[]
                    {
                        true, // no owners, we still want to enable safety reporting
                        null,
                        null
                    };

                    yield return new object[]
                    {
                        true,
                        new object[]
                        { // owners
                            new object[] { "external.MicrosoftAccount" }
                        },
                        null
                    };

                    yield return new object[]
                    {
                        false,
                        new object[]
                        { // owners
                            new object[] { "external.AzureActiveDirectory" }
                        },
                        null
                    };

                    yield return new object[]
                    {
                        false,
                        new object[]
                        { // owners
                            new object[] { "external.AzureActiveDirectory" },
                            new object[] { "external.MicrosoftAccount", "apikey.v1" }
                        },
                        null
                    };

                    yield return new object[]
                    {
                        false,
                        new object[]
                        { // owners
                            new object[] { "external.AzureActiveDirectory", "external.MicrosoftAccount" }
                        },
                        null
                    };

                    yield return new object[]
                    {
                        false,
                        new object[]
                        { // owners
                            new object[] { "external.MicrosoftAccount" },
                            new object[] { "external.AzureActiveDirectory", "apikey.v4" },
                            new object[] { "external.MicrosoftAccount", "apikey.v4" }
                        },
                        null
                    };

                    yield return new object[]
                    {
                        true,
                        new object[]
                        { // owners
                            new object [] { "external.MicrosoftAccount" }
                        },
                        new object[]
                        { // owner orgs
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount" }, // org credentials
                                new object [] { "external.MicrosoftAccount" }
                            }
                        },
                    };

                    yield return new object[]
                    {
                        true,
                        null,
                        new object[]
                        { // owner orgs
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount", "apikey.v4" }, // org credentials
                                new object[] { "external.MicrosoftAccount" },
                                new object[] { "external.MicrosoftAccount" },
                                new object[] { "external.MicrosoftAccount" }
                            },
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount" }, // org credentials
                                new object[] { "external.MicrosoftAccount", "apikey.v4", "apikey.v4" }
                            }
                        },
                    };

                    yield return new object[]
                    {
                        false,
                        null,
                        new object[]
                        { // owner orgs
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount", "apikey.v4" }, // org credentials
                                new object[] { "external.MicrosoftAccount" }
                            },
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount" }, // org credentials
                                new object[] {  "external.MicrosoftAccount", "apikey.v4", "apikey.v4" }
                            },
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount", "apikey.v4" }, // org credentials
                                new object[] { "external.MicrosoftAccount", "apikey.v4" },
                                new object[] { "external.MicrosoftAccount", "apikey.v4" },
                                new object[] { "external.AzureActiveDirectory" }
                            }
                        },
                    };

                    yield return new object[]
                    {
                        true,
                        new object[]
                        { // owners
                            new object[] {"external.MicrosoftAccount", "apikey.v4" }
                        },
                        new object[]
                        { // owner orgs
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount" }, // org credentials
                                new object[] { "external.MicrosoftAccount" }
                            },
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount", "apikey.v4" }, // org credentials
                                new object[] { "external.MicrosoftAccount" },
                                new object[] { "apikey.v4", "external.MicrosoftAccount", "apikey.v4" }
                            }
                        },
                    };

                    yield return new object[]
                    {
                        false,
                        new object[]
                        { // owners
                            new object[] {"external.MicrosoftAccount", "apikey.v4" }
                        },
                        new object[]
                        { // owner orgs
                            new object[]
                            { // members
                                new object[] { "apikey.v1", "external.MicrosoftAccount" }, // org credentials
                                new object[] { "external.MicrosoftAccount" }
                            },
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount" }, // org credentials
                                new object[] { "external.MicrosoftAccount", "apikey.v4" },
                                new object[] { "external.AzureActiveDirectory", "apikey.v4" }
                            }
                        },
                    };

                    yield return new object[]
                    {
                        false,
                        new object[]
                        { // owners
                            new object[] {"external.MicrosoftAccount", "apikey.v4" }
                        },
                        new object[]
                        { // owner orgs
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount" }, // org credentials
                                new object[] { "external.MicrosoftAccount" }
                            },
                            new object[]
                            { // members
                                new object[] { "external.AzureActiveDirectory", "apikey.v4" }, // org credentials
                                new object[] { "external.MicrosoftAccount", "apikey.v4" },
                                new object[] { "external.MicrosoftAccount", "apikey.v4" }
                            }
                        },
                    };

                    yield return new object[]
                    {
                        false,
                        new object[]
                        { // owners
                            new object[] {"external.MicrosoftAccount", "apikey.v4" }
                        },
                        new object[]
                        { // owner orgs
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount" }, // org credentials
                                new object[] { "external.MicrosoftAccount" }
                            },
                            new object[] { }, // allow for corner case where an org has no members
                            new object[]
                            { // members
                                new object[] { "external.MicrosoftAccount" }, // org credentials
                                new object[] { "external.MicrosoftAccount", "apikey.v4" },
                                new object[] { "external.AzureActiveDirectory", "apikey.v4" }
                            }
                        },
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public void ShowsFormWhenNotOwner(User currentUser, User owner)
            {
                var result = GetReportAbuseResult(currentUser, owner);

                var viewResult = Assert.IsType<ViewResult>(result);

                var model = Assert.IsType<ReportAbuseViewModel>(viewResult.Model);

                Assert.Equal(PackageId, model.PackageId);
                Assert.Equal(PackageVersion, model.PackageVersion);
                Assert.True(model.ShowReportAbuseForm);
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public void HidesFormForCertainPackages(User currentUser, User owner)
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = PackageId, Owners = { owner }, IsLocked = true },
                    Version = PackageVersion,
                    Listed = false,
                    User = new User()
                    {
                        UserStatusKey = UserStatus.Locked
                    }
                };

                var result = GetReportAbuseResult(currentUser, package);
                var viewResult = Assert.IsType<ViewResult>(result);

                var model = Assert.IsType<ReportAbuseViewModel>(viewResult.Model);

                Assert.Equal(PackageId, model.PackageId);
                Assert.Equal(PackageVersion, model.PackageVersion);
                Assert.False(model.ShowReportAbuseForm);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public void RedirectsToReportMyPackageWhenOwner(User currentUser, User owner)
            {
                var result = GetReportAbuseResult(currentUser, owner);

                var redirectResult = Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("ReportMyPackage", redirectResult.RouteValues["Action"]);
            }

            [Theory]
            [MemberData(nameof(Credential_Data))]
            public void IncludesSafetyCategoriesWhenNotAadPresent(bool expectingSafetyCategories, object[] directOwnerCredentials, object[] indirectOwnerCredentials) 
            {
                // Arrange
                List<User> owners = new List<User>();

                // -- Direct owners
                if (directOwnerCredentials != null)
                {
                    foreach(var ownerCredentialTypes in directOwnerCredentials)
                    {
                        owners.Add(new User
                        {
                            Credentials = ((object[])ownerCredentialTypes).Select(ct => new Credential { Type = (string)ct }).ToList()
                        });
                    }
                }

                // -- Organization owners
                if (indirectOwnerCredentials != null)
                {
                    foreach (var ownerMembers in indirectOwnerCredentials)
                    {
                        var organization = new Organization
                        {
                            Members = new List<Membership>()
                        };

                        var orgCredentialsDone = false;
                        foreach(var memberCredentialTypes in (object[])ownerMembers)
                        {
                            // the first array in an organization object contains the credentials of the org itself
                            if (!orgCredentialsDone)
                            {
                                organization.Credentials = ((object[])memberCredentialTypes).Select(ct => new Credential { Type = (string)ct }).ToList();
                                orgCredentialsDone = true;
                            }
                            else
                            {
                                var membership = new Membership
                                {
                                    Organization = organization
                                };

                                var member = new User
                                {
                                    Credentials = ((object[])memberCredentialTypes).Select(ct => new Credential { Type = (string)ct }).ToList(),
                                    Organizations = new List<Membership> { membership }
                                };

                                membership.Member = member;
                                organization.Members.Add(membership);
                            }
                        }

                        owners.Add(organization);
                    }
                }

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = PackageId, Owners = owners },
                    Version = PackageVersion
                };

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService.Setup(ff => ff.IsShowReportAbuseSafetyChangesEnabled()).Returns(true);
                featureFlagService.Setup(ff => ff.IsAllowAadContentSafetyReportsEnabled()).Returns(false);

                // Act
                var result = GetReportAbuseResult(null, package, featureFlagService);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);

                var model = Assert.IsType<ReportAbuseViewModel>(viewResult.Model);

                Assert.Equal(expectingSafetyCategories, model.ReasonChoices.Contains(ReportPackageReason.ChildSexualExploitationOrAbuse));
                Assert.Equal(expectingSafetyCategories, model.ReasonChoices.Contains(ReportPackageReason.TerrorismOrViolentExtremism));
                Assert.Equal(expectingSafetyCategories, model.ReasonChoices.Contains(ReportPackageReason.ImminentHarm));
                Assert.Equal(expectingSafetyCategories, model.ReasonChoices.Contains(ReportPackageReason.HateSpeech));
                Assert.Equal(expectingSafetyCategories, model.ReasonChoices.Contains(ReportPackageReason.RevengePorn));
                Assert.Equal(expectingSafetyCategories, model.ReasonChoices.Contains(ReportPackageReason.OtherNudityOrPornography));
            }

            private ActionResult GetReportAbuseResult(User currentUser, User owner)
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = PackageId, Owners = { owner } },
                    Version = PackageVersion
                };

                return GetReportAbuseResult(currentUser, package);
            }

            private ActionResult GetReportAbuseResult(User currentUser, Package package) =>
                GetReportAbuseResult(currentUser, package, featureFlagService: null);

            private ActionResult GetReportAbuseResult(User currentUser, Package package, Mock<IFeatureFlagService> featureFlagService)
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(PackageId, PackageVersion)).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    featureFlagService: featureFlagService,
                    httpContext: httpContext);
                controller.SetCurrentUser(currentUser);
                TestUtility.SetupUrlHelper(controller, httpContext);

                return controller.ReportAbuse(PackageId, PackageVersion);
            }

            [Fact]
            public async Task FormSendsMessageToGalleryOwnerWithEmailOnlyWhenUnauthenticated()
            {
                var result = await GetReportAbuseFormResult(null, Owner, out var package, out var messageService);

                Assert.NotNull(result);
                messageService.Verify(
                    s => s.SendMessageAsync(
                        It.Is<ReportAbuseMessage>(
                            r => r.Request.FromAddress.Address == ReporterEmailAddress
                                 && r.Request.Package == package
                                 && r.Request.Reason == EnumHelper.GetDescription(ReportPackageReason.ContainsMaliciousCode)
                                 && r.Request.Message == EncodedMessage
                                 && r.AlreadyContactedOwners),
                        false,
                        false));
            }

            [Theory]
            [InlineData(ReportPackageReason.ViolatesALicenseIOwn, true)]
            [InlineData(ReportPackageReason.ContainsSecurityVulnerability, true)]
            [InlineData(ReportPackageReason.RevengePorn, true)]
            [InlineData(ReportPackageReason.HasABugOrFailedToInstall, true)]
            [InlineData(ReportPackageReason.ContainsMaliciousCode, false)]
            [InlineData(ReportPackageReason.Other, false)]
            [InlineData(ReportPackageReason.ChildSexualExploitationOrAbuse, false)]
            [InlineData(ReportPackageReason.TerrorismOrViolentExtremism, false)]
            [InlineData(ReportPackageReason.HateSpeech, false)]
            [InlineData(ReportPackageReason.ImminentHarm, false)]
            [InlineData(ReportPackageReason.OtherNudityOrPornography, false)]
            public async Task FormRejectsDisallowedReportReasons(ReportPackageReason reason, bool shouldReject)
            {
                var result = await GetReportAbuseFormResult(null, Owner, out var package, out var messageService, reason);
                if (shouldReject)
                {
                    Assert.IsType<HttpNotFoundResult>(result);
                }
                else
                {
                    Assert.IsNotType<HttpNotFoundResult>(result);
                }
            }

            public static IEnumerable<object[]> FormSendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated_Data
            {
                get
                {
                    var authenticatedUserTest = new[]
                    {
                        new object[]
                        {
                            TestUtility.FakeUser,
                            Owner
                        }
                    };

                    return authenticatedUserTest.Concat(Owner_Data);
                }
            }

            [Theory]
            [MemberData(nameof(FormSendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated_Data))]
            public async Task FormSendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated(User currentUser, User owner)
            {
                var result = await GetReportAbuseFormResult(currentUser, owner, package: out var package, messageService: out var messageService);

                Assert.NotNull(result);
                messageService.Verify(
                    s => s.SendMessageAsync(
                        It.Is<ReportAbuseMessage>(
                            r => r.Request.Message == EncodedMessage
                                 && r.Request.FromAddress.Address == currentUser.EmailAddress
                                 && r.Request.FromAddress.DisplayName == currentUser.Username
                                 && r.Request.Package == package
                                 && r.Request.Reason == EnumHelper.GetDescription(ReportPackageReason.ContainsMaliciousCode)
                                 && r.AlreadyContactedOwners),
                        false,
                        false));
            }

            public Task<ActionResult> GetReportAbuseFormResult(User currentUser, User owner, out Package package, out Mock<IMessageService> messageService, ReportPackageReason reason = ReportPackageReason.ContainsMaliciousCode)
            {
                messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.SendMessageAsync(It.Is<ReportAbuseMessage>(r => r.Request.Message == UnencodedMessage), false, false));
                package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = PackageId, Owners = new[] { owner } },
                    Version = "2.0.1"
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(PackageId, PackageVersion)).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(currentUser);
                var model = new ReportAbuseViewModel
                {
                    Email = ReporterEmailAddress,
                    Message = UnencodedMessage,
                    Reason = reason,
                    AlreadyContactedOwner = true,
                    Signature = Signature
                };

                if (currentUser != null)
                {
                    model.Email = currentUser.EmailAddress;
                }

                TestUtility.SetupUrlHelper(controller, httpContext);
                return controller.ReportAbuse(PackageId, PackageVersion, model);
            }
        }

        public class TheReportMyPackageMethod
            : TestContainer
        {
            private Package _package;
            private static readonly User _owner = new User { EmailAddress = "frodo@hobbiton.example.com", Username = "Frodo", Key = 2 };
            private ReportMyPackageViewModel _viewModel;
            private Issue _supportRequest;
            private Mock<IPackageService> _packageService;
            private Mock<IMessageService> _messageService;
            private Mock<IPackageDeleteService> _packageDeleteService;
            private Mock<ISupportRequestService> _supportRequestService;
            private PackagesController _controller;

            public void SetupTest(User currentUser, User owner)
            {
                if (owner == null)
                {
                    owner = _owner;
                }

                _package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "Mordor",
                        Owners = { owner },
                    },
                    Version = "2.00.1",
                    NormalizedVersion = "2.0.1",
                };

                _viewModel = new ReportMyPackageViewModel
                {
                    Reason = ReportPackageReason.ContainsPrivateAndConfidentialData,
                    Message = "Message!",
                };

                _packageService = new Mock<IPackageService>();
                _packageService
                    .Setup(p => p.FindPackageByIdAndVersionStrict(_package.PackageRegistration.Id, _package.Version))
                    .Returns(_package);

                _messageService = new Mock<IMessageService>();
                _packageDeleteService = new Mock<IPackageDeleteService>();

                _supportRequest = new Issue
                {
                    Key = 23,
                };

                _supportRequestService = new Mock<ISupportRequestService>();
                _supportRequestService
                    .Setup(x => x.AddNewSupportRequestAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<User>(),
                        It.IsAny<Package>()))
                    .ReturnsAsync(() => _supportRequest);

                var httpContext = new Mock<HttpContextBase>();
                _controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService,
                    messageService: _messageService,
                    packageDeleteService: _packageDeleteService,
                    supportRequestService: _supportRequestService,
                    httpContext: httpContext);
                _controller.SetCurrentUser(currentUser);

                TestUtility.SetupUrlHelper(_controller, httpContext);
            }

            public static IEnumerable<object[]> OwnerAndNotOwner_Data => Owner_Data.Concat(NotOwner_Data);

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        null
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        null
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        null
                    };
                }
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task GetRedirectsNonOwnersToReportAbuse(User currentUser, User owner)
            {
                await RedirectsNonOwnersToReportAbuse(
                    currentUser, owner,
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version));
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task PostRedirectsNonOwnersToReportAbuse(User currentUser, User owner)
            {
                await RedirectsNonOwnersToReportAbuse(
                    currentUser, owner,
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version,
                        _viewModel));
            }

            private async Task RedirectsNonOwnersToReportAbuse(User currentUser, User owner, Func<Task<ActionResult>> actAsync)
            {
                SetupTest(currentUser, owner);
                var result = await actAsync();

                var redirectToRouteResult = Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("ReportAbuse", redirectToRouteResult.RouteValues["Action"]);
            }

            [Theory]
            [MemberData(nameof(OwnerAndNotOwner_Data))]
            public async Task GetRedirectsMissingPackageToNotFound(User currentUser, User owner)
            {
                await RedirectsMissingPackageToNotFound(
                    currentUser, owner,
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version));
            }

            [Theory]
            [MemberData(nameof(OwnerAndNotOwner_Data))]
            public async Task PostRedirectsMissingPackageToNotFound(User currentUser, User owner)
            {
                await RedirectsMissingPackageToNotFound(
                    currentUser, owner,
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version,
                        _viewModel));
            }

            private async Task RedirectsMissingPackageToNotFound(User currentUser, User owner, Func<Task<ActionResult>> actAsync)
            {
                // Arrange
                SetupTest(currentUser: null, owner: _owner);
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns<Package>(null);

                // Act
                var result = await actAsync();

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task HtmlEncodesMessageContent(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.Message = "I like the cut of your jib. It's <b>bold</b>.";
                _viewModel.Reason = ReportPackageReason.ViolatesALicenseIOwn;

                ReportPackageRequest reportRequest = null;
                _messageService
                    .Setup(s => s.SendMessageAsync(It.IsAny<ReportMyPackageMessage>(), false, false))
                    .Callback<IEmailBuilder, bool, bool>((msg, copySender, discloseSenderAddress) => reportRequest = ((ReportMyPackageMessage) msg).Request)
                    .Returns(Task.CompletedTask);

                // Act
                await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.NotNull(reportRequest);
                Assert.Equal(currentUser.EmailAddress, reportRequest.FromAddress.Address);
                Assert.Same(_package, reportRequest.Package);
                Assert.Equal(EnumHelper.GetDescription(ReportPackageReason.ViolatesALicenseIOwn), reportRequest.Reason);
                Assert.Equal("I like the cut of your jib. It&#39;s &lt;b&gt;bold&lt;/b&gt;.", reportRequest.Message);
            }

            [Fact]
            public async Task ChecksDeleteAllowedWithNoReasonOnGetEndpoint()
            {
                // Arrange
                SetupTest(TestUtility.FakeUser, TestUtility.FakeUser);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                _packageDeleteService.Verify(
                    x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        null,
                        null),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ChecksDeleteAllowedEvenIfDeleteWasNotRequested(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.ContactSupport;
                _viewModel.Message = "Test message!";

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                _packageDeleteService.Verify(
                    x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        _viewModel.Reason.Value,
                        PackageDeleteDecision.ContactSupport),
                    Times.Once);
                _supportRequestService.Verify(
                    x => x.AddNewSupportRequestAsync(
                        string.Format(
                            Strings.OwnerSupportRequestSubjectFormat,
                            _package.PackageRegistration.Id,
                            _package.NormalizedVersion),
                        "Test message!",
                        currentUser.EmailAddress,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        currentUser,
                        _package),
                    Times.Once);
                Assert.Equal(Strings.SupportRequestSentTransientMessage, _controller.TempData["Message"]);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task AllowsPackageDelete(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.DeletePackage;
                _viewModel.DeleteConfirmation = true;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        It.IsAny<ReportPackageReason?>(),
                        It.IsAny<PackageDeleteDecision?>()))
                    .ReturnsAsync(true);

                _messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<PackageDeletedNoticeMessage>(
                            msg =>
                            msg.Package == _package),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.IsType<RedirectResult>(result);

                _supportRequestService.Verify(
                    x => x.AddNewSupportRequestAsync(
                        string.Format(
                            Strings.OwnerSupportRequestSubjectFormat,
                            _package.PackageRegistration.Id,
                            _package.NormalizedVersion),
                        Strings.UserPackageDeleteSupportRequestMessage,
                        currentUser.EmailAddress,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        currentUser,
                        _package),
                    Times.Once);
                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.Is<IEnumerable<Package>>(p => p.First() == _package),
                        currentUser,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        Strings.AutomatedPackageDeleteSignature),
                    Times.Once);
                _supportRequestService.Verify(
                    x => x.UpdateIssueAsync(
                        _supportRequest.Key,
                        null,
                        IssueStatusKeys.Resolved,
                        null,
                        currentUser.Username),
                    Times.Once);
                _messageService.Verify(
                    x => x.SendMessageAsync(
                        It.IsAny<PackageDeletedNoticeMessage>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.Once);
                Assert.Equal(Strings.UserPackageDeleteCompleteTransientMessage, _controller.TempData["Message"]);

                _messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<ReportMyPackageMessage>(), false, false),
                    Times.Never);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task TreatsDeleteFailureAsNormalRequest(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.DeletePackage;
                _viewModel.DeleteConfirmation = true;
                _viewModel.CopySender = true;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        It.IsAny<ReportPackageReason?>(),
                        It.IsAny<PackageDeleteDecision?>()))
                    .ReturnsAsync(true);
                _packageDeleteService
                    .Setup(x => x.SoftDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Throws(new Exception("bad!"));

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.IsType<RedirectResult>(result);
                Assert.False(_viewModel.CopySender, "The sender is not copied when an automatic delete fails.");
                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.Is<IEnumerable<Package>>(p => p.First() == _package),
                        currentUser,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        Strings.AutomatedPackageDeleteSignature),
                    Times.Once);
                _supportRequestService.Verify(
                    x => x.UpdateIssueAsync(
                        It.IsAny<int>(),
                        It.IsAny<int?>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Never);
                _messageService.Verify(
                    x => x.SendMessageAsync(
                        It.IsAny<PackageDeletedNoticeMessage>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.Never);
                _messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<ReportMyPackageMessage>(), false, false),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task RequiresMessageWhenNotDeleting(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.ContactSupport;
                _viewModel.Message = null;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        It.IsAny<ReportPackageReason?>(),
                        It.IsAny<PackageDeleteDecision?>()))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.Contains(
                    Strings.MessageIsRequired,
                    _controller
                        .ModelState
                        .Values
                        .SelectMany(x => x.Errors)
                        .Select(x => x.ErrorMessage));
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task RequiresConfirmationWhenDeleting(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.DeletePackage;
                _viewModel.DeleteConfirmation = false;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        It.IsAny<ReportPackageReason?>(),
                        It.IsAny<PackageDeleteDecision?>()))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.Contains(
                    Strings.UserPackageDeleteConfirmationIsRequired,
                    _controller
                        .ModelState
                        .Values
                        .SelectMany(x => x.Errors)
                        .Select(x => x.ErrorMessage));
            }

            private static readonly IEnumerable<ReportPackageReason> ReasonsRequiringDeleteDecision = new[]
            {
                ReportPackageReason.ContainsMaliciousCode,
                ReportPackageReason.ContainsPrivateAndConfidentialData,
                ReportPackageReason.ReleasedInPublicByAccident
            };

            private static readonly IEnumerable<ReportPackageReason> ReasonsNotRequiringDeleteDecision = new[]
            {
                ReportPackageReason.Other
            };

            private static IEnumerable<object[]> MergeOwnersWithReasons(IEnumerable<ReportPackageReason> reasons)
            {
                foreach (var ownerData in Owner_Data)
                {
                    foreach (var reason in reasons)
                    {
                        yield return ownerData.Concat(new object[] { reason }).ToArray();
                    }
                }
            }

            public static IEnumerable<object[]> RequiresDeleteDecision_Data => MergeOwnersWithReasons(ReasonsRequiringDeleteDecision);

            [Theory]
            [MemberData(nameof(RequiresDeleteDecision_Data))]
            public async Task RequiresDeleteDecision(User currentUser, User owner, ReportPackageReason reason)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.Reason = reason;
                _viewModel.DeleteDecision = null;
                _viewModel.DeleteConfirmation = true;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        It.IsAny<ReportPackageReason?>(),
                        It.IsAny<PackageDeleteDecision?>()))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.Contains(
                    Strings.UserPackageDeleteDecisionIsRequired,
                    _controller
                        .ModelState
                        .Values
                        .SelectMany(x => x.Errors)
                        .Select(x => x.ErrorMessage));
            }

            public static IEnumerable<object[]> DoesNotRequireDeleteDecision_Data => MergeOwnersWithReasons(ReasonsNotRequiringDeleteDecision);

            [Theory]
            [MemberData(nameof(DoesNotRequireDeleteDecision_Data))]
            public async Task DoesNotRequireDeleteDecision(User currentUser, User owner, ReportPackageReason reason)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.Reason = reason;
                _viewModel.DeleteDecision = null;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        It.IsAny<ReportPackageReason?>(),
                        It.IsAny<PackageDeleteDecision?>()))
                    .ReturnsAsync(false);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(
                    Strings.SupportRequestSentTransientMessage,
                    _controller.TempData["Message"]);

                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Never);
                _messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<ReportMyPackageMessage>(), false, false),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task IgnoresDeleteRequestWhenNotAllowed(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.Reason = ReportPackageReason.ContainsPrivateAndConfidentialData;
                _viewModel.DeleteDecision = PackageDeleteDecision.DeletePackage;
                _viewModel.DeleteConfirmation = true;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>(),
                        It.IsAny<ReportPackageReason?>(),
                        It.IsAny<PackageDeleteDecision?>()))
                    .ReturnsAsync(false);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(
                    Strings.SupportRequestSentTransientMessage,
                    _controller.TempData["Message"]);

                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Never);
                _messageService.Verify(
                    x => x.SendMessageAsync(It.IsAny<ReportMyPackageMessage>(), false, false),
                    Times.Once);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _controller?.Dispose();
                    base.Dispose(disposing);
                }
            }
        }

        public class TheUploadFileActionForGetRequests
            : TestContainer
        {
            public static IEnumerable<object[]> WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewId_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(true, TestUtility.FakeUser, new[] { TestUtility.FakeUser });
                    yield return MemberDataHelper.AsData(true, TestUtility.FakeAdminUser, new[] { TestUtility.FakeAdminUser });
                    yield return MemberDataHelper.AsData(false, TestUtility.FakeUser, new[] { TestUtility.FakeUser });
                    yield return MemberDataHelper.AsData(false, TestUtility.FakeAdminUser, new[] { TestUtility.FakeAdminUser });
                    yield return MemberDataHelper.AsData(false, TestUtility.FakeOrganizationAdmin, new[] { TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization });
                    yield return MemberDataHelper.AsData(false, TestUtility.FakeOrganizationCollaborator, new[] { TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization });
                }
            }

            [Theory]
            [MemberData(nameof(WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewId_Data))]
            public async Task WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewId(bool isSymbolsPackage, User currentUser, IEnumerable<User> expectedPossibleOwners)
            {
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(It.IsAny<int>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(currentUser.Username, false)).Returns(currentUser);
                    var fakeNuGetPackage = isSymbolsPackage ? TestPackage.CreateTestSymbolPackageStream("theId", "1.0.42") : null;

                    var controller = CreateController(
                        GetConfigurationService(),
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(currentUser);

                    var result = ((ViewResult) await controller.UploadPackage()).Model as SubmitPackageRequest;

                    Assert.NotNull(result);
                    Assert.True(result.IsUploadInProgress);
                    Assert.NotNull(result.InProgressUpload);
                    Assert.Equal(result.InProgressUpload.IsSymbolsPackage, isSymbolsPackage);
                    AssertIdenticalPossibleOwners(result.InProgressUpload.PossibleOwners, expectedPossibleOwners);
                }
            }

            public static IEnumerable<object[]> WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewIdInReservedNamespace_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, new[] { TestUtility.FakeUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser, new[] { TestUtility.FakeUser });

                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, new[] { TestUtility.FakeAdminUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser, new[] { TestUtility.FakeAdminUser });

                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, new[] { TestUtility.FakeOrganizationAdmin });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization, new[] { TestUtility.FakeOrganization });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, new[] { TestUtility.FakeOrganizationCollaborator });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, new[] { TestUtility.FakeOrganization });
                }
            }

            [Theory]
            [MemberData(nameof(WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewIdInReservedNamespace_Data))]
            public async Task WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewIdInReservedNamespace(User currentUser, User reservedNamespaceOwner, IEnumerable<User> expectedPossibleOwners)
            {
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(It.IsAny<int>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(currentUser.Username, false)).Returns(currentUser);

                    var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                    fakeReservedNamespaceService
                        .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
                        .Returns(new[] { new ReservedNamespace { Owners = new[] { reservedNamespaceOwner } } });

                    var controller = CreateController(
                        GetConfigurationService(),
                        uploadFileService: fakeUploadFileService,
                        userService: fakeUserService,
                        reservedNamespaceService: fakeReservedNamespaceService);
                    controller.SetCurrentUser(currentUser);

                    var result = ((ViewResult) await controller.UploadPackage()).Model as SubmitPackageRequest;

                    Assert.NotNull(result);
                    Assert.True(result.IsUploadInProgress);
                    Assert.NotNull(result.InProgressUpload);
                    AssertIdenticalPossibleOwners(result.InProgressUpload.PossibleOwners, expectedPossibleOwners);
                }
            }

            public static IEnumerable<object[]> WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewVersion_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, new[] { TestUtility.FakeUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser, new[] { TestUtility.FakeUser });

                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, new[] { TestUtility.FakeAdminUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser, new[] { TestUtility.FakeAdminUser });

                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, new[] { TestUtility.FakeOrganizationAdmin });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization, new[] { TestUtility.FakeOrganization });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, new[] { TestUtility.FakeOrganizationCollaborator });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, new[] { TestUtility.FakeOrganization });
                }
            }

            [Theory]
            [MemberData(nameof(WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewVersion_Data))]
            public async Task WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewVersion(User currentUser, User existingPackageOwner, IEnumerable<User> expectedPossibleOwners)
            {
                var packageId = "CrestedGecko";
                var packageVersion = "1.4.2";

                Stream fakeFileStream = TestPackage.CreateTestPackageStream(packageId, packageVersion);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(It.IsAny<int>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakePackageService = new Mock<IPackageService>();
                fakePackageService
                    .Setup(x => x.FindPackageRegistrationById(packageId))
                    .Returns(new PackageRegistration { Id = packageId, Owners = new[] { existingPackageOwner } });

                var fakeUserService = new Mock<IUserService>();
                fakeUserService.Setup(x => x.FindByUsername(currentUser.Username, false)).Returns(currentUser);

                var controller = CreateController(
                    GetConfigurationService(),
                    userService: fakeUserService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeFileStream,
                    packageService: fakePackageService);
                controller.SetCurrentUser(currentUser);

                var result = ((ViewResult) await controller.UploadPackage()).Model as SubmitPackageRequest;

                Assert.NotNull(result);
                Assert.True(result.IsUploadInProgress);
                Assert.NotNull(result.InProgressUpload);
                AssertIdenticalPossibleOwners(result.InProgressUpload.PossibleOwners, expectedPossibleOwners);
            }

            [Fact]
            public async Task WillShowTheViewWhenThereIsNoUploadInProgress()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = ((ViewResult) await controller.UploadPackage()).Model as SubmitPackageRequest;

                Assert.NotNull(result);
                Assert.False(result.IsSymbolsUploadEnabled);
            }

            [Fact]
            public async Task WillConsiderUserLockedStatus()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                var user = new User { UserStatusKey = UserStatus.Locked };
                controller.SetCurrentUser(user);

                var result = ((ViewResult) await controller.UploadPackage()).Model as SubmitPackageRequest;

                Assert.NotNull(result);
                Assert.True(result.IsUserLocked);
            }

            [Fact]
            public async Task WillSetTheErrorMessageInTempDataWhenValidationFails()
            {
                var expectedMessage = "Bad, bad package!";
                var currentUser = TestUtility.FakeUser;
                Stream fakeFileStream = TestPackage.CreateTestPackageStream("CrestedGecko", "1.4.2");

                var fakeUserService = new Mock<IUserService>();
                fakeUserService
                    .Setup(x => x.FindByUsername(currentUser.Username, false))
                    .Returns(currentUser);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(fakeFileStream));

                var fakePackageUploadService = new Mock<IPackageUploadService>();
                fakePackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                    .ReturnsAsync(PackageValidationResult.Invalid(expectedMessage));

                var controller = CreateController(
                    GetConfigurationService(),
                    userService: fakeUserService,
                    uploadFileService: fakeUploadFileService,
                    packageUploadService: fakePackageUploadService);
                controller.SetCurrentUser(currentUser);

                var result = ((ViewResult) await controller.UploadPackage()).Model as SubmitPackageRequest;

                Assert.NotNull(result);
                Assert.Null(result.InProgressUpload);
                Assert.Equal(controller.TempData["Message"], expectedMessage);
            }

            [Fact]
            public async Task WillSetShowWarningsFromValidationBeforeGeneratePackage()
            {
                var expectedMessage = "Tricky package!";
                var currentUser = TestUtility.FakeUser;
                Stream fakeFileStream = TestPackage.CreateTestPackageStream("CrestedGecko", "1.4.2");

                var fakeUserService = new Mock<IUserService>();
                fakeUserService
                    .Setup(x => x.FindByUsername(currentUser.Username, false))
                    .Returns(currentUser);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(fakeFileStream));

                var fakePackageUploadService = new Mock<IPackageUploadService>();
                fakePackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                    .ReturnsAsync(PackageValidationResult.AcceptedWithWarnings(new[] { new PlainTextOnlyValidationMessage(expectedMessage) }));

                var controller = CreateController(
                    GetConfigurationService(),
                    userService: fakeUserService,
                    uploadFileService: fakeUploadFileService,
                    packageUploadService: fakePackageUploadService);
                controller.SetCurrentUser(currentUser);

                var result = ((ViewResult) await controller.UploadPackage()).Model as SubmitPackageRequest;

                Assert.NotNull(result);
                Assert.NotNull(result.InProgressUpload);
                Assert.False(controller.TempData.ContainsKey("Message"));
                var actualMessage = Assert.Single(result.InProgressUpload.Warnings);
                Assert.Equal(expectedMessage, actualMessage.PlainTextMessage);
            }

            [Fact]
            public async Task WillSetErrorMessageFromValidationForSymbolsPackage()
            {
                var expectedMessage = "Tricky package!";
                var currentUser = TestUtility.FakeUser;
                Stream fakeFileStream = TestPackage.CreateTestSymbolPackageStream("CrestedGecko", "1.4.2");

                var fakeUserService = new Mock<IUserService>();
                fakeUserService
                    .Setup(x => x.FindByUsername(currentUser.Username, false))
                    .Returns(currentUser);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(fakeFileStream));

                var fakeSymbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                fakeSymbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.Invalid(expectedMessage));

                var controller = CreateController(
                    GetConfigurationService(),
                    userService: fakeUserService,
                    fakeNuGetPackage: fakeFileStream,
                    uploadFileService: fakeUploadFileService,
                    symbolPackageUploadService: fakeSymbolPackageUploadService);
                controller.SetCurrentUser(currentUser);

                var result = ((ViewResult) await controller.UploadPackage()).Model as SubmitPackageRequest;

                Assert.NotNull(result);
                Assert.Null(result.InProgressUpload);
                Assert.True(controller.TempData.ContainsKey("Message"));
                Assert.Equal(expectedMessage, controller.TempData["Message"]);
            }

            private void AssertIdenticalPossibleOwners(IEnumerable<string> possibleOwners, IEnumerable<User> expectedPossibleOwners)
            {
                Assert.True(possibleOwners.SequenceEqual(expectedPossibleOwners.Select(u => u.Username)));
            }
        }

        public class TheUploadFileActionForPostRequests
            : TestContainer
        {
            private const string PackageId = "theId";
            private const string PackageVersion = "1.0.0";

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillReturn409WhenThereIsAlreadyAnUploadInProgress(bool isSymbolsPackage)
            {
                var fakeFileStream = isSymbolsPackage
                    ? TestPackage.CreateTestSymbolPackageStream()
                    : TestPackage.CreateTestPackageStream();
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: fakeFileStream,
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(null) as JsonResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfPackageFileIsNull()
            {
                var controller = CreateController(GetConfigurationService());
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(null) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(Strings.UploadFileIsRequired, ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfFileIsNotANuGetPackage()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.notNuPkg");
                var controller = CreateController(GetConfigurationService());
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(Strings.UploadFileMustBeNuGetPackage, ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfEnsureValidThrowsException()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var readPackageException = new Exception();

                var controller = CreateController(
                    GetConfigurationService(),
                    readPackageException: readPackageException);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(Strings.FailedToReadUploadFile, ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            private const string EnsureValidExceptionMessage = "naughty package";

            [Theory]
            [InlineData(typeof(InvalidPackageException), true)]
            [InlineData(typeof(InvalidDataException), true)]
            [InlineData(typeof(EntityException), true)]
            [InlineData(typeof(Exception), false)]
            public async Task WillShowViewWithErrorsIfEnsureValidThrowsExceptionMessage(Type exceptionType, bool expectExceptionMessageInResponse)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var readPackageException =
                    exceptionType.GetConstructor(new[] { typeof(string) }).Invoke(new[] { EnsureValidExceptionMessage });

                var controller = CreateController(
                    GetConfigurationService(),
                    readPackageException: readPackageException as Exception);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(
                    expectExceptionMessageInResponse ? EnsureValidExceptionMessage : Strings.FailedToReadUploadFile,
                    ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            [Fact]
            [UseInvariantCultureAttribute]
            public async Task WillRejectBrokenZipFiles()
            {
                // Arrange
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("file.nupkg");
                var fakeFileStream = new MemoryStream(TestDataResourceUtility.GetResourceBytes("Zip64Package.Corrupted.1.0.0.nupkg"));
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: fakeFileStream);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;
                
                Assert.NotNull(result);
                Assert.Equal("Central Directory corrupt.", ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            [Theory]
            [InlineData("PackageWithDoubleForwardSlash.1.0.0.nupkg")]
            [InlineData("PackageWithDoubleBackwardSlash.1.0.0.nupkg")]
            [InlineData("PackageWithVeryLongZipFileEntry.1.0.0.nupkg")]
            [UseInvariantCultureAttribute]
            public async Task WillRejectMalformedZipWithEntryDoubleSlashInPath(string zipPath)
            {
                // Arrange
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("file.nupkg");
                var fakeFileStream = new MemoryStream(TestDataResourceUtility.GetResourceBytes(zipPath));
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: fakeFileStream);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);

                if (zipPath.Contains("Forward"))
                {
                    Assert.Equal(String.Format(Strings.PackageEntryWithDoubleForwardSlash, "malformedfile.txt"), ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                }
                else if (zipPath.Contains("Backward"))
                {
                    Assert.Equal(String.Format(Strings.PackageEntryWithDoubleBackSlash, "malformedfile.txt"), ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                }
                else
                {
                    string longFileName = "a".PadRight(270, 'a') + ".txt";
                    Assert.Equal(String.Format(Strings.PackageEntryWithDoubleForwardSlash, longFileName), ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                    string normalizedZipEntry = ZipArchiveHelpers.NormalizeForwardSlashesInPath(longFileName);
                    Assert.Equal(260, normalizedZipEntry.Length);
                }
            }

            [Theory]
            [InlineData("ILike*Asterisks")]
            [InlineData("I_.Like.-Separators")]
            [InlineData("-StartWithSeparator")]
            [InlineData("EndWithSeparator.")]
            [InlineData("EndsWithHyphen-")]
            [InlineData("$id$")]
            public async Task WillShowViewWithErrorsIfPackageIdIsInvalid(string packageId)
            {
                // Arrange
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns(packageId + ".nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream(packageId, "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: TestPackage.CreateTestPackageStream(packageId, "1.0.0"));
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal($"The package manifest contains an invalid ID: '{packageId}'", ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            [Theory]
            [InlineData("Contains#Invalid$Characters!@#$%^&*")]
            [InlineData("Contains#Invalid$Characters!@#$%^&*EndsOnValidCharacter")]
            [UseInvariantCultureAttribute]
            public async Task WillShowViewWithErrorsIfPackageIdIsBreaksParsing(string packageId)
            {
                // Arrange
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns(packageId + ".nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream(packageId, "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: TestPackage.CreateTestPackageStream(packageId, "1.0.0"));
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.StartsWith("An error occurred while parsing EntityName.", ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            public static IEnumerable<object[]> WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser);
                }
            }

            [Theory]
            [MemberData(nameof(WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed_Data))]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed(User currentUser, User existingPackageOwner)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { existingPackageOwner } };
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakePackageRegistration);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            public static IEnumerable<object[]> WillShowTheViewWithErrorsWhenThePackageIdIsBlockedByReservedNamespace_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillShowTheViewWithErrorsWhenThePackageIdIsBlockedByReservedNamespace_Data))]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdMatchesOwnedByOtherUserNamespace(User currentUser, User reservedNamespaceOwner)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                Stream fakeFileStream = TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(currentUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));

                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => null);

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new[] { new ReservedNamespace { Owners = new[] { new User { Key = 123123123 } } } });

                var fakeTelemetryService = new Mock<ITelemetryService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService,
                    telemetryService: fakeTelemetryService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(Strings.UploadPackage_IdNamespaceConflictHtml, ((JsonValidationMessage[]) result.Data)[0].RawHtmlMessage);
                Assert.Null(((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                fakeTelemetryService.Verify(
                    x => x.TrackPackagePushNamespaceConflictEvent(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        currentUser,
                        controller.OwinContext.Request.User.Identity),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(WillShowTheViewWithErrorsWhenThePackageIdIsBlockedByReservedNamespace_Data))]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdMatchesUnownedNamespace(User currentUser, User reservedNamespaceOwner)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                Stream fakeFileStream = TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(currentUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));

                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => null);

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new[] { new ReservedNamespace() });

                var fakeTelemetryService = new Mock<ITelemetryService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService,
                    telemetryService: fakeTelemetryService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Null(((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                Assert.Equal(Strings.UploadPackage_OwnerlessIdNamespaceConflictHtml, ((JsonValidationMessage[]) result.Data)[0].RawHtmlMessage);
                fakeTelemetryService.Verify(
                    x => x.TrackPackagePushOwnerlessNamespaceConflictEvent(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        currentUser,
                        controller.OwinContext.Request.User.Identity),
                    Times.Once);
            }

            public static IEnumerable<object[]> WillUploadThePackageWhenIdMatchesOwnedNamespace_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillUploadThePackageWhenIdMatchesOwnedNamespace_Data))]
            public async Task WillUploadThePackageWhenIdMatchesOwnedNamespace(User currentUser, User reservedNamespaceOwner)
            {
                var packageId = "Random.Package1";
                var version = "1.0.0";

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeUploadedFileStream = TestPackage.CreateTestPackageStream(packageId, version);
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeUploadedFileStream);
                Stream fakeSavedFileStream = TestPackage.CreateTestPackageStream(packageId, version);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(currentUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeSavedFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(currentUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => null);

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new[] { new ReservedNamespace { Owners = new[] { reservedNamespaceOwner } } });

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeSavedFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(currentUser.Key, fakeUploadedFileStream));
                fakeUploadedFileStream.Dispose();

                var model = (VerifyPackageRequest) result.Data;
                Assert.Equal(reservedNamespaceOwner.Username, model.PossibleOwners.Single());
            }

            public static IEnumerable<object[]> PackageAlreadyExists_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(PackageAlreadyExists_Data))]
            public async Task WillUploadThePackageWhenIdMatchesUnownedNamespaceButPackageExists(User currentUser, User existingPackageOwner)
            {
                var packageId = "Random.Package1";
                var version = "1.0.0";

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeUploadedFileStream = TestPackage.CreateTestPackageStream(packageId, version);
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeUploadedFileStream);
                Stream fakeSavedFileStream = TestPackage.CreateTestPackageStream(packageId, version);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(currentUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeSavedFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(currentUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => new PackageRegistration { Id = packageId, Owners = new[] { existingPackageOwner } });

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new[] { new ReservedNamespace { Owners = new[] { new User { Key = 332331 } } } });

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeSavedFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(currentUser.Key, fakeUploadedFileStream));
                fakeUploadedFileStream.Dispose();

                var model = (VerifyPackageRequest) result.Data;
                Assert.Equal(existingPackageOwner.Username, model.PossibleOwners.Single());
            }

            public static IEnumerable<object[]> WillShowTheViewWithErrorsWhenThePackageAlreadyExists_Data =>
                MemberDataHelper.Combine(
                    PackageAlreadyExists_Data,
                    MemberDataHelper.AsDataSet(PackageStatus.Available, PackageStatus.Deleted, PackageStatus.Validating));

            [Theory]
            [MemberData(nameof(WillShowTheViewWithErrorsWhenThePackageAlreadyExists_Data))]
            public async Task WillShowTheViewWithErrorsWhenThePackageAlreadyExists(User currentUser, User existingPackageOwner, PackageStatus status)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { existingPackageOwner } }, Version = "1.0.0", PackageStatusKey = status });
                var fakePackageDeleteService = new Mock<IPackageDeleteService>();
                var fakeTelemetryService = new Mock<ITelemetryService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService,
                    packageDeleteService: fakePackageDeleteService,
                    telemetryService: fakeTelemetryService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(
                    String.Format(Strings.PackageExistsAndCannotBeModified, "theId", "1.0.0"),
                    ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                fakePackageDeleteService.Verify(
                    x => x.HardDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>()),
                    Times.Never());

                fakeTelemetryService.Verify(
                    x => x.TrackPackageReupload(It.IsAny<Package>()),
                    Times.Never());
            }

            [Theory]
            [InlineData(PackageStatus.Available)]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.Validating)]
            public async Task WillShowTheViewWithErrorsWhenThePackageAlreadyExistsAndOnlyDiffersByMetadata(PackageStatus status)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0+metadata2");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "1.0.0+metadata" });
                var fakePackageDeleteService = new Mock<IPackageDeleteService>();
                var fakeTelemetryService = new Mock<ITelemetryService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService,
                    packageDeleteService: fakePackageDeleteService,
                    telemetryService: fakeTelemetryService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(
                    String.Format(Strings.PackageVersionDiffersOnlyByMetadataAndCannotBeModified, "theId", "1.0.0+metadata"),
                    ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                fakePackageDeleteService.Verify(
                    x => x.HardDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>()),
                    Times.Never());

                fakeTelemetryService.Verify(
                    x => x.TrackPackageReupload(It.IsAny<Package>()),
                    Times.Never());
            }

            [Theory]
            [MemberData(nameof(PackageAlreadyExists_Data))]
            public async Task WillReuploadThePackageWhenPackageFailedValidation(User currentUser, User existingPackageOwner)
            {
                var id = "theId";
                var version = "1.0.0";
                Func<Package, bool> isPackage = (Package p) => p.PackageRegistration.Id == id && p.Version == version;

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                Stream fakeFileStream = TestPackage.CreateTestPackageStream(id, version);
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = id, Owners = new[] { existingPackageOwner } }, Version = version, PackageStatusKey = PackageStatus.FailedValidation });
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(currentUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(currentUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var fakePackageDeleteService = new Mock<IPackageDeleteService>();
                var fakeTelemetryService = new Mock<ITelemetryService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService,
                    packageDeleteService: fakePackageDeleteService,
                    uploadFileService: fakeUploadFileService,
                    telemetryService: fakeTelemetryService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.True(result.Data is VerifyPackageRequest);
                fakePackageDeleteService.Verify(
                    x => x.HardDeletePackagesAsync(
                        It.Is<IEnumerable<Package>>(packages => isPackage(packages.Single())),
                        currentUser,
                        Strings.FailedValidationHardDeleteReason,
                        Strings.AutomatedPackageDeleteSignature,
                        false),
                    Times.Once());

                fakeTelemetryService.Verify(
                    x => x.TrackPackageReupload(It.Is<Package>(package => isPackage(package))),
                    Times.Once());
            }

            [Fact]
            public async Task WillSaveTheUploadFile()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("thePackageId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeFileStream);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, fakeFileStream));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillRedirectToVerifyPackageActionAfterSaving()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                Stream fakeFileStream = TestPackage.CreateTestPackageStream("thePackageId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakeUserService = new Mock<IUserService>();
                fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    userService: fakeUserService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.True(result.Data is VerifyPackageRequest);
            }

            [Fact]
            public async Task WillShowViewWithErrorWhenThePackageRegistrationIsLocked()
            {
                // Arrange
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageRegistration = new PackageRegistration { Id = "theId", IsLocked = true, Owners = new[] { new User { Key = TestUtility.FakeUser.Key } } };
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakePackageRegistration);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                // Assert
                Assert.IsType<JsonResult>(result);
                Assert.Equal(403, controller.Response.StatusCode);
            }

            [Fact]
            public async Task WillShowValidationErrorsFoundBeforeGeneratePackage()
            {
                var expectedMessage = "Bad package.";
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns(PackageId + ".nupkg");
                Stream fakeFileStream = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                fakePackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                    .ReturnsAsync(PackageValidationResult.Invalid(expectedMessage));

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageUploadService: fakePackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.Equal(expectedMessage, ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            [Fact]
            public async Task WillCleanupUploadContainerOnErrorsFoundBeforeGeneratePackage()
            {
                var expectedMessage = "Bad package.";
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile
                    .Setup(x => x.FileName)
                    .Returns(PackageId + ".nupkg");
                Stream fakeFileStream = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                fakeUploadedFile
                    .Setup(x => x.InputStream)
                    .Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService
                    .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult(0));
                fakeUploadFileService
                    .SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService
                    .Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                fakePackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                    .ReturnsAsync(PackageValidationResult.Invalid(expectedMessage));

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageUploadService: fakePackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileService
                    .Verify(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key), Times.Once);
            }

            [Fact]
            public async Task WillCleanupUploadContainerOnExceptionInBeforeGeneratePackage()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile
                    .Setup(x => x.FileName)
                    .Returns(PackageId + ".nupkg");
                Stream fakeFileStream = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                fakeUploadedFile
                    .Setup(x => x.InputStream)
                    .Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService
                    .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult(0));
                fakeUploadFileService
                    .SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService
                    .Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                fakePackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                    .ThrowsAsync(new Exception("TestExceptionMessage"));

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageUploadService: fakePackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileService
                    .Verify(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key), Times.Once);
            }

            [Fact]
            public async Task WillShowValidationWarningsFoundBeforeGeneratePackage()
            {
                var expectedMessage = "Iffy package.";
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns(PackageId + ".nupkg");
                Stream fakeFileStream = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                fakePackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                    .ReturnsAsync(PackageValidationResult.AcceptedWithWarnings(new[] { new PlainTextOnlyValidationMessage(expectedMessage) }));

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageUploadService: fakePackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                var data = Assert.IsAssignableFrom<VerifyPackageRequest>(result.Data);
                var actualMessage = Assert.Single(data.Warnings);
                Assert.Equal(expectedMessage, actualMessage.PlainTextMessage);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfSymbolsPackageValidationThrowsException()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.snupkg");
                var fakeFileStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var symbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                symbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ThrowsAsync(new Exception());

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: fakeFileStream,
                    symbolPackageUploadService: symbolPackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal(Strings.FailedToReadUploadFile, ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            [Fact]
            public async Task WillReturnConflictWhenUserDoesNotHaveOwnPackage()
            {
                var packageId = "theId";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId,
                        Owners = new List<User>() { new User() { Key = 12232 } }
                    }
                };

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.snupkg");
                var fakeFileStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var symbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                symbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(package));

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: fakeFileStream,
                    symbolPackageUploadService: symbolPackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Conflict, controller.Response.StatusCode);
                Assert.Equal(string.Format(Strings.PackageIdNotAvailable, packageId), (result.Data as JsonValidationMessage[])[0].PlainTextMessage);
            }

            [Fact]
            public async Task WillPreventSymbolsUploadIfOriginalPackageIsLocked()
            {
                var packageId = "theId";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId,
                        Owners = new List<User>() { TestUtility.FakeUser },
                        IsLocked = true
                    }
                };

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.snupkg");
                var fakeFileStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var symbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                symbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(package));

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: fakeFileStream,
                    symbolPackageUploadService: symbolPackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.Equal(string.Format(Strings.PackageIsLocked, packageId), ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            [Fact]
            public async Task WillPreventSymbolsUploadIfUserNotAllowedToUpload()
            {
                var packageId = "theId";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId,
                        Owners = new List<User>() { TestUtility.FakeUser },
                        IsLocked = true
                    }
                };

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.snupkg");
                var fakeFileStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var symbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                var message = "message";
                symbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.UserNotAllowedToUpload(message));

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: fakeFileStream,
                    symbolPackageUploadService: symbolPackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.Equal(message, ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
            }

            public static IEnumerable<object[]> SymbolValidationResultTypes => Enum
                .GetValues(typeof(SymbolPackageValidationResultType))
                .Cast<SymbolPackageValidationResultType>()
                .Select(r => new object[] { r });

            [Theory]
            [MemberData(nameof(SymbolValidationResultTypes))]
            public async Task WillNotThrowForAnySymbolPackageValidationResultType(SymbolPackageValidationResultType type)
            {
                var packageId = "theId";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId,
                        Owners = new List<User>() { new User() { Key = 12232 } }
                    }
                };

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.snupkg");
                var fakeFileStream = TestPackage.CreateTestSymbolPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var symbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                symbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(new SymbolPackageValidationResult(type, "something"));

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: fakeFileStream,
                    symbolPackageUploadService: symbolPackageUploadService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillSaveTheSymbolsPackageUploadFile()
            {
                var packageId = "thePackageId";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId,
                        Owners = new List<User>() { TestUtility.FakeUser }
                    }
                }; var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.snupkg");
                var fakeFileStream = TestPackage.CreateTestSymbolPackageStream(packageId, "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var symbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                symbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(package));

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    symbolPackageUploadService: symbolPackageUploadService,
                    fakeNuGetPackage: fakeFileStream);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, fakeFileStream));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillRedirectToVerifyPackageActionAfterSavingForSymbolsPackage()
            {
                var packageId = "thePackageId";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId,
                        Owners = new List<User>() { TestUtility.FakeUser }
                    }
                };
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.snupkg");
                Stream fakeFileStream = TestPackage.CreateTestSymbolPackageStream(packageId, "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService
                    .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult(0));
                fakeUploadFileService
                    .SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService
                    .Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>()))
                    .Returns(Task.FromResult(0));
                var symbolPackageUploadService = new Mock<ISymbolPackageUploadService>();
                symbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(package));

                var fakeUserService = new Mock<IUserService>();
                fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    symbolPackageUploadService: symbolPackageUploadService,
                    userService: fakeUserService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.True(result.Data is VerifyPackageRequest);
            }

            [Theory]
            [InlineData("icon.png", "image/png")]
            [InlineData("icon.jpg", "image/jpeg")]
            public async Task WillUseDataUrlForEmbeddedIcons(string resourceFilename, string contentType)
            {
                var packageId = "SomePackageId";
                var version = "1.0.0";

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var iconFileContents = TestDataResourceUtility.GetResourceBytes(resourceFilename);
                var expectedDataUrl = $"data:{contentType};base64,{Convert.ToBase64String(iconFileContents)}";
                var fakeUploadedFileStream = TestPackage.CreateTestPackageStream(
                    packageId,
                    version,
                    iconFilename: resourceFilename,
                    iconFileContents: iconFileContents);
                var fakeSavedFileStream = new MemoryStream();
                await fakeUploadedFileStream.CopyToAsync(fakeSavedFileStream);
                fakeUploadedFileStream.Seek(0, SeekOrigin.Begin);
                fakeSavedFileStream.Seek(0, SeekOrigin.Begin);
                fakeUploadedFile
                    .Setup(x => x.InputStream)
                    .Returns(fakeUploadedFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService
                    .SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .ReturnsAsync(null)
                    .ReturnsAsync(fakeSavedFileStream);
                fakeUploadFileService
                    .Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>()))
                    .Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService
                    .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns<PackageRegistration>(null);

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeSavedFileStream);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object);

                var model = Assert.IsType<VerifyPackageRequest>(result.Data);
                Assert.Equal(expectedDataUrl, model.IconUrl);
            }
        }

        public class TheVerifyPackageActionForPostRequests
            : TestContainer
        {
            private const string PackageId = "theId";
            private const string PackageVersion = "1.0.0";

            [Fact]
            public async Task WillTrackFailureIfUnexpectedExceptionWithoutIdVersion()
            {
                // Arrange
                var fakeUserService = new Mock<IUserService>();
                fakeUserService
                    .Setup(x => x.FindByUsername(It.IsAny<string>(), false))
                    .Throws<Exception>();
                var fakeTelemetryService = new Mock<ITelemetryService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    userService: fakeUserService,
                    telemetryService: fakeTelemetryService);
                var user = TestUtility.FakeUser;
                controller.SetCurrentUser(user);

                // Act
                await Assert.ThrowsAnyAsync<Exception>(() => controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = user.Username }));

                // Assert
                fakeTelemetryService.Verify(x => x.TrackPackagePushFailureEvent(null, null), Times.Once());
            }

            [Fact]
            public async Task WillTrackFailureIfUnexpectedExceptionWithIdVersion()
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;
                var ownerInForm = currentUser;

                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakePackageService = new Mock<IPackageService>();
                    fakePackageService
                        .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                        .Throws<Exception>();

                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(currentUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(ownerInForm.Username, false)).Returns(ownerInForm);

                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService,
                        packageService: fakePackageService,
                        telemetryService: fakeTelemetryService);
                    controller.SetCurrentUser(currentUser);

                    // Act
                    await Assert.ThrowsAnyAsync<Exception>(() => controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = ownerInForm.Username }));

                    // Assert
                    fakeTelemetryService.Verify(x => x.TrackPackagePushFailureEvent(PackageId, new NuGetVersion(PackageVersion)), Times.Once());
                    fakeTelemetryService.Verify(x => x.TrackPackagePushFailureEvent(null, null), Times.Never());
                }
            }

            [Fact]
            public async Task WillRedirectToUploadPageWhenThereIsNoUploadInProgress()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                TestUtility.SetupUrlHelperForUrlGeneration(controller);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null }) as JsonResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillReturnConflictIfCommittingReturnsConflict()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));

                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.CommitPackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<Stream>()))
                        .ReturnsAsync(PackageCommitResult.Conflict);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    var jsonResult = Assert.IsType<JsonResult>(result);
                    Assert.Equal((int)HttpStatusCode.Conflict, controller.Response.StatusCode);
                }
            }

            [Fact]
            public async Task WillThrowIfOwnerNonExistent()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                }
            }

            [Fact]
            public async Task WillThrowIfOwnerNotValid()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    var owner = new User { Key = 999, Username = "invalidOwner" };
                    fakeUserService.Setup(x => x.FindByUsername(owner.Username, false)).Returns(owner);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                }
            }

            public static IEnumerable<object[]> ValidationResultTypes => Enum
                .GetValues(typeof(PackageValidationResultType))
                .Cast<PackageValidationResultType>()
                .Select(r => new object[] { r });

            [Theory]
            [MemberData(nameof(ValidationResultTypes))]
            public async Task DoesNotThrowForAnyPackageValidationResultType(PackageValidationResultType type)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion, type);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    fakePackageUploadService.Verify(
                        x => x.ValidateAfterGeneratePackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<User>(),
                            It.IsAny<User>(),
                            It.IsAny<bool>()),
                        Times.Once);
                }
            }

            public static IEnumerable<object[]> CommitResults => Enum
                .GetValues(typeof(PackageCommitResult))
                .Cast<PackageCommitResult>()
                .Select(r => new object[] { r });

            [Theory]
            [MemberData(nameof(CommitResults))]
            public async Task DoesNotThrowForAnyPackageCommitResult(PackageCommitResult commitResult)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.CommitPackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<Stream>()))
                        .ReturnsAsync(commitResult);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    fakePackageUploadService.Verify(x => x.CommitPackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<Stream>()), Times.Once);
                }
            }

            public static IEnumerable<object[]> WillCreateThePackageForNewId_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillCreateThePackageForNewId_Data))]

            public Task WillCreateThePackageForNewId(User currentUser, User ownerInForm)
            {
                return VerifyCreateThePackage(currentUser, ownerInForm, succeeds: true);
            }

            [Theory]
            [MemberData(nameof(WillCreateThePackageForNewId_Data))]

            public Task WillCreateThePackageIfOwnerInFormOwnsTheReservedNamespace(User currentUser, User ownerInForm)
            {
                return VerifyCreateThePackage(
                    currentUser,
                    ownerInForm,
                    succeeds: true,
                    reservedNamespaceOwner: ownerInForm);
            }

            public static IEnumerable<object[]> WillCreateThePackageIfOwnerInFormOwnsTheExistingPackage_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillCreateThePackageIfOwnerInFormOwnsTheExistingPackage_Data))]
            public Task WillCreateThePackageIfOwnerInFormOwnsTheExistingPackage(User currentUser, User ownerInForm)
            {
                return VerifyCreateThePackage(
                    currentUser,
                    ownerInForm,
                    succeeds: true,
                    existingPackageOwner: ownerInForm);
            }

            [Theory]
            [MemberData(nameof(WillCreateThePackageIfOwnerInFormOwnsTheExistingPackage_Data))]
            public Task WillCreateThePackageIfOwnerInFormOwnsTheExistingPackageInReservedNamespace(User currentUser, User ownerInForm)
            {
                return VerifyCreateThePackage(
                    currentUser,
                    ownerInForm,
                    succeeds: true,
                    existingPackageOwner: ownerInForm,
                    reservedNamespaceOwner: new User { Key = 787 });
            }

            public static IEnumerable<object[]> WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheExistingPackage_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, TestUtility.FakeUser);
                }
            }

            [Theory]
            [MemberData(nameof(WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheExistingPackage_Data))]
            public Task WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheExistingPackage(User currentUser, User ownerInForm, User existingPackageOwner)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.VerifyPackage_OwnerInvalid, ownerInForm.Username, PackageId);

                return VerifyCreateThePackage(
                    currentUser,
                    ownerInForm,
                    succeeds: false,
                    existingPackageOwner: existingPackageOwner,
                    expectedMessage: message);
            }

            public static IEnumerable<object[]> WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheReservedNamespace_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization, TestUtility.FakeUser);
                }
            }

            [Theory]
            [MemberData(nameof(WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheReservedNamespace_Data))]
            public Task WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheReservedNamespace(User currentUser, User ownerInForm, User reservedNamespaceOwner)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_IdNamespaceConflict, currentUser.Username, ownerInForm.Username);

                return VerifyCreateThePackage(
                    currentUser,
                    ownerInForm,
                    succeeds: false,
                    reservedNamespaceOwner: reservedNamespaceOwner,
                    errorResponseCode: HttpStatusCode.Conflict,
                    expectedMessage: message);
            }

            public static IEnumerable<object[]> WillNotCreateThePackageIfCurrentUserDoesNotHavePermissionsToUploadOnBehalfOfTheOwnerInTheForm_Data
            {
                get
                {
                    // User on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, null, null);
                    // User on behalf of an unrelated user who owns the package/reserved namespace.
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeOrganization, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, null, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeOrganization, TestUtility.FakeOrganization);
                    // User who owns the package/reserved namespace on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeUser, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, null, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeUser, TestUtility.FakeUser);

                    // Admin on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, null, null);
                    // Admin on behalf of an unrelated user who owns the package/reserved namespace.
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, TestUtility.FakeUser, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, null, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, TestUtility.FakeUser, TestUtility.FakeUser);
                    // Admin who owns the package/reserved namespace on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, TestUtility.FakeAdminUser, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, null, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);

                    // Organization admin whose organization owns the package/reserved namespace on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, TestUtility.FakeOrganization, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, null, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeOrganization);

                    // Organization collaborator whose organization owns the package/reserved namespace on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, TestUtility.FakeOrganization, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, null, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillNotCreateThePackageIfCurrentUserDoesNotHavePermissionsToUploadOnBehalfOfTheOwnerInTheForm_Data))]
            public Task WillNotCreateThePackageIfCurrentUserDoesNotHavePermissionsToUploadOnBehalfOfTheOwnerInTheForm(User currentUser, User ownerInForm, User existingPackageOwner = null, User reservedNamespaceOwner = null)
            {
                var templateString = existingPackageOwner == null ? Strings.UploadPackage_NewIdOnBehalfOfUserNotAllowed : Strings.UploadPackage_NewVersionOnBehalfOfUserNotAllowed;
                var message = string.Format(CultureInfo.CurrentCulture, templateString, currentUser.Username, ownerInForm.Username);

                return VerifyCreateThePackage(
                    currentUser,
                    ownerInForm,
                    succeeds: false,
                    existingPackageOwner: existingPackageOwner,
                    reservedNamespaceOwner: reservedNamespaceOwner,
                    expectedMessage: message);
            }

            [Fact]
            public async Task WillShowTheValidationMessageWhenPackageSecurityPolicyCreatesErrorMessage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    var expectedMessage = "The package is just bad.";

                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.ValidateAfterGeneratePackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<User>(),
                            It.IsAny<User>(),
                            It.IsAny<bool>()))
                        .ReturnsAsync(PackageValidationResult.Accepted);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var securityPolicyService = new Mock<ISecurityPolicyService>();
                    securityPolicyService
                        .Setup(m => m.EvaluatePackagePoliciesAsync(SecurityPolicyAction.PackagePush, It.IsAny<Package>(), It.IsAny<User>(), It.IsAny<User>(), It.IsAny<HttpContextBase>()))
                        .ReturnsAsync(SecurityPolicyResult.CreateErrorResult(expectedMessage));

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService,
                        securityPolicyService: securityPolicyService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    fakePackageUploadService.Verify(
                        x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Once);

                    var jsonResult = Assert.IsType<JsonResult>(result);
                    Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                    var message = ((JsonValidationMessage[]) jsonResult.Data)[0];
                    Assert.Equal(expectedMessage, message.PlainTextMessage);
                }
            }

            [Theory]
            [InlineData(PackageValidationResultType.Invalid, false)]
            [InlineData(PackageValidationResultType.Invalid, true)]
            public async Task WillShowTheValidationMessageWhenValidationAfterGenerateFails(PackageValidationResultType type, bool hasRawHtml)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    const string expectedMessage = "The package is just bad.";
                    var messageMock = new Mock<IValidationMessage>();
                    messageMock
                        .SetupGet(m => m.PlainTextMessage)
                        .Returns(expectedMessage);
                    messageMock
                        .SetupGet(m => m.HasRawHtmlRepresentation)
                        .Returns(hasRawHtml);
                    messageMock
                        .SetupGet(m => m.RawHtmlMessage)
                        .Returns(hasRawHtml ? null : expectedMessage);
                    var expectedResult = new PackageValidationResult(type, messageMock.Object);

                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.ValidateAfterGeneratePackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<User>(),
                            It.IsAny<User>(),
                            It.IsAny<bool>()))
                        .ReturnsAsync(expectedResult);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    VerifyPackageValidationResultMessage(type, expectedResult, controller, result);
                    fakePackageUploadService.Verify(
                        x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Once);
                }
            }

            [Theory]
            [InlineData(PackageValidationResultType.Invalid, false)]
            [InlineData(PackageValidationResultType.Invalid, true)]
            public async Task WillShowTheValidationMessageWhenValidationBeforeGenerateFails(PackageValidationResultType type, bool hasRawHtml)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    const string expectedMessage = "The package is just bad.";
                    var messageMock = new Mock<IValidationMessage>();
                    messageMock
                        .SetupGet(m => m.PlainTextMessage)
                        .Returns(expectedMessage);
                    messageMock
                        .SetupGet(m => m.HasRawHtmlRepresentation)
                        .Returns(hasRawHtml);
                    messageMock
                        .SetupGet(m => m.RawHtmlMessage)
                        .Returns(hasRawHtml ? null : expectedMessage);
                    var expectedResult = new PackageValidationResult(type, messageMock.Object);

                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.ValidateBeforeGeneratePackageAsync(
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageMetadata>(),
                            It.IsAny<User>()))
                        .ReturnsAsync(expectedResult);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    VerifyPackageValidationResultMessage(type, expectedResult, controller, result);
                    fakePackageUploadService.Verify(
                        x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Never);
                }
            }

            private static void VerifyPackageValidationResultMessage(PackageValidationResultType type, PackageValidationResult expectedResult, PackagesController controller, JsonResult result)
            {
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                var message = ((JsonValidationMessage[]) jsonResult.Data)[0];

                if (!expectedResult.Message.HasRawHtmlRepresentation)
                {
                    Assert.Null(message.RawHtmlMessage);
                    Assert.Equal(expectedResult.Message.PlainTextMessage, message.PlainTextMessage);
                }
                else
                {
                    Assert.Null(message.PlainTextMessage);
                    Assert.Equal(expectedResult.Message.RawHtmlMessage, message.RawHtmlMessage);
                }
            }

            private async Task VerifyCreateThePackage(User currentUser, User ownerInForm, bool succeeds, User existingPackageOwner = null, User reservedNamespaceOwner = null, HttpStatusCode errorResponseCode = HttpStatusCode.BadRequest, string expectedMessage = null)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakePackageService = new Mock<IPackageService>();
                    var existingPackageRegistration = existingPackageOwner == null ?
                        null :
                        new PackageRegistration { Id = PackageId, Owners = new[] { existingPackageOwner } };
                    fakePackageService
                        .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                        .Returns(existingPackageRegistration);

                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(currentUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                    var matchingReservedNamespaces = reservedNamespaceOwner == null ?
                        Enumerable.Empty<ReservedNamespace>() :
                        new[] { new ReservedNamespace { Owners = new[] { reservedNamespaceOwner } } };
                    fakeReservedNamespaceService.Setup(x => x.GetReservedNamespacesForId(PackageId)).Returns(matchingReservedNamespaces.ToList().AsReadOnly());

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(ownerInForm.Username, false)).Returns(ownerInForm);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService,
                        packageService: fakePackageService,
                        reservedNamespaceService: fakeReservedNamespaceService);
                    controller.SetCurrentUser(currentUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = ownerInForm.Username });

                    if (succeeds)
                    {
                        fakePackageUploadService.Verify(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            ownerInForm,
                            currentUser), Times.Once);
                    }
                    else
                    {
                        Assert.Equal((int)errorResponseCode, controller.Response.StatusCode);
                        var jsonResult = Assert.IsType<JsonResult>(result);
                        if (expectedMessage != null)
                        {
                            var message = ((JsonValidationMessage[]) jsonResult.Data)[0];
                            Assert.Equal(expectedMessage, message.PlainTextMessage);
                        }
                    }
                }
            }

            [Fact]
            public async Task WillUpdateIndexingService()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = PackageId }, Version = PackageVersion };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SavePackageFileAsync(fakePackage, It.IsAny<Stream>())).Returns(Task.FromResult(0)).Verifiable();

                    var fakeIndexingService = new Mock<IIndexingService>(MockBehavior.Strict);
                    fakeIndexingService.Setup(f => f.UpdateIndex()).Verifiable();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        packageFileService: fakePackageFileService,
                        indexingService: fakeIndexingService,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    // Assert
                    fakeIndexingService.Verify();
                }
            }

            [Fact]
            public async Task WillNotCommitChangesToPackageService()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.CompletedTask);
                    var fakePackageService = new Mock<IPackageService>(MockBehavior.Strict);
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = PackageId }, Version = PackageVersion };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    fakePackageService.Setup(x => x.PublishPackageAsync(fakePackage, false))
                        .Returns(Task.CompletedTask);
                    fakePackageService.Setup(x => x.FindPackageRegistrationById(fakePackage.PackageRegistration.Id))
                        .Returns((PackageRegistration)null);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakePackageUpdateService = new Mock<IPackageUpdateService>();
                    fakePackageUpdateService
                        .Setup(x => x.MarkPackageUnlistedAsync(fakePackage, false, false))
                        .Returns(Task.CompletedTask);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUpdateService: fakePackageUpdateService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    // There's no assert. If the method completes, it means the test passed because we set MockBehavior to Strict
                    // for the fakePackageService. We verified that it only calls methods passing commitSettings = false.
                }
            }

            [Fact]
            public async Task WillPublishThePackage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = PackageId }, Version = PackageVersion };
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    fakePackageService.Verify(x => x.PublishPackageAsync(fakePackage, false), Times.Once());
                }
            }

            [Fact]
            public async Task WillMarkThePackageUnlistedWhenListedArgumentIsFalse()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = PackageId, Owners = new[] { TestUtility.FakeUser } }, Version = PackageVersion }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                    var fakePackageUpdateService = new Mock<IPackageUpdateService>();
                    fakePackageUpdateService
                        .Setup(x => x.MarkPackageUnlistedAsync(It.Is<Package>(p => p.PackageRegistration.Id == PackageId && p.Version == PackageVersion), false, false))
                        .Returns(Task.CompletedTask)
                        .Verifiable();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUpdateService: fakePackageUpdateService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    fakePackageUpdateService.Verify();
                }
            }

            [Theory]
            [InlineData(null)]
            [InlineData(true)]
            public async Task WillNotMarkThePackageUnlistedWhenListedArgumentIsNullorTrue(bool? listed)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                    var fakePackageUpdateService = new Mock<IPackageUpdateService>(MockBehavior.Strict);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUpdateService: fakePackageUpdateService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = listed.GetValueOrDefault(true), Owner = TestUtility.FakeUser.Username });
                }
            }

            [Fact]
            public async Task WillDeleteTheUploadFile()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0)).Verifiable();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = PackageId, Owners = new[] { TestUtility.FakeUser } }, Version = PackageVersion }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    fakeUploadFileService.Verify();
                }
            }

            [Fact]
            public async Task WillSetAFlashMessage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    Assert.Equal(controller.TempData["Message"], String.Format(Strings.SuccessfullyUploadedPackage, PackageId, PackageVersion));
                }
            }

            [Fact]
            public async Task WillRedirectToPackagePage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(
                        new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username }) as JsonResult;

                    Assert.NotNull(result);
                    Assert.NotNull(result.Data);
                    Assert.Equal(
                        "{ location = /packages/" + PackageId + "/ }",
                        result.Data.ToString());
                }
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(x => x.ValidateAfterGeneratePackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<User>(),
                            It.IsAny<User>(),
                            It.IsAny<bool>()))
                        .ReturnsAsync(PackageValidationResult.Accepted());
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = PackageId, Owners = new[] { TestUtility.FakeUser } }, Version = PackageVersion };
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var auditingService = new TestAuditingService();

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        auditingService: auditingService,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username });

                    // Assert
                    Assert.True(auditingService.WroteRecord<PackageAuditRecord>(ar =>
                        ar.Action == AuditedPackageAction.Create
                        && ar.Id == fakePackage.PackageRegistration.Id
                        && ar.Version == fakePackage.Version));
                }
            }

            /// <remarks>
            /// There is a race condition between API and Web UI uploads where we can end up
            /// in a situation where user may have "verify package page" open in their browser
            /// pushes the same package with command line client, then clicks "Verify" in
            /// the browser. Browser will report failure (as package already exists). That
            /// failure must not be counted as "package push failure".
            /// </remarks>
            [Fact]
            public async Task DoesNotReportPackagePushFailureOnDuplicatePackage()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    fakePackageUploadService
                        .Setup(pus => pus.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Throws(new PackageAlreadyExistsException());

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        userService: fakeUserService,
                        telemetryService: fakeTelemetryService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await Assert.ThrowsAsync<PackageAlreadyExistsException>(() => controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username }));

                    // Assert
                    fakeTelemetryService
                        .Verify(ts => ts.TrackPackagePushFailureEvent(It.IsAny<string>(), It.IsAny<NuGetVersion>()), Times.Never);
                }
            }

            [Fact]
            public async Task WillNotCommitChangesToReadMeService()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService
                        .Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));

                    fakeUploadFileService
                        .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.CompletedTask);

                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakePackage = new Package
                    {
                        PackageRegistration = new PackageRegistration
                        {
                            Id = PackageId,
                        },
                        Version = PackageVersion
                    };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var fakeReadMeService = new Mock<IReadMeService>();

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        telemetryService: fakeTelemetryService,
                        userService: fakeUserService,
                        readMeService: fakeReadMeService.Object);

                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var request = new VerifyPackageRequest
                    {
                        Listed = true,
                        Owner = TestUtility.FakeUser.Username,
                        Edit = new EditPackageVersionReadMeRequest(),
                    };

                    // Act
                    await controller.VerifyPackage(request);

                    // Assert
                    fakeReadMeService.Verify(
                        x => x.SaveReadMeMdIfChanged(
                            fakePackage,
                            request.Edit,
                            controller.Request.ContentEncoding,
                            false),
                        Times.Once);
                    fakeReadMeService.Verify(
                        x => x.SaveReadMeMdIfChanged(
                            It.IsAny<Package>(),
                            It.IsAny<EditPackageVersionReadMeRequest>(),
                            It.IsAny<Encoding>(),
                            It.IsAny<bool>()),
                        Times.Once);
                }
            }

            [Fact]
            public async Task WillFailWhenCurrentUserIsLocked()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    var currentUser = new User { Key = 23, Username = "Bob", EmailAddress = "bob@example.com", UserStatusKey = UserStatus.Locked };
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(currentUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.CompletedTask);
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(currentUser.Username, false)).Returns(currentUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        telemetryService: fakeTelemetryService,
                        userService: fakeUserService);

                    controller.SetCurrentUser(currentUser);

                    // Act
                    var response = await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = currentUser.Username });

                    // Assert
                    Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                    Assert.Equal(ServicesStrings.UserAccountIsLocked, ((JsonValidationMessage[]) response.Data)[0].PlainTextMessage);
                }
            }


            [Fact]
            public async Task WillFailWhenAddedOwnerIsLocked()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    var owner = new User { Key = 23, Username = "Bob", EmailAddress = "bob@example.com", UserStatusKey = UserStatus.Locked };
                    var currentUser = TestUtility.FakeUser;
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(currentUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.CompletedTask);
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(owner.Username, false)).Returns(owner);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        telemetryService: fakeTelemetryService,
                        userService: fakeUserService);

                    controller.SetCurrentUser(currentUser);

                    // Act
                    var response = await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = owner.Username });

                    // Assert
                    Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                    Assert.Equal(
                        string.Format(CultureInfo.CurrentCulture, ServicesStrings.SpecificAccountIsLocked, owner.Username),
                        ((JsonValidationMessage[]) response.Data)[0].PlainTextMessage);
                }
            }

            [Fact]
            public async Task WillSendPackagePublishedEvent()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        telemetryService: fakeTelemetryService,
                        userService: fakeUserService);

                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username });

                    // Assert
                    fakeTelemetryService.Verify(x => x.TrackPackagePushEvent(It.IsAny<Package>(), TestUtility.FakeUser, controller.OwinContext.Request.User.Identity), Times.Once);
                }
            }

            [Theory]
            [InlineData(false, false, true)]
            [InlineData(true, false, true)]
            [InlineData(false, true, true)]
            [InlineData(true, true, false)]
            public async Task WillSendPackageAddedNotice(bool asyncValidationEnabled, bool blockingValidationEnabled, bool callExpected)
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);
                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = PackageId }, Version = PackageVersion };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(PackageId, PackageVersion);
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var configurationService = GetConfigurationService();
                    configurationService.Current.AsynchronousPackageValidationEnabled = asyncValidationEnabled;
                    configurationService.Current.BlockingAsynchronousPackageValidationEnabled = blockingValidationEnabled;

                    var fakeMessageService = new Mock<IMessageService>();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        configurationService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        telemetryService: fakeTelemetryService,
                        messageService: fakeMessageService,
                        userService: fakeUserService);

                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username, Edit = null });

                    // Assert
                    fakeMessageService
                        .Verify(ms => ms.SendMessageAsync(
                            It.Is<PackageAddedMessage>(msg => msg.Package == fakePackage),
                            false,
                            false),
                        Times.Exactly(callExpected ? 1 : 0));
                }
            }

            public static IEnumerable<object[]> WillApplyReadMe_Data
            {
                get
                {
                    yield return new object[] { new EditPackageVersionReadMeRequest() {
                    ReadMe = new ReadMeRequest { SourceType = "Written", SourceText = "markdown"} }
                };
                }
            }

            [Theory]
            [MemberData(nameof(WillApplyReadMe_Data))]
            public async Task WillApplyReadMeForWrittenReadMeData(EditPackageVersionReadMeRequest edit)
            {
                // Arrange
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);

                    var fakePackageUploadService = GetValidPackageUploadService(PackageId, PackageVersion);
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = PackageId }, Version = PackageVersion };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .ReturnsAsync(fakePackage);

                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SaveReadMeMdFileAsync(fakePackage, It.IsAny<string>())).Returns(Task.CompletedTask);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        packageFileService: fakePackageFileService,
                        userService: fakeUserService);

                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username, Edit = edit });

                    var hasReadMe = !string.IsNullOrEmpty(edit.ReadMe?.SourceType);
                    fakePackageFileService.Verify(x => x.SaveReadMeMdFileAsync(fakePackage, "markdown"), Times.Exactly(hasReadMe ? 1 : 0));
                }
            }

            [Theory]
            [MemberData(nameof(CommitResults))]
            public async Task DoesNotThrowForAnyPackageCommitResultForSymbolsPackage(PackageCommitResult commitResult)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService
                        .Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService
                        .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult(0));
                    var fakeSymbolsPackageUploadService = GetValidSymbolPackageUploadService(PackageId, PackageVersion, TestUtility.FakeUser, commit: commitResult);
                    var fakeNuGetPackage = TestPackage.CreateTestSymbolPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        symbolPackageUploadService: fakeSymbolsPackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    fakeSymbolsPackageUploadService.Verify(x => x.CreateAndUploadSymbolsPackage(
                        It.IsAny<Package>(),
                        It.IsAny<Stream>()), Times.Once);
                }
            }

            public static IEnumerable<object[]> SymbolValidationResultTypes => Enum
                .GetValues(typeof(SymbolPackageValidationResultType))
                .Cast<SymbolPackageValidationResultType>()
                .Select(r => new object[] { r });

            [Theory]
            [MemberData(nameof(SymbolValidationResultTypes))]
            public async Task DoesNotThrowForAnySymbolPackageValidationResultType(PackageCommitResult commitResult)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService
                        .Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService
                        .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult(0));
                    var fakeSymbolsPackageUploadService = GetValidSymbolPackageUploadService(PackageId, PackageVersion, TestUtility.FakeUser, commit: commitResult);
                    var fakeNuGetPackage = TestPackage.CreateTestSymbolPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        symbolPackageUploadService: fakeSymbolsPackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    fakeSymbolsPackageUploadService.Verify(x => x.ValidateUploadedSymbolsPackage(
                        It.IsAny<Stream>(),
                        It.IsAny<User>()), Times.Once);
                    fakeSymbolsPackageUploadService.Verify(x => x.CreateAndUploadSymbolsPackage(
                        It.IsAny<Package>(),
                        It.IsAny<Stream>()), Times.Once);
                }
            }

            [Fact]
            public async Task WillShowErrorMessageWhenFailedValidations()
            {
                var message = "funky symbols";
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService
                        .Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService
                        .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult(0));
                    var fakeSymbolsPackageUploadService = new Mock<ISymbolPackageUploadService>();
                    fakeSymbolsPackageUploadService.Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                        .ReturnsAsync(SymbolPackageValidationResult.Invalid(message));
                    var fakeNuGetPackage = TestPackage.CreateTestSymbolPackageStream(PackageId, PackageVersion);

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        symbolPackageUploadService: fakeSymbolsPackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Owner = TestUtility.FakeUser.Username });

                    Assert.NotNull(result);
                    Assert.Equal(message, ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                }
            }

            [Fact]
            public async Task WillReturnUnexpectedErrorWhenSymbolsCreationFails()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService
                        .Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService
                        .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult(0));
                    var fakeSymbolsPackageUploadService = GetValidSymbolPackageUploadService(PackageId, PackageVersion, TestUtility.FakeUser);
                    fakeSymbolsPackageUploadService
                        .Setup(x => x.CreateAndUploadSymbolsPackage(It.IsAny<Package>(), It.IsAny<Stream>()))
                        .ThrowsAsync(new Exception());
                    var fakeNuGetPackage = TestPackage.CreateTestSymbolPackageStream(PackageId, PackageVersion);
                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);
                    var telemetryService = new Mock<ITelemetryService>();

                    var controller = CreateController(
                        GetConfigurationService(),
                        symbolPackageUploadService: fakeSymbolsPackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService,
                        telemetryService: telemetryService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Owner = TestUtility.FakeUser.Username });

                    Assert.NotNull(result);
                    Assert.Equal(Strings.VerifyPackage_UnexpectedError, ((JsonValidationMessage[]) result.Data)[0].PlainTextMessage);
                    telemetryService
                        .Verify(x => x.TrackSymbolPackagePushFailureEvent(PackageId, PackageVersion), Times.Once);
                }
            }

            [Fact]
            public async Task WillRedirectToPackageDetailsPageAfterSuccessfullyCreatingSymbolsPackage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService
                        .Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService
                        .Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult(0));
                    var fakeSymbolsPackageUploadService = GetValidSymbolPackageUploadService(PackageId, PackageVersion, TestUtility.FakeUser);
                    var fakeNuGetPackage = TestPackage.CreateTestSymbolPackageStream(PackageId, PackageVersion);
                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username, false)).Returns(TestUtility.FakeUser);
                    var telemetryService = new Mock<ITelemetryService>();
                    var auditingService = new TestAuditingService();

                    var controller = CreateController(
                        GetConfigurationService(),
                        symbolPackageUploadService: fakeSymbolsPackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService,
                        telemetryService: telemetryService,
                        auditingService: auditingService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Owner = TestUtility.FakeUser.Username });

                    // Assert
                    Assert.NotNull(result);
                    telemetryService
                        .Verify(x => x.TrackSymbolPackagePushEvent(PackageId, PackageVersion), Times.Once);

                    Assert.True(auditingService.WroteRecord<PackageAuditRecord>(ar =>
                        ar.Action == AuditedPackageAction.SymbolsCreate
                        && ar.Id == PackageId
                        && ar.Version == PackageVersion
                        && ar.Reason == PackageCreatedVia.Web));

                    fakeUploadFileService
                        .Verify(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key), Times.Once);
                }
            }
        }

        public class TheUploadProgressAction : TestContainer
        {
            private static readonly string FakeUploadName = "upload-" + TestUtility.FakeUserName + Guid.Empty.ToString();

            [Fact]
            public void WillReturnHttpNotFoundForUnknownUser()
            {
                // Arrange
                var cacheService = new Mock<ICacheService>(MockBehavior.Strict);
                cacheService.Setup(c => c.GetItem(FakeUploadName)).Returns((object)null);

                var controller = CreateController(
                        GetConfigurationService(),
                        cacheService: cacheService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                var result = controller.UploadPackageProgress();

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal(JsonRequestBehavior.AllowGet, jsonResult.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
            }

            [Fact]
            public void WillReturnCorrectResultForKnownUser()
            {
                var cacheService = new Mock<ICacheService>(MockBehavior.Strict);
                cacheService.Setup(c => c.GetItem(FakeUploadName))
                            .Returns(new AsyncFileUploadProgress(100) { FileName = "haha", TotalBytesRead = 80 });

                var controller = CreateController(
                        GetConfigurationService(),
                        cacheService: cacheService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                var result = controller.UploadPackageProgress() as JsonResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
                Assert.True(result.Data is AsyncFileUploadProgress);
                var progress = (AsyncFileUploadProgress)result.Data;
                Assert.Equal(80, progress.TotalBytesRead);
                Assert.Equal(100, progress.ContentLength);
                Assert.Equal("haha", progress.FileName);
            }
        }

        public class TheSetLicenseReportVisibilityMethod
            : TestContainer
        {
            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    foreach (var visible in new[] { true, false })
                    {
                        yield return new object[]
                        {
                            null,
                            TestUtility.FakeUser,
                            visible
                        };

                        yield return new object[]
                        {
                            TestUtility.FakeUser,
                            new User { Key = 5535 },
                            visible
                        };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns403IfNotOwner(User currentUser, User owner, bool visible)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);
                // Note: this Mock must be strict because it guarantees that SetLicenseReportVisibilityAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.SetLicenseReportVisibility("Foo", "1.0", visible: visible, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                var httpStatusCodeResult = Assert.IsType<HttpStatusCodeResult>(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    foreach (var visible in new[] { true, false })
                    {
                        yield return new object[]
                        {
                            TestUtility.FakeUser,
                            TestUtility.FakeUser,
                            visible
                        };

                        yield return new object[]
                        {
                            TestUtility.FakeAdminUser,
                            TestUtility.FakeUser,
                            visible
                        };

                        yield return new object[]
                        {
                            TestUtility.FakeOrganizationAdmin,
                            TestUtility.FakeOrganization,
                            visible
                        };

                        yield return new object[]
                        {
                            TestUtility.FakeOrganizationCollaborator,
                            TestUtility.FakeOrganization,
                            visible
                        };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task IndexingAndPackageServicesAreUpdated(User currentUser, User owner, bool visible)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    HideLicenseReport = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.SetLicenseReportVisibilityAsync(It.IsAny<Package>(), It.Is<bool>(t => t == !visible), It.IsAny<bool>()))
                    .Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.SetLicenseReportVisibilityAsync(It.IsAny<Package>(), It.Is<bool>(t => t == visible), It.IsAny<bool>()))
                    .Returns(Task.CompletedTask).Verifiable();
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package).Verifiable();

                var indexingService = new Mock<IIndexingService>();

                var controller = CreateController(
                        GetConfigurationService(),
                        packageService: packageService,
                        indexingService: indexingService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.SetLicenseReportVisibility("Foo", "1.0", visible: visible, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                var redirectResult = Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", redirectResult.Url);
            }
        }

        public class TheRevalidateMethod : TestContainer
        {
            private Package _package;
            private readonly Mock<IPackageService> _packageService;
            private readonly Mock<IValidationService> _validationService;
            private readonly TestGalleryConfigurationService _configurationService;
            private readonly PackagesController _target;

            public TheRevalidateMethod()
            {
                _package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "NuGet.Versioning" },
                    Version = "3.4.0",
                };

                _packageService = new Mock<IPackageService>();
                _packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Returns(_package);

                _validationService = new Mock<IValidationService>();

                _configurationService = GetConfigurationService();

                _target = CreateController(
                    _configurationService,
                    packageService: _packageService,
                    validationService: _validationService);
            }

            [Fact]
            public async Task RevalidateIsCalledWithTheExistingPackage()
            {
                // Arrange & Act
                await _target.Revalidate(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                _validationService.Verify(
                    x => x.RevalidateAsync(_package),
                    Times.Once);
            }

            [Fact]
            public async Task RedirectsToCustomReturnUrl()
            {
                // Arrange & Act
                var result = await _target.Revalidate(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    "/Admin");

                // Assert
                var redirect = Assert.IsType<SafeRedirectResult>(result);
                Assert.Equal("/Admin", redirect.Url);
                Assert.Equal("/", redirect.SafeUrl);
            }

            [Fact]
            public async Task RedirectsAfterRevalidatingPackage()
            {
                // Arrange & Act
                var result = await _target.Revalidate(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                var redirect = Assert.IsType<SafeRedirectResult>(result);
                Assert.Equal($"/packages/{_package.PackageRegistration.Id}/{_package.Version}", redirect.Url);
                Assert.Equal("/", redirect.SafeUrl);
            }

            [Fact]
            public async Task ReturnsNotFoundForUnknownPackage()
            {
                // Arrange
                _packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Returns<Package>(null);

                // Act
                var result = await _target.Revalidate(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task ReturnsNotFoundForDeletedPackage()
            {
                // Arrange
                _packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Returns(new Package { PackageStatusKey = PackageStatus.Deleted });

                // Act
                var result = await _target.Revalidate(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _target?.Dispose();
                    base.Dispose(disposing);
                }
            }
        }

        public class TheRevalidateSymbolsMethod : TestContainer
        {
            private Package _package;
            private SymbolPackage _symbolPackage;
            private readonly Mock<IPackageService> _packageService;
            private readonly Mock<IValidationService> _validationService;
            private readonly TestGalleryConfigurationService _configurationService;
            private readonly PackagesController _target;

            public TheRevalidateSymbolsMethod()
            {
                _package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "NuGet.Versioning" },
                    Version = "3.4.0",
                };
                _symbolPackage = new SymbolPackage
                {
                    Package = _package,
                    StatusKey = PackageStatus.Available
                };
                _package.SymbolPackages.Add(_symbolPackage);

                _packageService = new Mock<IPackageService>();
                _packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Returns(_package);

                _validationService = new Mock<IValidationService>();

                _configurationService = GetConfigurationService();

                _target = CreateController(
                    _configurationService,
                    packageService: _packageService,
                    validationService: _validationService);
            }

            [Fact]
            public async Task RevalidateIsCalledWithTheExistingSymbolsPackage()
            {
                // Arrange & Act
                await _target.RevalidateSymbols(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                _validationService.Verify(
                    x => x.RevalidateAsync(_symbolPackage),
                    Times.Once);
            }

            [Fact]
            public async Task RedirectsToCustomReturnUrl()
            {
                // Arrange & Act
                var result = await _target.RevalidateSymbols(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    "/Admin");

                // Assert
                var redirect = Assert.IsType<SafeRedirectResult>(result);
                Assert.Equal("/Admin", redirect.Url);
                Assert.Equal("/", redirect.SafeUrl);
            }

            [Theory]
            [InlineData(PackageStatus.Available)]
            [InlineData(PackageStatus.FailedValidation)]
            [InlineData(PackageStatus.Validating)]
            public async Task RedirectsAfterRevalidatingSymbolsPackageForAllValidStatus(PackageStatus status)
            {
                // Arrange & Act
                _symbolPackage.StatusKey = status;
                var result = await _target.RevalidateSymbols(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                var redirect = Assert.IsType<SafeRedirectResult>(result);
                Assert.Equal($"/packages/{_package.Id}/{_package.Version}", redirect.Url);
                Assert.Equal("/", redirect.SafeUrl);
            }

            [Fact]
            public async Task ReturnsNotFoundForUnknownPackage()
            {
                // Arrange
                _packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Returns<Package>(null);

                // Act
                var result = await _target.RevalidateSymbols(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                ResultAssert.IsStatusCode(result, HttpStatusCode.NotFound);
            }

            [Fact]
            public async Task ReturnsNotFoundForDeletedPackage()
            {
                // Arrange
                _packageService
                    .Setup(svc => svc.FindPackageByIdAndVersionStrict(
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Returns(new Package { PackageStatusKey = PackageStatus.Deleted });

                // Act
                var result = await _target.RevalidateSymbols(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                ResultAssert.IsStatusCode(result, HttpStatusCode.NotFound);
            }

            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(921)]
            public async Task ReturnsBadRequestForInvalidSymbolPackageStatus(PackageStatus status)
            {
                // Arrange and Act
                _symbolPackage.StatusKey = status;
                var result = await _target.RevalidateSymbols(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _target?.Dispose();
                    base.Dispose(disposing);
                }
            }
        }

        public class TheSetRequiredSignerMethod : TestContainer
        {
            private readonly PackageRegistration _packageRegistration;
            private readonly User _signer;

            public TheSetRequiredSignerMethod()
            {
                _packageRegistration = new PackageRegistration()
                {
                    Key = 1,
                    Id = "a"
                };
                _signer = new User()
                {
                    Key = 2,
                    Username = "b"
                };
            }

            [Fact]
            public async Task WhenPackageRegistrationNotFound_ReturnsNotFound()
            {
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                packageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns<PackageRegistration>(null);

                var result = await controller.SetRequiredSigner(_packageRegistration.Id, _signer.Username);

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
            }

            [Fact]
            public async Task WhenSignerNotFound_ReturnsNotFound()
            {
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    userService: userService);

                var currentUser = new User()
                {
                    Key = 3,
                    Username = "c"
                };

                _packageRegistration.Owners.Add(currentUser);

                packageService.Setup(x => x.FindPackageRegistrationById(
                        It.Is<string>(id => id == _packageRegistration.Id)))
                    .Returns(_packageRegistration);
                userService.Setup(x => x.FindByUsername(It.Is<string>(username => username == _signer.Username), false))
                    .Returns<User>(null);

                controller.SetCurrentUser(currentUser);
                controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var result = await controller.SetRequiredSigner(_packageRegistration.Id, _signer.Username);

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
            }

            [Fact]
            public async Task WhenCurrentUserIsNotAuthenticated_ReturnsForbidden()
            {
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                _packageRegistration.Owners.Add(_signer);

                packageService.Setup(x => x.FindPackageRegistrationById(
                        It.Is<string>(id => id == _packageRegistration.Id)))
                    .Returns(_packageRegistration);

                var result = await controller.SetRequiredSigner(_packageRegistration.Id, _signer.Username);

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
            }

            [Fact]
            public async Task WhenCurrentUserIsNotMultiFactorAuthenticated_ReturnsForbidden()
            {
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    userService: userService);

                packageService.Setup(x => x.FindPackageRegistrationById(
                        It.Is<string>(id => id == _packageRegistration.Id)))
                    .Returns(_packageRegistration);
                userService.Setup(x => x.FindByUsername(It.Is<string>(username => username == _signer.Username), false))
                    .Returns(_signer);

                _packageRegistration.Owners.Add(_signer);
                controller.SetCurrentUser(_signer);

                var result = await controller.SetRequiredSigner(_packageRegistration.Id, _signer.Username);

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
            }

            [Fact]
            public async Task WhenCurrentUserIsNotPackageOwner_ReturnsForbidden()
            {
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    userService: userService);

                packageService.Setup(x => x.FindPackageRegistrationById(
                        It.Is<string>(id => id == _packageRegistration.Id)))
                    .Returns(_packageRegistration);
                userService.Setup(x => x.FindByUsername(It.Is<string>(username => username == _signer.Username), false))
                    .Returns(_signer);

                controller.SetCurrentUser(_signer);

                var result = await controller.SetRequiredSigner(_packageRegistration.Id, _signer.Username);

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenCurrentUserIsAuthenticatedOwner_ReturnsOK(bool multiFactorAuthenticatedButNotAADLoggedIn)
            {
                var packageService = new Mock<IPackageService>();
                var userService = new Mock<IUserService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    userService: userService);

                _packageRegistration.Owners.Add(_signer);

                packageService.Setup(x => x.FindPackageRegistrationById(
                        It.Is<string>(id => id == _packageRegistration.Id)))
                    .Returns(_packageRegistration);
                userService.Setup(x => x.FindByUsername(It.Is<string>(username => username == _signer.Username), false))
                    .Returns(_signer);

                controller.SetCurrentUser(_signer);
                if (multiFactorAuthenticatedButNotAADLoggedIn)
                {
                    controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);
                }
                else
                {
                    controller.OwinContext.AddClaim(NuGetClaims.ExternalLoginCredentialType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory);
                }

                var result = await controller.SetRequiredSigner(_packageRegistration.Id, _signer.Username);

                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.OK, controller.Response.StatusCode);
            }
        }

        public class ThePreviewReadMeMethod : TestContainer
        {
            [Fact]
            public async Task ReturnsProperResponseModelWhenSucceeds()
            {
                var readmeService = new Mock<IReadMeService>();
                var controller = CreateController(GetConfigurationService(),
                    readMeService: readmeService.Object);

                var request = new ReadMeRequest();

                readmeService
                    .Setup(rs => rs.HasReadMeSource(request))
                    .Returns(true);

                const string html = "some HTML";

                readmeService
                    .Setup(rs => rs.GetReadMeHtmlAsync(request, It.IsAny<Encoding>()))
                    .ReturnsAsync(new RenderedMarkdownResult { Content = html });

                var result = await controller.PreviewReadMe(request);

                var readmeResult = Assert.IsType<RenderedMarkdownResult>(result.Data);
                Assert.Equal(html, readmeResult.Content);
            }

            [Fact]
            public async Task ReturnsProperResponseModelWhenNoReadme()
            {
                var readmeService = new Mock<IReadMeService>();
                var controller = CreateController(GetConfigurationService(),
                    readMeService: readmeService.Object);

                var request = new ReadMeRequest();

                readmeService
                    .Setup(rs => rs.HasReadMeSource(request))
                    .Returns(false);

                var result = await controller.PreviewReadMe(request);

                var stringArray = Assert.IsType<string[]>(result.Data);
                Assert.Single(stringArray);
                Assert.Equal("There is no Markdown Documentation available to preview.", stringArray[0]);
            }

            [Fact]
            public async Task ReturnsProperResponseModelWhenConversionFails()
            {
                var readmeService = new Mock<IReadMeService>();
                var controller = CreateController(GetConfigurationService(),
                    readMeService: readmeService.Object);

                var request = new ReadMeRequest();

                readmeService
                    .Setup(rs => rs.HasReadMeSource(request))
                    .Returns(true);

                const string exceptionMessage = "failure";
                readmeService
                    .Setup(rs => rs.GetReadMeHtmlAsync(request, It.IsAny<Encoding>()))
                    .ThrowsAsync(new Exception(exceptionMessage));

                var result = await controller.PreviewReadMe(request);

                var stringArray = Assert.IsType<string[]>(result.Data);
                Assert.Single(stringArray);
                Assert.Contains(exceptionMessage, stringArray[0]);
            }
        }

        public class TheGetReadMeMethod : TestContainer
        {
            [Fact]
            public async Task ReturnsNotFoundIfPackageIsMissing()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns((Package)null);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // Act
                var result = await controller.GetReadMeMd("a", "1.9.2019");

                // Assert
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
            }

            [Fact]
            public async Task ReturnsNotFoundIfPackageIsDeleted()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new Package { PackageStatusKey = PackageStatus.Deleted });

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // Act
                var result = await controller.GetReadMeMd("a", "1.9.2019");

                // Assert
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task ReturnsForbiddenIfNotAllowed(User currentUser, User owner)
            {
                // Arrange
                var packageId = "package";
                var packageRegistration = new PackageRegistration { Id = packageId };
                packageRegistration.Owners.Add(owner);

                var package = new Package
                {
                    Key = 2,
                    PackageRegistration = packageRegistration,
                    Version = "1.1.1",
                    Listed = true,
                    IsLatestSemVer2 = true,
                    HasReadMe = false
                };

                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(packageRegistration.Id, package.Version))
                    .Returns(package);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.GetReadMeMd(packageId, package.Version);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsForPackageWithoutReadMe(User currentUser, User owner)
            {
                // Arrange
                var packageId = "package";
                var packageRegistration = new PackageRegistration { Id = packageId };
                packageRegistration.Owners.Add(owner);

                var package = new Package
                {
                    Key = 2,
                    PackageRegistration = packageRegistration,
                    Version = "42.12.43",
                    Listed = true,
                    IsLatestSemVer2 = true,
                    HasReadMe = false
                };

                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(packageRegistration.Id, package.Version))
                    .Returns(package);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.GetReadMeMd(packageId, package.Version);

                // Assert
                var request = Assert.IsType<EditPackageVersionReadMeRequest>(result.Data);
                Assert.Null(request.ReadMe.SourceType);
                Assert.Null(request.ReadMe.SourceText);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsForPackageWithReadMe(User currentUser, User owner)
            {
                // Arrange
                var packageId = "package";
                var packageRegistration = new PackageRegistration { Id = packageId };
                packageRegistration.Owners.Add(owner);

                var package = new Package
                {
                    Key = 2,
                    PackageRegistration = packageRegistration,
                    Version = "42.12.43",
                    Listed = true,
                    IsLatestSemVer2 = true,
                    HasReadMe = true
                };

                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(packageRegistration.Id, package.Version))
                    .Returns(package);

                var readMe = "readMe";
                var readMeService = new Mock<IReadMeService>();
                readMeService
                    .Setup(x => x.GetReadMeMdAsync(package))
                    .ReturnsAsync(readMe);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    readMeService: readMeService.Object);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.GetReadMeMd(packageId, package.Version);

                // Assert
                var request = Assert.IsType<EditPackageVersionReadMeRequest>(result.Data);
                Assert.Equal(ReadMeService.TypeWritten, request.ReadMe.SourceType);
                Assert.Equal(readMe, request.ReadMe.SourceText);
            }
        }

        public class TheReflowMethod : TestContainer
        {
            private readonly Mock<IPackageService> _packageService;
            private string _packageId = "packageId";
            private string _packageVersion = "1.0.0";

            public TheReflowMethod()
            {
                _packageService = new Mock<IPackageService>();
            }

            [Fact]
            public async Task GivenDeletedPackageReturns404()
            {
                // arrange
                _packageService
                    .Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion))
                    .Returns(new Package { PackageStatusKey = PackageStatus.Deleted });
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService);

                // act
                var result = await controller.Reflow(_packageId, _packageVersion);

                // assert
                Assert.IsType<HttpNotFoundResult>(result);
            }
        }

        public class TheLicenseMethod : TestContainer
        {
            private readonly Mock<IPackageService> _packageService;
            private readonly Mock<IPackageFileService> _packageFileService;
            private readonly Mock<ICoreLicenseFileService> _coreLicenseFileService;
            private string _packageId = "packageId";
            private string _packageVersion = "1.0.0";

            public TheLicenseMethod()
            {
                _packageService = new Mock<IPackageService>();
                _packageFileService = new Mock<IPackageFileService>();
                _coreLicenseFileService = new Mock<ICoreLicenseFileService>();
            }

            [Fact]
            public async Task GivenInvalidPackageReturns404()
            {
                // arrange
                _packageService.Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion)).Returns<Package>(null);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService);

                // act
                var result = await controller.License(_packageId, _packageVersion);

                // assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task GivenNullVersionReturns404()
            {
                // arrange
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService);

                // act
                var result = await controller.License(_packageId, version: null);

                // assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task GivenDeletedPackageReturns404()
            {
                // arrange
                _packageService
                    .Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion))
                    .Returns(new Package { PackageStatusKey = PackageStatus.Deleted });
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService);

                // act
                var result = await controller.License(_packageId, _packageVersion);

                // assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [InlineData("MIT")]
            [InlineData("some expression")]
            [InlineData("(MIT OR GPL-3.0-only)")]
            public async Task GivenValidPackageSplitExpressionAndSetSegmentsWhenLicenseExpressionExists(string licenseExpression)
            {
                // arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = _packageId },
                    Version = _packageVersion,
                    LicenseExpression = licenseExpression
                };

                var splitterMock = new Mock<ILicenseExpressionSplitter>();
                var segments = new List<CompositeLicenseExpressionSegment>();
                splitterMock
                    .Setup(les => les.SplitExpression(licenseExpression))
                    .Returns(segments);

                _packageService.Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService,
                    licenseExpressionSplitter: splitterMock);

                // act
                var result = await controller.License(_packageId, _packageVersion);

                // assert
                splitterMock
                    .Verify(les => les.SplitExpression(licenseExpression), Times.Once);
                splitterMock
                    .Verify(les => les.SplitExpression(It.IsAny<string>()), Times.Once);

                var model = ResultAssert.IsView<DisplayLicenseViewModel>(result);
                Assert.Equal(_packageId, model.Id);
                Assert.Equal(_packageVersion, model.Version);
                Assert.Equal(licenseExpression, model.LicenseExpression);
                Assert.Equal(segments, model.LicenseExpressionSegments);
            }

            [Fact]
            public async Task GivenValidPackageButInvalidLicenseExpressionThrowException()
            {
                // arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = _packageId },
                    Version = _packageVersion,
                    LicenseExpression = "some invalid expression"
                };

                var expectedExceptionMessage = "Splitting license expression fails!";
                var splitterMock = new Mock<ILicenseExpressionSplitter>();
                splitterMock.Setup(les => les.SplitExpression(It.IsAny<string>())).Throws(new Exception(expectedExceptionMessage));

                _packageService.Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService,
                    licenseExpressionSplitter: splitterMock);

                // act & Assert
                var exception = await Assert.ThrowsAnyAsync<Exception>(() => controller.License(_packageId, _packageVersion));
                Assert.Equal(expectedExceptionMessage, exception.Message);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            public async Task GivenValidPackageInfoSetLicenseFileContentsWhenLicenseFileExists(EmbeddedLicenseFileType embeddedLicenseFileType)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = _packageId },
                    Version = _packageVersion,
                };
                package.EmbeddedLicenseType = embeddedLicenseFileType;

                _packageService.Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService,
                    coreLicenseFileService: _coreLicenseFileService);

                var licenseFileContents = "This is a license file";
                var fakeFileStream = new MemoryStream(Encoding.UTF8.GetBytes(licenseFileContents));
                _coreLicenseFileService
                    .Setup(p => p.DownloadLicenseFileAsync(package))
                    .Returns(Task.FromResult<Stream>(fakeFileStream));

                // Act
                var result = await controller.License(_packageId, _packageVersion);

                // Assert
                _coreLicenseFileService
                    .Verify(p => p.DownloadLicenseFileAsync(package),
                        Times.Once);
                var model = ResultAssert.IsView<DisplayLicenseViewModel>(result);
                Assert.Equal(licenseFileContents, model.LicenseFileContents);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            public async Task GivenValidPackageInfoButTooLargeLicenseFileThrowException(EmbeddedLicenseFileType embeddedLicenseFileType)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = _packageId },
                    Version = _packageVersion,
                };
                package.EmbeddedLicenseType = embeddedLicenseFileType;

                _packageService.Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService,
                    coreLicenseFileService: _coreLicenseFileService);

                var fakeFileStream = new MemoryStream(new byte[PackagesController.MaxAllowedLicenseLengthForDisplaying + 1]);
                _coreLicenseFileService
                    .Setup(p => p.DownloadLicenseFileAsync(package))
                    .Returns(Task.FromResult<Stream>(fakeFileStream));

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() => controller.License(_packageId, _packageVersion));
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            public async Task GivenValidPackageInfoButInvalidLicenseFileThrowException(EmbeddedLicenseFileType embeddedLicenseFileType)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = _packageId },
                    Version = _packageVersion,
                };
                package.EmbeddedLicenseType = embeddedLicenseFileType;

                var expectedExceptionMessage = "Downloading license file fails!";
                _packageService.Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion)).Returns(package);
                _coreLicenseFileService.Setup(p => p.DownloadLicenseFileAsync(package)).Throws(new Exception(expectedExceptionMessage));
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService,
                    coreLicenseFileService: _coreLicenseFileService);

                // Act & Assert
                var exception = await Assert.ThrowsAnyAsync<Exception>(() => controller.License(_packageId, _packageVersion));
                Assert.Equal(expectedExceptionMessage, exception.Message);
            }

            [Fact]
            public async Task GivenValidPackageInfoSetLicenseUrlWhenLicenseUrlExists()
            {
                // Arrange
                var licenseUrl = "https://testlicenseurl/";
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = _packageId },
                    Version = _packageVersion,
                    LicenseUrl = licenseUrl
                };

                _packageService.Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService);

                // Act
                var result = await controller.License(_packageId, _packageVersion);

                // Assert
                var model = ResultAssert.IsView<DisplayLicenseViewModel>(result);
                Assert.Equal(licenseUrl, model.LicenseUrl);
            }

            [Fact]
            public async Task UsesProperIconUrl()
            {
                var iconUrlProvider = new Mock<IIconUrlProvider>();
                const string iconUrl = "https://some.test/icon";
                iconUrlProvider
                    .Setup(iup => iup.GetIconUrlString(It.IsAny<Package>()))
                    .Returns(iconUrl);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService,
                    iconUrlProvider: iconUrlProvider);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = _packageId },
                    Version = _packageVersion,
                    LicenseUrl = "https://license.test/"
                };

                _packageService
                    .Setup(p => p.FindPackageByIdAndVersionStrict(_packageId, _packageVersion))
                    .Returns(package);

                var result = await controller.License(_packageId, _packageVersion);
                var model = ResultAssert.IsView<DisplayLicenseViewModel>(result);
                iconUrlProvider
                    .Verify(iup => iup.GetIconUrlString(package), Times.AtLeastOnce);
                Assert.Equal(iconUrl, model.IconUrl);
            }
        }

        public class TheManagePackageOwnersMethod : TestContainer
        {
            [Fact]
            public void RedirectsToManageAction()
            {
                var id = "packageId";
                var controller = CreateController(GetConfigurationService());
                var result = controller.ManagePackageOwners(id);
                ResultAssert.IsRedirectToRoute(
                    result,
                    new { action = "Manage" },
                    true);
            }
        }

        public class TheDeleteMethod : TestContainer
        {
            [Fact]
            public void RedirectsToManageAction()
            {
                var id = "packageId";
                var version = "packageVersion";
                var controller = CreateController(GetConfigurationService());
                var result = controller.Delete(id, version);
                ResultAssert.IsRedirectToRoute(
                    result,
                    new { action = "Manage" },
                    true);
            }
        }

        public class TheEditMethod : TestContainer
        {
            [Fact]
            public void RedirectsToManageAction()
            {
                var id = "packageId";
                var version = "packageVersion";
                var controller = CreateController(GetConfigurationService());
                var result = controller.Edit(id, version);
                ResultAssert.IsRedirectToRoute(
                    result,
                    new { action = "Manage" },
                    true);
            }
        }
    }
}
