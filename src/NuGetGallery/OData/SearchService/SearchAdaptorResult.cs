// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery.OData
{
    public class SearchAdaptorResult
    {
        public SearchAdaptorResult(bool resultsAreProvidedBySearchService, IQueryable<Package> packages)
        {
            ResultsAreProvidedBySearchService = resultsAreProvidedBySearchService;
            Packages = packages;
        }

        public bool ResultsAreProvidedBySearchService { get; private set; }
        public IQueryable<Package> Packages { get; private set; }
    }
}