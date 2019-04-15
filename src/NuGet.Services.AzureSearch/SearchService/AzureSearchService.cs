// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AzureSearchService : ISearchService
    {
        private readonly ISearchTextBuilder _textBuilder;
        private readonly ISearchParametersBuilder _parametersBuilder;
        private readonly ISearchIndexClientWrapper _searchIndex;
        private readonly ISearchIndexClientWrapper _hijackIndex;
        private readonly ISearchResponseBuilder _responseBuilder;

        public AzureSearchService(
            ISearchTextBuilder textBuilder,
            ISearchParametersBuilder parametersBuilder,
            ISearchIndexClientWrapper searchIndex,
            ISearchIndexClientWrapper hijackIndex,
            ISearchResponseBuilder responseBuilder)
        {
            _textBuilder = textBuilder ?? throw new ArgumentNullException(nameof(textBuilder));
            _parametersBuilder = parametersBuilder ?? throw new ArgumentNullException(nameof(parametersBuilder));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _hijackIndex = hijackIndex ?? throw new ArgumentNullException(nameof(hijackIndex));
            _responseBuilder = responseBuilder ?? throw new ArgumentNullException(nameof(responseBuilder));
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
            var text = _textBuilder.V3Search(request);
            var parameters = _parametersBuilder.V3Search(request);

            var result = await Measure.DurationWithValueAsync(() => _searchIndex.Documents.SearchAsync<SearchDocument.Full>(
                text,
                parameters));

            return _responseBuilder.V3FromSearch(
                request,
                parameters,
                text,
                result.Value,
                result.Duration);
        }

        public async Task<AutocompleteResponse> AutocompleteAsync(AutocompleteRequest request)
        {
            var text = _textBuilder.Autocomplete(request);
            var parameters = _parametersBuilder.Autocomplete(request);

            var result = await Measure.DurationWithValueAsync(() => _searchIndex.Documents.SearchAsync<SearchDocument.Full>(
                text,
                parameters));

            return _responseBuilder.AutocompleteFromSearch(
                request,
                parameters,
                text,
                result.Value,
                result.Duration);
        }

        private async Task<V2SearchResponse> UseHijackIndexAsync(V2SearchRequest request)
        {
            var text = _textBuilder.V2Search(request);
            var parameters = _parametersBuilder.V2Search(request);

            var result = await Measure.DurationWithValueAsync(() => _hijackIndex.Documents.SearchAsync<HijackDocument.Full>(
                text,
                parameters));

            return _responseBuilder.V2FromHijack(
                request,
                parameters,
                text,
                result.Value,
                result.Duration);
        }

        private async Task<V2SearchResponse> UseSearchIndexAsync(V2SearchRequest request)
        {
            var text = _textBuilder.V2Search(request);
            var parameters = _parametersBuilder.V2Search(request);

            var result = await Measure.DurationWithValueAsync(() => _searchIndex.Documents.SearchAsync<SearchDocument.Full>(
                text,
                parameters));

            return _responseBuilder.V2FromSearch(
                request,
                parameters,
                text,
                result.Value,
                result.Duration);
        }
    }
}
