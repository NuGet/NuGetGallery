// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using System.Web.Http.Results;
using Microsoft.Data.OData;

namespace NuGetGallery.WebApi
{
    public class QueryResult<TModel>
        : IHttpActionResult
    {
        private readonly ODataQueryOptions<TModel> _queryOptions;
        private readonly IQueryable<TModel> _queryable;
        private readonly System.Web.Http.ApiController _controller;
        private readonly long? _totalResults;
        private readonly Func<ODataQueryOptions<TModel>, ODataQuerySettings, Uri> _generateNextLink;
        private readonly bool _isPagedResult;

        private readonly ODataValidationSettings _validationSettings;
        private readonly ODataQuerySettings _querySettings;

        public bool FormatAsCountResult { get; set; }
        public bool FormatAsSingleResult { get; set; }

        public QueryResult(ODataQueryOptions<TModel> queryOptions, IQueryable<TModel> queryable, System.Web.Http.ApiController controller, int maxPageSize)
            : this(queryOptions, queryable, controller, maxPageSize, null, null)
        {
        }

        public QueryResult(
            ODataQueryOptions<TModel> queryOptions, IQueryable<TModel> queryable, System.Web.Http.ApiController controller, int maxPageSize,
                long? totalResults, Func<ODataQueryOptions<TModel>, ODataQuerySettings, Uri> generateNextLink)
        {
            _queryOptions = queryOptions;
            _queryable = queryable;
            _controller = controller;
            _totalResults = totalResults;
            _generateNextLink = generateNextLink;

            if (_totalResults.HasValue && generateNextLink != null)
            {
                _isPagedResult = true;
            }

            _validationSettings = new ODataValidationSettings()
            {
                // disable projections
                AllowedQueryOptions = AllowedQueryOptions.All & ~AllowedQueryOptions.Select
            };

            _querySettings = new ODataQuerySettings()
            {
                PageSize = maxPageSize,
                HandleNullPropagation = HandleNullPropagationOption.False,
                EnsureStableOrdering = true
            };
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                return GetInnerResult().ExecuteAsync(cancellationToken);
            }
            catch (ODataException e)
            {
                var response = _controller.Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    string.Format(CultureInfo.InvariantCulture, "URI or query string invalid. {0}", e.Message),
                    e);

                return Task.FromResult(response);
            }
        }

        public IHttpActionResult GetInnerResult()
        {
            // If we're counting, don't set the max page size
            if (FormatAsCountResult)
            {
                _querySettings.PageSize = null;
            }

            // Query options
            var queryOptions = _queryOptions;

            // If the result is already paged, it has already been queried as well.
            // Remove options that would further limit the resultset.
            if (_isPagedResult && _generateNextLink != null && _totalResults.HasValue)
            {
                queryOptions = null;
            }

            // Apply the query
            var queryResult = ExecuteQuery(_queryable, queryOptions);
            if (queryResult == null)
            {
                // No results
                return NotFoundResult();
            }
            else if (FormatAsSingleResult)
            {
                // Single result
                if (FormatAsCountResult)
                {
                    // Handle $count
                    return CountResult(1);
                }
                else
                {
                    // Handle result that is already paged
                    return NegotiatedContentResult(queryResult.First());
                }
            }
            else
            {
                // Collection result
                if (_isPagedResult && _generateNextLink != null && _totalResults.HasValue)
                {
                    // Handle result that is already paged
                    if (FormatAsCountResult)
                    {
                        // Handle $count
                        return CountResult(_totalResults.Value);
                    }
                    else
                    {
                        // Handle result that is already paged
                        return NegotiatedContentResult(
                            new PageResult<TModel>(queryResult, _generateNextLink(_queryOptions, _querySettings), _totalResults));
                    }
                }
                else
                {
                    // Handle regular result
                    if (FormatAsCountResult)
                    {
                        // Handle $count
                        return CountResult(queryResult.Count());
                    }
                    else
                    {
                        // Handle regular queryable
                        return NegotiatedContentResult(queryResult);
                    }
                }
            }
        }

        private void ValidateQuery(ODataQueryOptions<TModel> queryOptions)
        {
            var queryParameters = _controller.Request.GetQueryNameValuePairs();
            foreach (var kvp in queryParameters)
            {
                if (!ODataQueryOptions.IsSystemQueryOption(kvp.Key) &&
                     kvp.Key.StartsWith("$", StringComparison.Ordinal))
                {
                    // We don't support any custom query options that start with $
                    var response = _controller.Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        string.Format(CultureInfo.InvariantCulture, "Query parameter {0} is not supported.", kvp.Key));

                    throw new HttpResponseException(response);
                }
            }

            queryOptions.Validate(_validationSettings);
        }

        private IQueryable<TModel> ExecuteQuery(IQueryable<TModel> queryable, ODataQueryOptions<TModel> queryOptions)
        {
            if (queryOptions == null)
            {
                return queryable;
            }

            ValidateQuery(queryOptions);
            return (IQueryable<TModel>)queryOptions.ApplyTo(queryable, _querySettings);
        }

        private NotFoundResult NotFoundResult()
        {
            return new NotFoundResult(_controller.Request);
        }

        private PlainTextResult CountResult(long count)
        {
            return new PlainTextResult(count.ToString(CultureInfo.InvariantCulture), _controller.Request);
        }

        private OkNegotiatedContentResult<TContentModel> NegotiatedContentResult<TContentModel>(TContentModel content)
        {
            return new OkNegotiatedContentResult<TContentModel>(content, _controller);
        }
    }
}