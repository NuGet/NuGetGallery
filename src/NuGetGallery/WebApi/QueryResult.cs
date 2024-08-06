// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using System.Web.Http.Results;
using Microsoft.Data.OData;
using Microsoft.Data.OData.Query;
using NuGetGallery.OData.QueryFilter;

namespace NuGetGallery.WebApi
{

    public class QueryResult<TModel>
        : IHttpActionResult
    {
        private readonly ODataQueryOptions<TModel> _queryOptions;
        private readonly IQueryable<TModel> _queryable;
        private readonly System.Web.Http.ApiController _controller;
        private readonly long? _totalResults;
        private readonly Func<ODataQueryOptions<TModel>, ODataQuerySettings, long?, Uri> _generateNextLink;
        private readonly bool? _customQuery;
        private readonly bool _isPagedResult;
        private readonly int? _semVerLevelKey;

        private readonly ODataValidationSettings _validationSettings;
        private readonly ODataQuerySettings _querySettings;

        public bool FormatAsCountResult { get; set; }
        public bool FormatAsSingleResult { get; set; }

        public QueryResult(
            ODataQueryOptions<TModel> queryOptions,
            IQueryable<TModel> queryable,
            System.Web.Http.ApiController controller,
            int maxPageSize,
            bool? customQuery)
            : this(queryOptions, queryable, controller, maxPageSize, null, null, customQuery)
        {
        }

        public QueryResult(
            ODataQueryOptions<TModel> queryOptions,
            IQueryable<TModel> queryable,
            System.Web.Http.ApiController controller,
            int maxPageSize,
            long? totalResults,
            Func<ODataQueryOptions<TModel>, ODataQuerySettings, long?, Uri> generateNextLink,
            bool? customQuery)
        {
            _queryOptions = queryOptions;
            _queryable = queryable;
            _controller = controller;
            _totalResults = totalResults;
            _generateNextLink = generateNextLink;
            _customQuery = customQuery;

            var queryDictionary = HttpUtility.ParseQueryString(queryOptions.Request.RequestUri.Query);
            _semVerLevelKey = SemVerLevelKey.ForSemVerLevel(queryDictionary["semVerLevel"]);

            if (_totalResults.HasValue && generateNextLink != null)
            {
                _isPagedResult = true;
            }

            // todo: if we decide to no longer support projections
            //AllowedQueryOptions = AllowedQueryOptions.All & ~AllowedQugeteryOptions.Select
            _validationSettings = new ODataValidationSettings()
            {
                MaxNodeCount = 250
            };

            _querySettings = new ODataQuerySettings(QueryResultDefaults.DefaultQuerySettings)
            {
                PageSize = maxPageSize
            };
        }

        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                response = await GetInnerResult().ExecuteAsync(cancellationToken);
            }
            catch (ODataException e)
            {
                response = _controller.Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    string.Format(CultureInfo.InvariantCulture, "URI or query string invalid. {0}", e.Message),
                    e);
            }
            catch (Exception e)
            {
                QuietLog.LogHandledException(e);
                throw;
            }

            if (_customQuery.HasValue)
            {
                response.Headers.Add(GalleryConstants.CustomQueryHeaderName, _customQuery.Value ? "true" : "false");
            }

            return response;
        }

        public IHttpActionResult GetInnerResult()
        {
            IQueryable queryResults = null;

            // If we're counting, don't set the max page size
            if (FormatAsCountResult)
            {
                _querySettings.PageSize = null;
            }

            // Query options
            var queryOptions = _queryOptions;

            // If the result is already paged, it has already been queried as well.
            // Make sure we only apply the options that are allowed at this stage.
            if (_isPagedResult && _generateNextLink != null && _totalResults.HasValue)
            {
                queryResults = _queryable.ToList().AsQueryable();

                if (queryOptions.Filter != null)
                {
                    if (_semVerLevelKey != SemVerLevelKey.Unknown
                        && (string.Equals(queryOptions.Filter.RawValue, ODataQueryFilter.IsLatestVersion, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(queryOptions.Filter.RawValue, ODataQueryFilter.IsAbsoluteLatestVersion, StringComparison.OrdinalIgnoreCase)))
                    {
                        // The client uses IsLatestVersion and IsAbsoluteLatestVersion by default,
                        // and just appends semVerLevel=2.0.0 to the query string.
                        // When semVerLevel=2.0.0, we should not restrict the filter to only return IsLatest(Stable)=true,
                        // but also include IsLatest(Stable)SemVer2=true. These additional properties are not exposed on the OData entities however.
                        // As the proper filtering already should 've happened earlier in the pipeline (SQL or search service),
                        // the OData filter is redundant, so all we need to do here is to avoid 
                        // the OData filter to be applied on an already correctly filtered result set.
                    }
                    else
                    {
                        queryResults = queryOptions.Filter.ApplyTo(queryResults, _querySettings);
                    }
                }

                if (queryOptions.OrderBy != null
                    && !(
                        // Only ordering by properties is supported for non-primitive collections.
                        // Expressions are not supported.
                        queryOptions.OrderBy.OrderByClause.ItemType.Definition.TypeKind != Microsoft.Data.Edm.EdmTypeKind.Primitive
                        && queryOptions.OrderBy.OrderByClause.Expression.Kind != QueryNodeKind.None
                        )
                    )
                {
                    queryResults = queryOptions.OrderBy.ApplyTo(queryResults, _querySettings);
                }

                if (queryOptions.Top != null)
                {
                    queryResults = queryOptions.Top.ApplyTo(queryResults, _querySettings);
                }

                // Remove options that would further limit the resultset.
                queryOptions = null;
            }
            else
            {
                // Apply the query as-is
                queryResults = ExecuteQuery(_queryable, queryOptions);
            }

            // Determine the resulting query
            var modelQueryResults = queryResults as IQueryable<TModel>; // cast succeeds if querying on model
            var projectedQueryResults = queryResults as IQueryable<IEdmEntityObject>; // cast succeeds in case of projection
            if (queryResults == null)
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
                    var count = 0;
                    if (modelQueryResults != null)
                    {
                        count = Math.Min(1, modelQueryResults.Count());
                    }
                    else if (projectedQueryResults != null)
                    {
                        count = Math.Min(1, projectedQueryResults.Count());
                    }

                    return CountResult(count);
                }
                else
                {
                    // Handle single result
                    if (modelQueryResults != null)
                    {
                        var model = modelQueryResults.FirstOrDefault();
                        if (model == null)
                        {
                            return NotFoundResult();
                        }

                        return NegotiatedContentResult(model);
                    }
                    else if (projectedQueryResults != null)
                    {
                        var model = projectedQueryResults.AsEnumerable().FirstOrDefault();
                        if (model == null)
                        {
                            return NotFoundResult();
                        }

                        return NegotiatedContentResult(model);
                    }
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
                        if (modelQueryResults != null)
                        {
                            return NegotiatedContentResult(
                                new PageResult<TModel>(modelQueryResults, _generateNextLink(_queryOptions, _querySettings, _totalResults), _totalResults));
                        }
                        else if (projectedQueryResults != null)
                        {
                            return NegotiatedContentResult(
                                new PageResult<IEdmEntityObject>(projectedQueryResults, _generateNextLink(_queryOptions, _querySettings, _totalResults), _totalResults));
                        }
                    }
                }
                else
                {
                    // Handle regular result
                    if (FormatAsCountResult)
                    {
                        // Handle $count
                        if (modelQueryResults != null)
                        {
                            return CountResult(modelQueryResults.Count());
                        }
                        else if (projectedQueryResults != null)
                        {
                            return CountResult(projectedQueryResults.AsEnumerable().Count());
                        }
                    }
                    else
                    {
                        // Handle regular queryable
                        if (modelQueryResults != null)
                        {
                            return NegotiatedContentResult(modelQueryResults);
                        }
                        else if (projectedQueryResults != null)
                        {
                            // For projections we have to craft the result with a proper type hint
                            // to make the OData XML serializers like the collection result.
                            var elementType = projectedQueryResults.GetType().GenericTypeArguments.FirstOrDefault();
                            if (elementType != null)
                            {
                                return ProjectedNegotiatedContentResult(projectedQueryResults, elementType);
                            }
                            return NegotiatedContentResult(projectedQueryResults);
                        }
                    }
                }
            }

            return BadRequest("Could not execute OData query. Executing the query returned neither a strong-typed model result nor a projection result.");
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

        private IQueryable ExecuteQuery(IQueryable<TModel> queryable, ODataQueryOptions<TModel> queryOptions)
        {
            if (queryOptions == null)
            {
                return queryable;
            }

            ValidateQuery(queryOptions);

            var queryResult = queryOptions.ApplyTo(queryable, _querySettings);

            var projection = queryResult as IQueryable<IEdmEntityObject>;
            if (projection != null)
            {
                return projection;
            }

            return queryResult;
        }

        private BadRequestErrorMessageResult BadRequest(string message)
        {
            return new BadRequestErrorMessageResult(message, _controller);
        }

        private NotFoundResult NotFoundResult()
        {
            return new NotFoundResult(_controller.Request);
        }

        private PlainTextResult CountResult(long count)
        {
            return new PlainTextResult(count.ToString(CultureInfo.InvariantCulture), _controller.Request);
        }

        private OkNegotiatedContentResult<TResponseModel> NegotiatedContentResult<TResponseModel>(TResponseModel content)
        {
            return new OkNegotiatedContentResult<TResponseModel>(content, _controller);
        }

        private IHttpActionResult ProjectedNegotiatedContentResult<TResponseModel>(TResponseModel content, Type projectedType)
        {
            var resultType = typeof(OkNegotiatedContentResult<>)
                .MakeGenericType(typeof(IQueryable<>)
                .MakeGenericType(projectedType));

            return Activator.CreateInstance(resultType, content, _controller) as IHttpActionResult;
        }
    }
}