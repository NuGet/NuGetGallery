// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch;

namespace NuGet.Services.SearchService.Controllers
{
    public class SearchController : ApiController
    {
        public const string Name = "Search";

        private readonly IOptionsSnapshot<AzureSearchConfiguration> _options;

        public SearchController(IOptionsSnapshot<AzureSearchConfiguration> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        [HttpGet]
        public async Task<string[]> V2Search()
        {
            await Task.Yield();
            return new string[]
            {
                nameof(_options.Value.SearchServiceName),
                _options.Value.SearchServiceName,
                nameof(_options.Value.HijackIndexName),
                _options.Value.HijackIndexName,
                nameof(_options.Value.SearchIndexName),
                _options.Value.SearchIndexName,
            };
        }
    }
}
