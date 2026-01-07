// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class SearchServiceFactory : ISearchServiceFactory
    {
        private readonly ISearchService _searchService;
        private readonly ISearchService _previewSearchService;

        public SearchServiceFactory(
            ISearchService searchService,
            ISearchService previewSearchService)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _previewSearchService = previewSearchService ?? throw new ArgumentNullException(nameof(previewSearchService));
        }

        public ISearchService GetService() => _searchService;
        public ISearchService GetPreviewService() => _previewSearchService;
    }
}