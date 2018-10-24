// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGetGallery.Configuration;
using NuGetGallery.WebApi;

namespace NuGetGallery.OData
{
    public abstract class NuGetODataController 
        : ODataController
    {
        private readonly IGalleryConfigurationService _configurationService;
        protected readonly ITelemetryService _telemetryService;

        protected const string Id = "Id";
        protected const string Version = "Version";

        protected NuGetODataController(
            IGalleryConfigurationService configurationService,
            ITelemetryService telemetryService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        protected virtual HttpContextBase GetTraditionalHttpContext()
        {
            object context;
            if (Request.Properties.TryGetValue("MS_HttpContext", out context))
            {
                var httpContext = context as HttpContext;
                if (httpContext != null)
                {
                    return new HttpContextWrapper(httpContext);
                }

                var httpContextBase = context as HttpContextBase;
                return httpContextBase;
            }

            return null;
        }

        protected virtual bool UseHttps()
        {
            return Request.RequestUri.Scheme == "https";
        }

        protected virtual string GetSiteRoot()
        {
            return _configurationService.GetSiteRoot(UseHttps()).TrimEnd('/') + '/';
        }

        /// <summary>
        /// Generates a QueryResult.
        /// </summary>
        /// <typeparam name="TModel">Model type.</typeparam>
        /// <param name="options">OData query options.</param>
        /// <param name="queryable">Queryable to build QueryResult from.</param>
        /// <param name="maxPageSize">Maximum page size.</param>
        /// <returns>A QueryResult instance.</returns>
        protected virtual IHttpActionResult TrackedQueryResult<TModel>(
            ODataQueryOptions<TModel> options,
            IQueryable<TModel> queryable,
            int maxPageSize,
            bool? customQuery)
        {
            _telemetryService.TrackODataCustomQuery(customQuery);

            return new QueryResult<TModel>(options, queryable, this, maxPageSize, customQuery);
        }

        /// <summary>
        /// Generates a QueryResult that is already paged. For example if an upstream service returns only one page of results, this overload will ensure only that page is returned.
        /// </summary>
        /// <remarks>The next link has to be generated manually.</remarks>
        /// <typeparam name="TModel">Model type.</typeparam>
        /// <param name="options">OData query options.</param>
        /// <param name="queryable">Queryable to build QueryResult from.</param>
        /// <param name="maxPageSize">Maximum page size.</param>
        /// <param name="totalResults">The total number of results. This number can be larger than the size of the page being served.</param>
        /// <param name="generateNextLink">Function that generates a next link.</param>
        /// <returns>A QueryResult instance.</returns>
        protected virtual IHttpActionResult TrackedQueryResult<TModel>(
            ODataQueryOptions<TModel> options,
            IQueryable<TModel> queryable,
            int maxPageSize,
            long totalResults,
            Func<ODataQueryOptions<TModel>, ODataQuerySettings, long?, Uri> generateNextLink,
            bool? customQuery)
        {
            _telemetryService.TrackODataCustomQuery(customQuery);

            return new QueryResult<TModel>(options, queryable, this, maxPageSize, totalResults, generateNextLink, customQuery);
        }

        /// <summary>
        /// The OrderById will be skipped when the request has an OrderBy clause that does not include Id 
        /// </summary>
        /// <param name="options">The request OData options.</param>
        /// <returns></returns>
        protected bool ShouldIgnoreOrderById<T>(ODataQueryOptions<T> options)
        {
            return options.OrderBy != null && 
                   !options.OrderBy.OrderByNodes.Any((node) =>
                                    {
                                        string nodeName = ((OrderByPropertyNode)node).Property.Name;
                                        return string.Equals(nodeName, Id, StringComparison.Ordinal);
                                    });
        }
    }
}