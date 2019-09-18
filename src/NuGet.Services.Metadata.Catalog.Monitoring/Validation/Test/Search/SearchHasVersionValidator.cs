// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class SearchHasVersionValidator : Validator<SearchEndpoint>
    {
        public SearchHasVersionValidator(
            SearchEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<Validator> logger)
            : base(endpoint, config, logger)
        {
        }

        protected override async Task RunInternalAsync(ValidationContext context)
        {
            var searchVisible = await IsVisibleInSearchAsync(context);
            var databaseState = await GetDatabaseStateAsync(context);
            var databaseVisible = databaseState == DatabaseState.Listed;
            if (databaseVisible != searchVisible)
            {
                const string listedString = "listed";
                const string unlistedString = "unlisted";

                throw new MetadataInconsistencyException(
                    $"Database shows {databaseState.ToString().ToLowerInvariant()}" +
                    $" but search shows {(searchVisible ? listedString : unlistedString)}.");
            }
        }

        private async Task<bool> IsVisibleInSearchAsync(ValidationContext context)
        {
            var searchPage = await context.GetSearchPageForIdAsync(Endpoint.BaseUri);
            var searchItem = searchPage.SingleOrDefault();
            bool searchListed;
            if (searchItem != null)
            {
                var searchVersions = await searchItem.GetVersionsAsync();
                searchListed = searchVersions.Any(x => x.Version == context.Package.Version);
            }
            else
            {
                searchListed = false;
            }

            return searchListed;
        }

        private static async Task<DatabaseState> GetDatabaseStateAsync(ValidationContext context)
        {
            var databaseResult = await context.GetIndexDatabaseAsync();
            if (databaseResult == null)
            {
                return DatabaseState.Unavailable;
            }

            return databaseResult.Listed ? DatabaseState.Listed : DatabaseState.Unlisted;
        }

        private enum DatabaseState
        {
            Unavailable,
            Unlisted,
            Listed,
        }
    }
}
