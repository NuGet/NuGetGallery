// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Search;

namespace NuGetGallery
{
    public class AutocompleteServicePackageVersionsQuery
        : AutocompleteServiceQuery, IAutocompletePackageVersionsQuery
    {
        public AutocompleteServicePackageVersionsQuery(IAppConfiguration configuration, IResilientSearchClient resilientSearchClient)
            : base(configuration, resilientSearchClient)
        {
        }

        public async Task<IReadOnlyList<string>> Execute(
            string id, 
            bool? includePrerelease,
            string semVerLevel = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return await RunServiceQuery("id=" + Uri.EscapeUriString(id), includePrerelease, semVerLevel);
        }
    }
}