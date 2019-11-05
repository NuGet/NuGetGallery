// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace GitHubVulnerabilities2Db.GraphQL
{
    /// <summary>
    /// A GraphQL query response object.
    /// </summary>
    public class QueryResponse
    {
        public QueryResponseData Data { get; set; }
    }

    /// <summary>
    /// The nested data inside a <see cref="QueryResponse"/>.
    /// </summary>
    public class QueryResponseData
    {
        public SecurityAdvisory SecurityAdvisory { get; set; }

        /// <summary>
        /// https://developer.github.com/v4/object/securityadvisoryconnection/
        /// </summary>
        public ConnectionResponseData<SecurityAdvisory> SecurityAdvisories { get; set; }

        /// <summary>
        /// https://developer.github.com/v4/object/securityvulnerabilityconnection/
        /// </summary>
        public ConnectionResponseData<SecurityVulnerability> SecurityVulnerabilities { get; set; }
    }

    /// <summary>
    /// Allows accessing <typeparamref name="TNode"/>s returned by GraphQL query.
    /// </summary>
    public class ConnectionResponseData<TNode> where TNode : INode
    {
        public IEnumerable<Edge<TNode>> Edges { get; set; }
    }
}