// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AzureSearchService : ISearchService
    {
        private readonly IIndexOperationBuilder _operationBuilder;
        private readonly ISearchIndexClientWrapper _searchIndex;
        private readonly ISearchIndexClientWrapper _hijackIndex;
        private readonly ISearchResponseBuilder _responseBuilder;
        private readonly IAzureSearchTelemetryService _telemetryService;

        public AzureSearchService(
            IIndexOperationBuilder operationBuilder,
            ISearchIndexClientWrapper searchIndex,
            ISearchIndexClientWrapper hijackIndex,
            ISearchResponseBuilder responseBuilder,
            IAzureSearchTelemetryService telemetryService)
        {
            _operationBuilder = operationBuilder ?? throw new ArgumentNullException(nameof(operationBuilder));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _hijackIndex = hijackIndex ?? throw new ArgumentNullException(nameof(hijackIndex));
            _responseBuilder = responseBuilder ?? throw new ArgumentNullException(nameof(responseBuilder));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public async Task<V2SearchResponse> V2SearchAsync(V2SearchRequest request)
        {
            if (request.IgnoreFilter)
            {
                return await UseHijackIndexAsync(request);
            }
            else
            {
                return await UseSearchIndexAsync(request);
            }
        }

        public async Task<V3SearchResponse> V3SearchAsync(V3SearchRequest request)
        {
            var operation = _operationBuilder.V3Search(request);

            V3SearchResponse output;
            switch (operation.Type)
            {
                case IndexOperationType.Get:
                    var documentResult = await Measure.DurationWithValueAsync(
                        () => _searchIndex.Documents.GetOrNullAsync<SearchDocument.Full>(operation.DocumentKey));

                    output = _responseBuilder.V3FromSearchDocument(
                        request,
                        operation.DocumentKey,
                        documentResult.Value,
                        documentResult.Duration);

                    _telemetryService.TrackV3GetDocument(documentResult.Duration);
                    break;

                case IndexOperationType.Search:
                    var result = await Measure.DurationWithValueAsync(() => _searchIndex.Documents.SearchAsync<SearchDocument.Full>(
                        operation.SearchText,
                        operation.SearchParameters));

                    output = _responseBuilder.V3FromSearch(
                        request,
                        operation.SearchText,
                        operation.SearchParameters,
                        result.Value,
                        result.Duration);

                    _telemetryService.TrackV3SearchQuery(result.Duration);
                    break;

                default:
                    throw UnsupportedOperation(operation);
            }

            return output;
        }

        public async Task<AutocompleteResponse> AutocompleteAsync(AutocompleteRequest request)
        {
            var operation = _operationBuilder.Autocomplete(request);

            AutocompleteResponse output;
            switch (operation.Type)
            {
                case IndexOperationType.Search:
                    var result = await Measure.DurationWithValueAsync(() => _searchIndex.Documents.SearchAsync<SearchDocument.Full>(
                        operation.SearchText,
                        operation.SearchParameters));

                    output = _responseBuilder.AutocompleteFromSearch(
                        request,
                        operation.SearchText,
                        operation.SearchParameters,
                        result.Value,
                        result.Duration);

                    _telemetryService.TrackAutocompleteQuery(result.Duration);
                    break;

                default:
                    throw UnsupportedOperation(operation);
            }

            return output;
        }

        private async Task<V2SearchResponse> UseHijackIndexAsync(V2SearchRequest request)
        {
            var operation = _operationBuilder.V2SearchWithHijackIndex(request);

            V2SearchResponse output;
            switch (operation.Type)
            {
                case IndexOperationType.Search:
                    var result = await Measure.DurationWithValueAsync(() => _hijackIndex.Documents.SearchAsync<HijackDocument.Full>(
                        operation.SearchText,
                        operation.SearchParameters));

                    output = _responseBuilder.V2FromHijack(
                        request,
                        operation.SearchText,
                        operation.SearchParameters,
                        result.Value,
                        result.Duration);

                    _telemetryService.TrackV2SearchQueryWithHijackIndex(result.Duration);
                    break;

                default:
                    throw UnsupportedOperation(operation);
            }

            return output;
        }

        private async Task<V2SearchResponse> UseSearchIndexAsync(V2SearchRequest request)
        {
            var operation = _operationBuilder.V2SearchWithSearchIndex(request);

            V2SearchResponse output;
            switch (operation.Type)
            {
                case IndexOperationType.Search:
                    var result = await Measure.DurationWithValueAsync(() => _searchIndex.Documents.SearchAsync<SearchDocument.Full>(
                        operation.SearchText,
                        operation.SearchParameters));

                    output = _responseBuilder.V2FromSearch(
                        request,
                        operation.SearchText,
                        operation.SearchParameters,
                        result.Value,
                        result.Duration);

                    _telemetryService.TrackV2SearchQueryWithSearchIndex(result.Duration);
                    break;

                default:
                    throw UnsupportedOperation(operation);
            }

            return output;
        }

        private static NotImplementedException UnsupportedOperation(IndexOperation operation)
        {
            return new NotImplementedException($"The operation type {operation.Type} is not supported.");
        }
    }
}
