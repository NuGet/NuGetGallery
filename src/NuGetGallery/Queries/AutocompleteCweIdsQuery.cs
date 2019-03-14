// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class AutocompleteCweIdsQuery
        : IAutocompleteCweIdsQuery
    {
        // Search results should be limited anywhere between 5 - 10 results.
        private const int MaxResults = 5;

        private readonly IEntityRepository<Cwe> _cweRepository;

        public AutocompleteCweIdsQuery(IEntityRepository<Cwe> cweRepository)
        {
            _cweRepository = cweRepository ?? throw new ArgumentNullException(nameof(cweRepository));
        }

        public AutocompleteCweIdQueryResults Execute(string queryString)
        {
            // Validate search term and determine query type.
            if (!CweQueryStringValidator.TryValidate(queryString, out var queryMethod, out var validatedQueryString))
            {
                return new AutocompleteCweIdQueryResults(Strings.AutocompleteCweIds_ValidationError);
            }

            // Query the database.
            // Only include listed CVE entities.
            IReadOnlyCollection<Cwe> queryResults;
            switch (queryMethod)
            {
                case CweQueryMethod.ByCweId:
                    queryResults = _cweRepository.GetAll()
                        .Where(e => e.CweId.StartsWith(validatedQueryString) && e.Listed)
                        .OrderBy(e => e.CweId)
                        .Take(MaxResults)
                        .ToList();
                    break;

                case CweQueryMethod.ByName:
                    queryResults = _cweRepository.GetAll()
                        .Where(e => e.Name.Contains(validatedQueryString) && e.Listed)
                        .OrderBy(e => e.CweId)
                        .Take(MaxResults)
                        .ToList();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(queryMethod));
            }

            var results = queryResults
                .Select(e => new AutocompleteCweIdQueryResult(e.CweId, e.Name, e.Description))
                .OrderBy(e => CweIdHelper.GetCweIdNumericPartAsInteger(e.CweId))
                .ToList();

            return new AutocompleteCweIdQueryResults(results);
        }
    }
}