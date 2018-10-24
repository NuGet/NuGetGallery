// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Builder;
using System.Web.Http.OData.Query;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.OData;
using NuGetGallery.WebApi;

namespace NuGetGallery.Controllers
{
    public abstract class ODataFeedControllerFactsBase<TController>
        where TController : NuGetODataController
    {
        protected const string _siteRoot = "https://nuget.localtest.me";
        protected const string TestPackageId = "Some.Awesome.Package";

        protected readonly IReadOnlyCollection<Package> AvailablePackages;
        protected readonly IReadOnlyCollection<Package> UnavailablePackages;
        protected readonly IReadOnlyCollection<Package> NonSemVer2Packages;
        protected readonly IReadOnlyCollection<Package> SemVer2Packages;
        protected readonly IEntityRepository<Package> PackagesRepository;
        protected readonly IQueryable<Package> AllPackages;

        protected ODataFeedControllerFactsBase()
        {
            // Arrange
            AllPackages = CreatePackagesQueryable();
            AvailablePackages = AllPackages.Where(p => p.PackageStatusKey == PackageStatus.Available).ToList();
            UnavailablePackages = AllPackages.Where(p => p.PackageStatusKey != PackageStatus.Available).ToList();
            NonSemVer2Packages = AvailablePackages.Where(p => p.SemVerLevelKey == SemVerLevelKey.Unknown).ToList();
            SemVer2Packages = AvailablePackages.Where(p => p.SemVerLevelKey == SemVerLevelKey.SemVer2).ToList();

            var packagesRepositoryMock = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            packagesRepositoryMock.Setup(m => m.GetAll()).Returns(AllPackages).Verifiable();
            PackagesRepository = packagesRepositoryMock.Object;
        }

        protected abstract TController CreateController(
            IEntityRepository<Package> packagesRepository,
            IGalleryConfigurationService configurationService,
            ISearchService searchService,
            ITelemetryService telemetryService);

        protected TController CreateTestableODataFeedController(HttpRequestMessage request)
        {
            var searchService = new Mock<ISearchService>().Object;
            var configurationService = new TestGalleryConfigurationService();
            configurationService.Current.SiteRoot = _siteRoot;
            var telemetryService = new Mock<ITelemetryService>();

            var controller = CreateController(
                PackagesRepository,
                configurationService,
                searchService,
                telemetryService.Object);

            AddRequestToController(request, controller);

            return controller;
        }

        protected static void AddRequestToController(HttpRequestMessage request, TController controller)
        {
            var httpRequest = new HttpRequest(string.Empty, request.RequestUri.AbsoluteUri, request.RequestUri.Query);
            var httpResponse = new HttpResponse(new StringWriter());
            var httpContext = new HttpContext(httpRequest, httpResponse);

            request.Properties.Add("MS_HttpContext", httpContext);
            controller.Request = request;

            HttpContext.Current = httpContext;

            controller.ControllerContext.Controller = controller;
            controller.ControllerContext.Configuration = new HttpConfiguration();
        }

        protected async Task<IReadOnlyCollection<TFeedPackage>> GetCollection<TFeedPackage>(
            Func<TController, ODataQueryOptions<TFeedPackage>, IHttpActionResult> controllerAction,
            string requestPath)
            where TFeedPackage : class
        {
            var queryResult = InvokeODataFeedControllerAction(controllerAction, requestPath);

            return await GetValueFromQueryResult(queryResult);
        }

        protected async Task<IReadOnlyCollection<TFeedPackage>> GetCollection<TFeedPackage>(
            Func<TController, ODataQueryOptions<TFeedPackage>, Task<IHttpActionResult>> asyncControllerAction,
            string requestPath)
            where TFeedPackage : class
        {
            var queryResult = await InvokeODataFeedControllerActionAsync(asyncControllerAction, requestPath);

            return await GetValueFromQueryResult(queryResult);
        }
        
        protected async Task<int> GetInt<TFeedPackage>(
            Func<TController, ODataQueryOptions<TFeedPackage>, IHttpActionResult> controllerAction,
            string requestPath) 
            where TFeedPackage : class
        {
            var queryResult = InvokeODataFeedControllerAction(controllerAction, requestPath);

            return int.Parse(await GetValueFromQueryResult(queryResult));
        }

        protected async Task<int> GetInt<TFeedPackage>(
            Func<TController, ODataQueryOptions<TFeedPackage>, Task<IHttpActionResult>> asyncControllerAction,
            string requestPath) 
            where TFeedPackage : class
        {
            var queryResult = await InvokeODataFeedControllerActionAsync(asyncControllerAction, requestPath);

            return int.Parse(await GetValueFromQueryResult(queryResult));
        }

        private static IQueryable<Package> CreatePackagesQueryable()
        {
            var packageRegistration = new PackageRegistration { Id = TestPackageId };

            var list = new List<Package>
            {
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "0.9.0.0",
                    NormalizedVersion = "0.9.0",
                    SemVerLevelKey = SemVerLevelKey.Unknown,
                    PackageStatusKey = PackageStatus.Deleted,
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "0.8.0.0",
                    NormalizedVersion = "0.8.0",
                    SemVerLevelKey = SemVerLevelKey.Unknown,
                    PackageStatusKey = PackageStatus.Validating,
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "0.7.0.0",
                    NormalizedVersion = "0.7.0",
                    SemVerLevelKey = SemVerLevelKey.Unknown,
                    PackageStatusKey = PackageStatus.FailedValidation,
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "1.0.0.0",
                    NormalizedVersion = "1.0.0",
                    SemVerLevelKey = SemVerLevelKey.Unknown
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "1.0.0.0-alpha",
                    NormalizedVersion = "1.0.0-alpha",
                    IsPrerelease = true,
                    SemVerLevelKey = SemVerLevelKey.Unknown
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.0",
                    NormalizedVersion = "2.0.0",
                    SemVerLevelKey = SemVerLevelKey.Unknown,
                    IsLatestStable = true
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.0-alpha",
                    NormalizedVersion = "2.0.0-alpha",
                    IsPrerelease = true,
                    SemVerLevelKey = SemVerLevelKey.Unknown,
                    IsLatest = true
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.0-alpha.1",
                    NormalizedVersion = "2.0.0-alpha.1",
                    IsPrerelease = true,
                    SemVerLevelKey = SemVerLevelKey.SemVer2
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.0+metadata",
                    NormalizedVersion = "2.0.0",
                    SemVerLevelKey = SemVerLevelKey.SemVer2,
                    IsLatestStableSemVer2 = true
                },
                new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "2.0.1-alpha.1",
                    NormalizedVersion = "2.0.1-alpha.1",
                    IsPrerelease = true,
                    SemVerLevelKey = SemVerLevelKey.SemVer2,
                    IsLatestSemVer2 = true
                }
            };

            return list.AsQueryable();
        }

        protected static ODataQueryContext CreateODataQueryContext<TFeedPackage>()
            where TFeedPackage : class
        {
            var oDataModelBuilder = new ODataConventionModelBuilder();
            oDataModelBuilder.EntitySet<TFeedPackage>("Packages");

            return new ODataQueryContext(oDataModelBuilder.GetEdmModel(), typeof(TFeedPackage));
        }

        private static async Task<dynamic> GetValueFromQueryResult<TEntity>(QueryResult<TEntity> queryResult)
        {
            var httpResponseMessage = await queryResult.ExecuteAsync(CancellationToken.None);

            if (queryResult.FormatAsCountResult)
            {
                var stringContent = (StringContent)httpResponseMessage.Content;
                return await stringContent.ReadAsStringAsync();
            }
            else if (queryResult.FormatAsSingleResult)
            {
                var objectContent = (ObjectContent<TEntity>)httpResponseMessage.Content;
                return (TEntity)objectContent.Value;
            }
            else
            {
                var objectContent = (ObjectContent<IQueryable<TEntity>>)httpResponseMessage.Content;
                return ((IQueryable<TEntity>)objectContent.Value).ToList();
            }
        }

        private async Task<QueryResult<TFeedPackage>> InvokeODataFeedControllerActionAsync<TFeedPackage>(
            Func<TController, ODataQueryOptions<TFeedPackage>, Task<IHttpActionResult>> asyncControllerAction,
            string requestPath)
            where TFeedPackage : class
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_siteRoot}{requestPath}");
            var controller = CreateTestableODataFeedController(request);

            return (QueryResult<TFeedPackage>)await asyncControllerAction(controller,
                new ODataQueryOptions<TFeedPackage>(CreateODataQueryContext<TFeedPackage>(), request));
        }

        private QueryResult<TFeedPackage> InvokeODataFeedControllerAction<TFeedPackage>(
            Func<TController, ODataQueryOptions<TFeedPackage>, IHttpActionResult> controllerAction,
            string requestPath)
            where TFeedPackage : class
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_siteRoot}{requestPath}");
            var controller = CreateTestableODataFeedController(request);

            return (QueryResult<TFeedPackage>)controllerAction(controller,
                new ODataQueryOptions<TFeedPackage>(CreateODataQueryContext<TFeedPackage>(), request));
        }
    }
}