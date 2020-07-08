// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class IndexOperationBuilder : IIndexOperationBuilder
    {
        /// <summary>
        /// Azure Search can only skip up to 100,000 documents.
        /// https://docs.microsoft.com/en-us/rest/api/searchservice/search-documents#skip-optional
        /// </summary>
        private const int MaximumSkip = 100000;

        private readonly ISearchTextBuilder _textBuilder;
        private readonly ISearchParametersBuilder _parametersBuilder;

        public IndexOperationBuilder(
            ISearchTextBuilder textBuilder,
            ISearchParametersBuilder parametersBuilder)
        {
            _textBuilder = textBuilder ?? throw new ArgumentNullException(nameof(textBuilder));
            _parametersBuilder = parametersBuilder ?? throw new ArgumentNullException(nameof(parametersBuilder));
        }

        public IndexOperation V3Search(V3SearchRequest request)
        {
            if (HasInvalidParameters(request, request.PackageType))
            {
                return IndexOperation.Empty();
            }

            var parsed = _textBuilder.ParseV3Search(request);

            IndexOperation indexOperation;
            if (request.PackageType == null
                && TryGetSearchDocumentByKey(request, parsed, out indexOperation))
            {
                return indexOperation;
            }

            var text = _textBuilder.Build(parsed);
            var parameters = _parametersBuilder.V3Search(request, IsEmptySearchQuery(text));
            return IndexOperation.Search(text, parameters);
        }

        public IndexOperation V2SearchWithSearchIndex(V2SearchRequest request)
        {
            if (HasInvalidParameters(request, request.PackageType))
            {
                return IndexOperation.Empty();
            }

            var parsed = _textBuilder.ParseV2Search(request);

            IndexOperation indexOperation;
            if (request.PackageType == null
                && TryGetSearchDocumentByKey(request, parsed, out indexOperation))
            {
                return indexOperation;
            }

            var text = _textBuilder.Build(parsed);
            var parameters = _parametersBuilder.V2Search(request, IsEmptySearchQuery(text));
            return IndexOperation.Search(text, parameters);
        }

        public IndexOperation V2SearchWithHijackIndex(V2SearchRequest request)
        {
            if (HasInvalidParameters(request, packageType: null))
            {
                return IndexOperation.Empty();
            }

            var parsed = _textBuilder.ParseV2Search(request);

            IndexOperation indexOperation;
            if (TryGetHijackDocumentByKey(request, parsed, out indexOperation))
            {
                return indexOperation;
            }

            var text = _textBuilder.Build(parsed);
            var parameters = _parametersBuilder.V2Search(request, IsEmptySearchQuery(text));
            return IndexOperation.Search(text, parameters);
        }

        public IndexOperation Autocomplete(AutocompleteRequest request)
        {
            if (HasInvalidParameters(request, request.PackageType))
            {
                return IndexOperation.Empty();
            }

            var text = _textBuilder.Autocomplete(request);
            var parameters = _parametersBuilder.Autocomplete(request, IsEmptySearchQuery(text));
            return IndexOperation.Search(text, parameters);
        }

        private bool TryGetSearchDocumentByKey(
            SearchRequest request,
            ParsedQuery parsed,
            out IndexOperation indexOperation)
        {
            if (PagedToFirstItem(request)
                && parsed.Grouping.Count == 1
                && TryGetSinglePackageId(parsed, out var packageId))
            {
                var searchFilters = _parametersBuilder.GetSearchFilters(request);
                var documentKey = DocumentUtilities.GetSearchDocumentKey(packageId, searchFilters);

                indexOperation = IndexOperation.Get(documentKey);
                return true;
            }

            indexOperation = null;
            return false;
        }

        private bool TryGetHijackDocumentByKey(
            SearchRequest request,
            ParsedQuery parsed,
            out IndexOperation indexOperation)
        {
            if (PagedToFirstItem(request)
                && parsed.Grouping.Count == 2
                && TryGetSinglePackageId(parsed, out var packageId)
                && TryGetSingleVersion(parsed, out var normalizedVersion))
            {
                var documentKey = DocumentUtilities.GetHijackDocumentKey(packageId, normalizedVersion);

                indexOperation = IndexOperation.Get(documentKey);
                return true;
            }

            indexOperation = null;
            return false;
        }

        private bool TryGetSinglePackageId(
            ParsedQuery parsed,
            out string packageId)
        {
            if (parsed.Grouping.TryGetValue(QueryField.PackageId, out var terms)
                && terms.Count == 1)
            {
                packageId = terms.First();
                if (packageId.Length <= PackageIdValidator.MaxPackageIdLength
                    && PackageIdValidator.IsValidPackageId(packageId))
                {
                    return true;
                }
            }

            packageId = null;
            return false;
        }

        private bool TryGetSingleVersion(
            ParsedQuery parsed,
            out string normalizedVersion)
        {
            if (parsed.Grouping.TryGetValue(QueryField.Version, out var terms)
                && terms.Count == 1)
            {
                if (NuGetVersion.TryParse(terms.First(), out var parsedVersion))
                {
                    normalizedVersion = parsedVersion.ToNormalizedString();
                    return true;
                }
            }

            normalizedVersion = null;
            return false;
        }

        private static bool HasInvalidParameters(SearchRequest request, string packageType)
        {
            // Requests with bad parameters yield no results. For the package type case, by specification a package type
            // valid characters are the same as a package ID.
            return request.Skip > MaximumSkip
                || (packageType != null && !PackageIdValidator.IsValidPackageId(packageType));
        }

        private static bool PagedToFirstItem(SearchRequest request)
        {
            return request.Skip <= 0 && request.Take >= 1;
        }

        private static bool IsEmptySearchQuery(string parsedText)
        {
            return parsedText.Equals(SearchTextBuilder.MatchAllDocumentsQuery);
        }
    }
}
