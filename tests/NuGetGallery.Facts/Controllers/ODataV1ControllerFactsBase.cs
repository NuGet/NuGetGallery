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
using System.Web.Http.OData;
using System.Web.Http.OData.Builder;
using Moq;
using NuGetGallery.Framework;
using NuGetGallery.OData;
using System.Web.Http;
using System.Web.Http.OData.Query;
using NuGetGallery.WebApi;

namespace NuGetGallery.Controllers
{
    public abstract class ODataV1ControllerFactsBase
    {
        protected const string SiteRoot = "https://nuget.localtest.me";

        protected ODataV1FeedController CreateTestableODataV1FeedController(IEntityRepository<Package> packagesRepository, HttpRequestMessage request)
        {
            var searchService = new Mock<ISearchService>().Object;
            var configurationService = new TestGalleryConfigurationService();
            configurationService.Current.SiteRoot = SiteRoot;

            var controller = new ODataV1FeedController(packagesRepository, configurationService, searchService);

            var httpRequest = new HttpRequest(String.Empty, request.RequestUri.AbsoluteUri, request.RequestUri.Query);
            var httpResponse = new HttpResponse(new StringWriter());
            var httpContext = new HttpContext(httpRequest, httpResponse);

            request.Properties.Add("MS_HttpContext", httpContext);
            controller.Request = request;

            HttpContext.Current = httpContext;

            controller.ControllerContext.Controller = controller;
            controller.ControllerContext.Configuration = new HttpConfiguration();

            return controller;
        }

        protected static ODataQueryContext CreateODataQueryContext()
        {
            var oDataModelBuilder = new ODataConventionModelBuilder();
            oDataModelBuilder.EntitySet<V1FeedPackage>("Packages");

            return new ODataQueryContext(oDataModelBuilder.GetEdmModel(), typeof(V1FeedPackage));
        }

        protected async Task<IReadOnlyCollection<T>> GetODataV1FeedCollection<T>(
            Func<ODataV1FeedController, ODataQueryOptions<T>, IHttpActionResult> controllerAction,
            string requestPath,
            IEntityRepository<Package> packagesRepository)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{SiteRoot}{requestPath}");
            var controller = CreateTestableODataV1FeedController(packagesRepository, request);

            var queryResult = ExecuteODataV1FeedControllerAction(
                controllerAction,
                request,
                controller);

            return await GetValueFromQueryResult(queryResult);
        }

        protected async Task<int> GetODataV1FeedCount<T>(
            Func<ODataV1FeedController, ODataQueryOptions<T>, IHttpActionResult> controllerAction,
            string requestPath,
            IEntityRepository<Package> packagesRepository)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{SiteRoot}{requestPath}");
            var controller = CreateTestableODataV1FeedController(packagesRepository, request);

            var queryResult = ExecuteODataV1FeedControllerAction(
                controllerAction,
                request,
                controller);

            return int.Parse(await GetValueFromQueryResult(queryResult));
        }

        protected async Task<IReadOnlyCollection<T>> GetODataV1FeedCollectionAsync<T>(
            Func<ODataV1FeedController, ODataQueryOptions<T>, Task<IHttpActionResult>> asyncControllerAction,
            string requestPath,
            IEntityRepository<Package> packagesRepository)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{SiteRoot}{requestPath}");
            var controller = CreateTestableODataV1FeedController(packagesRepository, request);

            var queryResult = await ExecuteODataV1FeedControllerActionAsync(
                asyncControllerAction,
                request,
                controller);

            return await GetValueFromQueryResult(queryResult);
        }

        protected async Task<int> GetODataV1FeedCountAsync<T>(
            Func<ODataV1FeedController, ODataQueryOptions<T>, Task<IHttpActionResult>> asyncControllerAction,
            string requestPath,
            IEntityRepository<Package> packagesRepository)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{SiteRoot}{requestPath}");
            var controller = CreateTestableODataV1FeedController(packagesRepository, request);

            var queryResult = await ExecuteODataV1FeedControllerActionAsync(
                asyncControllerAction,
                request,
                controller);

            return int.Parse(await GetValueFromQueryResult(queryResult));
        }

        private static QueryResult<T> ExecuteODataV1FeedControllerAction<T>(
            Func<ODataV1FeedController, ODataQueryOptions<T>, IHttpActionResult> controllerAction,
            HttpRequestMessage request,
            ODataV1FeedController controller)
        {
            return (QueryResult<T>)controllerAction(controller,
                new ODataQueryOptions<T>(CreateODataQueryContext(), request));
        }

        private static async Task<QueryResult<T>> ExecuteODataV1FeedControllerActionAsync<T>(
            Func<ODataV1FeedController, ODataQueryOptions<T>, Task<IHttpActionResult>> asyncControllerAction,
            HttpRequestMessage request,
            ODataV1FeedController controller)
        {
            return (QueryResult<T>)await asyncControllerAction(controller,
                new ODataQueryOptions<T>(CreateODataQueryContext(), request));
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
    }
}