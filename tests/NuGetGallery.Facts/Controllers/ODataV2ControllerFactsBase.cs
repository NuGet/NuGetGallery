// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Configuration;
using NuGetGallery.OData;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData.Query;

namespace NuGetGallery.Controllers
{
    public abstract class ODataV2ControllerFactsBase
        : ODataFeedControllerFactsBase<ODataV2FeedController>
    {
        protected override ODataV2FeedController CreateController(
            IEntityRepository<Package> packagesRepository,
            IGalleryConfigurationService configurationService, 
            ISearchService searchService)
        {
            return new ODataV2FeedController(packagesRepository, configurationService, searchService);
        }

        protected async Task<IReadOnlyCollection<V2FeedPackage>> GetCollection(
            Func<ODataV2FeedController, ODataQueryOptions<V2FeedPackage>, IHttpActionResult> controllerAction,
            string requestPath)
        {
            var queryResult = InvokeODataFeedControllerAction(controllerAction, requestPath);

            return await GetValueFromQueryResult(queryResult);
        }

        protected async Task<int> GetInt(
            Func<ODataV2FeedController, ODataQueryOptions<V2FeedPackage>, IHttpActionResult> controllerAction,
            string requestPath)
        {
            var queryResult = InvokeODataFeedControllerAction(controllerAction, requestPath);

            return int.Parse(await GetValueFromQueryResult(queryResult));
        }

        protected async Task<IReadOnlyCollection<V2FeedPackage>> GetCollection(
            Func<ODataV2FeedController, ODataQueryOptions<V2FeedPackage>, Task<IHttpActionResult>> asyncControllerAction,
            string requestPath)
        {
            var queryResult = await InvokeODataFeedControllerActionAsync(asyncControllerAction, requestPath);

            return await GetValueFromQueryResult(queryResult);
        }

        protected async Task<int> GetInt(
            Func<ODataV2FeedController, ODataQueryOptions<V2FeedPackage>, Task<IHttpActionResult>> asyncControllerAction,
            string requestPath)
        {
            var queryResult = await InvokeODataFeedControllerActionAsync(asyncControllerAction, requestPath);

            return int.Parse(await GetValueFromQueryResult(queryResult));
        }
    }
}