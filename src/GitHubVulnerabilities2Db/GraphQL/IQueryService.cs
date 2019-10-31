// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace GitHubVulnerabilities2Db.GraphQL
{
    public interface IQueryService
    {
        /// <summary>
        /// Queries a GraphQL API and deserializes the result.
        /// </summary>
        /// <param name="query">
        /// The text of the query. Will be wrapped in a JSON object as the "query" property.
        /// 
        /// E.g. "the query" becomes { "query": "the query" }
        /// </param>
        Task<QueryResponse> QueryAsync(string query, CancellationToken token);
    }
}